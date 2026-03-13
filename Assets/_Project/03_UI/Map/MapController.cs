using System;
using System.Collections.Generic;
using System.Text;
using Diceforge.Audio;
using Diceforge.Map;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class MapController : MonoBehaviour
{
    private enum NodeVisualState
    {
        Locked,
        Available,
        Completed
    }

    private const float CurrentPulseMinScale = 1.1f;
    private const float CurrentPulseMaxScale = 1.23f;
    private const float HoverScaleBump = 0.13f;
    private const float PulseSpeed = 13.0f;
    private const float CurrentRingMinOpacity = 0.22f;
    private const float CurrentRingMaxOpacity = 0.40f;
    private const float HoverRingOpacity = 0.56f;
    private const float HoverSparkleOpacity = 0.8f;

    [SerializeField] private UIDocument uiDocument;

    [Header("Map UI")]
    [SerializeField] private StyleSheet mapViewStyle;
    [SerializeField] private Texture2D mapBackgroundTexture;
    [SerializeField] private Sprite iconDisabled;
    [SerializeField] private Sprite iconOpen;
    [SerializeField] private Sprite iconPassed;
    [SerializeField] private Sprite chestNodeIcon;
    [SerializeField] private Sprite shopNodeIcon;

    private VisualElement _mapRoot;
    private VisualElement _background;
    private VisualElement _nodesLayer;
    private VisualElement _edgesLayer;
    private Label _titleLabel;
    private Button _continueButton;
    private Button _backButton;
    private Button _resetRunButton;
    private Button _unlockAllButton;
    private readonly Dictionary<string, MapNodeDefinition> _nodesById = new();
    private readonly Dictionary<string, VisualElement> _nodeContainersById = new();
    private readonly Dictionary<string, Button> _nodeButtonsById = new();
    private readonly Dictionary<string, VisualElement> _nodeRingsById = new();
    private readonly Dictionary<string, VisualElement> _nodeSparklesById = new();
    private MapDefinitionSO _currentMap;
    private MapRunState _currentState;
    private Action<string> _onNodeSelected;
    private bool _pendingLayoutRebuild;
    private IVisualElementScheduledItem _fxTicker;
    private bool _isMapVisible;
    private float _fxTime;
    private string _hoveredNodeId;
    private string _fxActiveCurrentId;
    private string _fxActiveHoverId;

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

        ValidateNodeIcons();

        _titleLabel.text = FormatChapterTitle(map.chapterId);
        _mapRoot.style.display = DisplayStyle.Flex;
        _mapRoot.BringToFront();
        ApplyMapBackground();
        _resetRunButton.style.display = devMode ? DisplayStyle.Flex : DisplayStyle.None;
        _unlockAllButton.style.display = devMode ? DisplayStyle.Flex : DisplayStyle.None;

        BuildNodes(devMode);
        StartFxTicker();
    }

    public void Hide()
    {
        _isMapVisible = false;
        _hoveredNodeId = null;
        _fxActiveCurrentId = null;
        _fxActiveHoverId = null;
        StopFxTicker();

        if (_mapRoot != null)
            _mapRoot.style.display = DisplayStyle.None;
    }

    private void EnsureView(VisualElement root, bool devMode)
    {
        _mapRoot = root.Q<VisualElement>("MapRoot");
        if (_mapRoot != null)
        {
            AttachMapStyleSheet(devMode);
            _background = _mapRoot.Q<VisualElement>("MapBackgroundImage");
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

        _background = new VisualElement { name = "MapBackgroundImage" };
        _background.AddToClassList("map-background-image");

        var topBar = new VisualElement { name = "MapTopBar" };
        topBar.AddToClassList("map-top-bar");
        _backButton = new Button(() => BackRequested?.Invoke()) { name = "MapBackButton", text = "Back" };
        _backButton.AddToClassList("menu-wood-button");
        _backButton.AddToClassList("menu-small-button");

        _titleLabel = new Label("Chapter 1") { name = "MapTitle" };
        _titleLabel.AddToClassList("menu-title");

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
        _resetRunButton.AddToClassList("menu-wood-button");
        _resetRunButton.AddToClassList("menu-small-button");

        _continueButton = new Button(() => ContinueRequested?.Invoke()) { name = "MapContinueButton", text = "Continue" };
        _continueButton.AddToClassList("menu-wood-button");
        _continueButton.AddToClassList("map-continue-button");

        _unlockAllButton = new Button(() => UnlockAllRequested?.Invoke()) { name = "MapUnlockAllButton", text = "Unlock All" };
        _unlockAllButton.AddToClassList("menu-wood-button");
        _unlockAllButton.AddToClassList("menu-small-button");

        bottomBar.Add(_resetRunButton);
        bottomBar.Add(_continueButton);
        bottomBar.Add(_unlockAllButton);

        _mapRoot.Add(_background);
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
        _nodeContainersById.Clear();
        _nodeButtonsById.Clear();
        _nodeRingsById.Clear();
        _nodeSparklesById.Clear();

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
        int nodeIndex = 0;

        foreach (var node in _currentMap.nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.id))
                continue;

            var nodeContainer = new VisualElement { name = $"Node_{node.id}" };
            nodeContainer.AddToClassList("map-node");
            CreateNodeFxLayers(node.id, nodeContainer);

/*            var shadow = new VisualElement { name = "NodeShadow" };
            shadow.AddToClassList("node-shadow");
            nodeContainer.Add(shadow);*/

            var button = new Button(() => HandleNodeClicked(node.id))
            {
                name = "NodeButton",
                text = string.Empty
            };
            button.AddToClassList("node-button");
            RegisterNodeHover(node.id, nodeContainer, button);
            nodeContainer.Add(button);

            /*var levelLabel = new Label(GetNodeLevelLabel(node.id, nodeIndex + 1)) { name = "LevelLabel" };
            levelLabel.AddToClassList("map-node-label");
            nodeContainer.Add(levelLabel);*/

           /* if (devMode)
            {
                var idLabel = new Label(node.id);
                idLabel.AddToClassList("node-id");
                nodeContainer.Add(idLabel);
            }*/

            ApplyNodeStateClasses(nodeContainer, button, node);
            nodeContainer.style.left = Length.Percent(node.positionNormalized.x * 100f);
            nodeContainer.style.top = Length.Percent((1f - node.positionNormalized.y) * 100f);

            _nodesLayer.Add(nodeContainer);
            _nodeContainersById[node.id] = nodeContainer;
            _nodeButtonsById[node.id] = button;

            instantiatedCount++;
            nodeIndex++;
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

    private static string GetNodeLevelLabel(string nodeId, int fallbackNumber)
    {
        if (!string.IsNullOrWhiteSpace(nodeId))
        {
            int separatorIndex = nodeId.LastIndexOf('_');
            if (separatorIndex >= 0 && separatorIndex < nodeId.Length - 1)
            {
                string trailing = nodeId.Substring(separatorIndex + 1);
                if (int.TryParse(trailing, out int parsed))
                    return parsed.ToString();
            }
        }

        return fallbackNumber.ToString();
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

    private void ApplyNodeStateClasses(VisualElement container, Button button, MapNodeDefinition node)
    {
        if (node == null)
            return;

        string id = node.id;
        container.RemoveFromClassList("locked");
        container.RemoveFromClassList("available");
        container.RemoveFromClassList("completed");
        container.RemoveFromClassList("current");
        container.RemoveFromClassList("map-node-battle");
        container.RemoveFromClassList("map-node-chest");
        container.RemoveFromClassList("map-node-shop");
        container.RemoveFromClassList("map-node-story");

        AddTypeClass(container, node.type);
        AddTypeClass(button, node.type);

        if (_currentState.IsCompleted(id))
        {
            container.AddToClassList("completed");
            ApplyNodeIcon(button, node.type, NodeVisualState.Completed);
            button.SetEnabled(false);
            ClearNodeFx(container, id);
            return;
        }

        bool unlocked = _currentState.IsUnlocked(id);
        if (id == _currentState.currentNodeId && unlocked)
        {
            container.AddToClassList("current");
            ApplyNodeIcon(button, node.type, NodeVisualState.Available);
            button.SetEnabled(true);
        }
        else if (unlocked)
        {
            container.AddToClassList("available");
            ApplyNodeIcon(button, node.type, NodeVisualState.Available);
            button.SetEnabled(true);
        }
        else
        {
            container.AddToClassList("locked");
            ApplyNodeIcon(button, node.type, NodeVisualState.Locked);
            button.SetEnabled(false);
            ClearNodeFx(container, id);
        }
    }

    private void ApplyMapBackground()
    {
        if (_background == null)
            return;

        _background.style.backgroundImage = mapBackgroundTexture != null
            ? new StyleBackground(mapBackgroundTexture)
            : StyleKeyword.Null;
        _background.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
    }

    private void ValidateNodeIcons()
    {
        if (iconDisabled == null || iconOpen == null || iconPassed == null)
            throw new InvalidOperationException("[MapController] Battle map icons are not configured on MapController.");

        if (_currentMap == null || _currentMap.nodes == null)
            return;

        bool requiresChestIcon = false;
        bool requiresShopIcon = false;
        for (int i = 0; i < _currentMap.nodes.Count; i++)
        {
            MapNodeDefinition node = _currentMap.nodes[i];
            if (node == null)
                continue;

            requiresChestIcon |= node.type == MapNodeType.Chest;
            requiresShopIcon |= node.type == MapNodeType.Shop;
        }

        if (requiresChestIcon && chestNodeIcon == null)
            throw new InvalidOperationException("[MapController] Chest node icon is required but not configured on MapController.");

        if (requiresShopIcon && shopNodeIcon == null)
            throw new InvalidOperationException("[MapController] Shop node icon is required but not configured on MapController.");
    }

    private void ApplyNodeIcon(Button button, MapNodeType nodeType, NodeVisualState visualState)
    {
        if (button == null)
            return;

        Sprite icon = ResolveNodeIcon(nodeType, visualState);
        button.style.backgroundImage = icon != null
            ? new StyleBackground(icon)
            : StyleKeyword.Null;
        button.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
    }

    private Sprite ResolveNodeIcon(MapNodeType nodeType, NodeVisualState visualState)
    {
        return nodeType switch
        {
            MapNodeType.Battle => visualState switch
            {
                NodeVisualState.Locked => iconDisabled,
                NodeVisualState.Available => iconOpen,
                NodeVisualState.Completed => iconPassed,
                _ => iconOpen
            },
            MapNodeType.Chest => chestNodeIcon,
            MapNodeType.Shop => shopNodeIcon,
            MapNodeType.Story => iconOpen,
            _ => iconOpen
        };
    }

    private static void AddTypeClass(VisualElement element, MapNodeType nodeType)
    {
        if (element == null)
            return;

        switch (nodeType)
        {
            case MapNodeType.Battle:
                element.AddToClassList("map-node-battle");
                break;
            case MapNodeType.Chest:
                element.AddToClassList("map-node-chest");
                break;
            case MapNodeType.Shop:
                element.AddToClassList("map-node-shop");
                break;
            case MapNodeType.Story:
                element.AddToClassList("map-node-story");
                break;
        }
    }

    private static string FormatChapterTitle(string chapterId)
    {
        if (string.IsNullOrWhiteSpace(chapterId))
            return "Chapter 1";

        var normalized = chapterId.Trim();
        if (normalized.Contains(' '))
            return normalized;

        var builder = new StringBuilder(normalized.Length + 1);
        for (int i = 0; i < normalized.Length; i++)
        {
            char character = normalized[i];
            if (i > 0 && char.IsDigit(character) && !char.IsWhiteSpace(normalized[i - 1]))
                builder.Append(' ');

            builder.Append(character);
        }

        return builder.ToString();
    }

    private void RegisterNodeHover(string nodeId, VisualElement nodeContainer, Button button)
    {
        button.RegisterCallback<PointerEnterEvent>(_ =>
        {
            if (!button.enabledInHierarchy || !IsNodeFxEligible(nodeId))
                return;

            nodeContainer.AddToClassList("map-node-hover");
            _hoveredNodeId = nodeId;
        });

        button.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            nodeContainer.RemoveFromClassList("map-node-hover");

            if (_hoveredNodeId == nodeId)
                _hoveredNodeId = null;
        });
    }

    private void CreateNodeFxLayers(string nodeId, VisualElement nodeContainer)
    {
        var ring = new VisualElement { name = "NodeRing" };
        ring.AddToClassList("node-ring");
        nodeContainer.Add(ring);

        var sparkle = new VisualElement { name = "NodeSparkle" };
        sparkle.AddToClassList("node-sparkle");
        nodeContainer.Add(sparkle);

        _nodeRingsById[nodeId] = ring;
        _nodeSparklesById[nodeId] = sparkle;
    }

    private void StartFxTicker()
    {
        if (_mapRoot == null)
            return;

        _isMapVisible = true;
        _fxTime = 0f;
        _fxActiveCurrentId = null;
        _fxActiveHoverId = null;

        if (_fxTicker == null)
            _fxTicker = _mapRoot.schedule.Execute(TickFx).Every(16);
        else
            _fxTicker.Resume();
    }

    private void StopFxTicker()
    {
        _fxTicker?.Pause();
        ResetFxVisuals();
    }

    private void TickFx()
    {
        if (!_isMapVisible || _currentState == null)
            return;

        _fxTime += Time.unscaledDeltaTime;

        string currentNodeId = _currentState.currentNodeId;
        string hoveredNodeId = _hoveredNodeId;

        if (_fxActiveCurrentId != null && _fxActiveCurrentId != currentNodeId && _nodeContainersById.TryGetValue(_fxActiveCurrentId, out var prevCurrentContainer))
            ClearNodeFx(prevCurrentContainer, _fxActiveCurrentId);

        if (_fxActiveHoverId != null && _fxActiveHoverId != hoveredNodeId && _fxActiveHoverId != currentNodeId && _nodeContainersById.TryGetValue(_fxActiveHoverId, out var prevHoveredContainer))
            ClearNodeFx(prevHoveredContainer, _fxActiveHoverId);

        if (!string.IsNullOrEmpty(currentNodeId) && _nodeContainersById.TryGetValue(currentNodeId, out var currentContainer) && IsNodeFxEligible(currentNodeId))
        {
            float pulseNormalized = (Mathf.Sin(_fxTime * PulseSpeed) + 1f) * 0.5f;
            float pulseScale = Mathf.Lerp(CurrentPulseMinScale, CurrentPulseMaxScale, pulseNormalized);
            float ringOpacity = Mathf.Lerp(CurrentRingMinOpacity, CurrentRingMaxOpacity, pulseNormalized);

            if (hoveredNodeId == currentNodeId)
            {
                pulseScale += HoverScaleBump;
                ringOpacity = Mathf.Max(ringOpacity, HoverRingOpacity);
            }

            currentContainer.style.scale = new StyleScale(new Scale(Vector3.one * pulseScale));
            SetNodeFxOpacity(currentNodeId, ringOpacity, hoveredNodeId == currentNodeId ? HoverSparkleOpacity : 0f);
            _fxActiveCurrentId = currentNodeId;
        }
        else if (!string.IsNullOrEmpty(currentNodeId) && _nodeContainersById.TryGetValue(currentNodeId, out var inactiveCurrentContainer))
        {
            ClearNodeFx(inactiveCurrentContainer, currentNodeId);
            _fxActiveCurrentId = null;
        }

        if (!string.IsNullOrEmpty(hoveredNodeId) && hoveredNodeId != currentNodeId && _nodeContainersById.TryGetValue(hoveredNodeId, out var hoveredContainer) && IsNodeFxEligible(hoveredNodeId))
        {
            hoveredContainer.style.scale = new StyleScale(new Scale(Vector3.one * (1f + HoverScaleBump)));
            SetNodeFxOpacity(hoveredNodeId, HoverRingOpacity, HoverSparkleOpacity);
            _fxActiveHoverId = hoveredNodeId;
        }
        else if (!string.IsNullOrEmpty(hoveredNodeId) && hoveredNodeId != currentNodeId && _nodeContainersById.TryGetValue(hoveredNodeId, out var invalidHoverContainer))
        {
            ClearNodeFx(invalidHoverContainer, hoveredNodeId);
            _fxActiveHoverId = null;
        }

        if (hoveredNodeId == currentNodeId)
            _fxActiveHoverId = null;
    }

    private bool IsNodeFxEligible(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId) || _currentState == null)
            return false;

        return _currentState.IsUnlocked(nodeId) && !_currentState.IsCompleted(nodeId);
    }

    private void ResetFxVisuals()
    {
        foreach (var pair in _nodeContainersById)
            ClearNodeFx(pair.Value, pair.Key);
    }

    private void ClearNodeFx(VisualElement nodeContainer, string nodeId)
    {
        if (nodeContainer != null)
        {
            nodeContainer.style.scale = new StyleScale(new Scale(Vector3.one));
            nodeContainer.RemoveFromClassList("map-node-hover");
        }

        SetNodeFxOpacity(nodeId, 0f, 0f);
    }

    private void SetNodeFxOpacity(string nodeId, float ringOpacity, float sparkleOpacity)
    {
        if (_nodeRingsById.TryGetValue(nodeId, out var ring))
            ring.style.opacity = ringOpacity;

        if (_nodeSparklesById.TryGetValue(nodeId, out var sparkle))
            sparkle.style.opacity = sparkleOpacity;
    }

}
