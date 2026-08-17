using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class EnemyAI_Shooter_A : AIControlBase
    {
        public WeaponBase m_Weapon;
        // Start is called before the first frame update
        void Start()
        {
            m_Weapon.m_Owner = gameObject;
            m_Weapon.m_OwnerCharacter = GetComponent<EnemyCharacter>();
        }

        // Update is called once per frame
        void Update()
        {
            CheckAlert();

            if (m_IsAlerted)
            {

                GetComponent<EnemyCharacter>().FaceTowardsPosition(PlayerCharacter.m_Current.transform.position);
            }
        }

        public override void StartAlert()
        {
            base.StartAlert();

            StartCoroutine(Co_AttackLoop());
        }

        IEnumerator Co_AttackLoop()
        {
            yield return new WaitForSeconds(1);
            while (true)
            {
                m_Weapon.FireWeapon();
                yield return new WaitForSeconds(2);
            }
        }
    }
}