using GameFramework;
using UnityEngine;
namespace GameFramework
{
    /// <summary>
    /// 游戏入口。
    /// </summary>
    public partial class GameFrameWork : MonoBehaviour
    {
        /// <summary>
        /// 获取游戏基础组件。
        /// </summary>
        public static BaseComponent Base
        {
            get;
            private set;
        }

        public static UIComponent UI
        {
            get;
            private set;
        }

        public static ObjectPoolComponent ObjectPool
        {
            get;
            private set;
        }

        public static FsmComponent Fsm
        {
            get;
            private set;
        }

        public static ProcedureComponent Procedure
        {
            get;
            private set;
        }

        public static LubanConfigComponent DataTable
        {
            get;
            private set;
        }

        public static EventComponent Event
        {
            get;
            private set;
        }

        public static SceneComponent Scene
        {
            get;
            private set;
        }

        public static SoundComponent Sound
        {
            get;
            private set;
        }

        private static void InitBuiltinComponents()
        {
            Base = GameEntry.GetComponent<BaseComponent>();
            UI = GameEntry.GetComponent<UIComponent>();
            ObjectPool = GameEntry.GetComponent<ObjectPoolComponent>();
            Fsm = GameEntry.GetComponent<FsmComponent>();
            Procedure = GameEntry.GetComponent<ProcedureComponent>();
            DataTable = GameEntry.GetComponent<LubanConfigComponent>();
            Event = GameEntry.GetComponent<EventComponent>();
            Scene = GameEntry.GetComponent<SceneComponent>();
            Sound = GameEntry.GetComponent<SoundComponent>();
        }
    }
}
