using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Diceforge.View
{
    /// <summary>Quiet stone-shaped targets and an evenly spaced route, without changing move rules.</summary>
    public sealed class DioramaMovePreview : MonoBehaviour
    {
        private const float DashLength = .15f;
        private const float DashWidth = .052f;
        private const float DashSpacing = .25f;
        private const float SurfaceLift = .025f;
        private static readonly Color Amber = new(.914f, .725f, .333f, 1f);
        private static readonly Color DestinationAmber = new(.945f, .824f, .541f, 1f);
        private static readonly Color Underedge = new(.659f, .502f, .216f, 1f);
        private static readonly Color Terracotta = new(.725f, .424f, .306f, 1f);
        private readonly List<Vector3> smoothedPath = new(256);
        private readonly List<Vector3> previousPoints = new(16);
        private readonly List<float> pathDistances = new(256);
        private readonly List<Vector3> routeVertices = new(4096);
        private readonly List<int> routeTriangles = new(8192);
        private readonly List<Vector3> edgeVertices = new(4096);
        private readonly List<int> edgeTriangles = new(8192);
        private readonly Dictionary<DioramaCell, GameObject> cellMarkers = new();
        private readonly List<Material> ownedMaterials = new();
        private Mesh rimMesh, interruptedRimMesh, washMesh, crossMesh, routeMesh, routeEdgeMesh;
        private Material sourceMaterial, destinationMaterial, washMaterial, routeMaterial, edgeMaterial, blockedMaterial;
        private MeshRenderer routeRenderer, routeEdgeRenderer;
        private GameObject landingMarker, blockedMarker, exitMarker;
        private Vector3 previousLanding;
        private bool previousBlocked, previousShowLanding, initialized;

        public bool IsVisible { get; private set; }
        public bool IsBlocked { get; private set; }
        public int DashCount { get; private set; }

        // Supply a saved transparent URP material so the required shader variant remains in WebGL builds.
        public void Initialize(Material template)
        {
            if (initialized) return;
            if (template == null) template = Resources.Load<Material>("DemoMovePreviewMaterial");
            if (template == null)
            {
                Debug.LogError("Move preview requires the saved DemoMovePreviewMaterial.", this);
                return;
            }
            initialized = true;
            sourceMaterial = CreateMaterial(template, "Available stone amber", WithAlpha(Amber, .72f));
            destinationMaterial = CreateMaterial(template, "Destination ivory amber", DestinationAmber);
            washMaterial = CreateMaterial(template, "Stone surface wash", WithAlpha(Amber, .09f));
            routeMaterial = CreateMaterial(template, "Route amber", WithAlpha(DestinationAmber, .98f));
            edgeMaterial = CreateMaterial(template, "Route underedge", WithAlpha(Underedge, .65f));
            blockedMaterial = CreateMaterial(template, "Blocked terracotta", WithAlpha(Terracotta, .92f));
            rimMesh = BuildStoneMesh(false, false);
            interruptedRimMesh = BuildStoneMesh(false, true);
            washMesh = BuildStoneMesh(true, false);
            crossMesh = BuildCrossMesh();
            routeMesh = new Mesh { name = "Dotted move route" };
            routeEdgeMesh = new Mesh { name = "Dotted route underedge" };
            routeMesh.MarkDynamic();
            routeEdgeMesh.MarkDynamic();
            routeRenderer = CreateRenderer("Dotted move route", transform, routeMesh, routeMaterial);
            routeEdgeRenderer = CreateRenderer("Route underedge", transform, routeEdgeMesh, edgeMaterial);
            landingMarker = CreateMarker("Destination stone", destinationMaterial, false, true);
            landingMarker.transform.localScale = Vector3.one * 1.02f;
            blockedMarker = CreateMarker("Unavailable destination", blockedMaterial, true, false);
            CreateRenderer("Unavailable cross", blockedMarker.transform, crossMesh, blockedMaterial);
            Hide();
        }

        public void SetAvailableCells(DioramaCell[] cells, IReadOnlyCollection<int> ids)
        {
            EnsureInitialized();
            if (!initialized || cells == null) return;
            foreach (var marker in cellMarkers.Values) if (marker != null) marker.SetActive(false);
            foreach (var cell in cells)
            {
                if (cell == null) continue;
                bool available = Contains(ids, cell.cellId);
                cell.available = available;
                if (cell.highlight != null) cell.highlight.enabled = false;
                if (!available) continue;
                if (!cellMarkers.TryGetValue(cell, out var marker) || marker == null)
                {
                    marker = CreateMarker("Available stone " + cell.cellId, sourceMaterial, false, true);
                    cellMarkers[cell] = marker;
                }
                marker.transform.position = cell.transform.position + Vector3.up * SurfaceLift;
                marker.SetActive(true);
            }
        }

        public void SetExitMarker(Vector3 position, bool visible)
        {
            EnsureInitialized();
            if (!initialized) return;
            if (exitMarker == null) exitMarker = CreateMarker("Trail exit stone", sourceMaterial, false, false);
            exitMarker.transform.position = position + Vector3.up * SurfaceLift;
            exitMarker.SetActive(visible);
        }

        // Points and landing are world positions. Pass the same logical path for both Reduced Motion modes.
        public void Show(IReadOnlyList<Vector3> points, Vector3 landing, bool blocked = false, bool showLanding = true)
        {
            EnsureInitialized();
            if (!initialized || points == null || points.Count < 2)
            {
                Hide();
                return;
            }
            if (!MatchesPrevious(points, landing, blocked, showLanding))
            {
                previousPoints.Clear();
                for (int i = 0; i < points.Count; i++) previousPoints.Add(points[i]);
                previousLanding = landing;
                previousBlocked = blocked;
                previousShowLanding = showLanding;
                BuildRoute(points);
            }
            IsVisible = true;
            IsBlocked = blocked;
            routeRenderer.sharedMaterial = blocked ? blockedMaterial : routeMaterial;
            routeRenderer.enabled = DashCount > 0;
            routeEdgeRenderer.enabled = DashCount > 0;
            landingMarker.transform.position = landing + Vector3.up * SurfaceLift;
            blockedMarker.transform.position = landing + Vector3.up * SurfaceLift;
            landingMarker.SetActive(showLanding && !blocked);
            blockedMarker.SetActive(showLanding && blocked);
        }

        public void Hide()
        {
            IsVisible = false;
            IsBlocked = false;
            if (routeRenderer != null) routeRenderer.enabled = false;
            if (routeEdgeRenderer != null) routeEdgeRenderer.enabled = false;
            if (landingMarker != null) landingMarker.SetActive(false);
            if (blockedMarker != null) blockedMarker.SetActive(false);
        }

        private void EnsureInitialized()
        {
            if (!initialized) Initialize(null);
        }

        private static bool Contains(IReadOnlyCollection<int> ids, int id)
        {
            if (ids == null) return false;
            foreach (int value in ids) if (value == id) return true;
            return false;
        }

        private bool MatchesPrevious(IReadOnlyList<Vector3> points, Vector3 landing, bool blocked, bool showLanding)
        {
            if (points.Count != previousPoints.Count || landing != previousLanding || blocked != previousBlocked ||
                showLanding != previousShowLanding) return false;
            for (int i = 0; i < points.Count; i++) if (points[i] != previousPoints[i]) return false;
            return true;
        }

        private void BuildRoute(IReadOnlyList<Vector3> points)
        {
            SampleSmoothPath(points, smoothedPath);
            pathDistances.Clear();
            pathDistances.Add(0);
            float total = 0;
            for (int i = 1; i < smoothedPath.Count; i++)
            {
                total += Vector3.Distance(smoothedPath[i - 1], smoothedPath[i]);
                pathDistances.Add(total);
            }
            routeVertices.Clear();
            routeTriangles.Clear();
            edgeVertices.Clear();
            edgeTriangles.Clear();
            DashCount = 0;
            // Keep the first/last dots clear of the character and destination border.
            float inset = Mathf.Min(.28f, total * .18f);
            float usable = total - inset * 2;
            if (usable > .025f)
            {
                DashCount = Mathf.Clamp(Mathf.FloorToInt(usable / DashSpacing) + 1, 1, 256);
                float spacing = DashCount > 1 ? usable / DashCount : 0;
                for (int i = 0; i < DashCount; i++)
                {
                    float distance = DashCount == 1 ? total * .5f : inset + spacing * (i + .5f);
                    Vector3 center = PointAtDistance(distance) + Vector3.up * .055f;
                    Vector3 before = PointAtDistance(Mathf.Max(0, distance - .04f));
                    Vector3 after = PointAtDistance(Mathf.Min(total, distance + .04f));
                    Vector3 tangent = (after - before).normalized;
                    if (tangent.sqrMagnitude < .1f) tangent = Vector3.forward;
                    float length = Mathf.Min(DashLength, usable / DashCount * .65f);
                    AddCapsule(center, tangent, length, DashWidth, routeVertices, routeTriangles);
                    AddCapsule(center - Vector3.up * .004f, tangent, length + .018f, DashWidth + .018f,
                        edgeVertices, edgeTriangles);
                }
            }
            AssignMesh(routeMesh, routeVertices, routeTriangles);
            AssignMesh(routeEdgeMesh, edgeVertices, edgeTriangles);
        }

        // Centripetal-style tangent limiting keeps the route within each supplied cell segment.
        // A monotone Hermite span avoids loops at sharp trail turns and repeated/vertical points.
        public static void SampleSmoothPath(IReadOnlyList<Vector3> points, List<Vector3> output)
        {
            output.Clear();
            if (points == null || points.Count == 0) return;
            output.Add(points[0]);
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 start = points[i], end = points[i + 1];
                Vector3 span = end - start;
                if (span.sqrMagnitude < .000001f) continue;
                Vector3 before = points[Mathf.Max(0, i - 1)];
                Vector3 after = points[Mathf.Min(points.Count - 1, i + 2)];
                Vector3 startTangent = i == 0 ? span : LimitTangent((end - before) * .5f, span);
                Vector3 endTangent = i == points.Count - 2 ? span : LimitTangent((after - start) * .5f, span);
                int samples = Mathf.Clamp(Mathf.CeilToInt(span.magnitude / .055f), 4, 96);
                for (int sample = 1; sample <= samples; sample++)
                {
                    float t = sample / (float)samples, t2 = t * t, t3 = t2 * t;
                    Vector3 point = (2 * t3 - 3 * t2 + 1) * start + (t3 - 2 * t2 + t) * startTangent +
                        (-2 * t3 + 3 * t2) * end + (t3 - t2) * endTangent;
                    // Cap the small corner smoothing corridor; never overshoot a landing in any axis.
                    point.x = Mathf.Clamp(point.x, Mathf.Min(start.x, end.x), Mathf.Max(start.x, end.x));
                    point.y = Mathf.Clamp(point.y, Mathf.Min(start.y, end.y), Mathf.Max(start.y, end.y));
                    point.z = Mathf.Clamp(point.z, Mathf.Min(start.z, end.z), Mathf.Max(start.z, end.z));
                    output.Add(point);
                }
            }
        }

        private static Vector3 LimitTangent(Vector3 tangent, Vector3 span)
        {
            if (Vector3.Dot(tangent, span) <= 0) return Vector3.zero;
            return Vector3.ClampMagnitude(tangent, span.magnitude);
        }

        private Vector3 PointAtDistance(float distance)
        {
            for (int i = 1; i < pathDistances.Count; i++)
            {
                if (distance > pathDistances[i]) continue;
                float span = pathDistances[i] - pathDistances[i - 1];
                float t = span > .000001f ? (distance - pathDistances[i - 1]) / span : 0;
                return Vector3.Lerp(smoothedPath[i - 1], smoothedPath[i], t);
            }
            return smoothedPath[smoothedPath.Count - 1];
        }

        private void AddCapsule(Vector3 center, Vector3 tangent, float length, float width,
            List<Vector3> vertices, List<int> triangles)
        {
            Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
            if (side.sqrMagnitude < .1f) side = Vector3.right;
            float radius = width * .5f, halfStem = Mathf.Max(0, (length - width) * .5f);
            int centerIndex = vertices.Count;
            vertices.Add(transform.InverseTransformPoint(center));
            const int capSegments = 6;
            for (int cap = 0; cap < 2; cap++)
            {
                for (int i = 0; i <= capSegments; i++)
                {
                    float angle = (-Mathf.PI * .5f + i * Mathf.PI / capSegments) + cap * Mathf.PI;
                    Vector3 position = center + tangent * ((cap == 0 ? halfStem : -halfStem) + Mathf.Cos(angle) * radius) +
                        side * (Mathf.Sin(angle) * radius);
                    vertices.Add(transform.InverseTransformPoint(position));
                }
            }
            const int outlineCount = (capSegments + 1) * 2;
            for (int i = 0; i < outlineCount; i++)
            {
                triangles.Add(centerIndex);
                triangles.Add(centerIndex + 1 + i);
                triangles.Add(centerIndex + 1 + (i + 1) % outlineCount);
            }
        }

        private GameObject CreateMarker(string markerName, Material material, bool interrupted, bool wash)
        {
            var marker = new GameObject(markerName);
            marker.transform.SetParent(transform, false);
            CreateRenderer("Stone edge", marker.transform, interrupted ? interruptedRimMesh : rimMesh, material);
            if (wash) CreateRenderer("Stone wash", marker.transform, washMesh, washMaterial);
            marker.SetActive(false);
            return marker;
        }

        private static MeshRenderer CreateRenderer(string objectName, Transform parent, Mesh mesh, Material material)
        {
            var visual = new GameObject(objectName);
            visual.transform.SetParent(parent, false);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        private Material CreateMaterial(Material template, string materialName, Color color)
        {
            var material = new Material(template) { name = materialName };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            ownedMaterials.Add(material);
            return material;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static Mesh BuildStoneMesh(bool fill, bool interrupted)
        {
            const int cornerSamples = 7;
            const int count = cornerSamples * 4;
            var vertices = new List<Vector3>(count * 2 + 1);
            var triangles = new List<int>(count * 6);
            if (fill) vertices.Add(Vector3.down * .002f);
            for (int corner = 0; corner < 4; corner++)
            {
                float centerX = corner == 0 || corner == 3 ? .35f : -.35f;
                float centerZ = corner < 2 ? .32f : -.32f;
                for (int i = 0; i < cornerSamples; i++)
                {
                    float angle = (corner * 90 + i * 90f / (cornerSamples - 1)) * Mathf.Deg2Rad;
                    float x = Mathf.Cos(angle), z = Mathf.Sin(angle);
                    vertices.Add(new Vector3(centerX + x * .12f, fill ? -.002f : 0, centerZ + z * .12f));
                    if (!fill) vertices.Add(new Vector3(centerX + x * .089f, 0, centerZ + z * .089f));
                }
            }
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                if (fill)
                {
                    triangles.Add(0); triangles.Add(next + 1); triangles.Add(i + 1);
                }
                else
                {
                    if (interrupted && i % cornerSamples == cornerSamples - 1) continue;
                    triangles.Add(i * 2); triangles.Add(i * 2 + 1); triangles.Add(next * 2);
                    triangles.Add(i * 2 + 1); triangles.Add(next * 2 + 1); triangles.Add(next * 2);
                }
            }
            var mesh = new Mesh { name = fill ? "Stone surface wash" : interrupted ? "Interrupted stone edge" : "Rounded stone edge" };
            AssignMesh(mesh, vertices, triangles);
            return mesh;
        }

        private static Mesh BuildCrossMesh()
        {
            var vertices = new List<Vector3>(8);
            var triangles = new List<int>(12);
            for (int stroke = 0; stroke < 2; stroke++)
            {
                Vector3 along = new Vector3(1, 0, stroke == 0 ? 1 : -1).normalized * .115f;
                Vector3 side = Vector3.Cross(Vector3.up, along).normalized * .0175f;
                Vector3 center = new(.16f, .008f, -.36f);
                int index = vertices.Count;
                vertices.Add(center - along - side); vertices.Add(center - along + side);
                vertices.Add(center + along + side); vertices.Add(center + along - side);
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
                triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
            }
            var mesh = new Mesh { name = "Unavailable stone cross" };
            AssignMesh(mesh, vertices, triangles);
            return mesh;
        }

        private static void AssignMesh(Mesh mesh, List<Vector3> vertices, List<int> triangles)
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            foreach (var material in ownedMaterials) if (material != null) Destroy(material);
            if (rimMesh != null) Destroy(rimMesh);
            if (interruptedRimMesh != null) Destroy(interruptedRimMesh);
            if (washMesh != null) Destroy(washMesh);
            if (crossMesh != null) Destroy(crossMesh);
            if (routeMesh != null) Destroy(routeMesh);
            if (routeEdgeMesh != null) Destroy(routeEdgeMesh);
        }
    }
}
