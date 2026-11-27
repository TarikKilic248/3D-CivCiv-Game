using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// CivCiv 3D - Civciv Yumurta Firlatici Yetenegi (ChickEggThrower)
/// Civcivin menzilli olarak zipzip yumurta firlatmasini saglar; bekleme suresi (cooldown),
/// yumurta cephanesi/yenilenme dongusu, firlatma geri tepme prosedurel animasyonu,
/// muzzle VFX ve SettingsUI.PlaySFX ses kancalarini icerir.
/// </summary>
public class ChickEggThrower : MonoBehaviour
{
    private const string VFXRootName = "ThrowerVFX";

    [Header("--- References ---")]
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private Transform _visualRoot;
    [SerializeField] private Transform _throwOrigin;
    [SerializeField] private GameObject _eggPrefab;

    [Header("--- Throw Mechanics ---")]
    [Tooltip("Yumurtanin ileri firlatma hizi")]
    [SerializeField] private float _throwForce = 15.0f;

    [Tooltip("Yumurtanin parabolik yay cizmesi icin yukari itme kuvveti")]
    [SerializeField] private float _upwardArcForce = 4.8f;

    [Tooltip("Yumurta firlatmalari arasi bekleme suresi (saniye)")]
    [SerializeField] private float _throwCooldown = 0.45f;

    [Tooltip("Firlatilan yumurtanin verecegi hasar")]
    [SerializeField] private int _eggDamage = 25;

    [Tooltip("Firlatilan yumurtanin hedefte yapacagi geri tepme kuvveti")]
    [SerializeField] private float _eggKnockback = 6.0f;

    [Header("--- Ammo & Recharge ---")]
    [Tooltip("Tasinabilecek maksimum yumurta cephanesi")]
    [SerializeField] private int _maxAmmo = 5;

    [Tooltip("Mevcut yumurta sayisi")]
    [SerializeField] private int _currentAmmo = 5;

    [Tooltip("Bir yumurtanin otomatik yeniden olusma suresi (saniye)")]
    [SerializeField] private float _rechargeDelay = 2.0f;

    [Tooltip("Sinirsiz yumurta modu")]
    [SerializeField] private bool _infiniteAmmo = false;

    [Header("--- Controls (Input) ---")]
    [SerializeField] private KeyCode _throwKey = KeyCode.Q;

    [Header("--- Procedural Recoil & Motion ---")]
    [Tooltip("Firlatma aninda geri tepme animasyon suresi")]
    [SerializeField] private float _recoilDuration = 0.16f;

    [Tooltip("Geri tepme squash-stretch olcegi (Hafif basik ve genis)")]
    [SerializeField] private Vector3 _recoilSquashScale = new Vector3(1.18f, 0.84f, 0.90f);

    [Tooltip("Geri tepme acisi (derece)")]
    [SerializeField] private float _recoilTiltDegrees = 14f;

    [SerializeField] private float _poseLerpSpeed = 14f;

    [Header("--- VFX & Audio ---")]
    [SerializeField] private ParticleSystem _throwMuzzleVFX;
    [SerializeField] private AudioClip _throwSfx;
    [SerializeField] [Range(0f, 1f)] private float _sfxVolume = 0.85f;

    // Runtime state
    private float _cooldownTimer = 0f;
    private float _rechargeTimer = 0f;
    private float _recoilTimer = 0f;
    private int _thrownCount = 0;
    private int _sfxPlayCount = 0;

    // Visual base pose cache
    private Transform _cachedPoseTarget;
    private Vector3 _baseVisualLocalScale = Vector3.one;
    private Quaternion _baseVisualLocalRotation = Quaternion.identity;

    // Events
    public event Action<EggProjectile> OnEggThrown;
    public event Action<int, int> OnAmmoChanged;
    public event Action OnCooldownReset;

    // Public Properties
    public bool CanThrow => _cooldownTimer <= 0f && (_infiniteAmmo || _currentAmmo > 0) &&
                            (_playerController == null || !_playerController.IsInputBlocked());
    public float Cooldown => _throwCooldown;
    public float CooldownTimer => _cooldownTimer;
    public int CurrentAmmo => _currentAmmo;
    public int MaxAmmo => _maxAmmo;
    public float RechargeDelay => _rechargeDelay;
    public float RechargeTimer => _rechargeTimer;
    public int EggDamage { get => _eggDamage; set => _eggDamage = value; }
    public float ThrowForce { get => _throwForce; set => _throwForce = value; }
    public float UpwardArcForce { get => _upwardArcForce; set => _upwardArcForce = value; }
    public int ThrownCount => _thrownCount;
    public int SfxPlayCount => _sfxPlayCount;
    public ParticleSystem ThrowMuzzleVFX => _throwMuzzleVFX;
    public Transform VisualRoot => _visualRoot;
    public Transform ThrowOrigin => _throwOrigin;
    public GameObject EggPrefab { get => _eggPrefab; set => _eggPrefab = value; }
    public bool InfiniteAmmo { get => _infiniteAmmo; set => _infiniteAmmo = value; }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_playerController == null) _playerController = GetComponent<PlayerController>();
        _maxAmmo = Mathf.Max(1, _maxAmmo);
        _currentAmmo = Mathf.Clamp(_currentAmmo, 0, _maxAmmo);
        AutoFindDefaultAudioClips();
    }

    private void Reset()
    {
        ResolveDependencies();
        EnsureVFXSetup();
        AutoFindDefaultAudioClips();
        _currentAmmo = _maxAmmo;
    }
#endif

    private void Awake()
    {
        ResolveDependencies();
        CacheBasePose();
        EnsureVFXSetup();
        _currentAmmo = _maxAmmo;
        _rechargeTimer = _rechargeDelay;
    }

    private void OnEnable()
    {
        ResolveDependencies();
        CacheBasePose();
    }

    public void ResolveDependencies()
    {
        if (_playerController == null) _playerController = GetComponent<PlayerController>();

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

        if (_throwOrigin == null)
        {
            Transform origin = transform.Find("EggThrowOrigin");
            if (origin == null)
            {
                origin = transform.Find("Muzzle");
            }
            _throwOrigin = origin;
        }
    }

    private void CacheBasePose()
    {
        if (_visualRoot == null || _visualRoot == transform) return;
        if (_cachedPoseTarget == _visualRoot) return;

        _cachedPoseTarget = _visualRoot;
        _baseVisualLocalRotation = _visualRoot.localRotation;
        _baseVisualLocalScale = _visualRoot.localScale;
        if (_baseVisualLocalScale.sqrMagnitude < 0.0001f)
        {
            _baseVisualLocalScale = Vector3.one;
        }
    }

    private void Update()
    {
        HandleInput();
        Step(Time.deltaTime > 0f ? Time.deltaTime : 0.02f);
    }

    private void HandleInput()
    {
        if (_playerController != null && _playerController.IsInputBlocked()) return;

        bool throwRequested = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.qKey.wasPressedThisFrame) throwRequested = true;
        }
        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.rightButton.wasPressedThisFrame) throwRequested = true;
        }
#endif

        try
        {
            if (!throwRequested && (Input.GetKeyDown(_throwKey) || Input.GetMouseButtonDown(1)))
            {
                throwRequested = true;
            }
        }
        catch
        {
            // Input System fallback
        }

        if (throwRequested && CanThrow)
        {
            TryThrowEgg();
        }
    }

    /// <summary>
    /// Cooldown, cephane yenilenmesi ve prosedurel geri tepme zamanlayicilarini ilerletir.
    /// </summary>
    public void Step(float deltaTime)
    {
        // 1. Cooldown zamanlayicisi
        if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= deltaTime;
            if (_cooldownTimer <= 0f)
            {
                _cooldownTimer = 0f;
                OnCooldownReset?.Invoke();
            }
        }

        // 2. Cephane otomatik yenilenmesi
        if (!_infiniteAmmo && _currentAmmo < _maxAmmo)
        {
            _rechargeTimer -= deltaTime;
            if (_rechargeTimer <= 0f)
            {
                _currentAmmo++;
                _rechargeTimer = _rechargeDelay;
                OnAmmoChanged?.Invoke(_currentAmmo, _maxAmmo);
            }
        }

        // 3. Prosedurel animasyon
        StepFeedback(deltaTime);
    }

    public void StepFeedback(float deltaTime)
    {
        if (_visualRoot == null || _visualRoot == transform) return;
        CacheBasePose();

        if (_recoilTimer > 0f)
        {
            _recoilTimer -= deltaTime;
            float progress = Mathf.Clamp01(_recoilTimer / Mathf.Max(0.01f, _recoilDuration));
            Vector3 targetScale = Vector3.Scale(_baseVisualLocalScale, Vector3.Lerp(Vector3.one, _recoilSquashScale, progress));
            Quaternion recoilTilt = Quaternion.Euler(-_recoilTiltDegrees * progress, 0f, 0f);
            Quaternion targetRot = _baseVisualLocalRotation * recoilTilt;

            _visualRoot.localScale = targetScale;
            _visualRoot.localRotation = targetRot;
        }
        else
        {
            _visualRoot.localScale = Vector3.Lerp(_visualRoot.localScale, _baseVisualLocalScale, deltaTime * _poseLerpSpeed);
            _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, _baseVisualLocalRotation, deltaTime * _poseLerpSpeed);
        }
    }

    /// <summary>
    /// Yumurta firlatmayi dener. Cooldown veya cephane yetersizse null doner.
    /// </summary>
    public EggProjectile TryThrowEgg()
    {
        if (!CanThrow) return null;

        // Cephane dusur
        if (!_infiniteAmmo)
        {
            _currentAmmo--;
            OnAmmoChanged?.Invoke(_currentAmmo, _maxAmmo);
            if (_rechargeTimer <= 0f)
            {
                _rechargeTimer = _rechargeDelay;
            }
        }

        // Cooldown baslat
        _cooldownTimer = _throwCooldown;

        // Firlatma konumu ve yonu
        Vector3 spawnPos = _throwOrigin != null
            ? _throwOrigin.position
            : transform.position + transform.forward * 0.7f + Vector3.up * 0.6f;

        Vector3 forwardDir = transform.forward;
        if (forwardDir.sqrMagnitude < 0.001f) forwardDir = Vector3.forward;

        Vector3 launchVelocity = forwardDir.normalized * _throwForce + Vector3.up * _upwardArcForce;

        // EggProjectile nesnesini olustur
        EggProjectile egg = CreateEggInstance(spawnPos, Quaternion.LookRotation(forwardDir));
        if (egg != null)
        {
            Collider shooterCol = GetComponentInChildren<Collider>();
            egg.Launch(launchVelocity, shooterCol, _eggDamage);
            egg.KnockbackForce = _eggKnockback;
        }

        // Prosedurel geri tepme
        _recoilTimer = Mathf.Max(0.08f, _recoilDuration);

        // Muzzle VFX
        if (_throwMuzzleVFX != null)
        {
            _throwMuzzleVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _throwMuzzleVFX.Play(true);
        }

        // SFX
        if (_throwSfx != null)
        {
            SettingsUI.PlaySFX(_throwSfx, _sfxVolume);
            _sfxPlayCount++;
        }

        _thrownCount++;
        OnEggThrown?.Invoke(egg);

        return egg;
    }

    private EggProjectile CreateEggInstance(Vector3 position, Quaternion rotation)
    {
        GameObject eggObj;
        if (_eggPrefab != null)
        {
            eggObj = Instantiate(_eggPrefab, position, rotation);
        }
        else
        {
            eggObj = new GameObject("EggProjectile");
            eggObj.transform.position = position;
            eggObj.transform.rotation = rotation;
        }

        EggProjectile egg = eggObj.GetComponent<EggProjectile>();
        if (egg == null)
        {
            egg = eggObj.AddComponent<EggProjectile>();
        }

        egg.EnsureVFXSetup();
        return egg;
    }

    public void RefillAmmo(int amount = -1)
    {
        _currentAmmo = amount < 0 ? _maxAmmo : Mathf.Min(_maxAmmo, _currentAmmo + amount);
        OnAmmoChanged?.Invoke(_currentAmmo, _maxAmmo);
    }

    public void SetAmmo(int ammo)
    {
        _currentAmmo = Mathf.Clamp(ammo, 0, _maxAmmo);
        OnAmmoChanged?.Invoke(_currentAmmo, _maxAmmo);
    }

    public bool EnsureVFXSetup()
    {
        bool createdAny = false;
        ResolveDependencies();

        // 1. Throw Origin
        if (_throwOrigin == null)
        {
            Transform existingOrigin = transform.Find("EggThrowOrigin");
            if (existingOrigin == null)
            {
                GameObject originObj = new GameObject("EggThrowOrigin");
                originObj.transform.SetParent(transform, false);
                originObj.transform.localPosition = new Vector3(0f, 0.55f, 0.65f);
                _throwOrigin = originObj.transform;
                createdAny = true;
            }
            else
            {
                _throwOrigin = existingOrigin;
            }
        }

        // 2. Muzzle VFX (Tüy ve toz pufu)
        if (_throwMuzzleVFX == null)
        {
            Transform vfxRoot = transform.Find(VFXRootName);
            if (vfxRoot == null)
            {
                GameObject rootObj = new GameObject(VFXRootName);
                rootObj.transform.SetParent(transform, false);
                vfxRoot = rootObj.transform;
                createdAny = true;
            }

            Transform existingPs = vfxRoot.Find("EggThrowMuzzleVFX");
            GameObject psObj = existingPs != null ? existingPs.gameObject : new GameObject("EggThrowMuzzleVFX");
            if (existingPs == null)
            {
                psObj.transform.SetParent(vfxRoot, false);
                psObj.transform.localPosition = _throwOrigin != null ? _throwOrigin.localPosition : new Vector3(0f, 0.55f, 0.65f);
                createdAny = true;
            }

            _throwMuzzleVFX = psObj.GetComponent<ParticleSystem>();
            if (_throwMuzzleVFX == null)
            {
                _throwMuzzleVFX = psObj.AddComponent<ParticleSystem>();
                createdAny = true;
            }

            ConfigureMuzzleParticles(_throwMuzzleVFX);
        }

#if UNITY_EDITOR
        AutoFindDefaultAudioClips();
#endif

        return createdAny;
    }

    private static void ConfigureMuzzleParticles(ParticleSystem ps)
    {
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.25f;
        main.startLifetime = 0.28f;
        main.startSpeed = 2.8f;
        main.startSize = 0.16f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.92f, 0.45f, 0.95f),
            new Color(1f, 0.58f, 0.14f, 0.85f)
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 22f;
        shape.radius = 0.12f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                renderer.sharedMaterial = new Material(shader) { name = "EggMuzzle_Mat" };
            }
        }
    }

    private void AutoFindDefaultAudioClips()
    {
#if UNITY_EDITOR
        if (_throwSfx != null) return;
        string[] guids = AssetDatabase.FindAssets("t:AudioClip");
        if (guids == null || guids.Length == 0) return;
        AudioClip fallback = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guids[0]));
        if (fallback != null) _throwSfx = fallback;
#endif
    }
}
