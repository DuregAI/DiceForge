using Diceforge.Core;
using Diceforge.Map;
using Diceforge.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Diceforge.View
{
    public sealed class ResultOverlayView : MonoBehaviour
    {
        private const string DatabasePath = "Progression/ProgressionDatabase";

        [SerializeField] private UIDocument document;
        [SerializeField] private BattleDebugController battleController;

        private ProgressionDatabase _database;
        private VisualElement _root;
        private VisualElement _overlayRoot;
        private VisualElement _panel;
        private Image _fxBackLayer;
        private Image _fxFrontLayer;
        private Label _resultLabel;
        private Label _summaryLabel;
        private VisualElement _rewardsContainer;
        private ScrollView _rewardsList;
        private Button _restartButton;
        private Button _backToMenuButton;
        private LevelUpWindowPresenter _levelUpPresenter;
        private RewardPopupEffectsBridge _backEffectsBridge;
        private RewardPopupEffectsBridge _frontEffectsBridge;
        private bool _isVisible;

        private void OnEnable()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (battleController == null)
                battleController = FindAnyObjectByType<BattleDebugController>();

            _root = document != null ? document.rootVisualElement : null;
            if (_root == null)
                return;

            _levelUpPresenter = GetComponent<LevelUpWindowPresenter>() ?? gameObject.AddComponent<LevelUpWindowPresenter>();
            _levelUpPresenter.Initialize(_root);
            RewardPopupEffectsBridge[] effectBridges = GetComponents<RewardPopupEffectsBridge>();
            if (effectBridges.Length > 0)
                _backEffectsBridge = effectBridges[0];
            else
                _backEffectsBridge = gameObject.AddComponent<RewardPopupEffectsBridge>();

            if (effectBridges.Length > 1)
                _frontEffectsBridge = effectBridges[1];
            else
                _frontEffectsBridge = gameObject.AddComponent<RewardPopupEffectsBridge>();

            _root.pickingMode = PickingMode.Ignore;

            _overlayRoot = _root.Q<VisualElement>("resultOverlayRoot");
            _panel = _root.Q<VisualElement>("resultPanel");
            _fxBackLayer = _root.Q<Image>("resultFxBackLayer");
            _fxFrontLayer = _root.Q<Image>("resultFxFrontLayer");
            _resultLabel = _root.Q<Label>("resultLabel");
            _summaryLabel = _root.Q<Label>("resultSummaryLabel");
            _rewardsContainer = _root.Q<VisualElement>("resultRewardsContainer");
            _rewardsList = _root.Q<ScrollView>("resultRewardsList");
            _restartButton = _root.Q<Button>("restartButton");
            _backToMenuButton = _root.Q<Button>("backToMenuButton");

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
        }

        private void OnDisable()
        {
            if (battleController != null)
                battleController.OnMatchEnded -= HandleMatchEnded;

            if (_restartButton != null)
                _restartButton.clicked -= HandleRestartClicked;
            if (_backToMenuButton != null)
                _backToMenuButton.clicked -= HandleBackToMenuClicked;

            _backEffectsBridge?.StopActivePresentation();
            _frontEffectsBridge?.StopActivePresentation();
        }

        private void HandleMatchEnded(MatchResult result)
        {
            if (_isVisible)
                return;

            bool won = battleController != null && battleController.LocalPlayer == result.Winner;
            if (MapFlowRuntime.IsMapBattleActive)
                MapFlowRuntime.ReportBattleResult(won);

            if (_resultLabel != null)
                _resultLabel.text = won ? "Victory" : "Defeat";

            RewardBundle rewards = BuildRewardPreview(result, won);
            bool hasRewards = rewards != null && !rewards.IsEmpty;

            if (_summaryLabel != null)
                _summaryLabel.text = won
                    ? (hasRewards ? "Spoils gathered from this clash" : "The battle is won.")
                    : (hasRewards ? "Defeat rewards secured" : "No rewards earned this time.");

            PopulateRewards(rewards);

            if (!MapFlowRuntime.IsMapBattleActive)
            {
                LevelUpPresentationData levelUpData = ProfileService.ApplyReward(rewards, LevelUpSourceContexts.Battle);
                _levelUpPresenter?.Show(levelUpData);
            }

            if (_overlayRoot != null)
            {
                _overlayRoot.style.display = DisplayStyle.Flex;
                _overlayRoot.pickingMode = PickingMode.Position;
            }

            if (won)
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
        }

        private RewardBundle BuildRewardPreview(MatchResult result, bool won)
        {
            if (MapFlowRuntime.IsMapBattleActive)
            {
                if (!won)
                    return new RewardBundle();

                MapDefinitionSO map = !string.IsNullOrWhiteSpace(MapFlowRuntime.ChapterId)
                    ? MapDefinitionSO.LoadChapter(MapFlowRuntime.ChapterId)
                    : null;
                MapNodeDefinition node = map != null ? map.GetNode(MapFlowRuntime.SelectedNodeId) : null;
                return BuildMapRewardBundle(node);
            }

            string modeId = MatchService.ActivePreset != null ? MatchService.ActivePreset.modeId : string.Empty;
            return RewardService.CalculateMatchRewards(result, modeId);
        }

        private RewardBundle BuildMapRewardBundle(MapNodeDefinition node)
        {
            var bundle = new RewardBundle();
            if (node == null || node.reward == null)
                return bundle;

            if (node.reward.currencies != null)
            {
                for (int i = 0; i < node.reward.currencies.Count; i++)
                {
                    ProfileAmount currency = node.reward.currencies[i];
                    if (currency == null || string.IsNullOrWhiteSpace(currency.id) || currency.amount <= 0)
                        continue;

                    bundle.currencies.Add(new ProfileAmount(currency.id, currency.amount));
                }
            }

            if (node.reward.items != null)
            {
                for (int i = 0; i < node.reward.items.Count; i++)
                {
                    ProfileAmount item = node.reward.items[i];
                    if (item == null || string.IsNullOrWhiteSpace(item.id) || item.amount <= 0)
                        continue;

                    bundle.items.Add(new ProfileAmount(item.id, item.amount));
                }
            }

            bundle.xp = Mathf.Max(0, node.reward.xp);

            if (node.reward.chests != null)
            {
                for (int i = 0; i < node.reward.chests.Count; i++)
                {
                    MapChestRewardEntry chestReward = node.reward.chests[i];
                    if (chestReward == null || string.IsNullOrWhiteSpace(chestReward.chestId) || chestReward.amount <= 0)
                        continue;

                    for (int j = 0; j < chestReward.amount; j++)
                        bundle.chests.Add(new ChestInstance($"preview-{node.id}-{i}-{j}", chestReward.chestId));
                }
            }

            return bundle;
        }

        private void HandleRestartClicked()
        {
            _backEffectsBridge?.StopActivePresentation();
            _frontEffectsBridge?.StopActivePresentation();
            battleController?.RestartMatch();
            HideOverlay();
        }

        private void HandleBackToMenuClicked()
        {
            _backEffectsBridge?.StopActivePresentation();
            _frontEffectsBridge?.StopActivePresentation();

            if (MapFlowRuntime.IsMapBattleActive)
                MapFlowRuntime.RequestReturnToMap();

            string currentSceneName = SceneManager.GetActiveScene().name;
            if (!string.Equals(currentSceneName, "MainMenu", System.StringComparison.Ordinal))
                SceneManager.LoadScene("MainMenu");
        }

        private void HideOverlay()
        {
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

            _backEffectsBridge?.StopActivePresentation();
            _frontEffectsBridge?.StopActivePresentation();
            _isVisible = false;
        }

        private static void ConfigureFxLayer(Image layer)
        {
            if (layer == null)
                return;

            layer.scaleMode = ScaleMode.StretchToFill;
            layer.pickingMode = PickingMode.Ignore;
        }

        private void PopulateRewards(RewardBundle bundle)
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
                    AddRewardRow(definition != null ? definition.icon : null, $"+{currency.amount}", name);
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
                    AddRewardRow(definition != null ? definition.icon : null, $"x{item.amount}", name);
                    hasEntries = true;
                }
            }

            if (bundle != null && bundle.xp > 0)
            {
                AddRewardRow(FindXpIcon(), $"+{bundle.xp}", "XP");
                hasEntries = true;
            }

            if (bundle != null && bundle.chests != null)
            {
                for (int i = 0; i < bundle.chests.Count; i++)
                {
                    ChestInstance chest = bundle.chests[i];
                    if (chest == null || string.IsNullOrWhiteSpace(chest.chestTypeId))
                        continue;

                    ChestDefinition definition = FindChest(chest.chestTypeId);
                    string name = definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
                        ? definition.displayName
                        : chest.chestTypeId;
                    AddRewardRow(definition != null ? definition.icon : null, "x1", name);
                    hasEntries = true;
                }
            }

            if (_rewardsContainer != null)
                _rewardsContainer.style.display = DisplayStyle.Flex;

            if (!hasEntries)
                AddRewardRow(null, "-", "No rewards");
        }

        private void AddRewardRow(Sprite icon, string amount, string name)
        {
            if (_rewardsList == null)
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
            _rewardsList.Add(row);
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

        private ChestDefinition FindChest(string chestId)
        {
            ProgressionDatabase database = GetDatabase();
            if (database == null || database.chestCatalog == null || database.chestCatalog.chests == null)
                return null;

            return database.chestCatalog.chests.Find(c => c != null && c.id == chestId);
        }

        private Sprite FindXpIcon()
        {
            CurrencyDefinition essence = FindCurrency(ProgressionIds.Essence);
            CurrencyDefinition gold = FindCurrency(ProgressionIds.SoftGold);
            return gold != null ? gold.icon : essence != null ? essence.icon : null;
        }

        private ProgressionDatabase GetDatabase()
        {
            _database ??= Resources.Load<ProgressionDatabase>(DatabasePath);
            return _database;
        }
    }
}
