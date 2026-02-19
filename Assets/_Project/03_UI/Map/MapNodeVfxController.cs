using UnityEngine;
using UnityEngine.UIElements;

public sealed class MapNodeVfxController : MonoBehaviour
{
    private const float UpdateIntervalSeconds = 1f / 30f;

    [SerializeField] private ParticleSystem sparklePrefab;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private float worldDepth = 5f;
    [SerializeField] private float idleRate = 8f;
    [SerializeField] private float hoverRate = 16f;

    private ParticleSystem _psCurrent;
    private ParticleSystem _psHover;
    private Button _currentTarget;
    private Button _hoverTarget;
    private bool _visible;
    private float _nextUpdateAt;

    public void SetCurrentTarget(Button nodeButtonOrNull)
    {
        if (_currentTarget == nodeButtonOrNull)
            return;

        _currentTarget = nodeButtonOrNull;
        ApplyTarget(_psCurrent, _currentTarget, idleRate);
    }

    public void SetHoverTarget(Button nodeButtonOrNull)
    {
        if (_hoverTarget == nodeButtonOrNull)
            return;

        _hoverTarget = nodeButtonOrNull;
        ApplyTarget(_psHover, _hoverTarget, hoverRate);
    }

    public void SetVisible(bool visible)
    {
        _visible = visible;

        if (!_visible)
        {
            StopAndHide(_psCurrent);
            StopAndHide(_psHover);
            return;
        }

        ApplyTarget(_psCurrent, _currentTarget, idleRate);
        ApplyTarget(_psHover, _hoverTarget, hoverRate);
    }

    private void Awake()
    {
        EnsureInstances();
        StopAndHide(_psCurrent);
        StopAndHide(_psHover);
    }

    private void OnDisable()
    {
        SetVisible(false);
    }

    private void Update()
    {
        if (!_visible || Time.unscaledTime < _nextUpdateAt)
            return;

        _nextUpdateAt = Time.unscaledTime + UpdateIntervalSeconds;
        RefreshPosition(_psCurrent, _currentTarget);
        RefreshPosition(_psHover, _hoverTarget);
    }

    private void EnsureInstances()
    {
        if (sparklePrefab == null)
            return;

        if (_psCurrent == null)
            _psCurrent = Instantiate(sparklePrefab, transform);

        if (_psHover == null)
            _psHover = Instantiate(sparklePrefab, transform);
    }

    private void ApplyTarget(ParticleSystem ps, Button target, float rate)
    {
        if (ps == null)
            return;

        if (!_visible || !TryGetWorldPosition(target, out var worldPosition))
        {
            StopAndHide(ps);
            return;
        }

        ps.gameObject.SetActive(true);
        SetEmission(ps, rate);
        ps.transform.position = worldPosition;

        if (!ps.isPlaying)
            ps.Play(true);
    }

    private void RefreshPosition(ParticleSystem ps, Button target)
    {
        if (ps == null)
            return;

        if (!_visible || !TryGetWorldPosition(target, out var worldPosition))
        {
            StopAndHide(ps);
            return;
        }

        if (!ps.gameObject.activeSelf)
        {
            ps.gameObject.SetActive(true);
            if (!ps.isPlaying)
                ps.Play(true);
        }

        ps.transform.position = worldPosition;
    }

    private bool TryGetWorldPosition(Button button, out Vector3 worldPosition)
    {
        worldPosition = default;

        if (button == null || button.panel == null)
            return false;

        if (button.resolvedStyle.display == DisplayStyle.None || button.worldBound.width <= 0f || button.worldBound.height <= 0f)
            return false;

        if (worldCamera == null)
            worldCamera = Camera.main;

        if (worldCamera == null)
            return false;

        Rect r = button.worldBound;
        Vector2 panelPos = new Vector2(r.center.x, r.center.y);
        Vector2 screenPos = RuntimePanelUtils.PanelToScreenPoint(button.panel, panelPos);
        worldPosition = worldCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, worldDepth));
        return true;
    }

    private static void SetEmission(ParticleSystem ps, float rate)
    {
        var emission = ps.emission;
        emission.rateOverTime = rate;
    }

    private static void StopAndHide(ParticleSystem ps)
    {
        if (ps == null)
            return;

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.gameObject.SetActive(false);
    }
}
