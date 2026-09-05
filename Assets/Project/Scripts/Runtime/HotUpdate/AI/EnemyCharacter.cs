using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CommandoRobot
{
    public class EnemyCharacter : CharacterBase
    {
        [SerializeField]
        protected GameObject m_SpawnParticlePrefab;

        [HideInInspector]
        public Vector3 MoveDirection;
        [HideInInspector]
        public Vector3 InitPosition;

        public GameObject[] m_DropItemPrefabs;

        [HideInInspector]
        public bool m_FacePlayer = false;


        [HideInInspector]
        public bool m_Alerted = false;

        public int m_ItemDropCount = 1;
        // Start is called before the first frame update
        void Start()
        {
            m_DamageControl = GetComponent<DamageControl>();

            InitPosition = transform.position;
            GameControl.m_Current.m_TotalEnemyCount++;
            //GameObject obj = Instantiate(m_SpawnParticlePrefab);
            //obj.transform.position = InitPosition;
            //Destroy(obj, 3);
            if (m_WeaponPrefab != null)
                SetWeapon(m_WeaponPrefab);
        }

        // Update is called once per frame
        void Update()
        {
            //AI
            //Vector3 forward = Vector3.zero - transform.position;
            //forward.y = 0;
            //Quaternion rotation = Quaternion.LookRotation(forward);
            //transform.rotation = Quaternion.Lerp(transform.rotation, rotation, 10 * Time.deltaTime);
            if (m_MovementInput.magnitude > .1f)
                m_CharMovement.AddMovement(m_MovementInput);
            m_MovementInput = Vector3.zero;
            //Vector3 axis = Quaternion.Euler(0, 30 * m_DamageControl.DamageShakeAngle, 0) * Vector3.right;
            //ShakeBase.transform.localRotation = Quaternion.AngleAxis(-30 * m_DamageControl.DamageShakeAmount, axis);
            UpdateAnimatorData();
            UpdateRotation();
            CheckDeath();

            //if (Time.time>2)
            //{
            //    GetComponent<EnemyIdlePatrol>().StopPatrol();
            //}
        }

        private void LateUpdate()
        {
            UpdateRecoilTransforms();
        }

        public override void HandleDeath()
        {
            PlayDeathSound();

            GameObject obj = Instantiate(m_DeathParticle);
            obj.transform.position = transform.position;
            Destroy(obj, 3);

            DropItem(m_ItemDropCount);

            GameControl.m_Current.m_EnemyKilledCount++;

            CameraControl.m_Current.PlayShake(
                CameraShakePreset.Kill,
                ShakeSettings.Position(0.22f, 0.35f, 32f));

            Destroy(gameObject);
        }

        public virtual void HandleFacePlayer()
        {
            if (!m_FacePlayer || PlayerCharacter.m_Current == null)
                return;

            Vector3 dir = PlayerCharacter.m_Current.transform.position - transform.position;
            dir.y = 0;

            dir.Normalize();
            m_CharBody.m_RotationBase.rotation = Quaternion.Lerp(m_CharBody.m_RotationBase.rotation, Quaternion.LookRotation(dir), 10 * Time.deltaTime);
        }


        public virtual void DropItem(int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject obj1 = Instantiate(m_DropItemPrefabs[0]);
                obj1.transform.position = transform.position;
                obj1.GetComponent<Rigidbody>().velocity = new Vector3(Random.Range(-5, 5), Random.Range(10, 20), Random.Range(-5, 5));
                obj1.GetComponent<Rigidbody>().angularVelocity = new Vector3(Random.Range(-20, 20), Random.Range(-20, 20), Random.Range(-20, 20));
            }
        }


        public virtual void EnableEnemy()
        {

        }

        void OnDrawGizmos()
        {

            Gizmos.color = Color.red;
            //Gizmos.DrawLine(transform.position,MoveTargetPosition + new Vector3(0, 0.2f, 0));
            //Gizmos.DrawSphere(MoveTargetPosition, .5f);

        }
    }

}