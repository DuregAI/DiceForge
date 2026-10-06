using System.Collections;
using System.Collections.Generic;
using Diceforge.Core;
using Diceforge.Diagnostics;
using Diceforge.Map;
using Diceforge.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Diceforge.View
{
    public sealed class ResultOverlayView : MonoBehaviour
    {
        private System.IDisposable backdropDismiss;
        private const string DatabasePath = "Progression/ProgressionDatabase";
        private const float RewardStageDelaySeconds = 0.45f;
        private const float XpStageDelaySeconds = 0.9f;

        [SerializeField] private UIDocument document;
        [SerializeField] private BattleDebugController battleController;
        [SerializeField] private bool showProgressionDetails = false;

        private ProgressionDatabase _database;
        private VisualElement _root;
        private VisualElement _overlayRoot;
        private VisualElement _panel;
        private Image _fxBackLayer;
        private Image _fxFrontLayer;
        private Label _resultLabel;
        private Label _summaryLabel;
        private VisualElement _xpStage;
        private Label _xpValueLabel;
        private VisualElement _rewardsContainer;
        private ScrollView _rewardsList;
        private Button _restartButton;
        private Button _backToMenuButton;
        private Button _retrySaveButton;
        private LevelUpWindowPresenter _levelUpPresenter;
        private ChestRewardWindowPresenter _chestRewardPresenter;
        private RewardPopupEffectsBridge _backEffectsBridge;
        private RewardPopupEffectsBridge _frontEffectsBridge;
        private Coroutine _presentationRoutine;
        private bool _isVisible;
        private bool _lastRestartVisibleState = true;
        private MenuLocalization localization;
        private string _languagePreference;
        private string T(string source) => localization != null ? localization.T(source) : source;

        private void OnEnable()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (battleController == null)
                battleController = FindAnyObjectByType<BattleDebugController>();

            _root = document != null ? document.rootVisualElement : null;
            if (_root == null)
                return;
            document.sortingOrder = 20;

            if (showProgressionDetails)
                InitializeProgressionPresentation();
            localization = new MenuLocalization(_root, null);
            _languagePreference = PlayerPrefs.GetString(MenuLocalization.PreferenceKey, "en");
            _levelUpPresenter?.SetTranslator(T);
            _chestRewardPresenter?.SetTranslator(T);

            _root.pickingMode = PickingMode.Ignore;

            _overlayRoot = _root.Q<VisualElement>("resultOverlayRoot");
            _panel = _root.Q<VisualElement>("resultPanel");
            _fxBackLayer = _root.Q<Image>("resultFxBackLayer");
            _fxFrontLayer = _root.Q<Image>("resultFxFrontLayer");
            _resultLabel = _root.Q<Label>("resultLabel");
            _summaryLabel = _root.Q<Label>("resultSummaryLabel");
            _xpStage = _root.Q<VisualElement>("resultXpStage");
            _xpValueLabel = _root.Q<Label>("resultXpValueLabel");
            _rewardsContainer = _root.Q<VisualElement>("resultRewardsContainer");
            _rewardsList = _root.Q<ScrollView>("resultRewardsList");
            _restartButton = _root.Q<Button>("restartButton");
            _backToMenuButton = _root.Q<Button>("backToMenuButton");
            backdropDismiss = new Diceforge.UI.ModalDismiss(_overlayRoot, _panel, HandleBackToMenuClicked);
            _retrySaveButton = _root.Q<Button>("retryResultSaveButton");
            if (_retrySaveButton == null && _panel != null)
            {
                _retrySaveButton = new Button { name = "retryResultSaveButton", text = "Retry save" };
                _retrySaveButton.AddToClassList("result-overlay-button");
                _retrySaveButton.AddToClassList("is-primary");
                (_root.Q("resultPanelContent") ?? _panel).Add(_retrySaveButton);
            }
            if (_retrySaveButton != null)
            {
                _retrySaveButton.style.display = DisplayStyle.None;
                _retrySaveButton.clicked += RetrySave;
            }

            if (_overlayRoot != null)
                _overlayRoot.pickingMode = PickingMode.Ignore;
            ConfigureFxLayer(_fxBackLayer);
            ConfigureFxLayer(_fxFrontLayer);

            if (_restartButton != null)
                _restartButton.clicked += HandleRestartClicked;
            if (_backToMenuButton != null)
                _backToMenuButton.clicked += HandleBackToMenuClicked;

            HideOverlay();

            if (battleController != null)
                battleController.OnMatchEnded += HandleMatchEnded;
            if (battleController != null && battleController.IsMatchEnded) ShowResult();
        }

        private void InitializeProgressionPresentation()
        {
            _levelUpPresenter = GetComponent<LevelUpWindowPresenter>() ?? gameObject.AddComponent<LevelUpWindowPresenter>();
            _levelUpPresenter.Initialize(_root);
            _chestRewardPresenter = GetComponent<ChestRewardWindowPresenter>() ?? gameObject.AddComponent<ChestRewardWindowPresenter>();
            _chestRewardPresenter.Initialize(_root);
            RewardPopupEffectsBridge[] effectBridges = GetComponents<RewardPopupEffectsBridge>();
            _backEffectsBridge = effectBridges.Length > 0
                ? effectBridges[0] : gameObject.AddComponent<RewardPopupEffectsBridge>();
            _frontEffectsBridge = effectBridges.Length > 1
                ? effectBridges[1] : gameObject.AddComponent<RewardPopupEffectsBridge>();
        }

        private void LateUpdate()
        {
            if (!_isVisible || localization == null || showProgressionDetails)
                return;

            string language = PlayerPrefs.GetString(MenuLocalization.PreferenceKey, "en");
            if (language == _languagePreference)
                return;

            _languagePreference = language;
            localization.Refresh();
            var session = battleController?.RewardSession;
            if (session == null)
                return;
            if (session.HasPendingSave)
                ShowResult();
            else if (!showProgressionDetails && session.Outcome != null)
                ShowSimpleOutcome(session.Outcome);
        }

        private void OnDisable()
        {
            localization?.Dispose();
            localization = null;
            backdropDismiss?.Dispose();
            if (battleController != null)
                battleController.OnMatchEnded -= HandleMatchEnded;
            if (_retrySaveButton != null) _retrySaveButton.clicked -= RetrySave;

            if (_restartButton != null)
                _restartButton.clicked -= HandleRestartClicked;
            if (_backToMenuButton != null)
                _backToMenuButton.clicked -= HandleBackToMenuClicked;

            StopPresentationRoutine();
            _backEffectsBridge?.StopActivePresentation();
            _frontEffectsBridge?.StopActivePresentation();
        }

        private void HandleMatchEnded(MatchResult result)
        {
            ShowResult();
        }

        private void RetrySave()
        {
            battleController?.RetryResultSave();
            ShowResult();
        }

        private void ShowResult()
        {
            var session = battleController?.RewardSession;
            if (session == null) return;
            DioramaHud.SetResultVisible(true);
            if (_overlayRoot != null)
            {
                _overlayRoot.style.display = DisplayStyle.Flex;
                _overlayRoot.pickingMode = PickingMode.Position;
                _overlayRoot.EnableInClassList("is-simple-result", !showProgressionDetails);
            }
            _panel?.EnableInClassList("is-save-error", session.HasPendingSave);
            if (session.HasPendingSave)
            {
                HideProgressionPresentation();
                SetOutcomeClasses(null);
                if (_resultLabel != null) _resultLabel.text = T("Could not save the result");
                UpdateSummaryText(T("Retry saving to continue."));
                _restartButton?.SetEnabled(false);
                _backToMenuButton?.SetEnabled(false);
                if (_retrySaveButton != null)
                {
                    _retrySaveButton.text = T("Retry save");
                    _retrySaveButton.style.display = DisplayStyle.Flex;
                }
                _isVisible = true;
                return;
            }
            PostBattleRewardOutcome outcome = session.Outcome;
            if (outcome == null) return;
            if (_retrySaveButton != null) _retrySaveButton.style.display = DisplayStyle.None;
            SetOutcomeClasses(outcome);
            if (!showProgressionDetails)
            {
                HideProgressionPresentation();
                ShowSimpleOutcome(outcome);
                return;
            }
            if (_levelUpPresenter == null || _chestRewardPresenter == null || _backEffectsBridge == null || _frontEffectsBridge == null)
            {
                InitializeProgressionPresentation();
                _levelUpPresenter.SetTranslator(T);
                _chestRewardPresenter.SetTranslator(T);
            }
            if (_fxBackLayer != null) _fxBackLayer.style.display = DisplayStyle.Flex;
            if (_fxFrontLayer != null) _fxFrontLayer.style.display = DisplayStyle.Flex;
            PrepareOutcomeView(outcome);
            if (session.PresentationStarted)
            {
                UpdateSummaryText(BuildFinalSummary(outcome));
                SetNavigationButtonsReady(true, outcome);
                _isVisible = true;
                return;
            }
            session.PresentationStarted = true;

            if (outcome.Won)
            {
                _backEffectsBridge?.BeginPresentation("level_up_arcane", _fxBackLayer, _resultLabel);
                _frontEffectsBridge?.BeginPresentation("level_up_arcane", _fxFrontLayer, _resultLabel);
            }
            else
            {
                _backEffectsBridge?.StopActivePresentation();
                _frontEffectsBridge?.StopActivePresentation();
            }

            _isVisible = true;
            _presentationRoutine = StartCoroutine(RunPresentationSequence(outcome));
        }

        private void SetOutcomeClasses(PostBattleRewardOutcome outcome)
        {
            _panel?.EnableInClassList("is-victory", outcome != null && !outcome.IsDraw && outcome.Won);
            _panel?.EnableInClassList("is-defeat", outcome != null && !outcome.IsDraw && !outcome.Won);
            _panel?.EnableInClassList("is-draw", outcome != null && outcome.IsDraw);
        }

        private void ShowSimpleOutcome(PostBattleRewardOutcome outcome)
        {
            if (_resultLabel != null)
                _resultLabel.text = T(battleController?.DemoLevel != null
                    ? outcome.Won ? "Trail complete" : "Let's try again"
                    : outcome.IsDraw ? "Draw" : outcome.Won ? "Victory" : "Defeat");
            UpdateSummaryText(T(battleController?.DemoLevel != null
                ? outcome.Won ? battleController.DemoLevel.levelId == "L6" ? "The shared light is back. The wedding took place." : "Everyone has reached the exit." : "This trail is still waiting for your friends."
                : outcome.IsDraw ? "Neither side is ahead."
                : outcome.Won ? "The battle is won." : "A new attempt awaits."));
            SetNavigationButtonsReady(true, outcome);
            _isVisible = true;
        }

        private void HideProgressionPresentation()
        {
            StopPresentationRoutine();
            _backEffectsBridge?.StopActivePresentation();
            _frontEffectsBridge?.StopActivePresentation();
            _rewardsList?.Clear();
            SetXpStageVisible(false);
            if (_rewardsContainer != null) _rewardsContainer.style.display = DisplayStyle.None;
            if (_fxBackLayer != null) { _fxBackLayer.image = null; _fxBackLayer.style.display = DisplayStyle.None; }
            if (_fxFrontLayer != null) { _fxFrontLayer.image = null; _fxFrontLayer.style.display = DisplayStyle.None; }
            foreach (string id in new[] { "resultChestContainer", "levelUpOverlay", "chestRewardOverlay" })
            {
                var element = _root?.Q(id);
                if (element != null) element.style.display = DisplayStyle.None;
            }
        }

        private void PrepareOutcomeView(PostBattleRewardOutcome outcome)
        {
            if (_resultLabel != null)
                _resultLabel.text = T(battleController?.DemoLevel != null
                    ? outcome.Won ? "Trail complete" : "Let's try again"
                    : outcome.IsDraw ? "Draw" : outcome.Won ? "Victory" : "Defeat");

            if (_xpValueLabel != null)
                _xpValueLabel.text = $"+{outcome.ApplicationResult.XpGained} XP";

            PopulateRewardSummary(outcome.RewardBundle);
            SetXpStageVisible(false);
            SetNavigationButtonsReady(false, outcome);
            UpdateSummaryText(BuildInitialSummary(outcome));
        }

        private IEnumerator RunPresentationSequence(PostBattleRewardOutcome outcome)
        {
            yield return new WaitForSecondsRealtime(RewardStageDelaySeconds);

            if (outcome.ApplicationResult.XpGained > 0)
            {
                SetXpStageVisible(true);
                UpdateSummaryText(string.Format(T(outcome.Won
                    ? "XP +{0} fuels the forge."
                    : "XP +{0} recovered from the clash."), outcome.ApplicationResult.XpGained));
                yield return new WaitForSecondsRealtime(XpStageDelaySeconds);
            }

            if (outcome.ApplicationResult.LevelUpData != null)
            {
                bool levelUpClosed = false;
                _backEffectsBridge?.StopActivePresentation();
                _frontEffectsBridge?.StopActivePresentation();
                _levelUpPresenter?.Show(outcome.ApplicationResult.LevelUpData, () => levelUpClosed = true);

                while (!levelUpClosed)
                    yield return null;

                if (outcome.Won)
                {
                    _backEffectsBridge?.BeginPresentation("level_up_arcane", _fxBackLayer, _resultLabel);
                    _frontEffectsBridge?.BeginPresentation("level_up_arcane", _fxFrontLayer, _resultLabel);
                }
            }

            if (outcome.ApplicationResult.HasChestRewards)
            {
                ChestRewardPresentationData chestPresentation = BuildChestPresentationData(outcome.ApplicationResult);
                if (chestPresentation != null)
                {
                    bool chestClosed = false;
                    _backEffectsBridge?.StopActivePresentation();
                    _frontEffectsBridge?.StopActivePresentation();
                    UpdateSummaryText(BuildChestSummary(chestPresentation.TotalChestCount));
                    _chestRewardPresenter?.Show(chestPresentation, () => chestClosed = true);

                    while (!chestClosed)
                        yield return null;
                }

                if (outcome.Won)
                {
                    _backEffectsBridge?.BeginPresentation("level_up_arcane", _fxBackLayer, _resultLabel);
                    _frontEffectsBridge?.BeginPresentation("level_up_arcane", _fxFrontLayer, _resultLabel);
                }
            }

            UpdateSummaryText(BuildFinalSummary(outcome));
            SetNavigationButtonsReady(true, outcome);
            _presentationRoutine = null;
        }

        private void HandleRestartClicked()
        {
            if (battleController != null && battleController.HasPendingResultSave) return;
            if (_restartButton != null && !_restartButton.enabledSelf)
                return;

            Diceforge.Transitions.ScreenTransition.Switch(() =>
            {
                _backEffectsBridge?.StopActivePresentation();
                _frontEffectsBridge?.StopActivePresentation();
                battleController?.RestartMatch();
                HideOverlay();
            });
        }

        private void HandleBackToMenuClicked()
        {
            if (Diceforge.Transitions.ScreenTransition.IsBusy) return;
            if (battleController != null && battleController.HasPendingResultSave) return;
            if (_backToMenuButton != null && !_backToMenuButton.enabledSelf)
                return;

            _backEffectsBridge?.StopActivePresentation();
            _frontEffectsBridge?.StopActivePresentation();

            if (MapFlowRuntime.IsMapBattleActive)
                MapFlowRuntime.RequestReturnToMap();

            string currentSceneName = SceneManager.GetActiveScene().name;
            if (!string.Equals(currentSceneName, "MainMenu", System.StringComparison.Ordinal))
                Diceforge.Transitions.ScreenTransition.LoadScene("MainMenu");
        }

        private void HideOverlay()
        {
            StopPresentationRoutine();
            DioramaHud.SetResultVisible(false);

            if (_overlayRoot != null)
            {
                _overlayRoot.style.display = DisplayStyle.None;
                _overlayRoot.pickingMode = PickingMode.Ignore;
            }

            if (_rewardsList != null)
                _rewardsList.Clear();

            if (_fxBackLayer != null)
                _fxBackLayer.image = null;
            if (_fxFrontLayer != null)
                _fxFrontLayer.image = null;

            SetXpStageVisible(false);
            UpdateSummaryText(string.Empty);

            if (_restartButton != null)
                _restartButton.style.display = _lastRestartVisibleState ? DisplayStyle.Flex : DisplayStyle.None;

            _backEffectsBridge?.StopActivePresentation();
            _frontEffectsBridge?.StopActivePresentation();
            _isVisible = false;
        }

        private void StopPresentationRoutine()
        {
            if (_presentationRoutine == null)
                return;

            StopCoroutine(_presentationRoutine);
            _presentationRoutine = null;
        }

        private static void ConfigureFxLayer(Image layer)
        {
            if (layer == null)
                return;

            layer.scaleMode = ScaleMode.StretchToFill;
            layer.pickingMode = PickingMode.Ignore;
        }

        private void SetNavigationButtonsReady(bool ready, PostBattleRewardOutcome outcome)
        {
            bool showRestart = !(outcome.IsMapBattle && outcome.Won);
            _lastRestartVisibleState = showRestart;

            if (_restartButton != null)
            {
                _restartButton.style.display = showRestart ? DisplayStyle.Flex : DisplayStyle.None;
                _restartButton.text = T("Retry battle");
                _restartButton.EnableInClassList("is-primary", !outcome.Won);
                _restartButton.SetEnabled(ready && showRestart);
            }

            if (_backToMenuButton != null)
            {
                _backToMenuButton.text = T(outcome.IsMapBattle ? "To map" : "Back to menu");
                _backToMenuButton.EnableInClassList("is-primary", outcome.Won);
                _backToMenuButton.SetEnabled(ready);
            }
        }

        private void SetXpStageVisible(bool visible)
        {
            if (_xpStage != null)
                _xpStage.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateSummaryText(string text)
        {
            if (_summaryLabel != null)
                _summaryLabel.text = string.IsNullOrWhiteSpace(text) ? string.Empty : text;
        }

        private string BuildInitialSummary(PostBattleRewardOutcome outcome)
        {
            if (outcome.IsDraw)
                return T("Turn limit reached. Neither side is ahead. No rewards earned.");

            if (outcome.Won)
                return T(outcome.HasRewardSummary ? "Spoils gathered from this clash" : "The battle is won.");

            if (outcome.RewardBundle != null && !outcome.RewardBundle.IsEmpty)
                return T("Defeat rewards secured.");

            return T("No rewards earned this time.");
        }

        private string BuildFinalSummary(PostBattleRewardOutcome outcome)
        {
            if (outcome.ApplicationResult.LevelUpData != null)
                return string.Format(T("Level {0} reached."), outcome.ApplicationResult.LevelUpData.NewLevel);

            if (outcome.ApplicationResult.HasChestRewards)
                return BuildChestSummary(outcome.ApplicationResult.GrantedChests.Count);

            if (outcome.ApplicationResult.XpGained > 0)
                return string.Format(T("XP +{0} applied."), outcome.ApplicationResult.XpGained);

            return BuildInitialSummary(outcome);
        }

        private string BuildChestSummary(int chestCount)
        {
            return chestCount == 1
                ? T("A chest was added to your stash.")
                : string.Format(T("{0} chests were added to your stash."), chestCount);
        }

        private void PopulateRewardSummary(RewardBundle bundle)
        {
            if (_rewardsList == null)
                return;

            _rewardsList.Clear();
            bool hasEntries = false;

            if (bundle != null && bundle.currencies != null)
            {
                for (int i = 0; i < bundle.currencies.Count; i++)
                {
                    ProfileAmount currency = bundle.currencies[i];
                    if (currency == null || currency.amount <= 0)
                        continue;

                    CurrencyDefinition definition = FindCurrency(currency.id);
                    string name = definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
                        ? definition.displayName
                        : currency.id;
                    AddRewardRow(_rewardsList, definition != null ? definition.icon : null, $"+{currency.amount}", T(name));
                    hasEntries = true;
                }
            }

            if (bundle != null && bundle.items != null)
            {
                for (int i = 0; i < bundle.items.Count; i++)
                {
                    ProfileAmount item = bundle.items[i];
                    if (item == null || item.amount <= 0)
                        continue;

                    ItemDefinition definition = FindItem(item.id);
                    string name = definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
                        ? definition.displayName
                        : item.id;
                    AddRewardRow(_rewardsList, definition != null ? definition.icon : null, $"x{item.amount}", T(name));
                    hasEntries = true;
                }
            }

            if (_rewardsContainer != null)
                _rewardsContainer.style.display = hasEntries ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void AddRewardRow(ScrollView targetList, Sprite icon, string amount, string name)
        {
            if (targetList == null)
                return;

            var row = new VisualElement();
            row.AddToClassList("result-overlay-reward-row");

            var iconElement = new VisualElement();
            iconElement.AddToClassList("result-overlay-reward-icon");
            if (icon != null)
                iconElement.style.backgroundImage = new StyleBackground(icon);

            var amountLabel = new Label(amount);
            amountLabel.AddToClassList("result-overlay-reward-amount");

            var nameLabel = new Label(name);
            nameLabel.AddToClassList("result-overlay-reward-name");

            row.Add(iconElement);
            row.Add(amountLabel);
            row.Add(nameLabel);
            targetList.Add(row);
        }

        private ChestRewardPresentationData BuildChestPresentationData(RewardApplicationResult applicationResult)
        {
            if (applicationResult == null || !applicationResult.HasChestRewards)
                return null;

            ProgressionDatabase database = GetDatabase();
            ChestRewardPresentationData presentationData = ChestRewardPresentationData.CreateFromGrantedChests(
                database != null ? database.chestCatalog : null,
                applicationResult.GrantedChests,
                applicationResult.SourceContext,
                applicationResult.NewLevel,
                applicationResult.DidLevelUp);

            if (presentationData == null)
                Debug.LogError("[ResultOverlayView] Chest reward presentation was skipped because the granted chest payload could not be converted into presentation data.", this);

            return presentationData;
        }

        private CurrencyDefinition FindCurrency(string currencyId)
        {
            ProgressionDatabase database = GetDatabase();
            if (database == null || database.currencyCatalog == null || database.currencyCatalog.currencies == null)
                return null;

            return database.currencyCatalog.currencies.Find(c => c != null && c.id == currencyId);
        }

        private ItemDefinition FindItem(string itemId)
        {
            ProgressionDatabase database = GetDatabase();
            if (database == null || database.itemCatalog == null || database.itemCatalog.items == null)
                return null;

            return database.itemCatalog.items.Find(i => i != null && i.id == itemId);
        }

        private ProgressionDatabase GetDatabase()
        {
            _database ??= Resources.Load<ProgressionDatabase>(DatabasePath);
            return _database;
        }
    }
}
