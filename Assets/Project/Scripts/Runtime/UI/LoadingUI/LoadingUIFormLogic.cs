using GameFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 场景切换 Loading 界面：进度与提示展示（Resources / LoadingPanel）。
/// </summary>
public sealed class LoadingUIFormLogic : UIFormLogic
{
    private const float ProgressSpeedPerSecond = 25f;

    [Header("Loading")]
    [SerializeField] private GameObject m_LoadingRoot;
    [SerializeField] private TextMeshProUGUI m_LoadingTips;
    [SerializeField] private Slider m_LoadingSlider;
    [SerializeField] private TextMeshProUGUI m_LoadingProgress;

    private float _progress;
    private float _targetProgress;

    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);

        _progress = 0f;
        _targetProgress = 0f;
        SetLoadingTips("加载场景中…");
        ApplyProgressVisual();
    }

    protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);

        if (Mathf.Approximately(_progress, _targetProgress))
            return;

        _progress = Mathf.MoveTowards(_progress, _targetProgress, ProgressSpeedPerSecond * elapseSeconds);
        ApplyProgressVisual();
    }

    public void SetLoadingTips(string tips)
    {
        SetActiveSafe(m_LoadingRoot, true);
        if (m_LoadingTips != null)
            m_LoadingTips.text = tips ?? string.Empty;
    }

    /// <summary>展示进度是否已追上目标且达到 100。</summary>
    public bool IsVisualProgressComplete =>
        _targetProgress >= 100f && _progress >= 99.9f;

    /// <summary>目标进度，单位 0~100。</summary>
    public void SetProgressTarget(float targetPercent)
    {
        _targetProgress = Mathf.Clamp(targetPercent, 0f, 100f);
        if (_targetProgress <= 0f)
        {
            _progress = 0f;
            ApplyProgressVisual();
        }
    }

    private void ApplyProgressVisual()
    {
        SetActiveSafe(m_LoadingRoot, true);

        if (m_LoadingSlider != null)
            m_LoadingSlider.value = _progress;

        if (m_LoadingProgress != null)
            m_LoadingProgress.text = $"{_progress:0}%";
    }

    private static void SetActiveSafe(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }
}
