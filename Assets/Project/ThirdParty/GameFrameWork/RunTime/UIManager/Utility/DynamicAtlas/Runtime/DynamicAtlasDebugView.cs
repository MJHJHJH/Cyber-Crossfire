using UnityEngine;
using UnityEngine.UI;

namespace DynamicAtlas.Demo
{
    /// <summary>
    /// 调试：将指定分组/页的动态图集纹理显示到 RawImage。
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class DynamicAtlasDebugView : MonoBehaviour
    {
        [SerializeField] private RawImage _rawImage;
        [SerializeField] private DynamicAtlasGroup _group = DynamicAtlasGroup.Size1024;
        [SerializeField] private int _pageIndex;
        [SerializeField] private bool _refreshEveryFrame = true;

        public DynamicAtlasGroup Group
        {
            get => _group;
            set => _group = value;
        }

        public int PageIndex
        {
            get => _pageIndex;
            set => _pageIndex = Mathf.Max(0, value);
        }

        private void Awake()
        {
            if (_rawImage == null)
                _rawImage = GetComponent<RawImage>();
        }

        private void LateUpdate()
        {
            if (_refreshEveryFrame)
                Refresh();
        }

        [ContextMenu("Refresh")]
        public void Refresh()
        {
            if (_rawImage == null)
                return;

            int pageCount = DynamicAtlasManager.Instance.GetPageCount(_group);
            if (pageCount <= 0)
            {
                _rawImage.texture = null;
                return;
            }

            int index = Mathf.Clamp(_pageIndex, 0, pageCount - 1);
            if (DynamicAtlasManager.Instance.TryGetPageTexture(_group, index, out Texture2D texture))
                _rawImage.texture = texture;
            else
                _rawImage.texture = null;
        }

        [ContextMenu("Next Page")]
        public void NextPage()
        {
            int pageCount = DynamicAtlasManager.Instance.GetPageCount(_group);
            if (pageCount <= 0)
                return;

            _pageIndex = (_pageIndex + 1) % pageCount;
            Refresh();
        }

        [ContextMenu("Prev Page")]
        public void PrevPage()
        {
            int pageCount = DynamicAtlasManager.Instance.GetPageCount(_group);
            if (pageCount <= 0)
                return;

            _pageIndex = (_pageIndex - 1 + pageCount) % pageCount;
            Refresh();
        }
    }
}
