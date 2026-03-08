using Diceforge.Audio;
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
    private Button _settingsButton;
    private Button _closeSettingsButton;
    private VisualElement _settingsOverlay;
    private Slider _musicSlider;
    private Slider _sfxSlider;
    private DialogueSequence _runtimeSequence;
    private AudioManager _audioManager;
    private bool _isSettingsOverlayVisible;

    private void Awake()
    {
        if (tutorialDocument == null)
            tutorialDocument = GetComponent<UIDocument>();

        if (dialogueRunner == null)
            dialogueRunner = GetComponent<DialogueRunner>();

        var root = tutorialDocument?.rootVisualElement;
        _exitButton = root?.Q<Button>("TutorialExitButton");
        _settingsButton = root?.Q<Button>("TutorialSettingsButton");
        _closeSettingsButton = root?.Q<Button>("TutorialSettingsCloseButton");
        _settingsOverlay = root?.Q<VisualElement>("TutorialSettingsOverlay");
        _musicSlider = root?.Q<Slider>("TutorialMusicSlider");
        _sfxSlider = root?.Q<Slider>("TutorialSfxSlider");

        if (_exitButton != null)
            _exitButton.clicked += ExitTutorial;

        if (_settingsButton != null)
            _settingsButton.clicked += ToggleSettingsOverlay;

        if (_closeSettingsButton != null)
            _closeSettingsButton.clicked += CloseSettingsOverlay;

        if (_musicSlider != null)
            _musicSlider.RegisterValueChangedCallback(HandleMusicSliderChanged);

        if (_sfxSlider != null)
            _sfxSlider.RegisterValueChangedCallback(HandleSfxSliderChanged);

        if (_settingsOverlay != null)
            _settingsOverlay.style.display = DisplayStyle.None;
    }

    private void Start()
    {
        _audioManager = AudioManager.Instance != null
            ? AudioManager.Instance
            : FindAnyObjectByType<AudioManager>();
        _audioManager?.EnsureMusicForContext(MusicContext.Tutorial);
        if (_audioManager != null)
        {
            _audioManager.OnVolumesChanged -= HandleAudioVolumesChanged;
            _audioManager.OnVolumesChanged += HandleAudioVolumesChanged;
            RefreshVolumeSliders();
        }

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
            _exitButton.clicked -= ExitTutorial;

        if (_settingsButton != null)
            _settingsButton.clicked -= ToggleSettingsOverlay;

        if (_closeSettingsButton != null)
            _closeSettingsButton.clicked -= CloseSettingsOverlay;

        if (_musicSlider != null)
            _musicSlider.UnregisterValueChangedCallback(HandleMusicSliderChanged);

        if (_sfxSlider != null)
            _sfxSlider.UnregisterValueChangedCallback(HandleSfxSliderChanged);

        if (_audioManager != null)
            _audioManager.OnVolumesChanged -= HandleAudioVolumesChanged;

        if (_runtimeSequence != null)
        {
            Destroy(_runtimeSequence);
            _runtimeSequence = null;
        }
    }

    private DialogueSequence BuildRuntimeSequence(DialogueSequence source)
    {
        if (source == null)
            return null;

        var runtimeSequence = ScriptableObject.CreateInstance<DialogueSequence>();
        runtimeSequence.lines = new System.Collections.Generic.List<DialogueLine>(source.lines.Count);
        foreach (var sourceLine in source.lines)
        {
            if (sourceLine == null)
                continue;

            runtimeSequence.lines.Add(new DialogueLine
            {
                speakerId = sourceLine.speakerId,
                text = sourceLine.text,
                portrait = sourceLine.portrait != null ? sourceLine.portrait : tutorialPortraitLibrary?.GetDefaultBySpeakerId(sourceLine.speakerId)
            });
        }

        return runtimeSequence;
    }

    private void ToggleSettingsOverlay()
    {
        _isSettingsOverlayVisible = !_isSettingsOverlayVisible;
        UpdateSettingsOverlayState();
    }

    private void CloseSettingsOverlay()
    {
        _isSettingsOverlayVisible = false;
        UpdateSettingsOverlayState();
    }

    private void UpdateSettingsOverlayState()
    {
        if (_settingsOverlay != null)
            _settingsOverlay.style.display = _isSettingsOverlayVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void HandleMusicSliderChanged(ChangeEvent<float> evt)
    {
        _audioManager?.SetMusicVolume(evt.newValue);
    }

    private void HandleSfxSliderChanged(ChangeEvent<float> evt)
    {
        _audioManager?.SetSfxVolume(evt.newValue);
    }

    private void HandleAudioVolumesChanged(float musicVolume, float sfxVolume)
    {
        if (_musicSlider != null)
            _musicSlider.SetValueWithoutNotify(musicVolume);

        if (_sfxSlider != null)
            _sfxSlider.SetValueWithoutNotify(sfxVolume);
    }

    private void RefreshVolumeSliders()
    {
        if (_audioManager == null)
            return;

        if (_musicSlider != null)
            _musicSlider.SetValueWithoutNotify(_audioManager.MusicVolume);

        if (_sfxSlider != null)
            _sfxSlider.SetValueWithoutNotify(_audioManager.SfxVolume);
    }

    private static void ExitTutorial()
    {
        TutorialFlow.ExitTutorial();
    }

    private static void HandleTutorialDialogueFinished()
    {
        TutorialFlow.StartTrainingBattle();
    }
}
