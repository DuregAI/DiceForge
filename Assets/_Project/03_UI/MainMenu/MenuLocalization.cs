using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Only the menu owns these bindings. Other screens can adopt the same catalog later.
internal sealed class MenuLocalization : IDisposable
{
    internal const string PreferenceKey = "ui.language";
    [Serializable] private sealed class Catalog { public Entry[] entries; }
    [Serializable] private sealed class Entry { public string en; public string ru; public string zh; }
    private readonly Dictionary<string, Entry> entries = new();
    private readonly List<Action> bindings = new();
    private readonly VisualElement root;
    private readonly Button button;
    private readonly LanguageBadge badge;
    private readonly Action changed;
    private Font bodyFont;
    private Font russianFont;
    public int Language { get; private set; }

    public MenuLocalization(VisualElement root, Action changed)
    {
        this.root = root;
        this.changed = changed;
        string saved = PlayerPrefs.GetString(PreferenceKey, "en");
        Language = saved == "ru" ? 1 : saved == "zh-Hans" ? 2 : 0;
        var asset = Resources.Load<TextAsset>("Localization/menu");
        if (asset != null)
            foreach (var entry in JsonUtility.FromJson<Catalog>(asset.text).entries)
                entries.Add(entry.en, entry);
        foreach (string scope in new[] { "MenuPanel", "SettingsPanel", "FeedbackModal" })
        {
            var panel = root.Q(scope);
            if (panel == null) continue;
            panel.Query<TextElement>().ForEach(element =>
            {
                // Bind the original source once, never attempt to translate an already translated value.
                string source = element.text;
                if (!string.IsNullOrEmpty(source) && entries.ContainsKey(source))
                    bindings.Add(() => element.text = T(source));
            });
            panel.Query<VisualElement>().ForEach(element =>
            {
                string source = element.tooltip;
                if (!string.IsNullOrEmpty(source) && entries.ContainsKey(source))
                    bindings.Add(() => element.tooltip = T(source));
            });
        }
        button = root.Q<Button>("btnLanguage");
        badge = new LanguageBadge();
        if (button != null) { button.Add(badge); button.clicked += Cycle; }
        Apply();
    }

    public string T(string source)
    {
        if (source == null || !entries.TryGetValue(source, out var entry)) return source;
        string result = Language == 1 ? entry.ru : Language == 2 ? entry.zh : entry.en;
        return string.IsNullOrEmpty(result) ? entry.en : result;
    }

    private void Cycle()
    {
        Language = (Language + 1) % 3;
        PlayerPrefs.SetString(PreferenceKey, Language == 1 ? "ru" : Language == 2 ? "zh-Hans" : "en");
        PlayerPrefs.Save();
        Apply();
        changed?.Invoke();
    }

    private void Apply()
    {
        root.EnableInClassList("locale-ru", Language == 1);
        root.EnableInClassList("locale-zh", Language == 2);
        if (Language != 0)
        {
            bodyFont ??= Resources.Load<Font>("Localization/NotoSansCJKsc-Regular");
            russianFont ??= Resources.Load<Font>("Localization/RobotoSlab");
        }
        foreach (string scope in new[] { "MenuPanel", "SettingsPanel", "FeedbackModal" })
        {
            var panel = root.Q(scope);
            if (panel == null) continue;
            panel.style.unityFontDefinition = Language == 0 ? new StyleFontDefinition(StyleKeyword.Null) : new StyleFontDefinition(FontDefinition.FromFont(bodyFont));
        }
        foreach (string id in new[] { "btnLong", "btnTutorial" })
        {
            var action = root.Q<Button>(id);
            if (action != null) action.style.unityFontDefinition = Language == 0
                ? new StyleFontDefinition(StyleKeyword.Null)
                : new StyleFontDefinition(FontDefinition.FromFont(Language == 1 ? russianFont : bodyFont));
        }
        foreach (var binding in bindings) binding();
        badge.SetLanguage(Language);
        if (button != null) button.tooltip = Language == 0 ? "English · click for Русский" : Language == 1 ? "Русский · нажмите для 中文" : "简体中文 · 点击切换 English";
    }

    public void Dispose() { if (button != null) button.clicked -= Cycle; }
}
