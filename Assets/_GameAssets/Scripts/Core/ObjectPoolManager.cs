using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sık üretilen efekt ve oyun nesneleri için nesne havuzu yöneticisi (GC baskısını azaltır).
/// Unity 6000.6 uyumlu: Object.GetInstanceID() yerine GameObject referans anahtarları kullanır.
/// </summary>
public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; }

    [System.Serializable]
    public class PoolConfig
    {
        public GameObject prefab;
        public int initialSize = 5;
    }

    [Header("--- Prewarm Pools ---")]
    [SerializeField] private List<PoolConfig> _prewarmPools = new List<PoolConfig>();

    // Prefab bazlı havuz kuyrukları
    private readonly Dictionary<GameObject, Queue<GameObject>> _pools = new Dictionary<GameObject, Queue<GameObject>>();
    // Aktif örneklerin listesi (takip ve istatistik için)
    private readonly HashSet<GameObject> _activeInstances = new HashSet<GameObject>();
    // Havuz içi gecikmeli iade istekleri
    private readonly List<DelayedReturnEntry> _delayedReturns = new List<DelayedReturnEntry>();

    private struct DelayedReturnEntry
    {
        public GameObject instance;
        public float returnTime;
    }

    public int ActiveCount => _activeInstances.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        PrewarmConfiguredPools();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        StepDelayedReturns(Time.time);
    }

    public void StepDelayedReturns(float currentTime)
    {
        for (int i = _delayedReturns.Count - 1; i >= 0; i--)
        {
            var item = _delayedReturns[i];
            if (currentTime >= item.returnTime)
            {
                _delayedReturns.RemoveAt(i);
                if (item.instance != null)
                {
                    ReturnObject(item.instance);
                }
            }
        }
    }

    private void PrewarmConfiguredPools()
    {
        if (_prewarmPools == null) return;
        foreach (var config in _prewarmPools)
        {
            if (config != null && config.prefab != null && config.initialSize > 0)
            {
                Prewarm(config.prefab, config.initialSize);
            }
        }
    }

    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0) return;
        Queue<GameObject> pool = GetOrCreatePool(prefab);

        for (int i = 0; i < count; i++)
        {
            GameObject instance = CreateNewInstance(prefab);
            instance.SetActive(false);
            var pooled = instance.GetComponent<PooledObject>();
            if (pooled != null) pooled.SetPooledState(true);
            pool.Enqueue(instance);
        }
    }

    private Queue<GameObject> GetOrCreatePool(GameObject prefab)
    {
        if (!_pools.TryGetValue(prefab, out var pool))
        {
            pool = new Queue<GameObject>();
            _pools[prefab] = pool;
        }
        return pool;
    }

    private GameObject CreateNewInstance(GameObject prefab)
    {
        GameObject instance = Instantiate(prefab, transform);
        instance.name = prefab.name + "_Pooled";
        var pooled = instance.GetComponent<PooledObject>();
        if (pooled == null)
        {
            pooled = instance.AddComponent<PooledObject>();
        }
        pooled.PrefabSource = prefab;
        pooled.OriginManager = this;
        return instance;
    }

    public GameObject SpawnObject(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null) return null;

        Queue<GameObject> pool = GetOrCreatePool(prefab);
        GameObject instance = null;

        while (pool.Count > 0)
        {
            instance = pool.Dequeue();
            if (instance != null) break;
        }

        if (instance == null)
        {
            instance = CreateNewInstance(prefab);
        }

        instance.transform.SetParent(parent != null ? parent : transform);
        instance.transform.position = position;
        instance.transform.rotation = rotation;

        var pooledComp = instance.GetComponent<PooledObject>();
        if (pooledComp != null)
        {
            pooledComp.SetPooledState(false);
            pooledComp.OriginManager = this;
        }

        _activeInstances.Add(instance);
        instance.SetActive(true);

        // ParticleSystem varsa temizleyip baştan oynat
        var ps = instance.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Clear();
            ps.Play();
        }
        var childPs = instance.GetComponentsInChildren<ParticleSystem>();
        for (int i = 0; i < childPs.Length; i++)
        {
            childPs[i].Clear();
            childPs[i].Play();
        }

        // IPoolable desteği
        var poolables = instance.GetComponents<IPoolable>();
        for (int i = 0; i < poolables.Length; i++)
        {
            poolables[i].OnSpawnedFromPool();
        }

        return instance;
    }

    public void ReturnObject(GameObject instance)
    {
        if (instance == null) return;

        var pooled = instance.GetComponent<PooledObject>();
        if (pooled != null && pooled.IsPooled)
        {
            return; // Zaten havuzda
        }

        if (pooled != null)
        {
            pooled.SetPooledState(true);
            _activeInstances.Remove(instance);

            var poolables = instance.GetComponents<IPoolable>();
            for (int i = 0; i < poolables.Length; i++)
            {
                poolables[i].OnReturnedToPool();
            }

            instance.SetActive(false);
            instance.transform.SetParent(transform);

            if (pooled.PrefabSource != null)
            {
                Queue<GameObject> pool = GetOrCreatePool(pooled.PrefabSource);
                pool.Enqueue(instance);
                return;
            }
        }

        _activeInstances.Remove(instance);
        instance.SetActive(false);
    }

    public void ReturnObject(GameObject instance, float delay)
    {
        if (instance == null) return;
        if (delay <= 0.001f)
        {
            ReturnObject(instance);
            return;
        }

        _delayedReturns.Add(new DelayedReturnEntry
        {
            instance = instance,
            returnTime = Time.time + delay
        });
    }

    public int GetPooledCount(GameObject prefab)
    {
        if (prefab == null) return 0;
        return _pools.TryGetValue(prefab, out var q) ? q.Count : 0;
    }

    public void ClearAll()
    {
        _delayedReturns.Clear();
        _activeInstances.Clear();
        foreach (var kvp in _pools)
        {
            var q = kvp.Value;
            while (q.Count > 0)
            {
                var item = q.Dequeue();
                if (item != null)
                {
                    DestroyImmediate(item);
                }
            }
        }
        _pools.Clear();
    }

    // --- Statik Kolaylık Yöntemleri ---

    public static ObjectPoolManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        var found = Object.FindAnyObjectByType<ObjectPoolManager>();
        if (found != null)
        {
            Instance = found;
            return Instance;
        }

        GameObject go = new GameObject("ObjectPoolManager");
        Instance = go.AddComponent<ObjectPoolManager>();
        return Instance;
    }

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null) return null;
        var manager = EnsureInstance();
        return manager.SpawnObject(prefab, position, rotation, parent);
    }

    public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
    {
        if (prefab == null) return null;
        GameObject spawned = Spawn(prefab.gameObject, position, rotation, parent);
        return spawned != null ? spawned.GetComponent<T>() : null;
    }

    public static void Return(GameObject instance, float delay = 0f)
    {
        if (instance == null) return;
        var manager = EnsureInstance();
        if (delay <= 0.001f)
        {
            manager.ReturnObject(instance);
        }
        else
        {
            manager.ReturnObject(instance, delay);
        }
    }
}

public interface IPoolable
{
    void OnSpawnedFromPool();
    void OnReturnedToPool();
}

public class PooledObject : MonoBehaviour
{
    [SerializeField] private GameObject _prefabSource;
    private ObjectPoolManager _originManager;
    private bool _isPooled;

    public GameObject PrefabSource
    {
        get => _prefabSource;
        set => _prefabSource = value;
    }

    public ObjectPoolManager OriginManager
    {
        get => _originManager;
        set => _originManager = value;
    }

    public bool IsPooled => _isPooled;

    public void SetPooledState(bool pooled)
    {
        _isPooled = pooled;
    }

    public void ReturnToPool(float delay = 0f)
    {
        if (_originManager != null)
        {
            _originManager.ReturnObject(gameObject, delay);
        }
        else
        {
            ObjectPoolManager.Return(gameObject, delay);
        }
    }
}
