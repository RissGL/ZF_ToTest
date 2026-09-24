namespace ZGameFramework.Core
{
    using System.Collections.Generic;
    using UnityEngine;
    using ZGameFramework.Core;

    public class PoolManager : PersistentMonoSingleton<PoolManager>
    {
        private Transform poolRoot;
        public class PoolData
        {
            public Transform parentObj;
            public Queue<GameObject> poolQueue;

            public PoolData(GameObject gameObject, Transform poolRoot)
            {
                parentObj = new GameObject(gameObject.name + "_Pool").transform;
                parentObj.SetParent(poolRoot);
                poolQueue = new Queue<GameObject>();
                PushObj(gameObject);
            }

            public void PushObj(GameObject obj)
            {
                obj.SetActive(false);
                obj.transform.SetParent(parentObj, false);
                poolQueue.Enqueue(obj);
            }

            public GameObject GetObj(Transform parent=null,bool worldPositionStays=true)
            {
                GameObject gameObject = poolQueue.Dequeue();
                gameObject.SetActive(true);
                gameObject.transform.SetParent(parent==null?parentObj:parent,worldPositionStays);
                return gameObject;
            }


        }

        private Dictionary<string, PoolData> poolDic = new Dictionary<string, PoolData>();

        protected override void Awake()
        {
            base.Awake();

            if (Instance == this)
            {
                poolRoot = new GameObject("PoolRoot").transform;
                poolRoot.SetParent(this.transform); 
            }
        }

        public GameObject GetGameObject(GameObject prefab, Transform parent = null,
            bool worldPositionStays = true)
        {
            string name = prefab.name;
            GameObject gameObject = null;

            if (poolDic.ContainsKey(name))
            {
                if (poolDic[name].poolQueue.Count > 0)
                {
                    gameObject = poolDic[name].GetObj(parent);
                }
                else
                {
                    gameObject = Instantiate(prefab, parent);
                    gameObject.name = name;
                }
            }
            else
            {
                gameObject = Instantiate(prefab, parent);
                gameObject.name = name;

                PoolData poolData = new PoolData(gameObject, poolRoot);
                poolDic.Add(name, poolData);

                gameObject=poolData.GetObj(parent);
            }

            return gameObject;
        }

        public void PushGameObject(GameObject gameObject)
        {
            string name = gameObject.name;

            if (poolDic.ContainsKey(name))
            {
                poolDic[name].PushObj(gameObject);
            }
            else
            {
                poolDic.Add(name, new PoolData(gameObject, poolRoot));
            }
        }

        public void Clear()
        {
            poolDic.Clear();
            foreach (Transform child in poolRoot)
            {
                Destroy(child.gameObject);
            }
        }
    }
}
