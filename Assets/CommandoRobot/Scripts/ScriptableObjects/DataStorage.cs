using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


namespace CommandoRobot.ScriptableObjects
{
    [CreateAssetMenu(fileName = "DataStorage", menuName = "CustomObjects/DataStorage", order = 1)]
    public class DataStorage : ScriptableObject
    {

        [SerializeField, Space]
        private Contents m_Contents;
        public int Coin;

        public int LevelUnlocked;

        public bool[] m_WeaponsUnlocked;
        public int[] m_WeaponsPrice;
        public int m_SelectedWeapon = 0;


        public void SaveData()
        {

            PlayerPrefs.SetInt("Coin", Coin);

            PlayerPrefs.SetInt("LevelUnlocked", LevelUnlocked);
            PlayerPrefs.SetInt("m_SelectedWeapon", m_SelectedWeapon);

            for (int i = 0; i < 6; i++)
            {
                if (m_WeaponsUnlocked[i])
                    PlayerPrefs.SetInt("m_WeaponsUnlocked" + i.ToString(), 1);
                else
                    PlayerPrefs.SetInt("m_WeaponsUnlocked" + i.ToString(), 0);
            }

            PlayerPrefs.Save();
        }

        public void LoadData()
        {

            Coin = PlayerPrefs.GetInt("Coin", 0);

            LevelUnlocked = PlayerPrefs.GetInt("LevelUnlocked", 0);
            m_SelectedWeapon = PlayerPrefs.GetInt("m_SelectedWeapon", 0);

            m_WeaponsUnlocked = new bool[6];
            for (int i = 0; i < 6; i++)
            {
                m_WeaponsUnlocked[i] = (PlayerPrefs.GetInt("m_WeaponsUnlocked" + i.ToString(), 0) == 1);
            }
            m_WeaponsUnlocked[0] = true;
        }

        public void ResetSaveData()
        {
            SaveData();
        }

        public bool CheckInternet()
        {
            if (Application.internetReachability == NetworkReachability.ReachableViaCarrierDataNetwork || Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork)
                return true;
            return false;
        }
    }
}
