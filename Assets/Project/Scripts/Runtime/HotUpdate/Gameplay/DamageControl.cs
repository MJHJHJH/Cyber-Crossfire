using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
namespace CommandoRobot
{
    public class DamageControl : MonoBehaviour
    {

        [HideInInspector]
        public float Damage = 100;

        public float MaxDamage = 100;

        [HideInInspector]
        public bool IsDead = false;
        [HideInInspector]
        public bool m_NoDamage = false;

        [HideInInspector]
        public Vector3 LastDamageDirection;
        [HideInInspector]
        public float LastDamageFactor = 1;

        [HideInInspector]
        public float DamageShakeAmount;
        [HideInInspector]
        public float DamageShakeAngle;

        public UnityEvent OnDamaged;


        // Use this for initialization

        void Awake()
        {
            OnDamaged = new UnityEvent();
        }
        void Start()
        {
            Damage = MaxDamage;
            IsDead = false;
            LastDamageDirection = Vector3.forward;
            DamageShakeAmount = 0;
            DamageShakeAngle = 0;
            //
        }

        // Update is called once per frame
        void Update()
        {
            DamageShakeAmount -= 12 * Time.deltaTime;
            if (DamageShakeAmount <= 0)
                DamageShakeAmount = 0;
        }

        public void ApplyDamage(float dmg, Vector3 dir, float DamageFactor)
        {
            if (m_NoDamage || IsDead)
                return;

            ApplyDamageShake();
            LastDamageDirection = dir;
            LastDamageDirection.Normalize();
            LastDamageFactor = DamageFactor;
            Damage -= dmg;
            if (Damage <= 0)
            {
                Damage = 0;
                IsDead = true;
            }
            OnDamaged.Invoke();
            StartCoroutine(Co_HitGlow());
        }

        public IEnumerator Co_HitGlow()
        {
            HitGlowControl[] glowControls = GetComponentsInChildren<HitGlowControl>();
            foreach (HitGlowControl item in glowControls)
            {
                item.SetGlow();
            }

            yield return new WaitForSeconds(.05f);

            foreach (HitGlowControl item in glowControls)
            {
                item.SetOriginal();
            }

            yield return null;
        }

        public void AddHealth(float h)
        {
            Damage = Mathf.Clamp(Damage + h, 0, MaxDamage);
        }

        public void ApplyDamageShake()
        {
            if (DamageShakeAmount == 0)
            {
                DamageShakeAmount = 1;
                DamageShakeAngle = Random.Range(-1f, 1f);
            }
        }

        public void Reset()
        {
            Damage = MaxDamage;
            IsDead = false;
        }
    }
}