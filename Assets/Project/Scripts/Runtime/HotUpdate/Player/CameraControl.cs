using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CommandoRobot.ScriptableObjects;
namespace CommandoRobot
{
    public class CameraControl : MonoBehaviour
    {


        private float m_ShakeTimer;
        private float m_ShakeArc;
        private float m_ShakeRadius = 1;

        public Vector3 m_TargetOffset = Vector3.zero;

        public static CameraControl m_Current;

        public Camera m_MyCamera;

        Vector3 Direction;

        void Awake()
        {
            m_Current = this;
        }

        void OnDestroy()
        {
            if (m_Current == this)
                m_Current = null;
        }

        void Start()
        {
            Direction = transform.forward;
            m_MyCamera = GetComponent<Camera>();

            if (PlayerCharacter.m_Current == null)
                return;

            float distance = 30;
            Direction = Quaternion.Euler(50, 0, 0) * Vector3.forward;
            Vector3 targetPosition = PlayerCharacter.m_Current.transform.position;
            transform.position = targetPosition + -distance * Direction;
            transform.forward = Direction;
        }

        private void Update()
        {
            m_MyCamera.fieldOfView = Mathf.Lerp(m_MyCamera.fieldOfView, 50, 5 * Time.deltaTime);
        }

        void FixedUpdate()
        {
            if (PlayerCharacter.m_Current == null)
                return;

            m_ShakeTimer -= Time.deltaTime;
            //ShakeArc += 100 * Time.deltaTime;

            if (m_ShakeTimer <= 0)
                m_ShakeTimer = 0;

            Vector3 ShakeOffset = Vector3.zero;
            float shakeSin = Mathf.Cos(30 * Time.time) * Mathf.Clamp(m_ShakeTimer, 0, 0.5f);
            float shakeCos = Mathf.Sin(50 * Time.time) * Mathf.Clamp(m_ShakeTimer, 0, 0.5f);
            ShakeOffset = new Vector3(m_ShakeRadius * shakeCos, m_ShakeRadius * shakeSin, 0);


            m_TargetOffset = Vector3.Lerp(m_TargetOffset, 6 * PlayerCharacter.m_Current.m_CharBody.m_UpperAimBase.forward, Time.deltaTime);
            float distance = 30;
            Direction = Quaternion.Euler(50, 0, 0) * Vector3.forward;
            Vector3 targetPosition = PlayerCharacter.m_Current.transform.position + m_TargetOffset;

            if (GameControl.m_Current.m_IsBossFight && GameControl.m_Current.m_Boss != null)
            {
                targetPosition = GameControl.m_Current.m_Boss.transform.position + PlayerCharacter.m_Current.transform.position;
                targetPosition = .5f * targetPosition;
            }


            transform.position = Vector3.Lerp(transform.position, targetPosition + -distance * Direction, 5 * Time.deltaTime);// - distance// * m_FaceVector;
            transform.position += ShakeOffset;
            transform.forward = Vector3.Lerp(transform.forward, Direction, 5 * Time.deltaTime);

        }

        public void StartShake(float t, float r)
        {
            if (m_ShakeTimer == 0 || m_ShakeRadius < r)
                m_ShakeRadius = r;

            m_ShakeTimer = t;
        }
    }
}
