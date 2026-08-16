using UnityEngine;

namespace GameFramework
{
    public sealed partial class SoundComponent : GameFrameworkComponent
    {
        /// <summary>
        /// Unity 侧播放信息：3D 绑定目标与位置（经核心模块 UserData 透传）。
        /// </summary>
        public sealed class PlaySoundInfo
        {
            public Transform BindingTransform { get; }
            public Vector3 WorldPosition { get; }
            public object UserData { get; }

            public PlaySoundInfo(Transform bindingTransform, Vector3 worldPosition, object userData)
            {
                BindingTransform = bindingTransform;
                WorldPosition = worldPosition;
                UserData = userData;
            }
        }
    }
}
