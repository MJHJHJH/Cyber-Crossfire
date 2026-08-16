using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class CharacterMovement : MonoBehaviour
    {

        public Vector3 m_Movement;
        public bool m_HaveMovementInput = false;
        public float m_MaxSpeed = 8;
        public float m_MoveAccel = 2;

        public Vector3 m_CurrentVelocity;
        public Vector3 m_CurrentHorVelocity;

        [HideInInspector]
        public bool m_AutoMoveTotTarget = false;
        [HideInInspector]
        public Vector3 m_TargetPosition;
        [HideInInspector]
        public bool m_ReachedTargetPosition = false;

        public bool m_Hover = false;

        [HideInInspector]
        public bool m_FaceTowardsMovement = true;

        Rigidbody _rigidBody;

        void Awake()
        {
            _rigidBody = GetComponent<Rigidbody>();
        }

        // Start is called before the first frame update
        void Start()
        {
            m_Movement = Vector3.zero;
            m_CurrentVelocity = Vector3.zero;
        }

        // Update is called once per frame
        void Update()
        {
            UpdateAutoMovement();

            if (!m_HaveMovementInput)
                m_Movement -= 20 * Time.deltaTime * m_Movement;

            m_HaveMovementInput = false;
        }

        public void UpdateAutoMovement()
        {
            if (!m_AutoMoveTotTarget) return;

            if (!m_ReachedTargetPosition)
            {
                if (m_TargetPosition != Vector3.zero)
                {
                    Vector3 toTarget = m_TargetPosition - transform.position;
                    toTarget.y = 0;

                    if (toTarget.magnitude <= .5f)
                    {
                        m_TargetPosition = Vector3.zero;
                        m_Movement = Vector3.zero;
                        m_ReachedTargetPosition = true;
                        m_AutoMoveTotTarget = false;
                    }

                    m_Movement = Vector3.ClampMagnitude(toTarget, 1);

                    //if (m_FaceMoveDirection && m_MovementDirection != Vector3.zero)
                    //{
                    //    transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(m_MovementDirection), 10 * Time.deltaTime);
                    //}
                }
            }
        }

        public void SetAutoMovement(Vector3 position)
        {
            m_ReachedTargetPosition = false;
            m_AutoMoveTotTarget = true;
            m_TargetPosition = position;
        }

        public void StopAutoMovement()
        {
            m_ReachedTargetPosition = true;
            m_AutoMoveTotTarget = false;
            m_TargetPosition = Vector3.zero;
            m_Movement = Vector3.zero;
        }

        public void AddMovement(Vector3 movement)
        {
            m_HaveMovementInput = true;
            m_Movement += movement;
            m_Movement = Vector3.ClampMagnitude(m_Movement, 1);
        }


        void FixedUpdate()
        {
            if (_rigidBody == null) return;

            bool kinematic = _rigidBody.isKinematic;
            Vector3 totalVelocity = kinematic ? m_CurrentVelocity : _rigidBody.velocity;
            float keepY = kinematic ? m_CurrentVelocity.y : _rigidBody.velocity.y;

            if (m_Movement.magnitude > .1f)
            {
                totalVelocity += m_MoveAccel * m_Movement;
                totalVelocity.y = 0;
                totalVelocity = Vector3.ClampMagnitude(totalVelocity, m_MaxSpeed);
                totalVelocity.y = keepY;
            }
            else
            {
                totalVelocity -= .4f * totalVelocity;
                totalVelocity.y = keepY;
            }

            if (m_Hover)
            {
                totalVelocity.y -= transform.position.y;
            }

            if (kinematic)
            {
                _rigidBody.MovePosition(_rigidBody.position + totalVelocity * Time.fixedDeltaTime);
                m_CurrentVelocity = totalVelocity;
            }
            else
            {
                _rigidBody.velocity = totalVelocity;
                m_CurrentVelocity = _rigidBody.velocity;
            }

            m_CurrentHorVelocity = m_CurrentVelocity;
            m_CurrentHorVelocity.y = 0;
        }


    }
}