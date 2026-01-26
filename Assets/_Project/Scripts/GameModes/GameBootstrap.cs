using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private GameModePreset fallbackPreset;

    private void Start()
    {
        var selectedPreset = GameModeSelection.SelectedPreset;
        if (selectedPreset == null)
        {
            if (fallbackPreset != null)
            {
                Debug.LogWarning("[GameBootstrap] No preset selected, using fallback.");
                selectedPreset = fallbackPreset;
            }
            else
            {
                Debug.LogWarning("[GameBootstrap] No preset selected, using fallback.");
                return;
            }
        }

        MatchService.BuildFromPreset(selectedPreset);
        Debug.Log($"[GameBootstrap] Starting mode: {selectedPreset.modeId} / {selectedPreset.displayName}");
    }
}
