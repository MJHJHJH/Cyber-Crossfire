using CommandoRobot.ScriptableObjects;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

            if (!m_MobileControl)
            {
                m_Movement.x = Input.GetAxis("Horizontal");
                m_Movement.z = Input.GetAxis("Vertical");

                // 开火/手雷不依赖瞄准模式，避免 m_MouseAim 场景配置不一致时无法开火
                m_Fire = Input.GetMouseButton(0) || Input.GetKey(KeyCode.Z);
                m_Grenade = Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.X);

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
    }
}