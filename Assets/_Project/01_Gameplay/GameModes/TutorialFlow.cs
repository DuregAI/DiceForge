using Diceforge.Progression;
using UnityEngine.SceneManagement;

public static class TutorialFlow
{
    public const string MainMenuSceneName = "MainMenu";
    public const string TutorialSceneName = "Tutorial";
    public const string ReplayConfirmationText = "Tutorial already completed. Replay?";

    public static bool RequiresReplayConfirmation()
    {
        return ProfileService.IsTutorialCompleted();
    }

    public static void EnterTutorial()
    {
        SceneManager.LoadScene(TutorialSceneName);
    }

    public static void ExitTutorial()
    {
        SceneManager.LoadScene(MainMenuSceneName);
    }

    public static void CompleteTutorialAndExit()
    {
        ProfileService.SetTutorialCompleted(true);
        ExitTutorial();
    }
}
