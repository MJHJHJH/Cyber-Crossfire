//------------------------------------------------------------
// Adapted from Game Framework
//------------------------------------------------------------

using System;
namespace GameFramework
{
    internal sealed partial class UIManager : GameFrameworkModule, IUIManager
    {
        private sealed class UIFormInstanceObject : ObjectBase
        {
            private object m_UIFormAsset;
            private IUIFormHelper m_UIFormHelper;
            private Action m_ReleaseAsset;

            public UIFormInstanceObject()
            {
                m_UIFormAsset = null;
                m_UIFormHelper = null;
                m_ReleaseAsset = null;
            }

            public static UIFormInstanceObject Create(string name, object uiFormAsset, object uiFormInstance, IUIFormHelper uiFormHelper, Action releaseAsset)
            {
                if (uiFormAsset == null)
                    throw new GameFrameworkException("UI form asset is invalid.");
                if (uiFormHelper == null)
                    throw new GameFrameworkException("UI form helper is invalid.");

                UIFormInstanceObject obj = ReferencePool.Acquire<UIFormInstanceObject>();
                obj.Initialize(name, uiFormInstance);
                obj.m_UIFormAsset = uiFormAsset;
                obj.m_UIFormHelper = uiFormHelper;
                obj.m_ReleaseAsset = releaseAsset;
                return obj;
            }

            public override void Clear()
            {
                base.Clear();
                m_UIFormAsset = null;
                m_UIFormHelper = null;
                m_ReleaseAsset = null;
            }

            protected internal override void Release(bool isShutdown)
            {
                m_UIFormHelper.ReleaseUIForm(m_UIFormAsset, Target);
                m_ReleaseAsset?.Invoke();
                m_ReleaseAsset = null;
            }
        }
    }
}
