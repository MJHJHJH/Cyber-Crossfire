using CommandoRobot.ScriptableObjects;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace CommandoRobot
{
    public class InputControl : MonoBehaviour
    {
        [HideInInspector]
        public Vector3 m_WorldAimPosition;

        //--inputs
        [HideInInspector]
        public Vector3 m_Movement;
        [HideInInspector]
        public bool m_Fire;
        [HideInInspector]
        public bool m_Grenade;

        public static InputControl m_Main;

        public bool m_MobileControl = false;
        public bool m_MouseAim = false;

        void Awake()
        {
            m_Main = this;
        }
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            m_Movement = Vector3.zero;
            m_Fire = false;
            m_Grenade = false;

            // 仅游戏正常运行（State_Gameplay 且未暂停/未结算）时采集输入；
            // 暂停界面打开、开局倒计时、胜负结算期间不响应移动/旋转瞄准/开火
            if (GameControl.m_Current != null && !GameControl.m_Current.IsGamePlaying)
                return;

            if (!m_MobileControl)
            {
                m_Movement.x = Input.GetAxis("Horizontal");
                m_Movement.z = Input.GetAxis("Vertical");

                // 开火/手雷不依赖瞄准模式，避免 m_MouseAim 场景配置不一致时无法开火
                // 仅当指针点在“可交互 UI”（按钮等）上时才屏蔽鼠标开火/手雷，避免点战斗HUD误射；
                // 纯展示图形（如跟随鼠标的准星）即使 raycastTarget=true 也不挡开火（键盘 Z/X 不受影响）
                bool overInteractiveUI = IsPointerOverInteractiveUI();
                m_Fire = (!overInteractiveUI && Input.GetMouseButton(0)) || Input.GetKey(KeyCode.Z);
                m_Grenade = (!overInteractiveUI && Input.GetMouseButtonDown(1)) || Input.GetKeyDown(KeyCode.X);

                if (CameraControl.m_Current != null)
                {
                    Ray ray = CameraControl.m_Current.GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
                    float dis = 0;
                    new Plane(Vector3.up, Vector3.zero).Raycast(ray, out dis);
                    m_WorldAimPosition = ray.origin + dis * ray.direction;
                }
            }
            else
            {
                if (Joystick.GeneralJoystick != null)
                {
                    m_Movement.x = Joystick.GeneralJoystick.LeftStick.StickDirection.x;
                    m_Movement.z = Joystick.GeneralJoystick.LeftStick.StickDirection.y;

                    if (Joystick.GeneralJoystick.ButtonA.Hold)
                        m_Fire = true;
                    if (Joystick.GeneralJoystick.ButtonB.Pressed)
                        m_Grenade = true;
                }
            }


            m_Movement = Vector3.ClampMagnitude(m_Movement, 1.0f);
        }

        static readonly List<RaycastResult> s_UiRaycastCache = new List<RaycastResult>();

        /// <summary>
        /// 指针是否位于“可交互”UI 上：用事件系统射线取指针下最上层命中项，
        /// 再沿父链查找 Selectable / 指针类处理器。纯展示图形（准星、图标、边框等）不算交互，不屏蔽开火。
        /// </summary>
        bool IsPointerOverInteractiveUI()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            PointerEventData pointerData = new PointerEventData(eventSystem);
            pointerData.position = Input.mousePosition;
            s_UiRaycastCache.Clear();
            eventSystem.RaycastAll(pointerData, s_UiRaycastCache);
            if (s_UiRaycastCache.Count == 0)
                return false;

            return HasInteractiveHandler(s_UiRaycastCache[0].gameObject);
        }

        static bool HasInteractiveHandler(GameObject root)
        {
            Transform node = root != null ? root.transform : null;
            while (node != null)
            {
                Component[] components = node.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    Component c = components[i];
                    // Button/Toggle/Slider/Dropdown 等可点控件均继承 Selectable
                    if (c is Selectable)
                        return true;
                    if (c is IPointerClickHandler
                        || c is IPointerDownHandler
                        || c is IPointerUpHandler
                        || c is IBeginDragHandler
                        || c is IDragHandler
                        || c is IScrollHandler
                        || c is ISubmitHandler)
                        return true;
                }
                node = node.parent;
            }
            return false;
        }
    }
}