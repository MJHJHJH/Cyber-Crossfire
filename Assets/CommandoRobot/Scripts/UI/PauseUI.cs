using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace CommandoRobot
{
    public class PauseUI : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        public void BtnContinue()
        {
            GameControl.m_Current.ResumeGame();
        }
        public void BtnExit()
        {
            GameControl.m_Current.ExitGame();
        }
    }
}