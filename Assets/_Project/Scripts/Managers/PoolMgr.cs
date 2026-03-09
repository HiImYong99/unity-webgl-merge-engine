using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// WebGL 최적화를 위한 범용 오브젝트 풀 매니저.
/// Instantiate/Destroy 부하를 줄여 GC Spike를 억제함.
/// </summary>
public class PoolMgr : MonoBehaviour
{
    private static PoolMgr _instance;
    public static PoolMgr Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("PoolMgr");
                _instance = go.AddComponent<PoolMgr>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // 레벨별 동물 프리팹 풀 (1~11레벨)
    private Dictionary<int, Queue<GameObject>> _animalPool = new Dictionary<int, Queue<GameObject>>();

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 레벨에 맞는 동물을 풀에서 가져옴.
    /// </summary>
    public GameObject GetAnimal(GameObject prefab, int level, Vector3 position, Quaternion rotation)
    {
        if (!_animalPool.ContainsKey(level))
        {
            _animalPool[level] = new Queue<GameObject>();
        }

        GameObject go;
        if (_animalPool[level].Count > 0)
        {
            go = _animalPool[level].Dequeue();
            go.transform.position = position;
            go.transform.rotation = rotation;
            go.SetActive(true);
        }
        else
        {
            go = Instantiate(prefab, position, rotation);
        }

        return go;
    }

    /// <summary>
    /// 동물을 풀로 반환함.
    /// </summary>
    public void ReturnAnimal(int level, GameObject animalObj)
    {
        if (animalObj == null) return;

        animalObj.SetActive(false);

        if (!_animalPool.ContainsKey(level))
        {
            _animalPool[level] = new Queue<GameObject>();
        }
        
        _animalPool[level].Enqueue(animalObj);
    }

    /// <summary>
    /// 모든 풀 초기화 (게임 리셋 시)
    /// </summary>
    public void ClearAllPools()
    {
        foreach (var queue in _animalPool.Values)
        {
            while (queue.Count > 0)
            {
                GameObject go = queue.Dequeue();
                if (go != null) Destroy(go);
            }
        }
        _animalPool.Clear();
    }
}
