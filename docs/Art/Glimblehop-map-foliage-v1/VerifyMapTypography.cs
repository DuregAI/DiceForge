using System;
using Diceforge.Map;
using UnityEngine;
using UnityEngine.UIElements;

public static class VerifyMapTypography
{
    public static object Check()
    {
        bool hadPreference = PlayerPrefs.HasKey("ui.language");
        string original = PlayerPrefs.GetString("ui.language", "en");
        int checkedLanguages = 0;
        try
        {
            foreach (string language in new[] { "en", "ru", "zh-Hans" })
            {
                PlayerPrefs.SetString("ui.language", language);
                using (var view = new WoodlandMapView(Resources.Load<StyleSheet>("Map/WoodlandMap")))
                {
                    view.SetProgress(3);
                    view.SetVisible(true);
                    var action = view.Root.Q<Label>("WoodlandActionText");
                    if (language == "en" && action.text != "PLAY LEVEL 4") throw new Exception("English action changed");
                    if (language != "en")
                    {
                        var number = action.Q<Label>(className: "wm-fixed-digits");
                        if (number == null || number.text != "4") throw new Exception("Missing separate original-font digit: " + language);
                        if (action.style.unityFontStyleAndWeight.value != FontStyle.Bold) throw new Exception("Missing bold: " + language);
                    }
                    if (view.Root.Q<Label>("WoodlandProgress").text != "3 / 6") throw new Exception("Progress changed");
                    view.SetProgress(6);
                    if (action.Q<Label>(className: "wm-fixed-digits") != null) throw new Exception("Stale level digit at completion");
                    view.SetVisible(false);
                    checkedLanguages++;
                }
            }
        }
        finally
        {
            if (hadPreference) PlayerPrefs.SetString("ui.language", original);
            else PlayerPrefs.DeleteKey("ui.language");
        }
        return new { checkedLanguages, passed = true, preferenceRestored = true };
    }
}
