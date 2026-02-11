using Diceforge.Progression;
using UnityEngine.SceneManagement;

public static class TutorialFlow
{
    public const string MainMenuSceneName = "MainMenu";
    public const string TutorialSceneName = "Tutorial";
    public const string ReplayConfirmationText = "Tutorial already completed. Replay?";

    private static GameModePreset _tutorialPreset;
    public static bool IsTrainingBattleActive { get; private set; }

    public static bool RequiresReplayConfirmation()
    {
        return ProfileService.IsTutorialCompleted();
    }

    public static void EnterTutorial(GameModePreset tutorialPreset = null)
    {
        _tutorialPreset = tutorialPreset;
        IsTrainingBattleActive = false;
        SceneManager.LoadScene(TutorialSceneName);
    }

    public static void StartTrainingBattle()
    {
        if (_tutorialPreset != null)
        {
            GameModeSelection.SetSelected(_tutorialPreset);
        }

        IsTrainingBattleActive = true;
        SceneManager.LoadScene("Battle");
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
