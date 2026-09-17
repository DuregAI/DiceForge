// Run with Unity CLI run_script; outside Assets so it never enters the player build.
using System;
using System.Linq;
using System.Threading.Tasks;
using Diceforge.Map;
using Diceforge.Transitions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public static class VerifyMapIntegration
{
    public static object Start()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu" || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Open the saved MainMenu scene first.");
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        return "Play mode requested";
    }

    private static UIDocument MenuDocument() => UnityEngine.Object.FindAnyObjectByType<MainMenuController>().GetComponent<UIDocument>();
    private static void Submit(Button button)
    {
        if (button == null || !button.enabledInHierarchy) throw new InvalidOperationException("Button unavailable");
        using var evt = NavigationSubmitEvent.GetPooled();
        evt.target = button;
        button.SendEvent(evt);
    }

    private static async Task WaitForTransition()
    {
        await Task.Delay(200);
        for (int i = 0; i < 120 && ScreenTransition.IsBusy; i++) await Task.Delay(100);
        if (ScreenTransition.IsBusy) throw new InvalidOperationException("Transition timed out");
    }

    public static async Task<object> OpenMap()
    {
        Submit(MenuDocument().rootVisualElement.Q<Button>("btnLong"));
        await WaitForTransition();
        return Inspect();
    }

    public static async Task<object> OpenAndBack()
    {
        await OpenMap();
        return await Back();
    }

    public static object ValidateContent()
    {
        var map = MapDefinitionSO.LoadChapter("Chapter1");
        if (Resources.Load<StyleSheet>("Map/WoodlandMap") == null) throw new Exception("Missing runtime stylesheet");
        var flow = UnityEngine.Object.FindAnyObjectByType<MapFlowOrchestrator>();
        var serialized = new SerializedObject(flow);
        var presets = serialized.FindProperty("battlePresets");
        foreach (var node in map.nodes)
        {
            bool found = false;
            for (int i = 0; i < presets.arraySize; i++)
            {
                var preset = presets.GetArrayElementAtIndex(i).objectReferenceValue;
                if (preset == null) continue;
                var data = new SerializedObject(preset);
                if (data.FindProperty("modeId").stringValue != node.battlePresetId) continue;
                if (data.FindProperty("mapConfig").objectReferenceValue == null) throw new Exception("Preset has no board");
                found = true;
            }
            if (!found) throw new Exception("Missing battle preset " + node.battlePresetId);
        }
        return new { levels = map.nodes.Select(n=>n.id).ToArray(), allPresetsWired = true, runtimeStyle = true };
    }

    public static object Inspect()
    {
        var map = MapDefinitionSO.LoadChapter("Chapter1");
        var root = MenuDocument().rootVisualElement;
        var surface = root.Q("WoodlandMapRoot");
        if (surface == null || surface.resolvedStyle.display == DisplayStyle.None) throw new Exception("New map is not visible");
        var labels = new[] { "WoodlandTitle", "WoodlandActionText", "WoodlandProgress" }.Select(name =>
        {
            var label = root.Q<Label>(name);
            var size = label.MeasureTextSize(label.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
            if (size.x > label.contentRect.width + 1) throw new Exception("Text overflow: " + name);
            return new { name, text = label.text, measured = size.x, available = label.contentRect.width,
                font = label.resolvedStyle.unityFont?.name };
        }).ToArray();
        return new { scene = SceneManager.GetActiveScene().name, levels = map.nodes.Count, labels,
            enabled = Enumerable.Range(1,6).Where(i => root.Q<Button>("WoodlandLevel"+i).enabledInHierarchy).ToArray(),
            bounds = surface.worldBound.ToString(), stage = root.Q("WoodlandMapStage").worldBound.ToString() };
    }

    public static async Task<object> Launch()
    {
        Submit(MenuDocument().rootVisualElement.Q<Button>("WoodlandPlay"));
        await WaitForTransition();
        return new { scene = SceneManager.GetActiveScene().name, node = MapFlowRuntime.SelectedNodeId, active = MapFlowRuntime.IsMapBattleActive };
    }

    // Return without reporting a win, loss or reward, so the player's progress is not advanced by QA.
    public static async Task<object> ReturnWithoutResult()
    {
        MapFlowRuntime.RequestReturnToMap();
        ScreenTransition.LoadScene("MainMenu");
        await WaitForTransition();
        return Inspect();
    }

    public static async Task<object> Back()
    {
        Submit(MenuDocument().rootVisualElement.Q<Button>("WoodlandBack"));
        await WaitForTransition();
        return new { mapDisplay = MenuDocument().rootVisualElement.Q("WoodlandMapRoot").resolvedStyle.display.ToString() };
    }

    public static object Stop() { EditorApplication.isPlaying = false; return "Stop requested"; }
}
