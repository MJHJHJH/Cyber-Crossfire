using UnityEngine;
using UnityEngine.UI;

namespace DynamicAtlas
{
    [AddComponentMenu("UI/Dynamic Atlas Image")]
    [RequireComponent(typeof(CanvasRenderer))]
    public class DynamicAtlasImage : Image
    {
        [SerializeField] private string _location;
        [SerializeField] private DynamicAtlasGroup _atlasGroup = DynamicAtlasGroup.Size1024;
        [SerializeField] private bool _loadOnEnable = true;

        private string _boundKey;
        private int _loadVersion;
        private bool _applyingAtlasSprite;

        /// <summary>
        /// 赋值 Source Image：解析为 Location（文件名）后合图，与 SetLocation 同一套 key。
        /// </summary>
        public new Sprite sprite
        {
            get => base.sprite;
            set => ApplySourceSprite(value);
        }

        public string Location
        {
            get => _location;
            set
            {
                if (_location == value)
                    return;
                _location = value;
                if (isActiveAndEnabled)
                    Reload();
            }
        }

        public DynamicAtlasGroup AtlasGroup
        {
            get => _atlasGroup;
            set => _atlasGroup = value;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (_loadOnEnable && !string.IsNullOrEmpty(_location))
            {
                Reload();
                return;
            }

            if (Application.isPlaying && base.sprite != null && !IsOwnBoundSprite(base.sprite))
                ApplySourceSprite(base.sprite);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _loadVersion++;
        }

        protected override void OnDestroy()
        {
            ReleaseBound();
            base.OnDestroy();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (!Application.isPlaying || _applyingAtlasSprite)
                return;

            if (base.sprite != null && !IsOwnBoundSprite(base.sprite))
                ApplySourceSprite(base.sprite);
        }
#endif

        /// <summary>
        /// 将外部 Sprite 解析为 Location 后合入动态图集并显示。
        /// </summary>
        public void ApplySourceSprite(Sprite source)
        {
            if (_applyingAtlasSprite)
            {
                base.sprite = source;
                return;
            }

            if (source == null)
            {
                _location = null;
                ReleaseBound();
                return;
            }

            if (IsOwnBoundSprite(source))
            {
                SetAtlasSprite(source);
                return;
            }

            if (source.texture != null &&
                DynamicAtlasManager.Instance.IsAtlasPageTexture(source.texture))
            {
                RecoverFromAtlasSprite(source);
                return;
            }

            if (!DynamicAtlasLocation.TryResolveFromSprite(source, out string location))
            {
                Debug.LogWarning("[DynamicAtlasImage] cannot resolve Location from sprite");
                return;
            }

            _location = location;
            _loadVersion++;

            AtlasInsertResult result = DynamicAtlasManager.Instance.Insert(location, source, _atlasGroup);
            if (!result.Success || result.Entry == null)
            {
                Debug.LogWarning($"[DynamicAtlasImage] source sprite insert failed: location={location}, reason={result.FailReason}");
                return;
            }

            BindEntry(result.Entry, _boundKey);
        }

        public void Reload()
        {
            if (string.IsNullOrEmpty(_location))
            {
                ReleaseBound();
                return;
            }

            if (!Application.isPlaying || !DynamicAtlasLoader.IsYooAssetReady())
                return;

            _loadVersion++;
            int version = _loadVersion;

            string previousKey = _boundKey;
            DynamicAtlasLoader.LoadAndInsertAsync(_location, _atlasGroup, result =>
            {
                if (this == null || version != _loadVersion)
                {
                    if (result.Success && result.Entry != null)
                        DynamicAtlasManager.Instance.Release(result.Entry.Key);
                    return;
                }

                if (!result.Success || result.Entry == null)
                {
                    Debug.LogWarning($"[DynamicAtlasImage] load/insert failed: {_location}, reason={result.FailReason}");
                    return;
                }

                BindEntry(result.Entry, previousKey);
            });
        }

        public void SetLocation(string location, bool reload = true)
        {
            _location = location;
            if (reload)
                Reload();
        }

        private void RecoverFromAtlasSprite(Sprite atlasSprite)
        {
            if (DynamicAtlasManager.Instance.TryGetEntryBySprite(atlasSprite, out AtlasEntry entry))
            {
                entry.RefCount++;
                if (string.IsNullOrEmpty(_location))
                    _location = entry.Location;
                BindEntry(entry, _boundKey);
                return;
            }

            if (!string.IsNullOrEmpty(_location))
            {
                if (DynamicAtlasManager.Instance.TryGet(_location, _atlasGroup, out AtlasEntry byLoc))
                {
                    byLoc.RefCount++;
                    BindEntry(byLoc, _boundKey);
                    return;
                }

                Reload();
                return;
            }

            Debug.LogWarning(
                "[DynamicAtlasImage] 收到图集 Sprite 但无法恢复绑定（无 Location / 条目已释放），已忽略以免污染图集");
        }

        private void BindEntry(AtlasEntry entry, string previousKey)
        {
            if (!string.IsNullOrEmpty(previousKey) && previousKey == entry.Key)
                DynamicAtlasManager.Instance.Release(entry.Key);
            else if (!string.IsNullOrEmpty(previousKey))
                DynamicAtlasManager.Instance.Release(previousKey);

            _boundKey = entry.Key;
            SetAtlasSprite(entry.Sprite);
        }

        private void SetAtlasSprite(Sprite atlasSprite)
        {
            _applyingAtlasSprite = true;
            base.sprite = atlasSprite;
            _applyingAtlasSprite = false;
        }

        private bool IsOwnBoundSprite(Sprite s)
        {
            if (s == null || string.IsNullOrEmpty(_boundKey))
                return false;

            return DynamicAtlasManager.Instance.TryGet(_boundKey, out AtlasEntry entry)
                   && entry != null
                   && ReferenceEquals(entry.Sprite, s);
        }

        private void ReleaseBound()
        {
            _loadVersion++;
            if (string.IsNullOrEmpty(_boundKey))
            {
                SetAtlasSprite(null);
                return;
            }

            DynamicAtlasManager.Instance.Release(_boundKey);
            _boundKey = null;
            SetAtlasSprite(null);
        }
    }
}
