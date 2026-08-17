using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class WeaponShotgun : WeaponBase
    {
        public override void FireWeapon()
        {
            if (ProjectilePrefab != null)
            {
                for (int i = -2; i < 3; i++)
                {
                    CreateProjectile(i * 3f);
                }
            }

            CreateParticle();
        }

        public override void FireWeaponPowered()
        {
            if (ProjectilePrefab != null)
            {
                for (int i = -4; i < 5; i++)
                {
                    CreateProjectile(i * 4f);
                }
            }

            CreateParticle();
        }
    }


}
