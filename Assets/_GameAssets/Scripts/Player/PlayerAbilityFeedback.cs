using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(PlayerController))]
public class PlayerAbilityFeedback : MonoBehaviour
{
    private const string VFXRootName = "AbilityVFX";

    [Header("--- References ---")]
    [SerializeField] private DashController _dashController;
    [SerializeField] private ChickGlideController _glideController;
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private Animator _animator;
    [SerializeField] private Transform _visualRoot;

    [Header("--- VFX Components ---")]
    [SerializeField] private TrailRenderer _dashTrail;
    [SerializeField] private ParticleSystem _dashBurstVFX;
    [SerializeField] private ParticleSystem _airJumpRingVFX;
    [SerializeField] private ParticleSystem _glideFeatherVFX;

    [Header("--- Audio (SFX) ---")]
    [SerializeField] private AudioClip _dashSfx;
    [SerializeField] private AudioClip _airJumpSfx;
    [SerializeField] private AudioClip _glideStartSfx;
    [SerializeField][Range(0f, 1f)] private float _sfxVolume = 0.85f;

    [Header("--- Glide Procedural Motion ---")]
    [SerializeField] private float _glideForwardTilt = 16f;
    [SerializeField] private float _glideSwayAngle = 12f;
    [SerializeField] private float _glideSwayFrequency = 9f;
    [SerializeField] private float _glideFlapScaleAmp = 0.08f;

    [Header("--- Air Jump Procedural Motion ---")]
    [SerializeField] private float _airJumpAnimDuration = 0.32f;
    [SerializeField] private float _airJumpSpinDegrees = 360f;
    [SerializeField] private Vector3 _airJumpStretchScale = new Vector3(0.86f, 1.24f, 0.86f);

    [Header("--- Dash Procedural Motion ---")]
    [SerializeField] private float _dashForwardTilt = 22f;
    [SerializeField] private Vector3 _dashStretchScale = new Vector3(0.84f, 0.88f, 1.30f);

    [Header("--- Pose Recovery ---")]
    [SerializeField] private float _poseLerpSpeed = 14f;

    // Base Pose Cache
    private Transform _cachedPoseTarget;
    private Quaternion _baseVisualLocalRotation = Quaternion.identity;
    private Vector3 _baseVisualLocalScale = Vector3.one;

    // Runtime Feedback State
    private bool _isGliding;
    private float _glideTime;
    private bool _isDashFeedbackActive;
    private float _dashFeedbackTimer;
    private float _airJumpAnimTimer;
    private bool _isSubscribed;

    // Telemetry Counters (for verification & debugging)
    private int _dashEventCount;
    private int _airJumpEventCount;
    private int _glideStartEventCount;
    private int _glideStopEventCount;

    // Animator Parameter Cache
    private static readonly int HashIsGliding = Animator.StringToHash("isGliding");
    private static readonly int HashDash = Animator.StringToHash("Dash");
    private static readonly int HashJump = Animator.StringToHash("Jump");
    private static readonly int HashGlideState = Animator.StringToHash("Glide");
    private readonly HashSet<int> _animatorParams = new HashSet<int>();

    // Public Accessors
    public Transform VisualRoot => _visualRoot;
    public TrailRenderer DashTrail => _dashTrail;
    public ParticleSystem DashBurstVFX => _dashBurstVFX;
    public ParticleSystem AirJumpRingVFX => _airJumpRingVFX;
    public ParticleSystem GlideFeatherVFX => _glideFeatherVFX;
    public bool IsGlideFeedbackActive => _isGliding;
    public bool IsDashFeedbackActive => _isDashFeedbackActive;
    public bool IsAirJumpFeedbackActive => _airJumpAnimTimer > 0f;
    public int DashEventCount => _dashEventCount;
    public int AirJumpEventCount => _airJumpEventCount;
    public int GlideStartEventCount => _glideStartEventCount;
    public int GlideStopEventCount => _glideStopEventCount;

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveDependencies();
        EnsureVFXSetup();
        AutoFindDefaultAudioClips();
    }

    private void OnValidate()
    {
        if (_playerController == null) _playerController = GetComponent<PlayerController>();
        if (_dashController == null) _dashController = GetComponent<DashController>();
        if (_glideController == null) _glideController = GetComponent<ChickGlideController>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        AutoFindDefaultAudioClips();
    }
#endif

    private void Awake()
    {
        ResolveDependencies();
        CacheBasePose();
        EnsureVFXSetup();
        CacheAnimatorParameters();
        SubscribeEvents();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        CacheBasePose();
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        if (_dashTrail != null) _dashTrail.emitting = false;
        SetGlideEmission(false);
        ResetVisualPoseImmediate();
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
        StepFeedback(dt);
    }

    public void ResolveDependencies()
    {
        if (_playerController == null) _playerController = GetComponent<PlayerController>();
        if (_dashController == null) _dashController = GetComponent<DashController>();
        if (_glideController == null) _glideController = GetComponent<ChickGlideController>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();

        if (_visualRoot == null)
        {
            _visualRoot = FindVisualRoot();
        }
    }

    private Transform FindVisualRoot()
    {
        if (_animator != null && _animator.transform != transform)
        {
            return _animator.transform;
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null) continue;
            string n = child.name;
            if (n == VFXRootName || n == "Orientation" || n.Contains("Camera")) continue;

            if (child.GetComponentInChildren<Renderer>() != null)
            {
                return child;
            }
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null) continue;
            string n = child.name;
            if (n == VFXRootName || n == "Orientation" || n.Contains("Camera")) continue;
            return child;
        }

        return transform;
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

    public void SubscribeEvents()
    {
        UnsubscribeEvents();

        if (_dashController != null)
        {
            _dashController.OnDashStarted += HandleDashStarted;
            _dashController.OnDashEnded += HandleDashEnded;
        }

        if (_glideController != null)
        {
            _glideController.OnAirJumpExecuted += HandleAirJumpExecuted;
            _glideController.OnGlideStateChanged += HandleGlideStateChanged;
        }

        _isSubscribed = true;
    }

    public void UnsubscribeEvents()
    {
        if (!_isSubscribed && _dashController == null && _glideController == null) return;

        if (_dashController != null)
        {
            _dashController.OnDashStarted -= HandleDashStarted;
            _dashController.OnDashEnded -= HandleDashEnded;
        }

        if (_glideController != null)
        {
            _glideController.OnAirJumpExecuted -= HandleAirJumpExecuted;
            _glideController.OnGlideStateChanged -= HandleGlideStateChanged;
        }

        _isSubscribed = false;
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

        // 1. Dash TrailRenderer (Warm Gold-Orange)
        if (_dashTrail == null)
        {
            Transform existing = vfxRoot.Find("DashTrail");
            GameObject trailObj = existing != null ? existing.gameObject : new GameObject("DashTrail");
            if (existing == null)
            {
                trailObj.transform.SetParent(vfxRoot, false);
                trailObj.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                createdAny = true;
            }

            _dashTrail = trailObj.GetComponent<TrailRenderer>();
            if (_dashTrail == null)
            {
                _dashTrail = trailObj.AddComponent<TrailRenderer>();
                createdAny = true;
            }
            ConfigureDashTrail(_dashTrail, sharedMat);
        }

        // 2. Dash Burst ParticleSystem (Gold-Orange Sparks/Dust)
        if (_dashBurstVFX == null)
        {
            Transform existing = vfxRoot.Find("DashBurstVFX");
            GameObject burstObj = existing != null ? existing.gameObject : new GameObject("DashBurstVFX");
            if (existing == null)
            {
                burstObj.transform.SetParent(vfxRoot, false);
                burstObj.transform.localPosition = new Vector3(0f, 0.5f, -0.2f);
                createdAny = true;
            }

            _dashBurstVFX = burstObj.GetComponent<ParticleSystem>();
            if (_dashBurstVFX == null)
            {
                _dashBurstVFX = burstObj.AddComponent<ParticleSystem>();
                createdAny = true;
            }
            ConfigureDashBurst(_dashBurstVFX, sharedMat);
        }

        // 3. Air Jump Expanding Ring ParticleSystem
        if (_airJumpRingVFX == null)
        {
            Transform existing = vfxRoot.Find("AirJumpRingVFX");
            GameObject ringObj = existing != null ? existing.gameObject : new GameObject("AirJumpRingVFX");
            if (existing == null)
            {
                ringObj.transform.SetParent(vfxRoot, false);
                ringObj.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                createdAny = true;
            }

            _airJumpRingVFX = ringObj.GetComponent<ParticleSystem>();
            if (_airJumpRingVFX == null)
            {
                _airJumpRingVFX = ringObj.AddComponent<ParticleSystem>();
                createdAny = true;
            }
            ConfigureAirJumpRing(_airJumpRingVFX, sharedMat);
        }

        // 4. Glide Wind / Feather ParticleSystem
        if (_glideFeatherVFX == null)
        {
            Transform existing = vfxRoot.Find("GlideFeatherVFX");
            GameObject glideObj = existing != null ? existing.gameObject : new GameObject("GlideFeatherVFX");
            if (existing == null)
            {
                glideObj.transform.SetParent(vfxRoot, false);
                glideObj.transform.localPosition = new Vector3(0f, 0.85f, 0f);
                createdAny = true;
            }

            _glideFeatherVFX = glideObj.GetComponent<ParticleSystem>();
            if (_glideFeatherVFX == null)
            {
                _glideFeatherVFX = glideObj.AddComponent<ParticleSystem>();
                createdAny = true;
            }
            ConfigureGlideFeather(_glideFeatherVFX, sharedMat);
        }

#if UNITY_EDITOR
        AutoFindDefaultAudioClips();
#endif

        return createdAny;
    }

    private void HandleDashStarted()
    {
        _dashEventCount++;
        _isDashFeedbackActive = true;
        _dashFeedbackTimer = (_dashController != null && _dashController.DashDuration > 0f)
            ? _dashController.DashDuration
            : 0.2f;

        EnsureVFXSetup();

        if (_dashTrail != null)
        {
            _dashTrail.Clear();
            _dashTrail.emitting = true;
        }

        if (_dashBurstVFX != null)
        {
            _dashBurstVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _dashBurstVFX.Play(true);
        }

        if (_animator != null && _animatorParams.Contains(HashDash))
        {
            _animator.SetTrigger(HashDash);
        }

        SettingsUI.PlaySFX(_dashSfx, _sfxVolume);
    }

    private void HandleDashEnded()
    {
        _isDashFeedbackActive = false;
        _dashFeedbackTimer = 0f;
        if (_dashTrail != null)
        {
            _dashTrail.emitting = false;
        }
    }

    private void HandleAirJumpExecuted()
    {
        _airJumpEventCount++;
        _airJumpAnimTimer = Mathf.Max(0.1f, _airJumpAnimDuration);

        EnsureVFXSetup();

        if (_airJumpRingVFX != null)
        {
            _airJumpRingVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _airJumpRingVFX.Play(true);
        }

        if (_animator != null && _animatorParams.Contains(HashJump))
        {
            _animator.SetTrigger(HashJump);
        }

        SettingsUI.PlaySFX(_airJumpSfx, _sfxVolume);
    }

    private void HandleGlideStateChanged(bool isGliding)
    {
        _isGliding = isGliding;
        EnsureVFXSetup();

        if (isGliding)
        {
            _glideStartEventCount++;
            _glideTime = 0f;
            SetGlideEmission(true);

            if (_glideFeatherVFX != null)
            {
                _glideFeatherVFX.Play(true);
            }

            if (_animator != null)
            {
                if (_animatorParams.Contains(HashIsGliding)) _animator.SetBool(HashIsGliding, true);
                if (_animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null && _animator.HasState(0, HashGlideState))
                {
                    _animator.CrossFadeInFixedTime(HashGlideState, 0.12f);
                }
            }

            SettingsUI.PlaySFX(_glideStartSfx, _sfxVolume * 0.8f);
        }
        else
        {
            _glideStopEventCount++;
            SetGlideEmission(false);

            if (_glideFeatherVFX != null)
            {
                _glideFeatherVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (_animator != null && _animatorParams.Contains(HashIsGliding))
            {
                _animator.SetBool(HashIsGliding, false);
            }
        }
    }

    public void StepFeedback(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        ResolveDependencies();
        CacheBasePose();
        if (_visualRoot == null) return;

        // Tick Dash timer
        if (_isDashFeedbackActive)
        {
            _dashFeedbackTimer -= deltaTime;
            if (_dashFeedbackTimer <= 0f && (_dashController == null || !_dashController.IsDashing))
            {
                HandleDashEnded();
            }
        }

        // Tick Air Jump timer
        if (_airJumpAnimTimer > 0f)
        {
            _airJumpAnimTimer = Mathf.Max(0f, _airJumpAnimTimer - deltaTime);
        }

        // Compute target procedural rotation & scale offsets
        Quaternion targetRot = _baseVisualLocalRotation;
        Vector3 targetScale = _baseVisualLocalScale;
        bool instantPose = false;

        if (_isDashFeedbackActive)
        {
            targetRot = _baseVisualLocalRotation * Quaternion.Euler(_dashForwardTilt, 0f, 0f);
            targetScale = Vector3.Scale(_baseVisualLocalScale, _dashStretchScale);
        }
        else if (_airJumpAnimTimer > 0f)
        {
            float duration = Mathf.Max(0.1f, _airJumpAnimDuration);
            float t = 1f - (_airJumpAnimTimer / duration);
            float stretchEnvelope = Mathf.Sin(t * Mathf.PI);
            Vector3 scaleFactor = Vector3.Lerp(Vector3.one, _airJumpStretchScale, stretchEnvelope);
            float spinYaw = _airJumpSpinDegrees * t;
            targetRot = _baseVisualLocalRotation * Quaternion.Euler(0f, spinYaw, 0f);
            targetScale = Vector3.Scale(_baseVisualLocalScale, scaleFactor);
            instantPose = true;
        }
        else if (_isGliding)
        {
            _glideTime += deltaTime;
            float sway = Mathf.Sin(_glideTime * _glideSwayFrequency) * _glideSwayAngle;
            float flap = Mathf.Sin(_glideTime * _glideSwayFrequency * 2f) * _glideFlapScaleAmp;
            targetRot = _baseVisualLocalRotation * Quaternion.Euler(_glideForwardTilt, 0f, sway);
            targetScale = Vector3.Scale(_baseVisualLocalScale, new Vector3(1f + flap, 1f - flap * 0.5f, 1f));
        }

        if (instantPose)
        {
            _visualRoot.localRotation = targetRot;
            _visualRoot.localScale = targetScale;
        }
        else
        {
            float blend = 1f - Mathf.Exp(-_poseLerpSpeed * deltaTime);
            _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, targetRot, blend);
            _visualRoot.localScale = Vector3.Lerp(_visualRoot.localScale, targetScale, blend);
        }
    }

    public void ResetVisualPoseImmediate()
    {
        if (_visualRoot != null && _cachedPoseTarget == _visualRoot)
        {
            _visualRoot.localRotation = _baseVisualLocalRotation;
            _visualRoot.localScale = _baseVisualLocalScale;
        }
    }

    private void SetGlideEmission(bool enabled)
    {
        if (_glideFeatherVFX == null) return;
        var emission = _glideFeatherVFX.emission;
        emission.enabled = enabled;
    }

    private void CacheAnimatorParameters()
    {
        _animatorParams.Clear();
        if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null) return;
        foreach (var p in _animator.parameters)
        {
            _animatorParams.Add(p.nameHash);
        }
    }

    private static Material CreateDefaultEffectMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        return shader != null ? new Material(shader) : null;
    }

    private static void ConfigureDashTrail(TrailRenderer trail, Material mat)
    {
        trail.time = 0.28f;
        trail.minVertexDistance = 0.08f;
        trail.startWidth = 0.45f;
        trail.endWidth = 0.05f;
        trail.emitting = false;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;

        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.84f, 0.31f), 0f),
                new GradientColorKey(new Color(1f, 0.60f, 0f), 0.55f),
                new GradientColorKey(new Color(0.95f, 0.35f, 0f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.6f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        trail.colorGradient = grad;
        if (mat != null) trail.sharedMaterial = mat;
    }

    private static void ConfigureDashBurst(ParticleSystem ps, Material mat)
    {
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.4f;
        main.startLifetime = 0.35f;
        main.startSpeed = 4.5f;
        main.startSize = 0.22f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.84f, 0.31f, 1f),
            new Color(1f, 0.58f, 0f, 1f)
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 32f;
        shape.radius = 0.2f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && mat != null) renderer.sharedMaterial = mat;
    }

    private static void ConfigureAirJumpRing(ParticleSystem ps, Material mat)
    {
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.4f;
        main.startLifetime = 0.32f;
        main.startSpeed = 3.2f;
        main.startSize = 0.22f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.97f, 0.86f, 0.95f),
            new Color(1f, 0.84f, 0.31f, 0.85f)
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.25f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && mat != null) renderer.sharedMaterial = mat;
    }

    private static void ConfigureGlideFeather(ParticleSystem ps, Material mat)
    {
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = 0.55f;
        main.startSpeed = 1.4f;
        main.startSize = 0.16f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.88f, 0.51f, 0.9f),
            new Color(1f, 0.97f, 0.88f, 0.8f)
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = false;
        emission.rateOverTime = 18f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.9f, 0.2f, 0.4f);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && mat != null) renderer.sharedMaterial = mat;
    }

    private void AutoFindDefaultAudioClips()
    {
#if UNITY_EDITOR
        if (_dashSfx != null && _airJumpSfx != null && _glideStartSfx != null) return;
        string[] guids = AssetDatabase.FindAssets("t:AudioClip cozy_fx");
        if (guids.Length == 0) return;
        AudioClip fallback = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guids[0]));
        if (fallback == null) return;

        if (_dashSfx == null) _dashSfx = fallback;
        if (_airJumpSfx == null) _airJumpSfx = fallback;
        if (_glideStartSfx == null) _glideStartSfx = fallback;
#endif
    }
}
