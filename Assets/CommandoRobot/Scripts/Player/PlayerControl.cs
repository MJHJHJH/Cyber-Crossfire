using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CommandoRobot.ScriptableObjects;
namespace CommandoRobot
{
    public class PlayerControl : MonoBehaviour
    {

        [HideInInspector]
        public PlayerCharacter MyPlayerChar;


        [HideInInspector]
        public PlayerControl OtherControl;

        [HideInInspector]
        public int Kills = 0;

        [HideInInspector]
        public bool m_IsDead = false;


        [HideInInspector]
        public WeaponBase[] MainWeapons = new WeaponBase[2];
        [HideInInspector]
        public int CurrentWeaponNum = 0;
        [HideInInspector]
        public WeaponBase CurrentWeapon;


        [HideInInspector]
        public Transform NextSpawnPoint;

        [HideInInspector]
        public Vector3 LastDeathPosition;


        public static PlayerControl m_Main;

        [HideInInspector]
        public int State = 0;
        [HideInInspector]
        public float StateStartTime = 0;

        [HideInInspector]
        public bool UsingPowerWeapon = false;

        public GameObject m_PlayerPrefab;
        void Awake()
        {
            m_Main = this;
        }

        void Start()
        {

            State = 0;
            StateStartTime = Time.time;

        }

        // Update is called once per frame
        void Update()
        {
            if (!m_IsDead)
            {
                if (PlayerCharacter.m_Current.m_IsDead)
                {
                    m_IsDead = true;
                    GameControl.m_Current.HandlePlayerDeath();
                }
            }
        }


        public void Kill()
        {
            m_IsDead = true;

        }


        public void EnablePlayer(bool enable)
        {
            MyPlayerChar.gameObject.SetActive(enable);
        }
        public void Respawn()
        {
            GameObject obj = Instantiate(m_PlayerPrefab);
            MyPlayerChar = obj.GetComponent<PlayerCharacter>();

            Vector3 SpawnPosition = Vector3.zero;
            SpawnPosition = GameControl.m_Current.m_Level.m_PlayerStartPoint.position + new Vector3(0, .1f, 0);

            MyPlayerChar.transform.position = SpawnPosition + new Vector3(0, .1f, 0);

            State = 1;
            StateStartTime = Time.time;
        }


        public void FindSpawnPoint()
        {

        }

        public void GiveWeapon(int num, int ammo)
        {
            MainWeapons[num].AmmoCount = ammo;
            MainWeapons[num].WeaponEnable = true;

            CurrentWeaponNum = num;
            CurrentWeapon = MainWeapons[num];
        }
    }
}