using Diceforge.Map;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class WoodlandMapPreviewWindow : EditorWindow
{
    private WoodlandMapView view;
    private VisualElement viewport;
    private Label status;

    [MenuItem("Glimblehop/Map/Preview Woodland Map")]
    public static void Open()
    {
        var window = GetWindow<WoodlandMapPreviewWindow>();
        window.titleContent = new GUIContent("Woodland Map");
        window.minSize = new Vector2(660, 460);
        window.Show();
    }

    public void CreateGUI()
    {
        view?.Dispose();
        rootVisualElement.Clear();
        var toolbar = new VisualElement();
        toolbar.style.flexDirection = FlexDirection.Row;
        toolbar.style.height = 26;
        var progress = new IntegerField("Completed") { value = 2 };
        progress.style.width = 210;
        progress.RegisterValueChangedCallback(e => view.SetProgress(e.newValue));
        toolbar.Add(progress);
        foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1024, 768), new Vector2(844, 390) })
        {
            var requested = size;
            toolbar.Add(new Button(() => SetViewport(requested.x, requested.y)) { text = size.x + " x " + size.y });
        }
        toolbar.Add(new Button(() => { viewport.style.width = StyleKeyword.Auto; viewport.style.height = StyleKeyword.Auto; viewport.style.flexGrow = 1; }) { text = "Fit window" });
        rootVisualElement.Add(toolbar);
        status = new Label("Presentation preview — no save writes or battle launch.");
        status.style.height = 22;
        rootVisualElement.Add(status);
        viewport = new VisualElement { name = "MapPreviewViewport" };
        viewport.style.flexShrink = 0;
        viewport.style.flexGrow = 1;
        rootVisualElement.Add(viewport);
        view = new WoodlandMapView(Resources.Load<StyleSheet>("Map/WoodlandMap"));
        view.LevelRequested += level => status.text = "Level " + level + " requested (preview only).";
        view.BackRequested += () => status.text = "Back requested (preview only).";
        viewport.Add(view.Root);
        view.SetProgress(2);
    }

    public void SetViewport(float width, float height)
    {
        viewport.style.flexGrow = 0;
        viewport.style.width = width;
        viewport.style.height = height;
    }

    private void OnDisable() { view?.Dispose(); view = null; }
}
