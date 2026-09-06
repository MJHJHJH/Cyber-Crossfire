using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class Explosion : MonoBehaviour
    {

        // Use this for initialization
        public float Radius = 5;

        [Tooltip("爆炸来源阵营：true=敌方爆炸(伤害玩家)，false=玩家爆炸(伤害敌人)。避免炸到自己人")]
        public bool m_IsEnemyTeam = true;
        void Start()
        {
            //CameraControl.MainCameraControl.StartShake(1f, 1f);

            //if (transform.position.y <= 2)
            //{
            //    GameObject obj = Instantiate(GlobalContents.MainGlobalContent.DecalsPrefabs[0]);
            //    Vector3 pos = transform.position;
            //    pos.y = 0.1f;
            //    obj.transform.position = pos;
            //    Destroy(obj, 30);
            //}


            Collider[] colls = Physics.OverlapSphere(transform.position, Radius);
            foreach (Collider col in colls)
            {
                if (col.gameObject.tag == "Player" && m_IsEnemyTeam)
                {
                    float lerp = Vector3.Distance(col.bounds.center, transform.position) / (float)Radius;
                    PlayerCharacter p = col.gameObject.GetComponent<PlayerCharacter>();
                    p.m_DamageControl.ApplyDamage(Mathf.Lerp(40, 5, lerp), p.transform.position + new Vector3(0, 2, 0) - transform.position, 4);
                    //print(Vector3.Distance(col.bounds.center, transform.position));
                }
                else if (col.gameObject.tag == "Block")
                {
                    Rigidbody rb = col.gameObject.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.AddForceAtPosition(col.gameObject.transform.position - transform.forward, transform.position);
                    }

                    DamageControl d = col.gameObject.GetComponent<DamageControl>();
                    if (d != null)
                    {
                        float lerp = Vector3.Distance(col.bounds.center, transform.position) / Radius;
                        d.ApplyDamage(Mathf.Lerp(10, 1, lerp), transform.forward, 1);
                    }
                }
                else if (col.gameObject.tag == "Enemy" && !m_IsEnemyTeam)
                {
                    DamageControl d = col.gameObject.GetComponent<DamageControl>();
                    if (d != null)
                    {
                        d.ApplyDamage(10, transform.forward, 1);
                    }
                }
            }

            //Destroy(gameObject);
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}