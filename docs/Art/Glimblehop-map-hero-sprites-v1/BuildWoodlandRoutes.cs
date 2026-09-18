using System;
using Diceforge.Map;
using UnityEditor;
using UnityEngine;

public static class BuildWoodlandRoutes
{
    public static object Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        const string path = "Assets/_Project/Resources/Map/Chapter1_WoodlandRoutes.asset";
        var asset = AssetDatabase.LoadAssetAtPath<WoodlandRoutesSO>(path);
        bool create = asset == null;
        if (create) asset = ScriptableObject.CreateInstance<WoodlandRoutesSO>();
        asset.referencePixelsPerSecond = 190;
        asset.routes = new[]
        {
            Route(1, new Vector2(280,572), new Vector2(312,549), new Vector2(347,527), new Vector2(388,507), new Vector2(430,482), new Vector2(474,455)),
            Route(2, new Vector2(587,435), new Vector2(624,450), new Vector2(651,472), new Vector2(680,495), new Vector2(719,517)),
            Route(3, new Vector2(828,526), new Vector2(867,514), new Vector2(907,487), new Vector2(950,467), new Vector2(1000,451), new Vector2(1050,444)),
            Route(4, new Vector2(1110,388), new Vector2(1096,365), new Vector2(1075,346), new Vector2(1053,331)),
            Route(5, new Vector2(1096,319), new Vector2(1134,302), new Vector2(1176,279), new Vector2(1204,279), new Vector2(1216,298))
        };
        if (create) AssetDatabase.CreateAsset(asset,path);
        else EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return new { routes = asset.routes.Length, path };
    }

    private static WoodlandRoutesSO.Route Route(int from, params Vector2[] points)
    {
        for (int i=0;i<points.Length;i++) points[i] = new Vector2(points[i].x/1672f,points[i].y/941f);
        return new WoodlandRoutesSO.Route { fromNodeId = "C1_"+from.ToString("D2"), toNodeId = "C1_"+(from+1).ToString("D2"), waypoints = points };
    }
}
