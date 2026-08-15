using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class DestructObject : MonoBehaviour
    {
        [HideInInspector]
        public DamageControl MyDamageControl;

        public GameObject m_DestroyPrefab;

        public GameObject m_DropItemPrefab;
        public int m_ItemDropCount = 1;

        [HideInInspector]
        public bool m_IsDead = false;
        // Use this for initialization
        void Start()
        {
            MyDamageControl = GetComponent<DamageControl>();

            m_IsDead = false;
        }

        // Update is called once per frame
        void Update()
        {

            if (!m_IsDead)
            {
                if (MyDamageControl.IsDead)
                {
                    if (m_DestroyPrefab != null)
                    {
                        GameObject obj = Instantiate(m_DestroyPrefab);
                        obj.transform.position = transform.position;
                        obj.transform.rotation = transform.rotation;
                        Destroy(obj, 5);
                    }

                    DropItem(m_ItemDropCount);
                    Destroy(gameObject);
                }
            }
        }

        public virtual void DropItem(int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject obj1 = Instantiate(m_DropItemPrefab);
                obj1.transform.position = transform.position;
                obj1.GetComponent<Rigidbody>().velocity = new Vector3(Random.Range(-5, 5), Random.Range(10, 20), Random.Range(-5, 5));
                obj1.GetComponent<Rigidbody>().angularVelocity = new Vector3(Random.Range(-20, 20), Random.Range(-20, 20), Random.Range(-20, 20));
            }
        }
    }
}