using System;
using System.Collections.Generic;
using Diceforge.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Diceforge.View
{
    public sealed class DioramaBoard : MonoBehaviour, IBoardGeometry
    {
        public DioramaLayout layout;
        public GameObject landscapeRoot;
        public GameObject portraitRoot;
        public UniversalRenderPipelineAsset desktopPipeline;
        public UniversalRenderPipelineAsset mobilePipeline;
        public DioramaLighting landscapeLighting;
        public DioramaLighting portraitLighting;
        public bool landscapeOnly;
        private LightmapData[] previousLightmaps;
        private LightProbes previousProbes;
        public event Action GeometryChanged;
        public bool IsPortrait { get; private set; }
        public static bool ReducedMotion { get; set; }
        private Camera cameraView;
        private RenderPipelineAsset previousPipeline;
        private DioramaCell[] cells;
        private int screenWidth, screenHeight;
        private bool configured;
        private LineRenderer preview;
        private GameObject landingRim;
        private readonly List<Vector3> previewPoints = new();
        private readonly Dictionary<int, TextMesh> badges = new();
        private Sprite badgeSprite;
        private bool trailMode;
        private Rect? demoGameplayViewport;
        private GameObject trailExit;
        private TextMesh blockedSign;
        private DioramaMovePreview demoPreview;
        private DioramaCameraController orbitCamera;
        private readonly List<Vector3> cameraPlayablePoints = new(36);
        public void ConfigureTrail(bool enabled)
        {
            trailMode = enabled;
            if (enabled) EnsureTrailPresentation();
            if (orbitCamera != null) orbitCamera.enabled = enabled;
            if (!enabled && demoPreview != null) { demoPreview.Hide(); demoPreview.SetAvailableCells(cells, null); }
            if (trailExit != null) trailExit.SetActive(false);
            FitCamera();
            RefreshTrailExit();
        }
        private void RefreshTrailExit()
        {
            if (demoPreview != null) demoPreview.SetExitMarker(ExitPosition(0), trailMode);
        }
        private void EnsureTrailPresentation()
        {
            if (demoPreview == null) demoPreview = gameObject.AddComponent<DioramaMovePreview>();
            if (orbitCamera == null) orbitCamera = gameObject.AddComponent<DioramaCameraController>();
        }
        public void Initialize()
        {
            if (configured) return;
            configured = true;
            previousPipeline = QualitySettings.renderPipeline;
            previousLightmaps=LightmapSettings.lightmaps;previousProbes=LightmapSettings.lightProbes;
            QualitySettings.renderPipeline = Application.isMobilePlatform ? mobilePipeline : desktopPipeline;
            cameraView = Camera.main;
            if (cameraView != null)
            {
                cameraView.orthographic = true;
                cameraView.transform.rotation = Quaternion.Euler(50, 0, 0);
                cameraView.clearFlags = CameraClearFlags.SolidColor;
                cameraView.backgroundColor = new Color(.13f,.20f,.14f);
                cameraView.nearClipPlane = .1f; cameraView.farClipPlane = 100;
                cameraView.GetUniversalAdditionalCameraData().SetRenderer(0);
            }
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                {
                    light.transform.rotation = Quaternion.Euler(48,-35,0);
                    light.color = new Color(1,.89f,.69f); light.intensity = 1.65f;
                    light.shadows = LightShadows.Soft;
                }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.60f,.68f,.60f);
            RenderSettings.ambientEquatorColor = new Color(.37f,.40f,.28f);
            RenderSettings.ambientGroundColor = new Color(.19f,.14f,.085f);
            ApplyOrientation(true);
        }
        private void Update()
        {
            if (configured && (screenWidth != Screen.width || screenHeight != Screen.height)) ApplyOrientation(false);
        }
        public void ApplyOrientation(bool force)
        {
            bool portrait = !landscapeOnly && Screen.height > Screen.width;
            bool changed = force || portrait != IsPortrait;
            IsPortrait = portrait; screenWidth = Screen.width; screenHeight = Screen.height;
            landscapeRoot.SetActive(!portrait); portraitRoot.SetActive(portrait);
            cells = (portrait ? portraitRoot : landscapeRoot).GetComponentsInChildren<DioramaCell>();
            if(changed)(portrait?portraitLighting:landscapeLighting)?.Apply(portrait?portraitRoot:landscapeRoot);
            FitCamera();
            RefreshTrailExit();
            if (changed) { Preview(null, null, -1); ClearBadges(); GeometryChanged?.Invoke(); }
        }
        public void SetDemoGameplayViewport(Rect viewport)
        {
            if (!trailMode || viewport.width <= 0 || viewport.height <= 0) return;
            if (demoGameplayViewport.HasValue && demoGameplayViewport.Value == viewport) return;
            demoGameplayViewport = viewport;
            FitCamera();
        }
        private void FitCamera()
        {
            if (cameraView == null) return;
            Bounds bounds = WorldBounds;
            if (trailMode)
            {
                bounds = new Bounds(CellPosition(layout.cellIds[0]), Vector3.zero);
                for (int i = 0; i < layout.cellIds.Length; i++) IncludeTrailCameraBounds(ref bounds, CellPosition(layout.cellIds[i]));
                IncludeTrailCameraBounds(ref bounds, ExitPosition(0));
            }
            float halfWidth = bounds.extents.x + .4f;
            float halfHeight = bounds.extents.z * Mathf.Sin(50*Mathf.Deg2Rad)
                + (trailMode ? bounds.extents.y * Mathf.Cos(50*Mathf.Deg2Rad) + .2f : .60f);
            Rect viewport = trailMode && demoGameplayViewport.HasValue ? demoGameplayViewport.Value : new Rect(0, 0, 1, 1);
            float freeHeight = trailMode && demoGameplayViewport.HasValue ? viewport.height : IsPortrait ? .69f : .74f;
            float homeSize = Mathf.Max(halfHeight/freeHeight, halfWidth / Mathf.Max(.2f,cameraView.aspect * viewport.width)) * 1.04f;
            float verticalOffset = trailMode && demoGameplayViewport.HasValue ? 1 - viewport.center.y * 2 : -.09f;
            Quaternion rotation = trailMode ? Quaternion.Euler(50, 0, 0) : cameraView.transform.rotation;
            Vector3 target = bounds.center + rotation * Vector3.up * (homeSize * verticalOffset);
            Vector3 position = target - rotation * Vector3.forward * 25;
            if (trailMode)
            {
                EnsureTrailPresentation();
                cameraPlayablePoints.Clear();
                for (int i = 0; i < layout.cellIds.Length; i++) AddCameraTileBounds(CellPosition(layout.cellIds[i]));
                AddCameraTileBounds(ExitPosition(0));
                orbitCamera.SetGameplayViewport(viewport, cameraPlayablePoints);
                orbitCamera.SetHomePose(cameraView, bounds.center, position, rotation, homeSize);
            }
            else
            {
                cameraView.orthographicSize = homeSize;
                cameraView.transform.SetPositionAndRotation(position, rotation);
            }
        }
        private static void IncludeTrailCameraBounds(ref Bounds bounds, Vector3 center)
        {
            bounds.Encapsulate(center + new Vector3(-.5f, 0f, -.5f));
            bounds.Encapsulate(center + new Vector3(.5f, 1.1f, .5f));
        }
        private void AddCameraTileBounds(Vector3 center)
        {
            const float edge = .43f;
            cameraPlayablePoints.Add(center + new Vector3(-edge, 0, -edge));
            cameraPlayablePoints.Add(center + new Vector3(edge, 0, -edge));
            cameraPlayablePoints.Add(center + new Vector3(edge, 0, edge));
            cameraPlayablePoints.Add(center + new Vector3(-edge, 0, edge));
        }
        private void LateUpdate()
        {
            if (!trailMode || cameraView == null) return;
            foreach (var badge in badges.Values)
                if (badge != null && badge.gameObject.activeSelf) badge.transform.rotation = cameraView.transform.rotation;
        }

        public Vector3 CellPosition(int id)
        {
            var positions = IsPortrait ? layout.portrait : layout.landscape;
            for (int i=0;i<layout.cellIds.Length;i++) if (layout.cellIds[i]==id) return transform.TransformPoint(positions[i]);
            throw new ArgumentOutOfRangeException(nameof(id));
        }
        public Quaternion CellRotation(int id) => Quaternion.Euler(0,180,0);
        public Vector3 FormationOffset(int slot, int count)
        {
            if(count<=1) return Vector3.zero;
            return (slot%3) switch { 0 => new Vector3(-.20f,0,-.10f), 1 => new Vector3(.20f,0,-.10f), _ => new Vector3(0,0,.21f) };
        }
        public Vector3 WaitingPosition(int player) => transform.TransformPoint(new Vector3((player==0?-1:1)*.66f,.23f,0));
        public Vector3 ExitPosition(int player) => trailMode
            ? CellPosition(7) + (CellPosition(7) - CellPosition(6)).normalized * .85f
            : WaitingPosition(player) + Vector3.forward*.8f;
        public Bounds WorldBounds
        {
            get
            {
                var bounds = new Bounds(transform.position, IsPortrait ? layout.portraitSize : layout.landscapeSize);
                if (trailMode) bounds.Encapsulate(new Bounds(ExitPosition(0), Vector3.one * 1.1f));
                return bounds;
            }
        }
        public void Highlight(IReadOnlyCollection<int> ids)
        {
            if(cells==null)return;
            if (trailMode) { EnsureTrailPresentation(); demoPreview.SetAvailableCells(cells, ids); return; }
            foreach(var c in cells) { bool on=false; if(ids!=null)foreach(int id in ids)if(id==c.cellId){on=true;break;} c.Highlight(on); }
        }
        public void Preview(Move? move,GameState state,int cell)
        {
            if (blockedSign != null) blockedSign.gameObject.SetActive(false);
            if(!move.HasValue || state==null)
            {
                if(preview!=null)preview.enabled=false;if(landingRim!=null)landingRim.SetActive(false);demoPreview?.Hide();return;
            }
            var value=move.Value;
            var path=BoardPathRules.GetPathInfo(state.Rules,state.CurrentPlayer);
            var points=previewPoints;
            points.Clear();
            int destination=cell;
            if(value.Kind==MoveKind.EnterFromBar)
            {
                points.Add(WaitingPosition((int)state.CurrentPlayer));points.Add(CellPosition(cell));
            }
            else
            {
                int steps=value.Kind==MoveKind.BearOff?Mathf.Max(0,BoardPathRules.PipsToBearOff(state.Rules,state.CurrentPlayer,value.FromCell)-1):value.PipUsed;
                for(int i=0;i<=steps;i++)
                {
                    destination=(value.FromCell+path.MoveDir*i+layout.cellIds.Length*2)%layout.cellIds.Length;
                    points.Add(CellPosition(destination) + (trailMode && steps > 1
                        ? Vector3.up * (Mathf.Sin(i / (float)steps * Mathf.PI) * .4f) : Vector3.zero));
                }
                if(value.Kind==MoveKind.BearOff)points.Add(ExitPosition((int)state.CurrentPlayer));
            }
            if (trailMode)
            {
                EnsureTrailPresentation();
                Vector3 landing = value.Kind == MoveKind.BearOff ? ExitPosition((int)state.CurrentPlayer) : CellPosition(destination);
                demoPreview.Show(points, landing);
                return;
            }
            if(preview==null)
            {
                var go=new GameObject("RoutePreview");go.transform.SetParent(transform,false);preview=go.AddComponent<LineRenderer>();
                preview.sharedMaterial=cells[0].highlight.sharedMaterial;preview.widthMultiplier=.06f;preview.numCornerVertices=4;preview.numCapVertices=4;
                preview.shadowCastingMode=ShadowCastingMode.Off;preview.receiveShadows=false;
            }
            preview.enabled=true;preview.positionCount=points.Count;
            preview.startWidth=.075f;preview.endWidth=.045f;
            preview.startColor=new Color(1f,.81f,.42f,.9f);
            preview.endColor=new Color(.55f,1f,.78f,1f);
            for(int i=0;i<points.Count;i++)preview.SetPosition(i,points[i]+Vector3.up*.09f);
            if(landingRim==null)
            {
                var source=cells[0].highlight;
                var ringMesh=source!=null?source.GetComponent<MeshFilter>()?.sharedMesh:null;
                if(ringMesh!=null && ringMesh.name=="FirstTrailSelectionRim")
                {
                    landingRim=new GameObject("LandingRim");landingRim.transform.SetParent(transform,false);
                    landingRim.transform.localScale=Vector3.one*1.08f;
                    landingRim.AddComponent<MeshFilter>().sharedMesh=ringMesh;
                    var renderer=landingRim.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial=source.sharedMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                }
            }
            if(landingRim!=null)
            {
                landingRim.SetActive(value.Kind!=MoveKind.BearOff);
                landingRim.transform.position=points[points.Count-1]+Vector3.up*.03f;
            }
        }
        public void PreviewBlocked(int from, int step, GameState state)
        {
            Preview(Move.MoveStone(from, step), state, from + step);
            if (trailMode)
            {
                demoPreview.Show(previewPoints, CellPosition(from + step), true);
                return;
            }
            if (preview != null) preview.startColor = preview.endColor = new Color(1, .48f, .32f);
            if (blockedSign == null)
            {
                var sign = new GameObject("UnavailableLanding");
                sign.transform.SetParent(transform, false);
                blockedSign = sign.AddComponent<TextMesh>();
                blockedSign.text = "X";
                blockedSign.anchor = TextAnchor.MiddleCenter;
                blockedSign.characterSize = .13f;
                blockedSign.fontSize = 36;
                blockedSign.color = new Color(1, .68f, .3f);
            }
            blockedSign.gameObject.SetActive(true);
            blockedSign.transform.position = CellPosition(from + step) + Vector3.up * .9f;
            if (cameraView != null) blockedSign.transform.rotation = cameraView.transform.rotation;
        }
        public void SetCount(int player,int cell,int count)
        {
            int key=player*1000+cell+1;
            if(!badges.TryGetValue(key,out var badge))
            {
                if(count<2)return;
                var go=new GameObject("Count_"+key);go.transform.SetParent(transform,false);
                badge=go.AddComponent<TextMesh>();badge.fontSize=64;badge.characterSize=.065f;badge.anchor=TextAnchor.MiddleCenter;
                badge.fontStyle=FontStyle.Bold;badge.color=player==0?new Color(1,.83f,.58f):new Color(.61f,.96f,.88f);
                badge.characterSize=.045f;
                var profile = Resources.Load<BattlePresentationProfile>("BattlePresentationProfile");
                if (profile != null && profile.countPlaque != null)
                {
                    if (badgeSprite == null)
                        badgeSprite = Sprite.Create(profile.countPlaque,
                            new Rect(0, 0, profile.countPlaque.width, profile.countPlaque.height),
                            new Vector2(.5f, .5f), 100f);
                    var backing = new GameObject("CountPlaque").AddComponent<SpriteRenderer>();
                    backing.transform.SetParent(go.transform, false);
                    backing.sprite = badgeSprite;
                    backing.transform.localScale = new Vector3(.8f / badgeSprite.bounds.size.x, .36f / badgeSprite.bounds.size.y, 1f);
                }
                badges.Add(key,badge);
            }
            badge.gameObject.SetActive(count>1);badge.text=count.ToString();
            badge.transform.position=(cell<0?WaitingPosition(player):CellPosition(cell))+new Vector3(.38f,.86f,0);
            if(cameraView!=null)
            {
                badge.transform.rotation=cameraView.transform.rotation;
                var backing=badge.GetComponentInChildren<SpriteRenderer>();
                if(backing!=null)backing.transform.localPosition=Vector3.back*.025f;
            }
        }
        private void ClearBadges(){foreach(var b in badges.Values)if(b!=null)Destroy(b.gameObject);badges.Clear();}
        private void OnDestroy(){if(badgeSprite!=null)Destroy(badgeSprite);if(configured){QualitySettings.renderPipeline=previousPipeline;LightmapSettings.lightmaps=previousLightmaps;LightmapSettings.lightProbes=previousProbes;}}
    }
}
