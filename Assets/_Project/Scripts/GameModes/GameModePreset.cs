using UnityEngine;

[CreateAssetMenu(menuName = "DiceForge/Game Mode Preset", fileName = "GM_New")]
public class GameModePreset : ScriptableObject
{
    public string modeId;
    public string displayName;
    public ScriptableObject ruleset;
    public ScriptableObject diceBagA;
    public ScriptableObject diceBagB;
    public ScriptableObject setup;
}
