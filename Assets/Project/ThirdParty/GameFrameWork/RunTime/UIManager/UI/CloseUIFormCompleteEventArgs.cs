//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework.Event;

namespace GameFramework
{
    /// <summary>
    /// 关闭界面完成事件。
    /// </summary>
    public sealed class CloseUIFormCompleteEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(CloseUIFormCompleteEventArgs).GetHashCode();

        public CloseUIFormCompleteEventArgs()
        {
            SerialId = 0;
            UIFormAssetName = null;
            UIGroup = null;
            UserData = null;
        }

        public override int Id => EventId;

        public int SerialId { get; private set; }

        public string UIFormAssetName { get; private set; }

        public IUIGroup UIGroup { get; private set; }

        public object UserData { get; private set; }

        public static CloseUIFormCompleteEventArgs Create(int serialId, string uiFormAssetName, IUIGroup uiGroup, object userData)
        {
            CloseUIFormCompleteEventArgs e = ReferencePool.Acquire<CloseUIFormCompleteEventArgs>();
            e.SerialId = serialId;
            e.UIFormAssetName = uiFormAssetName;
            e.UIGroup = uiGroup;
            e.UserData = userData;
            return e;
        }

        public static CloseUIFormCompleteEventArgs Create(CloseUIFormCompleteEventArgs e)
        {
            return Create(e.SerialId, e.UIFormAssetName, e.UIGroup, e.UserData);
        }

        public override void Clear()
        {
            SerialId = 0;
            UIFormAssetName = null;
            UIGroup = null;
            UserData = null;
        }
    }
}
