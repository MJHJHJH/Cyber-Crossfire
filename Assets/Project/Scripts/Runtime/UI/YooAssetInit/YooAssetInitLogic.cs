using GameFramework;
using GamePlay.AssetPatch;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// YooAsset 初始化/热更界面 View：控件绑定与进度追赶显示；业务事件由 Presenter 处理。
/// </summary>
public sealed class YooAssetInitLogic : UIFormLogic, IYooAssetInitView
{
    private const float ProgressSpeedPerSecond = 25f;

    [Header("Loading")]
    [SerializeField] private GameObject m_LoadingRoot;
    [SerializeField] private TextMeshProUGUI m_LoadingTips;
    [SerializeField] private Slider m_LoadingSlider;
    [SerializeField] private TextMeshProUGUI m_LoadingProgress;

    [Header("MessageBox")]
    [SerializeField] private GameObject m_MessageBoxRoot;
    [SerializeField] private TextMeshProUGUI m_MessageBoxTitle;
    [SerializeField] private TextMeshProUGUI m_MessageBoxContent;
    [SerializeField] private Button m_MessageBoxConfirmButton;
    [SerializeField] private TextMeshProUGUI m_MessageBoxConfirmLabel;
    [SerializeField] private Button m_MessageBoxLeaveButton;

    private YooAssetInitPresenter _presenter;
    private float _progress;
    private float _targetProgress;
    private bool _progressReadyNotified;

    protected override void OnOpen(object userData)
    {
        base.OnOpen(userData);

        _progress = 0f;
        _targetProgress = 0f;
        _progressReadyNotified = false;
        HideMessageBox();
        SetLoadingTips("准备初始化资源…");
        ApplyProgressVisual();

        BindButtons(true);

        if (_presenter == null)
            _presenter = new YooAssetInitPresenter();

        _presenter.Attach(this);
    }

    protected override void OnClose(bool isShutdown, object userData)
    {
        BindButtons(false);
        _presenter?.Detach();

        base.OnClose(isShutdown, userData);
    }

    protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);

        if (Mathf.Approximately(_progress, _targetProgress))
        {
            TryNotifyProgressReady();
            return;
        }

        _progress = Mathf.MoveTowards(_progress, _targetProgress, ProgressSpeedPerSecond * elapseSeconds);
        ApplyProgressVisual();
        TryNotifyProgressReady();
    }

    public void SetLoadingTips(string tips)
    {
        SetActiveSafe(m_LoadingRoot, true);
        if (m_LoadingTips != null)
            m_LoadingTips.text = tips ?? string.Empty;
    }

    public void SetProgressTarget(float targetPercent)
    {
        float clamped = Mathf.Clamp(targetPercent, 0f, 100f);
        if (clamped < 100f)
            _progressReadyNotified = false;

        if (clamped <= 0f)
        {
            _progress = 0f;
            ApplyProgressVisual();
        }

        _targetProgress = clamped;
        TryNotifyProgressReady();
    }

    public void ShowMessageBox(string title, string content, string confirmLabel)
    {
        if (m_MessageBoxRoot == null)
        {
            Debug.LogError("[YooAssetInit] MessageBox Root 未绑定，无法显示弹窗。");
            return;
        }

        SetActiveSafe(m_MessageBoxRoot, false);

        if (m_MessageBoxTitle == null)
            Debug.LogError("[YooAssetInit] MessageBox Title (TMP) 未绑定，无法设置标题。");
        else
            m_MessageBoxTitle.text = title ?? string.Empty;

        if (m_MessageBoxContent == null)
            Debug.LogError("[YooAssetInit] MessageBox Content (TMP) 未绑定，无法设置文案。");
        else
            m_MessageBoxContent.text = content ?? string.Empty;

        if (m_MessageBoxConfirmLabel != null)
            m_MessageBoxConfirmLabel.text = string.IsNullOrEmpty(confirmLabel) ? "确定" : confirmLabel;

        SetActiveSafe(m_LoadingRoot, true);
        ActivateSelfAndParents(m_MessageBoxRoot);
        m_MessageBoxRoot.transform.SetAsLastSibling();
    }

    public void HideMessageBox()
    {
        SetActiveSafe(m_MessageBoxRoot, false);
    }

    private void BindButtons(bool bind)
    {
        if (m_MessageBoxConfirmButton != null)
        {
            m_MessageBoxConfirmButton.onClick.RemoveListener(OnConfirmClick);
            if (bind)
                m_MessageBoxConfirmButton.onClick.AddListener(OnConfirmClick);
        }

        if (m_MessageBoxLeaveButton != null)
        {
            m_MessageBoxLeaveButton.onClick.RemoveListener(OnLeaveClick);
            if (bind)
                m_MessageBoxLeaveButton.onClick.AddListener(OnLeaveClick);
        }
    }

    private void OnConfirmClick()
    {
        _presenter?.OnConfirmClick();
    }

    private void OnLeaveClick()
    {
        _presenter?.OnLeaveClick();
    }

    private void TryNotifyProgressReady()
    {
        if (_progressReadyNotified)
            return;

        if (_targetProgress < 100f || _progress < 100f)
            return;

        _progressReadyNotified = true;
        GameFrameWork.Event?.Fire(this, AssetPatchUserProgressReadyEventArgs.Create());
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

    private static void ActivateSelfAndParents(GameObject go)
    {
        if (go == null)
            return;

        Transform current = go.transform;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
                current.gameObject.SetActive(true);
            current = current.parent;
        }
    }
}
