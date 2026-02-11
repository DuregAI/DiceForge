using Diceforge.Dialogue;
using Diceforge.UI.Dialogue;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class TutorialSceneController : MonoBehaviour
{
    [SerializeField] private UIDocument tutorialDocument;
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private DialogueSequence tutorialIntroSequence;

    private Button _exitButton;

    private void Awake()
    {
        if (tutorialDocument == null)
        {
            tutorialDocument = GetComponent<UIDocument>();
        }

        if (dialogueRunner == null)
        {
            dialogueRunner = GetComponent<DialogueRunner>();
        }

        var root = tutorialDocument?.rootVisualElement;
        _exitButton = root?.Q<Button>("TutorialExitButton");
        if (_exitButton != null)
        {
            _exitButton.clicked += ExitTutorial;
        }
    }

    private void Start()
    {
        if (dialogueRunner == null)
        {
            Debug.LogWarning("[Tutorial] DialogueRunner is missing.");
            return;
        }

        if (!dialogueRunner.Play(tutorialIntroSequence, HandleTutorialDialogueFinished))
        {
            Debug.LogWarning("[Tutorial] Unable to start tutorial intro dialogue.");
        }
    }

    private void OnDestroy()
    {
        if (_exitButton != null)
        {
            _exitButton.clicked -= ExitTutorial;
        }
    }

    private static void ExitTutorial()
    {
        TutorialFlow.ExitTutorial();
    }

    private static void HandleTutorialDialogueFinished()
    {
        TutorialFlow.CompleteTutorialAndExit();
    }
}
