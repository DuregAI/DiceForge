using UnityEngine;
using UnityEngine.UIElements;

internal static class SharedSettingsPanel
{
    internal static VisualElement AttachTo(VisualElement parent)
    {
        var template = Resources.Load<VisualTreeAsset>("SharedSettingsPanel");
        var style = Resources.Load<StyleSheet>("SharedSettingsPanel");
        if (parent == null || template == null || style == null)
        {
            Debug.LogError("[Settings] Shared settings UI or stylesheet is missing.");
            return null;
        }

        var tree = template.Instantiate();
        var overlay = tree.Q<VisualElement>("SettingsPanel");
        if (overlay == null)
        {
            Debug.LogError("[Settings] SettingsPanel is missing from the shared template.");
            return null;
        }

        overlay.RemoveFromHierarchy();
        parent.Add(overlay);
        if (!parent.styleSheets.Contains(style)) parent.styleSheets.Add(style);
        return overlay;
    }
}
