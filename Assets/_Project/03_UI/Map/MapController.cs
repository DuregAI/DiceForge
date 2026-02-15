using System;
using System.Collections.Generic;
using Diceforge.Map;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class MapController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    [Header("Map UI")]
    [SerializeField] private StyleSheet mapViewStyle;

    private VisualElement _mapRoot;
    private VisualElement _nodesLayer;
    private Label _titleLabel;
    private Button _continueButton;
    private Button _backButton;
    private Button _resetRunButton;
    private Button _unlockAllButton;
    private readonly Dictionary<string, MapNodeDefinition> _nodesById = new();
    private MapDefinitionSO _currentMap;
    private MapRunState _currentState;
    private Action<string> _onNodeSelected;
    private bool _pendingLayoutRebuild;

    public event Action BackRequested;
    public event Action ContinueRequested;
    public event Action ResetRunRequested;
    public event Action UnlockAllRequested;

    public void Show(MapDefinitionSO map, MapRunState state, Action<string> onNodeSelected, bool devMode)
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        var root = uiDocument?.rootVisualElement;
        if (root == null)
            return;

        EnsureView(root, devMode);

        _currentMap = map;
        _currentState = state;
        _onNodeSelected = onNodeSelected;
        _nodesById.Clear();

        _titleLabel.text = map.chapterId;
        _mapRoot.style.display = DisplayStyle.Flex;
        _mapRoot.BringToFront();
        _resetRunButton.style.display = devMode ? DisplayStyle.Flex : DisplayStyle.None;
        _unlockAllButton.style.display = devMode ? DisplayStyle.Flex : DisplayStyle.None;

        BuildNodes(devMode);
    }

    public void Hide()
    {
        if (_mapRoot != null)
            _mapRoot.style.display = DisplayStyle.None;
    }

    private void EnsureView(VisualElement root, bool devMode)
    {
        _mapRoot = root.Q<VisualElement>("MapRoot");
        if (_mapRoot != null)
        {
            AttachMapStyleSheet(devMode);
            _nodesLayer = _mapRoot.Q<VisualElement>("NodesLayer");
            _titleLabel = _mapRoot.Q<Label>("MapTitle");
            _continueButton = _mapRoot.Q<Button>("MapContinueButton");
            _backButton = _mapRoot.Q<Button>("MapBackButton");
            _resetRunButton = _mapRoot.Q<Button>("MapResetRunButton");
            _unlockAllButton = _mapRoot.Q<Button>("MapUnlockAllButton");

            if (_backButton != null)
            {
                _backButton.clicked -= HandleBack;
                _backButton.clicked += HandleBack;
            }

            if (_continueButton != null)
            {
                _continueButton.clicked -= HandleContinueRequested;
                _continueButton.clicked += HandleContinueRequested;
            }

            if (_resetRunButton != null)
            {
                _resetRunButton.clicked -= HandleResetRun;
                _resetRunButton.clicked += HandleResetRun;
            }

            if (_unlockAllButton != null)
            {
                _unlockAllButton.clicked -= HandleUnlockAll;
                _unlockAllButton.clicked += HandleUnlockAll;
            }
            return;
        }

        _mapRoot = new VisualElement { name = "MapRoot" };
        _mapRoot.AddToClassList("map-root");

        var topBar = new VisualElement { name = "MapTopBar" };
        topBar.AddToClassList("map-top-bar");
        _backButton = new Button(() => BackRequested?.Invoke()) { name = "MapBackButton", text = "Back" };
        _titleLabel = new Label("Chapter") { name = "MapTitle" };
        topBar.Add(_backButton);
        topBar.Add(_titleLabel);

        _nodesLayer = new VisualElement { name = "NodesLayer" };
        _nodesLayer.AddToClassList("map-nodes-layer");

        var bottomBar = new VisualElement { name = "MapBottomBar" };
        bottomBar.AddToClassList("map-bottom-bar");
        _continueButton = new Button(() => ContinueRequested?.Invoke()) { name = "MapContinueButton", text = "Continue" };
        _resetRunButton = new Button(() => ResetRunRequested?.Invoke()) { name = "MapResetRunButton", text = "Reset Run" };
        _unlockAllButton = new Button(() => UnlockAllRequested?.Invoke()) { name = "MapUnlockAllButton", text = "Unlock All" };

        bottomBar.Add(_continueButton);
        bottomBar.Add(_resetRunButton);
        bottomBar.Add(_unlockAllButton);

        _mapRoot.Add(topBar);
        _mapRoot.Add(_nodesLayer);
        _mapRoot.Add(bottomBar);
        root.Add(_mapRoot);

        AttachMapStyleSheet(devMode);
    }

    private void AttachMapStyleSheet(bool devMode)
    {
        DevLog(devMode, $"mapViewStyle assigned: {mapViewStyle != null}");
        if (_mapRoot == null || mapViewStyle == null || _mapRoot.styleSheets.Contains(mapViewStyle))
            return;

        _mapRoot.styleSheets.Add(mapViewStyle);
    }

    private void HandleBack() => BackRequested?.Invoke();
    private void HandleContinueRequested() => ContinueRequested?.Invoke();
    private void HandleResetRun() => ResetRunRequested?.Invoke();
    private void HandleUnlockAll() => UnlockAllRequested?.Invoke();

    private void BuildNodes(bool devMode)
    {
        if (_nodesLayer == null)
        {
            DevLog(devMode, "NodesLayer was not found. Check UXML name=\"NodesLayer\".");
            return;
        }

        _nodesLayer.Clear();
        if (_currentMap == null || _currentState == null)
            return;

        float layerWidth = _nodesLayer.resolvedStyle.width;
        float layerHeight = _nodesLayer.resolvedStyle.height;
        DevLog(devMode, $"BuildNodes: NodesLayer != null: {_nodesLayer != null}, size: {layerWidth}x{layerHeight}");

        if ((layerWidth <= 0f || layerHeight <= 0f) && !_pendingLayoutRebuild)
        {
            _pendingLayoutRebuild = true;
            _mapRoot?.schedule.Execute(() =>
            {
                _pendingLayoutRebuild = false;
                BuildNodes(devMode);
            }).ExecuteLater(0);

            DevLog(devMode, "NodesLayer has invalid size at build time. Scheduled one-frame delayed rebuild.");
            return;
        }

        int instantiatedCount = 0;
        int sampled = 0;

        foreach (var node in _currentMap.nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.id))
                continue;

            _nodesById[node.id] = node;
            var nodeContainer = new VisualElement { name = $"Node_{node.id}" };
            nodeContainer.AddToClassList("map-node");

            var button = new Button(() => _onNodeSelected?.Invoke(node.id)) { name = "NodeButton", text = node.type.ToString() };
            button.AddToClassList("node-button");
            nodeContainer.Add(button);

            if (devMode)
            {
                var idLabel = new Label(node.id);
                idLabel.AddToClassList("node-id");
                nodeContainer.Add(idLabel);
            }

            ApplyNodeStateClasses(nodeContainer, button, node.id);
            nodeContainer.style.position = Position.Absolute;
            nodeContainer.style.left = Length.Percent(node.positionNormalized.x * 100f);
            nodeContainer.style.top = Length.Percent((1f - node.positionNormalized.y) * 100f);
            _nodesLayer.Add(nodeContainer);

            instantiatedCount++;
            if (sampled < 2)
            {
                float px = node.positionNormalized.x * layerWidth;
                float py = (1f - node.positionNormalized.y) * layerHeight;
                DevLog(devMode,
                    $"Node[{sampled}] id={node.id}, normalized=({node.positionNormalized.x:F3},{node.positionNormalized.y:F3}), pixel=({px:F1},{py:F1}), style.left={nodeContainer.style.left}, style.top={nodeContainer.style.top}");
                sampled++;
            }
        }

        DevLog(devMode, $"BuildNodes instantiated {instantiatedCount} nodes.");

        if (!string.IsNullOrEmpty(_currentState.currentNodeId) && _nodesById.ContainsKey(_currentState.currentNodeId))
        {
            _continueButton.SetEnabled(true);
            _continueButton.clicked -= HandleContinue;
            _continueButton.clicked += HandleContinue;
        }
        else
        {
            _continueButton.SetEnabled(false);
        }
    }

    private static void DevLog(bool devMode, string message)
    {
        if (!devMode)
            return;

        Debug.Log($"[MapController][Dev] {message}");
    }

    private void HandleContinue()
    {
        if (string.IsNullOrEmpty(_currentState?.currentNodeId))
            return;

        _onNodeSelected?.Invoke(_currentState.currentNodeId);
    }

    private void ApplyNodeStateClasses(VisualElement container, Button button, string id)
    {
        container.RemoveFromClassList("locked");
        container.RemoveFromClassList("available");
        container.RemoveFromClassList("completed");
        container.RemoveFromClassList("current");

        if (_currentState.IsCompleted(id))
        {
            container.AddToClassList("completed");
            button.SetEnabled(false);
            return;
        }

        bool unlocked = _currentState.IsUnlocked(id);
        if (id == _currentState.currentNodeId && unlocked)
        {
            container.AddToClassList("current");
            button.SetEnabled(true);
        }
        else if (unlocked)
        {
            container.AddToClassList("available");
            button.SetEnabled(true);
        }
        else
        {
            container.AddToClassList("locked");
            button.SetEnabled(false);
        }
    }
}
