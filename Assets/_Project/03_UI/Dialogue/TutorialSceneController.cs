using Diceforge.Dialogue;
using Diceforge.UI.Dialogue;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class TutorialSceneController : MonoBehaviour
{
    [SerializeField] private UIDocument tutorialDocument;
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private DialogueSequence tutorialIntroSequence;
    [SerializeField] private TutorialPortraitLibrary tutorialPortraitLibrary;

    private Button _exitButton;
    private DialogueSequence _runtimeSequence;

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

        _runtimeSequence = BuildRuntimeSequence(tutorialIntroSequence);
        if (!dialogueRunner.Play(_runtimeSequence, HandleTutorialDialogueFinished))
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

        if (_runtimeSequence != null)
        {
            Destroy(_runtimeSequence);
            _runtimeSequence = null;
        }
    }

    private DialogueSequence BuildRuntimeSequence(DialogueSequence source)
    {
        if (source == null)
        {
            return null;
        }

        var runtimeSequence = ScriptableObject.CreateInstance<DialogueSequence>();
        runtimeSequence.lines = new System.Collections.Generic.List<DialogueLine>(source.lines.Count);
        foreach (var sourceLine in source.lines)
        {
            if (sourceLine == null)
            {
                continue;
            }

            runtimeSequence.lines.Add(new DialogueLine
            {
                speakerId = sourceLine.speakerId,
                text = sourceLine.text,
                portrait = sourceLine.portrait != null ? sourceLine.portrait : tutorialPortraitLibrary?.GetDefaultBySpeakerId(sourceLine.speakerId)
            });
        }

        return runtimeSequence;
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
