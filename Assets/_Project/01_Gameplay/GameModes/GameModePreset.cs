using Diceforge.Core;
using Diceforge.Presets;
using UnityEngine;

[CreateAssetMenu(menuName = "DiceForge/Game Mode Preset", fileName = "GM_New")]
public class GameModePreset : ScriptableObject
{
    public string modeId;
    public string displayName;
    public RulesetPreset rulesetPreset;
    public DiceBagDefinition diceBagA;
    public DiceBagDefinition diceBagB;
    public SetupPreset setupPreset;

    private void OnValidate()
    {
        Validate();
    }

    public void Validate()
    {
        if (rulesetPreset == null)
        {
            Debug.LogWarning("[GameModePreset] Missing RulesetPreset reference.", this);
        }

        if (diceBagA == null)
        {
            Debug.LogWarning("[GameModePreset] Missing DiceBagDefinition for Bag A.", this);
        }

        if (diceBagB == null)
        {
            Debug.LogWarning("[GameModePreset] Missing DiceBagDefinition for Bag B.", this);
        }

        if (setupPreset == null)
        {
            Debug.LogWarning("[GameModePreset] Missing SetupPreset reference.", this);
        }
    }
}
