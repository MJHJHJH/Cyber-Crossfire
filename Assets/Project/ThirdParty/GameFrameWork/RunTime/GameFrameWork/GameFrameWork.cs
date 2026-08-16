using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 游戏入口。
    /// </summary>
    public partial class GameFrameWork : MonoBehaviour
    {
        private void Start()
        {
            InitBuiltinComponents();
            // 启动链路由 ProcedureComponent → Launch → AssetInit → DataTableInit → Main 驱动
        }
    }
}
