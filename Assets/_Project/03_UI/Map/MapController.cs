using System;
using System.Collections.Generic;
using Diceforge.Audio;
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
    private VisualElement _edgesLayer;
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
            _edgesLayer = _mapRoot.Q<VisualElement>("MapEdgesLayer");
            _titleLabel = _mapRoot.Q<Label>("MapTitle");
            _continueButton = _mapRoot.Q<Button>("MapContinueButton");
            _backButton = _mapRoot.Q<Button>("MapBackButton");
            _resetRunButton = _mapRoot.Q<Button>("MapResetRunButton");
            _unlockAllButton = _mapRoot.Q<Button>("MapUnlockAllButton");

            if (_nodesLayer != null && _edgesLayer == null)
            {
                _edgesLayer = new VisualElement { name = "MapEdgesLayer" };
                _edgesLayer.AddToClassList("map-edges-layer");
                _nodesLayer.Insert(0, _edgesLayer);
            }

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

        var mapBackground = new VisualElement { name = "MapBackground" };
        mapBackground.AddToClassList("map-background");

        var topBar = new VisualElement { name = "MapTopBar" };
        topBar.AddToClassList("map-top-bar");
        _backButton = new Button(() => BackRequested?.Invoke()) { name = "MapBackButton", text = "Back" };
        _backButton.AddToClassList("map-top-button");

        _titleLabel = new Label("Chapter") { name = "MapTitle" };
        _titleLabel.AddToClassList("map-title");

        var currencySlot = new VisualElement { name = "MapCurrencySlot" };
        currencySlot.AddToClassList("map-currency-slot");

        topBar.Add(_backButton);
        topBar.Add(_titleLabel);
        topBar.Add(currencySlot);

        _nodesLayer = new VisualElement { name = "NodesLayer" };
        _nodesLayer.AddToClassList("map-nodes-layer");

        _edgesLayer = new VisualElement { name = "MapEdgesLayer" };
        _edgesLayer.AddToClassList("map-edges-layer");
        _nodesLayer.Add(_edgesLayer);

        var bottomBar = new VisualElement { name = "MapBottomBar" };
        bottomBar.AddToClassList("map-bottom-bar");

        _resetRunButton = new Button(() => ResetRunRequested?.Invoke()) { name = "MapResetRunButton", text = "Reset Run" };
        _resetRunButton.AddToClassList("map-dev-button");

        _continueButton = new Button(() => ContinueRequested?.Invoke()) { name = "MapContinueButton", text = "Continue" };
        _continueButton.AddToClassList("map-continue-button");

        _unlockAllButton = new Button(() => UnlockAllRequested?.Invoke()) { name = "MapUnlockAllButton", text = "Unlock All" };
        _unlockAllButton.AddToClassList("map-dev-button");

        bottomBar.Add(_resetRunButton);
        bottomBar.Add(_continueButton);
        bottomBar.Add(_unlockAllButton);

        _mapRoot.Add(mapBackground);
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
        _edgesLayer = new VisualElement { name = "MapEdgesLayer" };
        _edgesLayer.AddToClassList("map-edges-layer");
        _nodesLayer.Add(_edgesLayer);

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

        foreach (var node in _currentMap.nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.id))
                continue;

            _nodesById[node.id] = node;
        }

        BuildEdges(layerWidth, layerHeight);

        int instantiatedCount = 0;

        foreach (var node in _currentMap.nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.id))
                continue;

            var nodeContainer = new VisualElement { name = $"Node_{node.id}" };
            nodeContainer.AddToClassList("map-node");

            var shadow = new VisualElement { name = "NodeShadow" };
            shadow.AddToClassList("node-shadow");
            nodeContainer.Add(shadow);

            var button = new Button(() => HandleNodeClicked(node.id))
            {
                name = "NodeButton",
                text = GetNodeGlyph(node.type)
            };
            button.AddToClassList("node-button");
            nodeContainer.Add(button);

            var checkLabel = new Label("✓") { name = "NodeCheck" };
            checkLabel.AddToClassList("node-check");
            nodeContainer.Add(checkLabel);

            if (devMode)
            {
                var idLabel = new Label(node.id);
                idLabel.AddToClassList("node-id");
                nodeContainer.Add(idLabel);
            }

            ApplyNodeStateClasses(nodeContainer, button, node.id);
            nodeContainer.style.left = Length.Percent(node.positionNormalized.x * 100f);
            nodeContainer.style.top = Length.Percent((1f - node.positionNormalized.y) * 100f);
            _nodesLayer.Add(nodeContainer);

            instantiatedCount++;
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

    private void BuildEdges(float layerWidth, float layerHeight)
    {
        if (_edgesLayer == null || _currentMap == null || layerWidth <= 0f || layerHeight <= 0f)
            return;

        foreach (var node in _currentMap.nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.id) || node.nextIds == null)
                continue;

            float fromX = node.positionNormalized.x * layerWidth;
            float fromY = (1f - node.positionNormalized.y) * layerHeight;

            foreach (var nextId in node.nextIds)
            {
                if (string.IsNullOrWhiteSpace(nextId) || !_nodesById.TryGetValue(nextId, out var nextNode) || nextNode == null)
                    continue;

                float toX = nextNode.positionNormalized.x * layerWidth;
                float toY = (1f - nextNode.positionNormalized.y) * layerHeight;

                float dx = toX - fromX;
                float dy = toY - fromY;
                float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                if (distance <= 1f)
                    continue;

                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                var edge = new VisualElement { name = $"Edge_{node.id}_{nextId}" };
                edge.AddToClassList("map-edge");

                bool isActivePath = _currentState.IsCompleted(node.id) || node.id == _currentState.currentNodeId;
                if (isActivePath)
                    edge.AddToClassList("active");

                edge.style.left = fromX;
                edge.style.top = fromY - 1.5f;
                edge.style.width = distance;
                edge.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
                _edgesLayer.Add(edge);
            }
        }
    }

    private void HandleNodeClicked(string nodeId)
    {
        if (_currentState == null || !_currentState.IsUnlocked(nodeId))
            return;

        AudioManager.Instance?.PlayUiClick();
        _onNodeSelected?.Invoke(nodeId);
    }

    private static string GetNodeGlyph(MapNodeType type)
    {
        return type switch
        {
            MapNodeType.Battle => "⚔",
            MapNodeType.Chest => "🎁",
            MapNodeType.Shop => "⚙",
            MapNodeType.Story => "⋯",
            _ => type.ToString()
        };
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
