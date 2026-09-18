using System;
using UnityEngine;

namespace Diceforge.Map
{
    [CreateAssetMenu(menuName = "Diceforge/Map/Woodland Routes")]
    public sealed class WoodlandRoutesSO : ScriptableObject
    {
        [Serializable]
        public sealed class Route
        {
            public string fromNodeId;
            public string toNodeId;
            public Vector2[] waypoints = Array.Empty<Vector2>();
        }

        [Min(1f)] public float referencePixelsPerSecond = 190f;
        public Route[] routes = Array.Empty<Route>();

        public Vector2[] BuildPath(string from, string to, Vector2 start, Vector2 end)
        {
            var route = Array.Find(routes, r => r != null && r.fromNodeId == from && r.toNodeId == to);
            if (route == null || route.waypoints == null) return null;
            var path = new Vector2[route.waypoints.Length + 2];
            path[0] = start;
            for (int i = 0; i < route.waypoints.Length; i++)
            {
                var point = route.waypoints[i];
                if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1)
                    return null;
                path[i + 1] = Vector2.Scale(point, new Vector2(WoodlandMapView.ReferenceWidth, WoodlandMapView.ReferenceHeight));
            }
            path[path.Length - 1] = end;
            return path;
        }
    }

    public static class WoodlandTravelRules
    {
        public static string GetDestination(MapDefinitionSO map, MapRunState state, string chapterId, string fromNodeId, bool won)
        {
            if (!won || map == null || state == null || !map.useWoodlandLayout || map.chapterId != chapterId || !state.IsCompleted(fromNodeId))
                return null;
            var from = map.GetNode(fromNodeId);
            if (from?.nextIds == null || from.nextIds.Count != 1) return null;
            string to = from.nextIds[0];
            return state.currentNodeId == to && state.IsUnlocked(to) && !state.IsCompleted(to) ? to : null;
        }
    }
}
