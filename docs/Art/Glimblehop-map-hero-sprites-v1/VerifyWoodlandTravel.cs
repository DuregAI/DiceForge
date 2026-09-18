using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Diceforge.Map;
using UnityEngine;
using UnityEngine.UIElements;

public static class VerifyWoodlandTravel
{
    public static async Task<object> Check()
    {
        var controller = UnityEngine.Object.FindAnyObjectByType<MapController>();
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var map = (MapDefinitionSO)typeof(MapController).GetField("_currentMap",flags).GetValue(controller);
        var original = (MapRunState)typeof(MapController).GetField("_currentState",flags).GetValue(controller);
        var callback = (Action<string>)typeof(MapController).GetField("_onNodeSelected",flags).GetValue(controller);
        var results = new List<object>();
        bool cancelled = false;
        int frame = 0;
        string output = Path.GetFullPath("docs/Art/Glimblehop-map-hero-sprites-v1/travel-capture");
        Directory.CreateDirectory(output);
        try
        {
            for (int from = 0; from < 5; from++)
            {
                var state = new MapRunState { currentNodeId = map.nodes[from+1].id };
                for (int i=0;i<=from;i++) state.MarkCompleted(map.nodes[i].id);
                for (int i=0;i<=from+1;i++) state.Unlock(map.nodes[i].id);
                int clicks = 0;
                controller.Show(map,state,_ => clicks++,false);
                var view = (WoodlandMapView)typeof(MapController).GetField("_woodlandView",flags).GetValue(controller);
                var hero = view.HeroImage;
                var destination = view.GetHeroGroundPosition(from+1);
                controller.PlayWoodlandTravel(map.nodes[from].id,map.nodes[from+1].id);
                if (!controller.IsHeroTravelling || view.Root.Q<Button>("WoodlandPlay").enabledSelf) throw new Exception("Travel did not lock level launch");
                using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = view.Root.Q<Button>("WoodlandPlay"); view.Root.Q<Button>("WoodlandPlay").SendEvent(evt); }
                if (clicks != 0) throw new Exception("Level launched during travel");
                var seen = new HashSet<string>();
                bool moving = false, sawLeft = false;
                float previousX = hero.style.left.value.value;
                for (int tick=0;tick<80 && controller.IsHeroTravelling;tick++)
                {
                    await Task.Delay(85);
                    if (hero.sprite != null) { seen.Add(hero.sprite.name); moving |= hero.sprite.name.StartsWith("run_"); }
                    sawLeft |= hero.resolvedStyle.scale.value.x < 0;
                    if (from==0)
                    {
                        var screenshot=ScreenCapture.CaptureScreenshotAsTexture();
                        try { File.WriteAllBytes(Path.Combine(output,"frame-"+(frame++).ToString("D3")+".png"),screenshot.EncodeToPNG()); }
                        finally { UnityEngine.Object.Destroy(screenshot); }
                    }
                }
                if (controller.IsHeroTravelling) throw new Exception("Travel timed out");
                await Task.Delay(60);
                var actual = new Vector2(hero.style.left.value.value+72,hero.style.top.value.value+135);
                if (Vector2.Distance(actual,destination)>.1f) throw new Exception("Wrong arrival position");
                if (!moving || seen.Count<3 || !hero.sprite.name.StartsWith("idle_")) throw new Exception("Run/idle switching failed");
                if (!view.Root.Q<Button>("WoodlandPlay").enabledSelf) throw new Exception("Launch not restored");
                if (from==3 && !sawLeft) throw new Exception("Left turn was not reflected");
                results.Add(new { from=from+1,to=from+2,arrived=true,runFrames=seen.Count,leftFacing=sawLeft });
            }
            var cancelState = new MapRunState { currentNodeId = map.nodes[1].id };
            cancelState.MarkCompleted(map.nodes[0].id);cancelState.Unlock(map.nodes[1].id);
            controller.Show(map,cancelState,_ => {},false);
            controller.PlayWoodlandTravel(map.nodes[0].id,map.nodes[1].id);
            await Task.Delay(450);
            controller.Hide();
            await Task.Delay(150);
            if (controller.IsHeroTravelling) throw new Exception("Hidden map kept travelling");
            cancelled=true;
        }
        finally { controller.Show(map,original,callback,false); }
        return new { routes=results,cancelled,capturedFrames=frame,saveWrites=false };
    }
}
