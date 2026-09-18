using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Diceforge.Map;
using UnityEngine;
using UnityEngine.UIElements;

public static class VerifyWoodlandHero
{
    public static async Task<object> Check()
    {
        var doc = UnityEngine.Object.FindAnyObjectByType<MainMenuController>().GetComponent<UIDocument>();
        var root = doc.rootVisualElement;
        var hero = root.Q<Image>("WoodlandHero");
        if (hero == null || hero.image is not RenderTexture texture || !texture.IsCreated()) throw new Exception("Hero render texture is missing");
        var studio = GameObject.Find("WoodlandHeroStudio");
        var animator = studio.GetComponentInChildren<Animator>();
        var head = animator.GetComponentsInChildren<Transform>().First(t => t.name == "Head");
        var headBefore = head.localRotation;
        float before = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        await Task.Delay(500);
        float after = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        if (after <= before) throw new Exception("Idle animation is not advancing");
        float headMotion = Quaternion.Angle(headBefore, head.localRotation);
        if (headMotion < .01f) throw new Exception("Head bone is not moving");
        if (hero.pickingMode != PickingMode.Ignore) throw new Exception("Hero blocks level input");
        var active = Enumerable.Range(1, 6).Select(i => root.Q<Button>("WoodlandLevel" + i)).Single(n => n.enabledInHierarchy);
        if (Mathf.Abs(hero.worldBound.center.x - active.worldBound.center.x) > active.worldBound.width) throw new Exception("Hero not near current level");
        var result = new { activeLevel = active.name, animationAdvances = true, headMotionDegrees = headMotion, textureSize = texture.width,
            heroBounds = hero.worldBound.ToString(), cameraCount = studio.GetComponentsInChildren<Camera>().Length };
        var back = root.Q<Button>("WoodlandBack");
        using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = back; back.SendEvent(evt); }
        await Task.Delay(1800);
        if (GameObject.Find("WoodlandHeroStudio") != null || hero.image != null) throw new Exception("Hero resources survived map close");
        return new { presentation = result, cleanupPassed = true };
    }

    public static async Task<object> AllAnchors()
    {
        var controller = UnityEngine.Object.FindAnyObjectByType<MapController>();
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var view = (WoodlandMapView)typeof(MapController).GetField("_woodlandView", flags).GetValue(controller);
        var map = (MapDefinitionSO)typeof(MapController).GetField("_currentMap", flags).GetValue(controller);
        var state = (MapRunState)typeof(MapController).GetField("_currentState", flags).GetValue(controller);
        var texture = (RenderTexture)view.HeroImage.image;
        var previous = RenderTexture.active;
        var sample = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        Rect silhouette;
        try
        {
            RenderTexture.active = texture;
            sample.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            sample.Apply();
            var pixels = sample.GetPixels32();
            int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
            for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
            {
                if (pixels[y * texture.width + x].a < 80) continue;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
            if (maxX - minX < 30 || maxY - minY < 80) throw new Exception("Rendered hero is empty or too small");
            if (minX < 2 || minY < 2 || maxX > texture.width - 3 || maxY > texture.height - 3) throw new Exception("Hero silhouette touches texture edge");
            silhouette = Rect.MinMaxRect(minX / (float)texture.width, 1 - maxY / (float)texture.height,
                maxX / (float)texture.width, 1 - minY / (float)texture.height);
        }
        finally { RenderTexture.active = previous; UnityEngine.Object.Destroy(sample); }
        var results = new List<object>();
        try
        {
            for (int completed = 0; completed <= 6; completed++)
            {
                view.SetProgress(completed);
                await Task.Delay(250);
                var bounds = view.HeroImage.worldBound;
                var body = new Rect(bounds.x + silhouette.x * bounds.width, bounds.y + silhouette.y * bounds.height,
                    silhouette.width * bounds.width, silhouette.height * bounds.height);
                if (body.Overlaps(view.Root.Q("WoodlandTitlePlaque").worldBound)) throw new Exception("Hero overlaps title at progress " + completed);
                for (int i = 1; i <= 6; i++)
                    if (body.Overlaps(view.Root.Q<Button>("WoodlandLevel" + i).Q<Label>("Number").worldBound)) throw new Exception("Hero covers level number at progress " + completed);
                if (!view.Stage.worldBound.Contains(body.min) || !view.Stage.worldBound.Contains(body.max)) throw new Exception("Hero outside stage");
                results.Add(new { completed, body = body.ToString(), titleClear = true, numbersClear = true });
                if (completed == 0 || completed == 4 || completed == 5)
                {
                    var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                    try { File.WriteAllBytes(Path.GetFullPath("docs/Art/Glimblehop-map-kit-v1/hero-level-" + (completed + 1) + ".png"), screenshot.EncodeToPNG()); }
                    finally { UnityEngine.Object.Destroy(screenshot); }
                }
            }
            return new { silhouette = silhouette.ToString(), states = results };
        }
        finally { view.SetMapProgress(map, state); }
    }
}
