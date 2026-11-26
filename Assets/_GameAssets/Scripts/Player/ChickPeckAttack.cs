using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(PlayerController))]
public class ChickPeckAttack : MonoBehaviour
{
    private const string VFXRootName = "ChickAttackVFX";

    [Header("--- References ---")]
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private Rigidbody _playerRigidbody;
    [SerializeField] private Animator _animator;
    [SerializeField] private Transform _visualRoot;

    [Header("--- Peck Attack Settings ---")]
    [Tooltip("Gaga vurusunun verecegi hasar miktari")]
    [SerializeField] private int _peckDamage = 25;
    [Tooltip("Gaga vurusu on menzili")]
    [SerializeField] private float _peckRange = 1.8f;
    [Tooltip("Gaga vurusu algilama kuresi yaricapi")]
    [SerializeField] private float _peckRadius = 0.9f;
    [Tooltip("Gaga vuruslari arasi bekleme suresi (saniye)")]
    [SerializeField] private float _peckCooldown = 0.35f;
    [Tooltip("Gaga vurusu tusu (Eski Input)")]
    [SerializeField] private KeyCode _peckKey = KeyCode.F;

    [Header("--- Ground Pound Settings ---")]
    [Tooltip("Havadan yere cakilma alan hasari")]
    [SerializeField] private int _groundPoundDamage = 50;
    [Tooltip("Yere cakilma etki alani yaricapi")]
    [SerializeField] private float _groundPoundRadius = 3.5f;
    [Tooltip("Yere inis sirasinda uygulanan negatif dikey hiz")]
    [SerializeField] private float _groundPoundDownwardSpeed = 25f;
    [Tooltip("Yere cakilma bekleme suresi (saniye)")]
    [SerializeField] private float _groundPoundCooldown = 1.0f;
    [Tooltip("Yere cakilma tusu (Eski Input)")]
    [SerializeField] private KeyCode _groundPoundKey = KeyCode.LeftControl;

    [Header("--- Target Filtering ---")]
    [Tooltip("Hasar verilecek hedeflerin katmanlari")]
    [SerializeField] private LayerMask _targetLayers = ~0;

    [Header("--- VFX Components ---")]
    [SerializeField] private ParticleSystem _peckVFX;
    [SerializeField] private ParticleSystem _groundPoundImpactVFX;

    [Header("--- Audio (SFX) ---")]
    [SerializeField] private AudioClip _peckSfx;
    [SerializeField] private AudioClip _groundPoundStartSfx;
    [SerializeField] private AudioClip _groundPoundImpactSfx;
    [SerializeField][Range(0f, 1f)] private float _sfxVolume = 0.85f;

    [Header("--- Procedural Motion Settings ---")]
    [SerializeField] private float _peckTiltDegrees = 24f;
    [SerializeField] private Vector3 _peckStretchScale = new Vector3(0.85f, 0.85f, 1.3f);
    [SerializeField] private float _peckMotionDuration = 0.18f;
    [SerializeField] private Vector3 _poundPlungeScale = new Vector3(0.82f, 1.28f, 0.82f);
    [SerializeField] private Vector3 _poundImpactScale = new Vector3(1.36f, 0.64f, 1.36f);
    [SerializeField] private float _impactMotionDuration = 0.22f;
    [SerializeField] private float _poseLerpSpeed = 14f;

    // Runtime state
    private float _peckCooldownTimer;
    private float _groundPoundCooldownTimer;
    private bool _isGroundPounding;
    private float _peckAnimTimer;
    private float _impactAnimTimer;

    // Visual base pose
    private Transform _cachedPoseTarget;
    private Quaternion _baseVisualLocalRotation = Quaternion.identity;
    private Vector3 _baseVisualLocalScale = Vector3.one;

    // Telemetry & events
    private int _peckExecutionCount;
    private int _peckHitCount;
    private int _groundPoundStartCount;
    private int _groundPoundImpactCount;

    public event Action OnPeckExecuted;
    public event Action<int> OnPeckHit;
    public event Action OnGroundPoundStarted;
    public event Action<int> OnGroundPoundImpacted;

    // Animator cache
    private static readonly int HashPeck = Animator.StringToHash("Peck");
    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashGroundPound = Animator.StringToHash("GroundPound");
    private static readonly int HashImpact = Animator.StringToHash("Impact");
    private readonly HashSet<int> _animatorParams = new HashSet<int>();

    // Properties
    public bool CanPeck => _peckCooldownTimer <= 0f && (_playerController == null || !_playerController.IsInputBlocked());
    public bool CanGroundPound => !_isGroundPounding && _groundPoundCooldownTimer <= 0f &&
                                  (_playerController == null || !_playerController.IsGrounded) &&
                                  (_playerController == null || !_playerController.IsInputBlocked());
    public bool IsGroundPounding => _isGroundPounding;
    public float PeckCooldownTimer => _peckCooldownTimer;
    public float PeckCooldown => _peckCooldown;
    public int PeckDamage => _peckDamage;
    public float PeckRange => _peckRange;
    public float PeckRadius => _peckRadius;
    public float GroundPoundCooldownTimer => _groundPoundCooldownTimer;
    public float GroundPoundCooldown => _groundPoundCooldown;
    public int GroundPoundDamage => _groundPoundDamage;
    public float GroundPoundRadius => _groundPoundRadius;
    public float GroundPoundDownwardSpeed => _groundPoundDownwardSpeed;
    public ParticleSystem PeckVFX => _peckVFX;
    public ParticleSystem GroundPoundImpactVFX => _groundPoundImpactVFX;
    public Transform VisualRoot => _visualRoot;
    public int PeckExecutionCount => _peckExecutionCount;
    public int PeckHitCount => _peckHitCount;
    public int GroundPoundStartCount => _groundPoundStartCount;
    public int GroundPoundImpactCount => _groundPoundImpactCount;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_playerController == null) _playerController = GetComponent<PlayerController>();
        if (_playerRigidbody == null) _playerRigidbody = GetComponent<Rigidbody>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        AutoFindDefaultAudioClips();
    }

    private void Reset()
    {
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
        CacheAnimatorParameters();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        CacheBasePose();
    }

    public void ResolveDependencies()
    {
        if (_playerController == null) _playerController = GetComponent<PlayerController>();
        if (_playerRigidbody == null) _playerRigidbody = GetComponent<Rigidbody>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();

        if (_visualRoot == null)
        {
            var visual = transform.Find("VisualModel");
            if (visual == null) visual = transform.Find("Visual");
            if (visual == null && _animator != null) visual = _animator.transform;
            if (visual == null) visual = transform;
            _visualRoot = visual;
        }
    }

    private void CacheBasePose()
    {
        if (_visualRoot == null) return;
        if (_cachedPoseTarget == _visualRoot) return;

        _cachedPoseTarget = _visualRoot;
        _baseVisualLocalRotation = _visualRoot.localRotation;
        _baseVisualLocalScale = _visualRoot.localScale;
        if (_baseVisualLocalScale.sqrMagnitude < 0.0001f)
        {
            _baseVisualLocalScale = Vector3.one;
        }
    }

    private void CacheAnimatorParameters()
    {
        _animatorParams.Clear();
        if (_animator == null || _animator.runtimeAnimatorController == null) return;

        foreach (var param in _animator.parameters)
        {
            _animatorParams.Add(param.nameHash);
        }
    }

    private void Update()
    {
        HandleInput();
        StepTimers(Time.deltaTime > 0f ? Time.deltaTime : 0.02f);
    }

    private void FixedUpdate()
    {
        Step(Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f);
    }

    private void HandleInput()
    {
        if (_playerController != null && _playerController.IsInputBlocked())
            return;

        bool peckPressed = false;
        bool poundPressed = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.fKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) peckPressed = true;
            if (kb.leftCtrlKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame) poundPressed = true;
        }
        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame) peckPressed = true;
        }
#endif

        try
        {
            if (!peckPressed && (Input.GetKeyDown(_peckKey) || Input.GetMouseButtonDown(0))) peckPressed = true;
            if (!poundPressed && Input.GetKeyDown(_groundPoundKey)) poundPressed = true;
        }
        catch
        {
            // Input System exception fallback
        }

        if (peckPressed && CanPeck)
        {
            PerformPeck();
        }
        else if (poundPressed && CanGroundPound)
        {
            StartGroundPound();
        }
    }

    public void StepTimers(float deltaTime)
    {
        if (_peckCooldownTimer > 0f) _peckCooldownTimer -= deltaTime;
        if (_groundPoundCooldownTimer > 0f) _groundPoundCooldownTimer -= deltaTime;
        StepFeedback(deltaTime);
    }

    public void Step(float deltaTime)
    {
        ResolveDependencies();
        StepTimers(deltaTime);

        if (_isGroundPounding)
        {
            if (_playerRigidbody != null)
            {
                Vector3 vel = _playerRigidbody.linearVelocity;
                vel.x = 0f;
                vel.z = 0f;
                vel.y = -Mathf.Abs(_groundPoundDownwardSpeed);
                _playerRigidbody.linearVelocity = vel;
            }

            if (_playerController != null && _playerController.RefreshGroundState())
            {
                OnGroundPoundImpact();
            }
        }
    }

    public bool PerformPeck()
    {
        if (!CanPeck) return false;

        _peckCooldownTimer = _peckCooldown;
        _peckExecutionCount++;
        _peckAnimTimer = Mathf.Max(0.08f, _peckMotionDuration);

        EnsureVFXSetup();
        if (_peckVFX != null)
        {
            _peckVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _peckVFX.Play(true);
        }

        if (_animator != null)
        {
            if (_animatorParams.Contains(HashPeck)) _animator.SetTrigger(HashPeck);
            else if (_animatorParams.Contains(HashAttack)) _animator.SetTrigger(HashAttack);
        }

        SettingsUI.PlaySFX(_peckSfx, _sfxVolume);

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 peckCenter = origin + transform.forward * (_peckRange * 0.5f);
        Collider[] colliders = Physics.OverlapSphere(peckCenter, _peckRadius, _targetLayers, QueryTriggerInteraction.Collide);

        int hitCount = 0;
        var hitTargets = new HashSet<IDamageable>();
        foreach (var col in colliders)
        {
            if (col == null || col.transform == transform || col.transform.IsChildOf(transform))
                continue;

            var damageable = col.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsAlive && !damageable.IsInvincible && hitTargets.Add(damageable))
            {
                damageable.TakeDamage(_peckDamage, transform.forward);
                hitCount++;
            }
        }

        OnPeckExecuted?.Invoke();
        if (hitCount > 0)
        {
            _peckHitCount += hitCount;
            OnPeckHit?.Invoke(hitCount);
        }

        return true;
    }

    public bool StartGroundPound()
    {
        if (_groundPoundCooldownTimer > 0f) return false;
        if (_playerController != null && _playerController.IsGrounded) return false;

        _isGroundPounding = true;
        _groundPoundCooldownTimer = _groundPoundCooldown;
        _groundPoundStartCount++;

        if (_playerController != null)
        {
            _playerController.SetExternalMovementOverride(true);
        }

        if (_playerRigidbody != null)
        {
            Vector3 vel = _playerRigidbody.linearVelocity;
            vel.x = 0f;
            vel.z = 0f;
            vel.y = -Mathf.Abs(_groundPoundDownwardSpeed);
            _playerRigidbody.linearVelocity = vel;
        }

        if (_animator != null && _animatorParams.Contains(HashGroundPound))
        {
            _animator.SetTrigger(HashGroundPound);
        }

        SettingsUI.PlaySFX(_groundPoundStartSfx, _sfxVolume);
        OnGroundPoundStarted?.Invoke();
        return true;
    }

    public void OnGroundPoundImpact()
    {
        _isGroundPounding = false;
        if (_playerController != null)
        {
            _playerController.SetExternalMovementOverride(false);
        }

        _impactAnimTimer = Mathf.Max(0.1f, _impactMotionDuration);

        EnsureVFXSetup();
        if (_groundPoundImpactVFX != null)
        {
            _groundPoundImpactVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _groundPoundImpactVFX.Play(true);
        }

        if (_animator != null && _animatorParams.Contains(HashImpact))
        {
            _animator.SetTrigger(HashImpact);
        }

        SettingsUI.PlaySFX(_groundPoundImpactSfx, _sfxVolume);

        Vector3 impactCenter = transform.position;
        Collider[] colliders = Physics.OverlapSphere(impactCenter, _groundPoundRadius, _targetLayers, QueryTriggerInteraction.Collide);

        int hitCount = 0;
        var hitTargets = new HashSet<IDamageable>();
        foreach (var col in colliders)
        {
            if (col == null || col.transform == transform || col.transform.IsChildOf(transform))
                continue;

            var damageable = col.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsAlive && !damageable.IsInvincible && hitTargets.Add(damageable))
            {
                Vector3 hitDir = (col.transform.position - transform.position).normalized;
                if (hitDir.sqrMagnitude < 0.001f) hitDir = Vector3.up;
                damageable.TakeDamage(_groundPoundDamage, hitDir);
                hitCount++;
            }
        }

        _groundPoundImpactCount++;
        OnGroundPoundImpacted?.Invoke(hitCount);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_isGroundPounding)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y > 0.4f)
                {
                    OnGroundPoundImpact();
                    break;
                }
            }
        }
    }

    public void StepFeedback(float deltaTime)
    {
        if (_visualRoot == null) return;
        CacheBasePose();

        Quaternion targetRot = _baseVisualLocalRotation;
        Vector3 targetScale = _baseVisualLocalScale;

        if (_peckAnimTimer > 0f)
        {
            _peckAnimTimer -= deltaTime;
            float progress = Mathf.Clamp01(_peckAnimTimer / _peckMotionDuration);
            Quaternion tilt = Quaternion.Euler(_peckTiltDegrees * progress, 0f, 0f);
            targetRot = _baseVisualLocalRotation * tilt;
            targetScale = Vector3.Scale(_baseVisualLocalScale, Vector3.Lerp(Vector3.one, _peckStretchScale, progress));
        }
        else if (_isGroundPounding)
        {
            targetScale = Vector3.Scale(_baseVisualLocalScale, _poundPlungeScale);
        }
        else if (_impactAnimTimer > 0f)
        {
            _impactAnimTimer -= deltaTime;
            float progress = Mathf.Clamp01(_impactAnimTimer / _impactMotionDuration);
            targetScale = Vector3.Scale(_baseVisualLocalScale, Vector3.Lerp(Vector3.one, _poundImpactScale, progress));
        }

        _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, targetRot, deltaTime * _poseLerpSpeed);
        _visualRoot.localScale = Vector3.Lerp(_visualRoot.localScale, targetScale, deltaTime * _poseLerpSpeed);
    }

    public void ResetVisualPoseImmediate()
    {
        if (_visualRoot == null) return;
        CacheBasePose();
        _visualRoot.localRotation = _baseVisualLocalRotation;
        _visualRoot.localScale = _baseVisualLocalScale;
    }

    public bool EnsureVFXSetup()
    {
        bool createdAny = false;
        Transform vfxRoot = transform.Find(VFXRootName);
        if (vfxRoot == null)
        {
            GameObject rootObj = new GameObject(VFXRootName);
            rootObj.transform.SetParent(transform, false);
            rootObj.transform.localPosition = Vector3.zero;
            rootObj.transform.localRotation = Quaternion.identity;
            vfxRoot = rootObj.transform;
            createdAny = true;
        }

        Material sharedMat = CreateDefaultEffectMaterial();

        if (_peckVFX == null)
        {
            Transform existing = vfxRoot.Find("PeckVFX");
            GameObject peckObj = existing != null ? existing.gameObject : new GameObject("PeckVFX");
            if (existing == null)
            {
                peckObj.transform.SetParent(vfxRoot, false);
                peckObj.transform.localPosition = new Vector3(0f, 0.6f, 0.6f);
                createdAny = true;
            }

            _peckVFX = peckObj.GetComponent<ParticleSystem>();
            if (_peckVFX == null)
            {
                _peckVFX = peckObj.AddComponent<ParticleSystem>();
                createdAny = true;
            }
            ConfigurePeckParticles(_peckVFX, sharedMat);
        }

        if (_groundPoundImpactVFX == null)
        {
            Transform existing = vfxRoot.Find("GroundPoundImpactVFX");
            GameObject poundObj = existing != null ? existing.gameObject : new GameObject("GroundPoundImpactVFX");
            if (existing == null)
            {
                poundObj.transform.SetParent(vfxRoot, false);
                poundObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                createdAny = true;
            }

            _groundPoundImpactVFX = poundObj.GetComponent<ParticleSystem>();
            if (_groundPoundImpactVFX == null)
            {
                _groundPoundImpactVFX = poundObj.AddComponent<ParticleSystem>();
                createdAny = true;
            }
            ConfigureGroundPoundParticles(_groundPoundImpactVFX, sharedMat);
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

    private static void ConfigurePeckParticles(ParticleSystem ps, Material mat)
    {
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.25f;
        main.startLifetime = 0.22f;
        main.startSpeed = 3.2f;
        main.startSize = 0.16f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.95f, 0.4f, 1f),
            new Color(1f, 0.55f, 0.1f, 1f)
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 20f;
        shape.radius = 0.12f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && mat != null) renderer.sharedMaterial = mat;
    }

    private static void ConfigureGroundPoundParticles(ParticleSystem ps, Material mat)
    {
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.4f;
        main.startLifetime = 0.35f;
        main.startSpeed = 4.5f;
        main.startSize = 0.24f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.88f, 0.32f, 0.95f),
            new Color(1f, 0.42f, 0.12f, 0.85f)
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.35f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && mat != null) renderer.sharedMaterial = mat;
    }

    private void AutoFindDefaultAudioClips()
    {
#if UNITY_EDITOR
        if (_peckSfx != null && _groundPoundStartSfx != null && _groundPoundImpactSfx != null) return;
        string[] guids = AssetDatabase.FindAssets("t:AudioClip");
        if (guids == null || guids.Length == 0) return;
        AudioClip fallback = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guids[0]));
        if (fallback == null) return;

        if (_peckSfx == null) _peckSfx = fallback;
        if (_groundPoundStartSfx == null) _groundPoundStartSfx = fallback;
        if (_groundPoundImpactSfx == null) _groundPoundImpactSfx = fallback;
#endif
    }
}
