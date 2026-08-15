using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class EnemyAI_Canon_A : AIControlBase
    {
        public WeaponBase[] m_CanonGuns;
        // Start is called before the first frame update
        void Start()
        {
            foreach (WeaponBase item in m_CanonGuns)
            {
                item.m_Owner = gameObject;
                item.m_OwnerCharacter = GetComponent<EnemyCharacter>();
            }

        }

        // Update is called once per frame
        void Update()
        {
            CheckAlert();

            if (m_IsAlerted)
            {
                GetComponent<EnemyCharacter>().AimTowardsPosition(PlayerCharacter.m_Current.transform.position);
            }
        }

        public override void StartAlert()
        {
            base.StartAlert();

            StartCoroutine(Co_AttackLoop());
        }

        IEnumerator Co_AttackLoop()
        {
            yield return new WaitForSeconds(2);
            while (true)
            {
                m_CanonGuns[0].FireWeapon();
                m_CanonGuns[1].FireWeapon();
                yield return new WaitForSeconds(2);
            }
        }
    }
}