using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    private const string VFXRootName = "PlayerHealthVFX";

    [Header("--- Health Settings ---")]
    [Tooltip("Maksimum can degeri")]
    [SerializeField] private int _maxHealth = 100;

    [Tooltip("Mevcut can degeri")]
    [SerializeField] private int _currentHealth = 100;

    [Header("--- Invincibility Settings ---")]
    [Tooltip("Hasar aldiktan sonraki dokunulmazlik suresi (saniye)")]
    [SerializeField] private float _invincibilityDuration = 1.0f;

    [Header("--- Knockback Settings ---")]
    [Tooltip("Hasar anindaki geri savrulma kuvveti")]
    [SerializeField] private float _knockbackForce = 6f;

    [Tooltip("Geri savrulma sureci boyunca hareket kontrol engelleme suresi")]
    [SerializeField] private float _knockbackDuration = 0.2f;

    [Header("--- Visual & VFX Settings ---")]
    [Tooltip("Hasar alindiginda tetiklenen parcacik efekti")]
    [SerializeField] private ParticleSystem _hurtParticles;

    [Tooltip("Iyilestiginde tetiklenen parcacik efekti")]
    [SerializeField] private ParticleSystem _healParticles;

    [Tooltip("Prosedurel hareket uygulanacak gorsel transform")]
    [SerializeField] private Transform _visualRoot;

    [Tooltip("Hasar alma sarsinti / egim acisi")]
    [SerializeField] private float _hurtTiltAngle = 14f;

    [Tooltip("Hasar alma basiklasma (squash) carpani")]
    [SerializeField] private float _hurtSquashFactor = 0.85f;

    [Tooltip("Geri bildirim animasyon suresi")]
    [SerializeField] private float _feedbackDuration = 0.25f;

    [Header("--- Audio (SFX) ---")]
    [SerializeField] private AudioClip _hurtSound;
    [SerializeField] private AudioClip _healSound;
    [SerializeField] private AudioClip _deathSound;
    [SerializeField][Range(0f, 1f)] private float _sfxVolume = 0.85f;

    // Bagimliliklar
    private Rigidbody _rigidbody;
    private PlayerController _playerController;
    private Renderer[] _cachedRenderers;

    // Calisma Zamani Degiskenleri
    private float _invincibilityTimer = 0f;
    private float _knockbackTimer = 0f;
    private float _feedbackTimer = 0f;
    private bool _isInvincibleCustom = false;
    private bool _isHealFeedbackActive = false;

    // Gorsel Taban Pozisyonu
    private Transform _cachedPoseTarget;
    private Quaternion _baseVisualLocalRotation = Quaternion.identity;
    private Vector3 _baseVisualLocalScale = Vector3.one;

    // Olaylar (Events)
    public event Action<int, int> OnHealthChanged;
    public event Action OnDied;

    // IDamageable ve Public Ozellikler
    public int CurrentHealth => _currentHealth;
    public int MaxHealth => _maxHealth;
    public bool IsAlive => _currentHealth > 0;
    public bool IsInvincible => (_invincibilityTimer > 0f) || _isInvincibleCustom;
    public float InvincibilityDuration { get => _invincibilityDuration; set => _invincibilityDuration = value; }
    public float KnockbackForce { get => _knockbackForce; set => _knockbackForce = value; }
    public ParticleSystem HurtParticles => _hurtParticles;
    public ParticleSystem HealParticles => _healParticles;
    public Transform VisualRoot => _visualRoot;

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveDependencies();
        EnsureVFXSetup();
        AutoFindDefaultAudioClips();
    }

    private void OnValidate()
    {
        _maxHealth = Mathf.Max(1, _maxHealth);
        _currentHealth = Mathf.Clamp(_currentHealth, 0, _maxHealth);
        _invincibilityDuration = Mathf.Max(0.05f, _invincibilityDuration);
        _knockbackForce = Mathf.Max(0f, _knockbackForce);
        ResolveDependencies();
        AutoFindDefaultAudioClips();
    }
#endif

    private void Awake()
    {
        ResolveDependencies();
        CacheBasePose();
        EnsureVFXSetup();
        _currentHealth = Mathf.Clamp(_currentHealth, 0, _maxHealth);
    }

    private void Start()
    {
        ResolveDependencies();
        CacheBasePose();
    }

    private void Update()
    {
        float dt = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
        StepFeedback(dt);
    }

    private void OnDisable()
    {
        if (_playerController != null && _knockbackTimer > 0f)
        {
            _playerController.SetExternalMovementOverride(false);
        }
        _knockbackTimer = 0f;
        ResetVisualPoseImmediate();
    }

    public void ResolveDependencies()
    {
        if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
        if (_playerController == null) _playerController = GetComponent<PlayerController>();
        if (_visualRoot == null) _visualRoot = FindVisualRoot();
        if (_cachedRenderers == null || _cachedRenderers.Length == 0)
        {
            _cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    private Transform FindVisualRoot()
    {
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null && anim.transform != transform) return anim.transform;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null) continue;
            string n = child.name;
            if (n.Contains("VFX") || n == "Orientation" || n.Contains("Camera")) continue;
            if (child.GetComponentInChildren<Renderer>() != null) return child;
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

    public void SetInvincible(bool invincible)
    {
        _isInvincibleCustom = invincible;
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, Vector3.zero);
    }

    public void TakeDamage(int damage, Vector3 hitDirection)
    {
        if (!IsAlive) return;
        if (IsInvincible) return;
        if (damage <= 0) return;

        _currentHealth = Mathf.Max(0, _currentHealth - damage);
        _invincibilityTimer = _invincibilityDuration;

        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        ApplyKnockback(hitDirection);
        TriggerHurtFeedback();

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(Mathf.RoundToInt(damage), Vector3.zero);
    }

    public void TakeDamage(float damage, Vector3 hitDirection)
    {
        TakeDamage(Mathf.RoundToInt(damage), hitDirection);
    }

    public void Heal(int amount)
    {
        if (!IsAlive) return;
        if (amount <= 0) return;
        if (_currentHealth >= _maxHealth) return;

        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        TriggerHealFeedback();
    }

    public void Heal(float amount)
    {
        Heal(Mathf.RoundToInt(amount));
    }

    public void ResetHealth()
    {
        _currentHealth = _maxHealth;
        _invincibilityTimer = 0f;
        _knockbackTimer = 0f;
        _feedbackTimer = 0f;
        ResetVisualPoseImmediate();
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    private void ApplyKnockback(Vector3 hitDirection)
    {
        if (_knockbackForce <= 0.01f || _rigidbody == null || _rigidbody.isKinematic) return;

        Vector3 pushDir;
        if (hitDirection.sqrMagnitude > 0.001f)
        {
            pushDir = hitDirection.normalized;
        }
        else
        {
            pushDir = -transform.forward;
        }

        pushDir.y = 0.35f;
        pushDir = pushDir.normalized;

        _rigidbody.linearVelocity = new Vector3(0f, Mathf.Max(0f, _rigidbody.linearVelocity.y * 0.2f), 0f);
        _rigidbody.AddForce(pushDir * _knockbackForce, ForceMode.Impulse);

        if (_playerController != null)
        {
            _playerController.SetExternalMovementOverride(true);
        }
        _knockbackTimer = Mathf.Max(0.05f, _knockbackDuration);
    }

    private void TriggerHurtFeedback()
    {
        EnsureVFXSetup();
        _isHealFeedbackActive = false;
        _feedbackTimer = Mathf.Max(0.1f, _feedbackDuration);

        if (_hurtParticles != null)
        {
            _hurtParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _hurtParticles.Play(true);
        }

        SettingsUI.PlaySFX(_hurtSound, _sfxVolume);
    }

    private void TriggerHealFeedback()
    {
        EnsureVFXSetup();
        _isHealFeedbackActive = true;
        _feedbackTimer = Mathf.Max(0.1f, _feedbackDuration);

        if (_healParticles != null)
        {
            _healParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _healParticles.Play(true);
        }

        SettingsUI.PlaySFX(_healSound, _sfxVolume);
    }

    private void Die()
    {
        OnDied?.Invoke();
        SettingsUI.PlaySFX(_deathSound, _sfxVolume);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerGameOver();
        }
        else
        {
            GameManager gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            if (gm != null) gm.TriggerGameOver();
        }
    }

    public void StepFeedback(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        // 1. Invincibility Timer
        if (_invincibilityTimer > 0f)
        {
            _invincibilityTimer = Mathf.Max(0f, _invincibilityTimer - deltaTime);

            // Dokunulmazlik esnasinda gorsel yanip sonme
            if (_cachedRenderers != null && _cachedRenderers.Length > 0)
            {
                bool isBlinkVisible = (_invincibilityTimer <= 0f) || (Mathf.Repeat(_invincibilityTimer * 14f, 1f) > 0.35f);
                for (int i = 0; i < _cachedRenderers.Length; i++)
                {
                    if (_cachedRenderers[i] != null)
                        _cachedRenderers[i].enabled = isBlinkVisible;
                }
            }
        }
        else
        {
            EnsureRenderersVisible();
        }

        // 2. Knockback Override Timer
        if (_knockbackTimer > 0f)
        {
            _knockbackTimer -= deltaTime;
            if (_knockbackTimer <= 0f && _playerController != null)
            {
                _playerController.SetExternalMovementOverride(false);
            }
        }

        // 3. Prosedurel Hareket / Geri Bildirim
        if (_visualRoot == null) return;
        CacheBasePose();

        Quaternion targetRot = _baseVisualLocalRotation;
        Vector3 targetScale = _baseVisualLocalScale;

        if (_feedbackTimer > 0f)
        {
            _feedbackTimer -= deltaTime;
            float t = _feedbackTimer / Mathf.Max(0.05f, _feedbackDuration);

            if (_isHealFeedbackActive)
            {
                // Iyilestiginde yukari yaylanma / esneme
                Vector3 stretchFactor = new Vector3(0.92f, 1.15f, 0.92f);
                targetScale = Vector3.Scale(_baseVisualLocalScale, Vector3.Lerp(Vector3.one, stretchFactor, t));
            }
            else
            {
                // Hasar aldiginda geriye egilme ve basiklasma (squash)
                targetRot = _baseVisualLocalRotation * Quaternion.Euler(-_hurtTiltAngle * t, 0f, 0f);
                Vector3 squashFactor = new Vector3(1.18f, _hurtSquashFactor, 1.18f);
                targetScale = Vector3.Scale(_baseVisualLocalScale, Vector3.Lerp(Vector3.one, squashFactor, t));
            }

            _visualRoot.localRotation = targetRot;
            _visualRoot.localScale = targetScale;
        }
        else
        {
            float blend = 1f - Mathf.Exp(-14f * deltaTime);
            _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, _baseVisualLocalRotation, blend);
            _visualRoot.localScale = Vector3.Lerp(_visualRoot.localScale, _baseVisualLocalScale, blend);
        }
    }

    public void ResetVisualPoseImmediate()
    {
        if (_visualRoot != null && _cachedPoseTarget == _visualRoot)
        {
            _visualRoot.localRotation = _baseVisualLocalRotation;
            _visualRoot.localScale = _baseVisualLocalScale;
        }
        EnsureRenderersVisible();
    }

    private void EnsureRenderersVisible()
    {
        if (_cachedRenderers == null) return;
        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            if (_cachedRenderers[i] != null && !_cachedRenderers[i].enabled)
                _cachedRenderers[i].enabled = true;
        }
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

        // Hurt Particles
        if (_hurtParticles == null)
        {
            Transform existing = vfxRoot.Find("HurtParticles");
            GameObject hurtObj = existing != null ? existing.gameObject : new GameObject("HurtParticles");
            if (existing == null)
            {
                hurtObj.transform.SetParent(vfxRoot, false);
                hurtObj.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                createdAny = true;
            }

            _hurtParticles = hurtObj.GetComponent<ParticleSystem>();
            if (_hurtParticles == null)
            {
                _hurtParticles = hurtObj.AddComponent<ParticleSystem>();
                createdAny = true;
            }
            ConfigureHurtParticles(_hurtParticles, sharedMat);
        }

        // Heal Particles
        if (_healParticles == null)
        {
            Transform existing = vfxRoot.Find("HealParticles");
            GameObject healObj = existing != null ? existing.gameObject : new GameObject("HealParticles");
            if (existing == null)
            {
                healObj.transform.SetParent(vfxRoot, false);
                healObj.transform.localPosition = new Vector3(0f, 0.4f, 0f);
                createdAny = true;
            }

            _healParticles = healObj.GetComponent<ParticleSystem>();
            if (_healParticles == null)
            {
                _healParticles = healObj.AddComponent<ParticleSystem>();
                createdAny = true;
            }
            ConfigureHealParticles(_healParticles, sharedMat);
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

    private static void ConfigureHurtParticles(ParticleSystem ps, Material mat)
    {
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.35f;
        main.startLifetime = 0.35f;
        main.startSpeed = 3.5f;
        main.startSize = 0.2f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.28f, 0.18f, 1f),
            new Color(1f, 0.75f, 0.15f, 1f)
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && mat != null) renderer.sharedMaterial = mat;
    }

    private static void ConfigureHealParticles(ParticleSystem ps, Material mat)
    {
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.45f;
        main.startLifetime = 0.45f;
        main.startSpeed = 2.2f;
        main.startSize = 0.18f;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.4f, 1f, 0.45f, 1f),
            new Color(1f, 0.95f, 0.4f, 1f)
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.25f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && mat != null) renderer.sharedMaterial = mat;
    }

    private void AutoFindDefaultAudioClips()
    {
#if UNITY_EDITOR
        if (_hurtSound != null && _healSound != null && _deathSound != null) return;
        string[] guids = AssetDatabase.FindAssets("t:AudioClip");
        if (guids == null || guids.Length == 0) return;
        AudioClip fallback = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guids[0]));
        if (fallback == null) return;

        if (_hurtSound == null) _hurtSound = fallback;
        if (_healSound == null) _healSound = fallback;
        if (_deathSound == null) _deathSound = fallback;
#endif
    }
}
