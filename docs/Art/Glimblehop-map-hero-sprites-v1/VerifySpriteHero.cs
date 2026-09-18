using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Diceforge.Map;
using UnityEngine;
using UnityEngine.UIElements;

public static class VerifySpriteHero
{
    private static void Capture(string name)
    {
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        try { File.WriteAllBytes(Path.GetFullPath("docs/Art/Glimblehop-map-hero-sprites-v1/" + name + ".png"), image.EncodeToPNG()); }
        finally { UnityEngine.Object.Destroy(image); }
    }

    public static async Task<object> Check()
    {
        var controller = UnityEngine.Object.FindAnyObjectByType<MapController>();
        var presenter = controller.GetComponent<WoodlandSpriteHeroPresenter>();
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var view = (WoodlandMapView)typeof(MapController).GetField("_woodlandView",flags).GetValue(controller);
        var map = (MapDefinitionSO)typeof(MapController).GetField("_currentMap",flags).GetValue(controller);
        var state = (MapRunState)typeof(MapController).GetField("_currentState",flags).GetValue(controller);
        var hero = view.HeroImage;
        if (hero.sprite == null || !hero.sprite.name.StartsWith("idle_")) throw new Exception("Missing idle sprite");
        if (GameObject.Find("WoodlandHeroStudio") != null) throw new Exception("Old 3D renderer is still running");
        if (Resources.LoadAll<Sprite>("Map/HeroSprites/goblin-atlas").Length != 30) throw new Exception("Wrong frame count");
        if (hero.pickingMode != PickingMode.Ignore) throw new Exception("Hero intercepts clicks");
        Capture("unity-sprite-idle");
        try
        {
            foreach (int completed in new[] { 0,4,5,6 })
            {
                view.SetProgress(completed);
                await Task.Delay(250);
                Capture("unity-sprite-progress-" + completed);
            }
        }
        finally { view.SetMapProgress(map,state); }
        presenter.SetMoving(true);
        var frames = new HashSet<string>();
        for (int i=0;i<22;i++) { await Task.Delay(45); frames.Add(hero.sprite.name); }
        if (frames.Count < 10) throw new Exception("Run animation did not advance");
        Capture("unity-sprite-run");
        presenter.SetMoving(true,true);
        await Task.Delay(100);
        if (hero.resolvedStyle.scale.value.x >= 0) throw new Exception("Facing left failed");
        presenter.SetMoving(false);
        await Task.Delay(100);
        var idleFrames = new HashSet<string>();
        for (int i=0;i<35;i++) { await Task.Delay(100); idleFrames.Add(hero.sprite.name); }
        if (idleFrames.Count < 3) throw new Exception("Idle blink did not advance");
        var back = view.Root.Q<Button>("WoodlandBack");
        using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = back; back.SendEvent(evt); }
        await Task.Delay(1400);
        if (hero.sprite != null) throw new Exception("Sprite presentation survived map close");
        return new { runFramesObserved = frames.Count, idleFramesObserved = idleFrames.Count, leftFacing = true, cleanup = true, no3DCamera = true };
    }
}
