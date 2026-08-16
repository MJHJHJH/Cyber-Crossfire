using GameFramework;
using R3;
using TMPro;
using UI.Mvvm;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Mvvm.Demo
{
    /// <summary>
    /// MVVM Demo 面板。可挂序列化控件；若未绑定则 OnInit 生成最小 UGUI。
    /// Resources 打开：location = <see cref="ResourcesLocation"/>（Default 组）。
    /// </summary>
    public sealed class MvvmDemoPanelLogic : MvvmUIFormLogic<MvvmDemoViewModel>
    {
        public const string ResourcesLocation = "MvvmDemoPanel";
        public const string DefaultGroupName = "Default";

        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _countText;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private Button _incrementButton;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _closeButton;

        protected override void OnInit(object userData)
        {
            base.OnInit(userData);
            EnsureUi();
        }

        protected override void OnBind(MvvmDemoViewModel viewModel)
        {
            if (_titleText != null)
            {
                AddBinding(viewModel.Title.Subscribe(text =>
                {
                    if (_titleText != null)
                        _titleText.text = text ?? string.Empty;
                }));
            }

            if (_countText != null)
            {
                AddBinding(viewModel.Count.Subscribe(value =>
                {
                    if (_countText != null)
                        _countText.text = value.ToString();
                }));
            }

            if (_statusText != null)
            {
                AddBinding(viewModel.Status.Subscribe(text =>
                {
                    if (_statusText != null)
                        _statusText.text = text ?? string.Empty;
                }));
            }

            if (_incrementButton != null)
            {
                AddBinding(_incrementButton.OnClickAsObservable()
                    .Subscribe(_ => viewModel.Increment()));
            }

            if (_resetButton != null)
            {
                AddBinding(_resetButton.OnClickAsObservable()
                    .Subscribe(_ => viewModel.Reset()));
            }

            if (_closeButton != null)
            {
                AddBinding(_closeButton.OnClickAsObservable()
                    .Subscribe(_ => CloseSelf()));
            }
        }

        private void CloseSelf()
        {
            if (GameFrameWork.UI == null || UIForm == null)
                return;

            GameFrameWork.UI.Close(UIForm);
        }

        private void EnsureUi()
        {
            if (_titleText != null && _countText != null && _incrementButton != null)
                return;

            var root = CachedTransform as RectTransform;
            if (root == null)
                return;

            var panel = CreatePanelRoot(root);

            _titleText = CreateTmp(panel, "Title", new Vector2(0f, 120f), 36f);
            _countText = CreateTmp(panel, "Count", new Vector2(0f, 40f), 48f);
            _statusText = CreateTmp(panel, "Status", new Vector2(0f, -20f), 24f);

            _incrementButton = CreateButton(panel, "Increment", new Vector2(-120f, -100f), "＋");
            _resetButton = CreateButton(panel, "Reset", new Vector2(0f, -100f), "Reset");
            _closeButton = CreateButton(panel, "Close", new Vector2(120f, -100f), "Close");
        }

        private static RectTransform CreatePanelRoot(RectTransform parent)
        {
            var go = new GameObject("Content", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(480f, 360f);
            rt.anchoredPosition = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.12f, 0.14f, 0.18f, 0.92f);
            image.sprite = CreateWhiteSprite();
            return rt;
        }

        private static TextMeshProUGUI CreateTmp(RectTransform parent, string name, Vector2 anchoredPos, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(420f, 64f);
            rt.anchoredPosition = anchoredPos;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.text = name;
            return tmp;
        }

        private static Button CreateButton(RectTransform parent, string name, Vector2 anchoredPos, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(110f, 44f);
            rt.anchoredPosition = anchoredPos;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.25f, 0.45f, 0.75f, 1f);
            image.sprite = CreateWhiteSprite();
            image.type = Image.Type.Simple;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = label;
            tmp.fontSize = 22f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return go.GetComponent<Button>();
        }

        private static Sprite CreateWhiteSprite()
        {
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
