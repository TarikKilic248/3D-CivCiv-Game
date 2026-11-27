using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CivCiv 3D - Hizlandirma Halkasi (SpeedBoostRing)
/// Parkur ve suzulme rotalarinda oyuncuyu ileri firlatan, prosedurel donus/nabiz animasyonlu,
/// ParticleSystem ve TrailRenderer efektli, SettingsUI.PlaySFX ses kancali halka bileseni.
/// </summary>
public class SpeedBoostRing : MonoBehaviour
{
    [Header("--- Boost Settings ---")]
    [Tooltip("Ileri yonde uygulanacak firlatma ivmesi/hizi")]
    [SerializeField] private float _boostForce = 22f;

    [Tooltip("Ayni hedef veya halka icin tetiklenmeler arasi bekleme suresi (saniye)")]
    [SerializeField] private float _cooldown = 0.5f;

    [Tooltip("Dikey hiz korunup ekstra kucuk bir kaldirma kuvveti uygulansin mi")]
    [SerializeField] private bool _maintainOrElevateY = true;

    [Tooltip("Yatay firlatmada hafif yukari ivme")]
    [SerializeField] private float _upwardForce = 2f;

    [Header("--- Visuals & Procedural Motion ---")]
    [Tooltip("Prosedurel donus ve nabiz olceklendirmesinin uygulanacagi kok nesne")]
    [SerializeField] private Transform _visualRoot;

    [Tooltip("Bekleme sirasinda kendi etrafinda saniyede donus hizi (derece)")]
    [SerializeField] private float _idleSpinSpeed = 45f;

    [Tooltip("Tetiklenme aninda squash-stretch/nabiz genleme carpani")]
    [SerializeField] private float _pulseScale = 1.35f;

    [Tooltip("Nabiz animasyonunun toplam suresi (saniye)")]
    [SerializeField] private float _pulseDuration = 0.4f;

    [Header("--- VFX & Trail ---")]
    [Tooltip("Firlatma aninda tetiklenen parcacik efekti")]
    [SerializeField] private ParticleSystem _boostVFX;

    [Tooltip("Halka uzerindeki veya olusturulan iz efekti")]
    [SerializeField] private TrailRenderer _trailRenderer;

    [Tooltip("Iz efektinin ekranda kalma suresi")]
    [SerializeField] private float _trailDuration = 0.6f;

    [Header("--- Audio ---")]
    [Tooltip("Halkadan gecis ses efekti")]
    [SerializeField] private AudioClip _boostSfx;

    [Tooltip("Ses seviyesi")]
    [SerializeField][Range(0f, 1f)] private float _sfxVolume = 0.9f;

    // Calisma zamani durumlari
    private float _cooldownTimer;
    private float _pulseTimer = -1f;
    private float _trailTimer;
    private Vector3 _baseScale = Vector3.one;
    private TrailRenderer _activeTrail;
    private int _boostCount;
    private int _sfxPlayCount;
    private readonly Dictionary<Rigidbody, float> _targetCooldowns = new Dictionary<Rigidbody, float>();
    private readonly List<Rigidbody> _cooldownCleanupKeys = new List<Rigidbody>();

    // Olaylar
    public event Action<Rigidbody> OnRingPassed;

    // Public Ozellikler
    public float BoostForce
    {
        get => _boostForce;
        set => _boostForce = Mathf.Max(0f, value);
    }

    public float Cooldown
    {
        get => _cooldown;
        set => _cooldown = Mathf.Max(0f, value);
    }

    public bool IsOnCooldown => _cooldownTimer > 0f;
    public int BoostCount => _boostCount;
    public int SfxPlayCount => _sfxPlayCount;
    public Transform VisualRoot => _visualRoot;
    public ParticleSystem BoostVFX => _boostVFX;
    public ParticleSystem ParticleVFX => _boostVFX;
    public TrailRenderer Trail => _trailRenderer;
    public TrailRenderer ActiveTrail => _activeTrail;
    public AudioClip BoostSFX => _boostSfx;

    private void Awake()
    {
        if (_visualRoot == null)
        {
            Transform found = transform.Find("VisualRoot") ?? transform.Find("RingVisual");
            _visualRoot = found != null ? found : transform;
        }
        _baseScale = _visualRoot.localScale;
        EnsureVFXSetup();
    }

    private void Update()
    {
        StepFeedback(Time.deltaTime);
    }

    public bool EnsureVFXSetup()
    {
        bool changed = false;

        if (_visualRoot == null)
        {
            Transform found = transform.Find("VisualRoot") ?? transform.Find("RingVisual");
            _visualRoot = found != null ? found : transform;
            _baseScale = _visualRoot.localScale;
            changed = true;
        }

        // BoxCollider / Trigger guvencesi
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2.6f, 2.6f, 0.8f);
            changed = true;
        }
        else
        {
            col.isTrigger = true;
        }

        // ParticleSystem
        if (_boostVFX == null)
        {
            Transform vfxT = transform.Find("SpeedBoostRingVFX");
            GameObject go = vfxT != null ? vfxT.gameObject : new GameObject("SpeedBoostRingVFX");
            if (vfxT == null)
            {
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
            }

            _boostVFX = go.GetComponent<ParticleSystem>();
            if (_boostVFX == null) _boostVFX = go.AddComponent<ParticleSystem>();
            _boostVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _boostVFX.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.startLifetime = 0.6f;
            main.startSpeed = 6.5f;
            main.startSize = 0.22f;
            main.gravityModifier = 0.05f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.85f, 0.2f, 0.95f),
                new Color(1f, 0.45f, 0.05f, 0.95f));

            var emission = _boostVFX.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });

            var shape = _boostVFX.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = 0.75f;
            shape.rotation = Vector3.zero;

            var r = go.GetComponent<ParticleSystemRenderer>();
            if (r != null && r.sharedMaterial == null)
            {
                Shader spriteShader = Shader.Find("Sprites/Default");
                if (spriteShader != null)
                {
                    r.material = new Material(spriteShader);
                }
            }
            changed = true;
        }

        // TrailRenderer
        if (_trailRenderer == null)
        {
            Transform trailT = transform.Find("SpeedBoostRingTrail");
            GameObject go = trailT != null ? trailT.gameObject : new GameObject("SpeedBoostRingTrail");
            if (trailT == null)
            {
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
            }

            _trailRenderer = go.GetComponent<TrailRenderer>();
            if (_trailRenderer == null) _trailRenderer = go.AddComponent<TrailRenderer>();
            _trailRenderer.time = _trailDuration;
            _trailRenderer.startWidth = 0.35f;
            _trailRenderer.endWidth = 0.0f;
            _trailRenderer.startColor = new Color(1f, 0.85f, 0.2f, 0.9f);
            _trailRenderer.endColor = new Color(1f, 0.45f, 0.05f, 0f);
            _trailRenderer.emitting = false;

            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null && _trailRenderer.sharedMaterial == null)
            {
                _trailRenderer.material = new Material(spriteShader);
            }
            changed = true;
        }

        // Procedural Audio (Chirp / Whoosh)
        if (_boostSfx == null)
        {
            int rate = 22050;
            int len = (int)(rate * 0.35f);
            float[] data = new float[len];
            float phase = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float freq = 320f + 480f * t;
                phase += 2f * Mathf.PI * freq / rate;
                data[i] = Mathf.Sin(phase) * (1f - t) * 0.5f;
            }
            _boostSfx = AudioClip.Create("SpeedBoostRingWhoosh", len, 1, rate, false);
            _boostSfx.SetData(data, 0);
            changed = true;
        }

        return changed;
    }

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb != null)
        {
            TriggerBoost(rb);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        Rigidbody rb = collision.rigidbody;
        if (rb != null)
        {
            TriggerBoost(rb);
        }
    }

    public bool IsOnCooldownFor(Rigidbody rb = null)
    {
        if (rb != null && _targetCooldowns.TryGetValue(rb, out float t) && t > 0f)
            return true;
        return _cooldownTimer > 0f;
    }

    public bool TriggerBoost(Rigidbody rb)
    {
        if (rb == null) return false;
        if (IsOnCooldownFor(rb)) return false;

        Vector3 boostDir = transform.forward;
        if (boostDir.sqrMagnitude < 0.001f) boostDir = Vector3.forward;
        boostDir.Normalize();

        Vector3 currentVel = rb.linearVelocity;
        float currentForward = Vector3.Dot(currentVel, boostDir);
        float targetForward = Mathf.Max(currentForward + _boostForce, _boostForce);
        Vector3 lateral = currentVel - boostDir * currentForward;

        if (_maintainOrElevateY && lateral.y < _upwardForce)
        {
            lateral.y = _upwardForce;
        }

        rb.linearVelocity = boostDir * targetForward + lateral;

        // Civciv suzulme sayaci sifirlama
        ChickGlideController glide = rb.GetComponent<ChickGlideController>();
        if (glide != null)
        {
            glide.ResetAirJumps();
        }

        _cooldownTimer = _cooldown;
        _targetCooldowns[rb] = _cooldown;
        _boostCount++;

        PlayFeedback(rb);
        OnRingPassed?.Invoke(rb);
        return true;
    }

    private void PlayFeedback(Rigidbody rb)
    {
        EnsureVFXSetup();

        _pulseTimer = 0f;
        ApplyPulsePose(0f);

        if (_boostVFX != null)
        {
            _boostVFX.Play(true);
        }

        if (_trailRenderer != null)
        {
            _trailRenderer.emitting = true;
        }

        // Karakter uzerinde TrailRenderer
        TrailRenderer playerTrail = rb.GetComponentInChildren<TrailRenderer>();
        if (playerTrail == null)
        {
            GameObject tg = new GameObject("SpeedBoostTrail");
            tg.transform.SetParent(rb.transform, false);
            tg.transform.localPosition = Vector3.zero;
            playerTrail = tg.AddComponent<TrailRenderer>();
            playerTrail.time = _trailDuration;
            playerTrail.startWidth = 0.4f;
            playerTrail.endWidth = 0f;
            playerTrail.startColor = new Color(1f, 0.85f, 0.2f, 0.9f);
            playerTrail.endColor = new Color(1f, 0.45f, 0.05f, 0f);

            Shader s = Shader.Find("Sprites/Default");
            if (s != null) playerTrail.material = new Material(s);
        }

        playerTrail.emitting = true;
        _activeTrail = playerTrail;
        _trailTimer = _trailDuration;

        _sfxPlayCount++;
        if (_boostSfx != null)
        {
            SettingsUI.PlaySFX(_boostSfx, _sfxVolume);
        }
    }

    public void Step(float deltaTime)
    {
        StepFeedback(deltaTime);
    }

    public void StepFeedback(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        // Global Cooldown
        if (_cooldownTimer > 0f)
        {
            _cooldownTimer = Mathf.Max(0f, _cooldownTimer - deltaTime);
        }

        // Hedef bazli Cooldown
        if (_targetCooldowns.Count > 0)
        {
            _cooldownCleanupKeys.Clear();
            var keys = new List<Rigidbody>(_targetCooldowns.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                Rigidbody key = keys[i];
                if (key == null)
                {
                    _cooldownCleanupKeys.Add(key);
                    continue;
                }
                float rem = _targetCooldowns[key] - deltaTime;
                if (rem <= 0f)
                {
                    _cooldownCleanupKeys.Add(key);
                }
                else
                {
                    _targetCooldowns[key] = rem;
                }
            }

            for (int i = 0; i < _cooldownCleanupKeys.Count; i++)
            {
                _targetCooldowns.Remove(_cooldownCleanupKeys[i]);
            }
        }

        // Trail timer
        if (_trailTimer > 0f)
        {
            _trailTimer -= deltaTime;
            if (_trailTimer <= 0f)
            {
                if (_activeTrail != null) _activeTrail.emitting = false;
                if (_trailRenderer != null) _trailRenderer.emitting = false;
            }
        }

        // Prosedurel donus (Idle spin)
        if (_visualRoot != null && _idleSpinSpeed != 0f)
        {
            _visualRoot.Rotate(Vector3.forward, _idleSpinSpeed * deltaTime, Space.Self);
        }

        // Prosedurel squash-stretch / nabiz animasyonu
        if (_pulseTimer < 0f || _visualRoot == null) return;

        _pulseTimer += deltaTime;
        float norm = _pulseTimer / Mathf.Max(0.01f, _pulseDuration);
        if (norm >= 1f)
        {
            _pulseTimer = -1f;
            _visualRoot.localScale = _baseScale;
            return;
        }

        ApplyPulsePose(norm);
    }

    private void ApplyPulsePose(float norm)
    {
        if (_visualRoot == null) return;

        float scaleFactor;
        if (norm < 0.22f)
        {
            scaleFactor = Mathf.Lerp(1f, _pulseScale, norm / 0.22f);
        }
        else
        {
            float k = (norm - 0.22f) / 0.78f;
            scaleFactor = 1f + (_pulseScale - 1f) * Mathf.Exp(-4.5f * k) * Mathf.Cos(k * Mathf.PI * 3f);
        }

        if (norm == 0f) scaleFactor = _pulseScale;

        float zScale = 1f / Mathf.Sqrt(Mathf.Max(0.2f, scaleFactor));
        _visualRoot.localScale = new Vector3(_baseScale.x * scaleFactor, _baseScale.y * scaleFactor, _baseScale.z * zScale);
    }

    public void ResetVisualPoseImmediate()
    {
        _pulseTimer = -1f;
        if (_visualRoot != null)
        {
            _visualRoot.localScale = _baseScale;
        }
    }
}
