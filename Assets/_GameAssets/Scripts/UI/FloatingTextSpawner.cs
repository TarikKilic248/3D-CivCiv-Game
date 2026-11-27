using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// CivCiv 3D - Uçan Geri Bildirim Yazıları Yöneticisi (FloatingTextSpawner)
/// Altın toplama, hasar alma/verme ve kombo olaylarına abone olarak dünya uzayında süzülen yazılar üretir.
/// Dahili havuz (pool) ile GameObject'leri yeniden kullanır, çöp üretmez.
/// </summary>
public class FloatingTextSpawner : MonoBehaviour
{
    public static FloatingTextSpawner Instance { get; private set; }

    [Header("--- Theme & Prefab ---")]
    [SerializeField] private UIThemeSO _theme;
    [SerializeField] private GameObject _textPrefab;
    [SerializeField] private AudioClip _comboPopClip;

    [Header("--- Animation Tuning ---")]
    [SerializeField] private float _defaultLifetime = 1.0f;
    [SerializeField] private float _floatSpeed = 1.5f;

    [Header("--- Event Sources ---")]
    [SerializeField] private CoinManager _coinManager;
    [SerializeField] private PlayerHealth _playerHealth;
    [SerializeField] private ComboManager _comboManager;
    [SerializeField] private Transform _playerTransform;

    private readonly List<FloatingText> _pool = new List<FloatingText>();
    private readonly Dictionary<PatrolEnemyAI, Action<int, Vector3>> _enemyHandlers = new Dictionary<PatrolEnemyAI, Action<int, Vector3>>();
    private readonly Dictionary<BreakableCrate, Action<int, Vector3>> _crateHandlers = new Dictionary<BreakableCrate, Action<int, Vector3>>();

    private int _lastPlayerHealth = -1;
    private int _lastComboStreak = 0;

    public int PoolCount => _pool.Count;
    public IReadOnlyList<FloatingText> Pool => _pool;
    public UIThemeSO Theme { get => _theme; set => _theme = value; }
    public AudioClip ComboPopClip { get => _comboPopClip; set => _comboPopClip = value; }
    public float DefaultLifetime { get => _defaultLifetime; set => _defaultLifetime = value; }
    public float FloatSpeed { get => _floatSpeed; set => _floatSpeed = value; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            return;
        }
        Instance = this;

        ResolveDependencies();
    }

    private void Start()
    {
        SubscribeEvents();
    }

    private void OnEnable()
    {
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        UnsubscribeEvents();
    }

    public void ResolveDependencies()
    {
        if (_coinManager == null)
        {
            _coinManager = CoinManager.Instance != null
                ? CoinManager.Instance
                : FindAnyObjectByType<CoinManager>();
        }

        if (_playerHealth == null)
        {
            _playerHealth = FindAnyObjectByType<PlayerHealth>();
        }

        if (_comboManager == null)
        {
            _comboManager = ComboManager.Instance != null
                ? ComboManager.Instance
                : FindAnyObjectByType<ComboManager>();
        }

        if (_playerTransform == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null)
            {
                _playerTransform = pc.transform;
            }
            else if (_playerHealth != null)
            {
                _playerTransform = _playerHealth.transform;
            }
        }

        if (_theme == null)
        {
            _theme = Resources.Load<UIThemeSO>("DefaultUITheme");
#if UNITY_EDITOR
            if (_theme == null)
            {
                _theme = UnityEditor.AssetDatabase.LoadAssetAtPath<UIThemeSO>(UIThemeSO.DefaultAssetPath);
            }
#endif
        }
    }

    public void BindTargets(CoinManager coinManager, PlayerHealth playerHealth, ComboManager comboManager, Transform playerTransform = null)
    {
        UnsubscribeEvents();
        _coinManager = coinManager;
        _playerHealth = playerHealth;
        _comboManager = comboManager;
        if (playerTransform != null)
        {
            _playerTransform = playerTransform;
        }
        SubscribeEvents();
    }

    public void SubscribeEvents()
    {
        ResolveDependencies();

        if (_coinManager != null)
        {
            _coinManager.CoinsCollected -= HandleCoinsCollected;
            _coinManager.CoinsCollected += HandleCoinsCollected;
        }

        if (_playerHealth != null)
        {
            _lastPlayerHealth = _playerHealth.CurrentHealth;
            _playerHealth.OnHealthChanged -= HandlePlayerHealthChanged;
            _playerHealth.OnHealthChanged += HandlePlayerHealthChanged;
        }

        if (_comboManager != null)
        {
            _lastComboStreak = _comboManager.CurrentStreak;
            _comboManager.OnComboUpdated -= HandleComboUpdated;
            _comboManager.OnComboUpdated += HandleComboUpdated;
        }

        var enemies = FindObjectsByType<PatrolEnemyAI>(FindObjectsInactive.Include);
        for (int i = 0; i < enemies.Length; i++)
        {
            RegisterEnemy(enemies[i]);
        }

        var crates = FindObjectsByType<BreakableCrate>(FindObjectsInactive.Include);
        for (int i = 0; i < crates.Length; i++)
        {
            RegisterCrate(crates[i]);
        }
    }

    public void UnsubscribeEvents()
    {
        if (_coinManager != null)
        {
            _coinManager.CoinsCollected -= HandleCoinsCollected;
        }

        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged -= HandlePlayerHealthChanged;
        }

        if (_comboManager != null)
        {
            _comboManager.OnComboUpdated -= HandleComboUpdated;
        }

        foreach (var pair in _enemyHandlers)
        {
            if (pair.Key != null && pair.Value != null)
            {
                pair.Key.OnHit -= pair.Value;
            }
        }
        _enemyHandlers.Clear();

        foreach (var pair in _crateHandlers)
        {
            if (pair.Key != null && pair.Value != null)
            {
                pair.Key.OnHit -= pair.Value;
            }
        }
        _crateHandlers.Clear();
    }

    public void RegisterEnemy(PatrolEnemyAI enemy)
    {
        if (enemy == null || _enemyHandlers.ContainsKey(enemy)) return;

        Action<int, Vector3> handler = (damage, dir) =>
        {
            if (enemy != null && damage > 0)
            {
                Spawn($"-{damage}", enemy.transform.position + Vector3.up * 1.2f, FloatingTextKind.Damage);
            }
        };

        _enemyHandlers[enemy] = handler;
        enemy.OnHit += handler;
    }

    public void RegisterCrate(BreakableCrate crate)
    {
        if (crate == null || _crateHandlers.ContainsKey(crate)) return;

        Action<int, Vector3> handler = (damage, dir) =>
        {
            if (crate != null && damage > 0)
            {
                Spawn($"-{damage}", crate.transform.position + Vector3.up * 1.0f, FloatingTextKind.Damage);
            }
        };

        _crateHandlers[crate] = handler;
        crate.OnHit += handler;
    }

    private void HandleCoinsCollected(int amount)
    {
        if (amount <= 0) return;
        Vector3 spawnPos = GetPlayerPosition() + Vector3.up * 1.5f + UnityEngine.Random.insideUnitSphere * 0.2f;
        Spawn($"+{amount}", spawnPos, FloatingTextKind.Gold);
    }

    private void HandlePlayerHealthChanged(int current, int max)
    {
        if (_lastPlayerHealth >= 0 && current < _lastPlayerHealth)
        {
            int damage = _lastPlayerHealth - current;
            Vector3 spawnPos = GetPlayerPosition() + Vector3.up * 1.8f;
            Spawn($"-{damage}", spawnPos, FloatingTextKind.Damage);
        }
        _lastPlayerHealth = current;
    }

    private void HandleComboUpdated(int streak, float multiplier, float normalizedTime)
    {
        if (streak > 1 && streak > _lastComboStreak)
        {
            Vector3 spawnPos = GetPlayerPosition() + Vector3.up * 2.2f;
            Spawn($"x{multiplier:0.#} KOMBO!", spawnPos, FloatingTextKind.Combo);
        }
        else if (streak == 0)
        {
            _lastComboStreak = 0;
            return;
        }

        _lastComboStreak = streak;
    }

    public FloatingText Spawn(string text, Vector3 worldPos, FloatingTextKind kind)
    {
        FloatingText target = null;
        for (int i = 0; i < _pool.Count; i++)
        {
            if (_pool[i] != null && !_pool[i].gameObject.activeSelf)
            {
                target = _pool[i];
                break;
            }
        }

        if (target == null)
        {
            target = CreateFloatingTextInstance();
            _pool.Add(target);
        }

        target.Initialize(text, worldPos, kind, _theme, _comboPopClip, _defaultLifetime, _floatSpeed);
        return target;
    }

    private FloatingText CreateFloatingTextInstance()
    {
        GameObject go;
        if (_textPrefab != null)
        {
            go = Instantiate(_textPrefab, transform);
        }
        else
        {
            go = new GameObject($"FloatingText_{_pool.Count + 1}");
            go.transform.SetParent(transform);
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 5.5f;
        }

        FloatingText ft = go.GetComponent<FloatingText>();
        if (ft == null)
        {
            ft = go.AddComponent<FloatingText>();
        }

        go.SetActive(false);
        return ft;
    }

    public Vector3 GetPlayerPosition()
    {
        if (_playerTransform != null)
        {
            return _playerTransform.position;
        }

        if (_playerHealth != null)
        {
            return _playerHealth.transform.position;
        }

        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null)
        {
            _playerTransform = pc.transform;
            return _playerTransform.position;
        }

        return transform.position;
    }
}
