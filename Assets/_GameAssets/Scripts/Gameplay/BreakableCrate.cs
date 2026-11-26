using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// CivCiv 3D - Kırılabilir Sandık Bileşeni (BreakableCrate)
/// IDamageable uygulayarak gaga vuruşu, havadan yere çakılma veya Dash çarpması ile
/// hasar alır, kırılır ve LootTableSO üzerinden ganimet/altın üretir.
/// </summary>
public class BreakableCrate : MonoBehaviour, IDamageable
{
    private const string VFXRootName = "CrateVFX";

    [Header("--- Health & Defense ---")]
    [Tooltip("Sandığın maksimum dayanıklılığı")]
    [SerializeField] private int _maxHealth = 25;

    [Tooltip("Mevcut can değeri")]
    [SerializeField] private int _currentHealth = 25;

    [Tooltip("Sandık geçici olarak dokunulmaz mı")]
    [SerializeField] private bool _isInvincible = false;

    [Header("--- Loot & Rewards ---")]
    [Tooltip("Ağırlıklı ganimet seçim tablosu (ScriptableObject)")]
    [SerializeField] private LootTableSO _lootTable;

    [Tooltip("Ganimet tablosu boşsa veya doğrudan ödül için altın prefabı")]
    [SerializeField] private GameObject _coinPrefab;

    [Tooltip("Doğrudan verilecek yedek altın miktarı")]
    [SerializeField] private int _directCoinReward = 3;

    [Tooltip("Ganimet fırlatma yatay kuvveti")]
    [SerializeField] private float _dropEjectionForce = 2.5f;

    [Tooltip("Ganimet fırlatma dikey zıplama kuvveti")]
    [SerializeField] private float _dropUpwardForce = 4.5f;

    [Header("--- Dash Interaction ---")]
    [Tooltip("Karakter Dash yaparken çarparsa uygulanacak hasar miktarı")]
    [SerializeField] private int _dashImpactDamage = 50;

    [Tooltip("Dash ile çarpmada anında kırılmayı tetikle")]
    [SerializeField] private bool _canBreakOnDash = true;

    [Header("--- Visual & Procedural Feedback ---")]
    [SerializeField] private Transform _visualRoot;
    [SerializeField] private float _hitShakeDuration = 0.18f;
    [SerializeField] private float _hitShakeIntensity = 0.12f;
    [SerializeField] private Vector3 _hitSquashScale = new Vector3(1.22f, 0.78f, 1.22f);
    [SerializeField] private float _poseLerpSpeed = 12f;

    [Header("--- Particle Systems (VFX) ---")]
    [SerializeField] private ParticleSystem _hitVFX;
    [SerializeField] private ParticleSystem _breakVFX;

    [Header("--- Audio (SFX) ---")]
    [SerializeField] private AudioClip _hitSfx;
    [SerializeField] private AudioClip _breakSfx;
    [SerializeField] [Range(0f, 1f)] private float _sfxVolume = 0.85f;

    // Runtime state
    private bool _isBroken = false;
    private int _spawnedDropCount = 0;
    private float _shakeTimer = 0f;
    private Vector3 _baseVisualLocalPos = Vector3.zero;
    private Vector3 _baseVisualLocalScale = Vector3.one;
    private Quaternion _baseVisualLocalRot = Quaternion.identity;
    private Transform _cachedPoseTarget;
    private Collider[] _cachedColliders;

    // Events
    public event Action<BreakableCrate> OnBroken;
    public event Action<int, Vector3> OnHit;
    public event Action<int, int> OnHealthChanged;

    // IDamageable implementation
    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _maxHealth;
    public bool IsAlive => !_isBroken && _currentHealth > 0;
    public bool IsInvincible => _isInvincible;

    // Public properties
    public bool IsBroken => _isBroken;
    public int SpawnedDropCount => _spawnedDropCount;
    public LootTableSO LootTable { get => _lootTable; set => _lootTable = value; }
    public GameObject CoinPrefab { get => _coinPrefab; set => _coinPrefab = value; }
    public int DirectCoinReward { get => _directCoinReward; set => _directCoinReward = value; }
    public int DashImpactDamage { get => _dashImpactDamage; set => _dashImpactDamage = value; }
    public ParticleSystem HitVFX => _hitVFX;
    public ParticleSystem BreakVFX => _breakVFX;
    public Transform VisualRoot => _visualRoot;

#if UNITY_EDITOR
    private void OnValidate()
    {
        _maxHealth = Mathf.Max(1, _maxHealth);
        if (_currentHealth <= 0 && !_isBroken)
        {
            _currentHealth = _maxHealth;
        }
        AutoFindDefaultAudioClips();
    }

    private void Reset()
    {
        _maxHealth = 25;
        _currentHealth = 25;
        ResolveDependencies();
        EnsureVFXSetup();
        AutoFindDefaultAudioClips();
    }
#endif

    private void Awake()
    {
        ResolveDependencies();
        CacheBasePose();
        EnsureVFXSetup();
        if (_currentHealth <= 0)
        {
            _currentHealth = _maxHealth;
        }
    }

    public void ResolveDependencies()
    {
        if (_visualRoot == null)
        {
            var visual = transform.Find("VisualModel");
            if (visual == null) visual = transform.Find("Visual");
            if (visual == null) visual = transform.Find("Mesh");
            if (visual == null && transform.childCount > 0)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i);
                    if (child.name != VFXRootName && child.GetComponent<Renderer>() != null)
                    {
                        visual = child;
                        break;
                    }
                }
            }
            if (visual == null) visual = transform;
            _visualRoot = visual;
        }

        _cachedColliders = GetComponentsInChildren<Collider>();
    }

    private void CacheBasePose()
    {
        if (_visualRoot == null) return;
        if (_cachedPoseTarget == _visualRoot) return;

        _cachedPoseTarget = _visualRoot;
        _baseVisualLocalPos = _visualRoot.localPosition;
        _baseVisualLocalRot = _visualRoot.localRotation;
        _baseVisualLocalScale = _visualRoot.localScale;
        if (_baseVisualLocalScale.sqrMagnitude < 0.0001f)
        {
            _baseVisualLocalScale = Vector3.one;
        }
    }

    private void Update()
    {
        StepFeedback(Time.deltaTime > 0f ? Time.deltaTime : 0.02f);
    }

    public void StepFeedback(float deltaTime)
    {
        if (_visualRoot == null || _visualRoot == transform) return;
        if (_isBroken) return;

        if (_shakeTimer > 0f)
        {
            _shakeTimer -= deltaTime;
            float ratio = Mathf.Clamp01(_shakeTimer / Mathf.Max(0.01f, _hitShakeDuration));
            Vector3 shakeOffset = UnityEngine.Random.insideUnitSphere * (_hitShakeIntensity * ratio);
            shakeOffset.y = Mathf.Abs(shakeOffset.y);
            _visualRoot.localPosition = _baseVisualLocalPos + shakeOffset;
        }
        else
        {
            _visualRoot.localPosition = Vector3.Lerp(_visualRoot.localPosition, _baseVisualLocalPos, deltaTime * _poseLerpSpeed);
        }

        _visualRoot.localScale = Vector3.Lerp(_visualRoot.localScale, _baseVisualLocalScale, deltaTime * _poseLerpSpeed);
        _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, _baseVisualLocalRot, deltaTime * _poseLerpSpeed);
    }

    public void ResetVisualPoseImmediate()
    {
        if (_visualRoot == null || _visualRoot == transform) return;
        _visualRoot.localPosition = _baseVisualLocalPos;
        _visualRoot.localRotation = _baseVisualLocalRot;
        _visualRoot.localScale = _baseVisualLocalScale;
        _shakeTimer = 0f;
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, Vector3.zero);
    }

    public void TakeDamage(int damage, Vector3 hitDirection)
    {
        if (_isBroken || _isInvincible || damage <= 0) return;

        _currentHealth = Mathf.Max(0, _currentHealth - damage);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        OnHit?.Invoke(damage, hitDirection);

        // Feedback
        _shakeTimer = Mathf.Max(0.05f, _hitShakeDuration);
        if (_visualRoot != null && _visualRoot != transform)
        {
            _visualRoot.localScale = Vector3.Scale(_baseVisualLocalScale, _hitSquashScale);
        }

        if (_hitVFX != null)
        {
            _hitVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _hitVFX.Play(true);
        }

        SettingsUI.PlaySFX(_hitSfx, _sfxVolume);

        if (_currentHealth <= 0)
        {
            Break(hitDirection);
        }
    }

    public void Heal(int amount)
    {
        if (_isBroken || amount <= 0) return;
        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    public void Break(Vector3 hitDirection = default)
    {
        if (_isBroken) return;

        _isBroken = true;
        _currentHealth = 0;

        // VFX & SFX
        EnsureVFXSetup();
        if (_breakVFX != null)
        {
            _breakVFX.transform.SetParent(null);
            _breakVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _breakVFX.Play(true);
            if (Application.isPlaying)
            {
                Destroy(_breakVFX.gameObject, 3f);
            }
        }

        SettingsUI.PlaySFX(_breakSfx, _sfxVolume);

        // Ganimet / Altın üretimi
        SpawnLootDrops(hitDirection);

        // Fizik ve görseli devre dışı bırak
        DisableColliders();
        HideVisual();

        OnBroken?.Invoke(this);

        if (Application.isPlaying)
        {
            Destroy(gameObject, 0.5f);
        }
    }

    private void SpawnLootDrops(Vector3 hitDirection)
    {
        int dropsProduced = 0;

        if (_lootTable != null)
        {
            LootTableSO.LootEntry entry = _lootTable.RollLoot();
            if (entry != null)
            {
                int count = entry.RollCount();
                if (entry.prefab != null)
                {
                    for (int i = 0; i < count; i++)
                    {
                        SpawnDropItem(entry.prefab, hitDirection);
                        dropsProduced++;
                    }
                }

                if (entry.directCoinAmount > 0)
                {
                    AwardDirectCoins(entry.directCoinAmount);
                    dropsProduced += entry.directCoinAmount;
                }
            }
        }

        // Yedek / Doğrudan ödül
        if (dropsProduced == 0)
        {
            if (_coinPrefab != null)
            {
                int coinCount = Mathf.Max(1, _directCoinReward);
                for (int i = 0; i < coinCount; i++)
                {
                    SpawnDropItem(_coinPrefab, hitDirection);
                    dropsProduced++;
                }
            }
            else if (_directCoinReward > 0)
            {
                AwardDirectCoins(_directCoinReward);
                dropsProduced += _directCoinReward;
            }
        }

        _spawnedDropCount = dropsProduced;
    }

    private GameObject SpawnDropItem(GameObject prefab, Vector3 impulseDirection)
    {
        if (prefab == null) return null;

        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
        Vector3 jitter = new Vector3(
            UnityEngine.Random.Range(-0.3f, 0.3f),
            UnityEngine.Random.Range(0f, 0.25f),
            UnityEngine.Random.Range(-0.3f, 0.3f)
        );

        GameObject item = Instantiate(prefab, spawnPos + jitter, Quaternion.identity);

        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 push = impulseDirection.sqrMagnitude > 0.01f ? impulseDirection.normalized : UnityEngine.Random.insideUnitSphere;
            push.y = 0f;
            Vector3 force = Vector3.up * _dropUpwardForce + push.normalized * _dropEjectionForce;
            rb.AddForce(force, ForceMode.Impulse);
        }

        return item;
    }

    private void AwardDirectCoins(int amount)
    {
        if (amount <= 0) return;
        CoinManager coinManager = CoinManager.Instance != null
            ? CoinManager.Instance
            : FindAnyObjectByType<CoinManager>();

        if (coinManager != null)
        {
            coinManager.AddCoins(amount);
        }
    }

    private void DisableColliders()
    {
        if (_cachedColliders == null || _cachedColliders.Length == 0)
        {
            _cachedColliders = GetComponentsInChildren<Collider>();
        }
        for (int i = 0; i < _cachedColliders.Length; i++)
        {
            if (_cachedColliders[i] != null)
            {
                _cachedColliders[i].enabled = false;
            }
        }
    }

    private void HideVisual()
    {
        if (_visualRoot != null && _visualRoot != transform)
        {
            _visualRoot.gameObject.SetActive(false);
        }
        else
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].GetComponent<ParticleSystem>() == null)
                {
                    renderers[i].enabled = false;
                }
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleImpact(collision.collider, collision.contacts.Length > 0 ? -collision.contacts[0].normal : transform.forward);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleImpact(other, (transform.position - other.transform.position).normalized);
    }

    private void HandleImpact(Collider sourceCollider, Vector3 direction)
    {
        if (_isBroken || !_canBreakOnDash || sourceCollider == null) return;

        DashController dash = sourceCollider.GetComponentInParent<DashController>();
        if (dash != null && dash.IsDashing)
        {
            TakeDamage(_dashImpactDamage, direction);
        }
    }

    public bool EnsureVFXSetup()
    {
        bool createdAny = false;
        Transform vfxRoot = transform.Find(VFXRootName);
        if (vfxRoot == null)
        {
            var rootObj = new GameObject(VFXRootName);
            rootObj.transform.SetParent(transform, false);
            vfxRoot = rootObj.transform;
            createdAny = true;
        }

        Material sharedMat = CreateDefaultEffectMaterial();

        if (_hitVFX == null)
        {
            Transform existing = vfxRoot.Find("HitVFX");
            GameObject hitObj = existing != null ? existing.gameObject : new GameObject("HitVFX");
            if (existing == null)
            {
                hitObj.transform.SetParent(vfxRoot, false);
                hitObj.transform.localPosition = new Vector3(0f, 0.4f, 0f);
                createdAny = true;
            }

            _hitVFX = hitObj.GetComponent<ParticleSystem>();
            if (_hitVFX == null)
            {
                _hitVFX = hitObj.AddComponent<ParticleSystem>();
                createdAny = true;
            }
            ConfigureHitParticles(_hitVFX, sharedMat);
        }

        if (_breakVFX == null)
        {
            Transform existing = vfxRoot.Find("BreakVFX");
            GameObject breakObj = existing != null ? existing.gameObject : new GameObject("BreakVFX");
            if (existing == null)
            {
                breakObj.transform.SetParent(vfxRoot, false);
                breakObj.transform.localPosition = new Vector3(0f, 0.4f, 0f);
                createdAny = true;
            }

            _breakVFX = breakObj.GetComponent<ParticleSystem>();
            if (_breakVFX == null)
            {
                _breakVFX = breakObj.AddComponent<ParticleSystem>();
                createdAny = true;
            }
            ConfigureBreakParticles(_breakVFX, sharedMat);
        }

#if UNITY_EDITOR
        AutoFindDefaultAudioClips();
#endif

        return createdAny;
    }

    private static Material CreateDefaultEffectMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        return shader != null ? new Material(shader) : null;
    }

    private static void ConfigureHitParticles(ParticleSystem ps, Material mat)
    {
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.25f;
        main.startLifetime = 0.2f;
        main.startSpeed = 2.8f;
        main.startSize = 0.14f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.84f, 0.31f, 1f),
            new Color(0.72f, 0.43f, 0.15f, 1f)
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && mat != null) renderer.sharedMaterial = mat;
    }

    private static void ConfigureBreakParticles(ParticleSystem ps, Material mat)
    {
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.45f;
        main.startLifetime = 0.38f;
        main.startSpeed = 4.2f;
        main.startSize = 0.22f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.88f, 0.35f, 1f),
            new Color(0.55f, 0.28f, 0.05f, 1f)
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.6f, 0.6f, 0.6f);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && mat != null) renderer.sharedMaterial = mat;
    }

    private void AutoFindDefaultAudioClips()
    {
#if UNITY_EDITOR
        if (_hitSfx != null && _breakSfx != null) return;
        string[] guids = AssetDatabase.FindAssets("t:AudioClip");
        if (guids == null || guids.Length == 0) return;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string lower = path.ToLowerInvariant();
            if (_breakSfx == null && (lower.Contains("break") || lower.Contains("impact") || lower.Contains("pound") || lower.Contains("hit")))
            {
                _breakSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            else if (_hitSfx == null && (lower.Contains("hit") || lower.Contains("peck") || lower.Contains("punch") || lower.Contains("wood")))
            {
                _hitSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
        }

        if (_breakSfx == null && guids.Length > 0)
        {
            _breakSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        if (_hitSfx == null)
        {
            _hitSfx = _breakSfx;
        }
#endif
    }
}
