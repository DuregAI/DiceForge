using Diceforge.Battle;
using Diceforge.MapSystem;
using Diceforge.Progression;
using UnityEngine.SceneManagement;

public static class TutorialFlow
{
    public const string MainMenuSceneName = "MainMenu";
    public const string TutorialSceneName = "Tutorial";
    public const string ReplayConfirmationText = "Tutorial already completed. Replay?";

    private static GameModePreset _tutorialPreset;
    private static BattleMapConfig _tutorialMapConfig;
    public static bool IsTrainingBattleActive { get; private set; }

    public static bool RequiresReplayConfirmation()
    {
        return ProfileService.IsTutorialCompleted();
    }

    public static void EnterTutorial(GameModePreset tutorialPreset, BattleMapConfig tutorialMapConfig)
    {
        _tutorialPreset = tutorialPreset;
        _tutorialMapConfig = tutorialMapConfig;
        IsTrainingBattleActive = false;
        SceneManager.LoadScene(TutorialSceneName);
    }

    public static void StartTrainingBattle()
    {
        if (_tutorialPreset == null)
            throw new System.InvalidOperationException("[TutorialFlow] StartTrainingBattle failed: tutorial preset is not assigned.");

        if (_tutorialMapConfig == null)
            throw new System.InvalidOperationException($"[TutorialFlow] StartTrainingBattle failed: tutorial map is not assigned for preset '{_tutorialPreset.name}'.");

        IsTrainingBattleActive = true;
        BattleLauncher.Start(new BattleStartRequest(_tutorialPreset, _tutorialMapConfig));
    }

    public static void ExitTutorial()
    {
        IsTrainingBattleActive = false;
        SceneManager.LoadScene(MainMenuSceneName);
    }

    public static void CompleteTutorialAndExit()
    {
        ProfileService.SetTutorialCompleted(true);
        ExitTutorial();
    }
}
