using UnityEngine;

public static class GameModeSelection
{
    private const string LastModeKey = "lastModeId";

    public static GameModePreset SelectedPreset { get; private set; }

    public static void SetSelected(GameModePreset preset)
    {
        SelectedPreset = preset;

        if (preset != null)
        {
            PlayerPrefs.SetString(LastModeKey, preset.modeId);
            PlayerPrefs.Save();
        }
    }
}
