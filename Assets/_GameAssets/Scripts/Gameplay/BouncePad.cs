using System;
using UnityEngine;

public class BouncePad : MonoBehaviour
{
    [Header("--- Bounce Settings ---")]
    [SerializeField] private float _bounceForce = 16f;
    [SerializeField] private float _groundPoundMultiplier = 1.5f;
    [SerializeField] private float _cooldown = 0.4f;

    [Header("--- Visuals ---")]
    [SerializeField] private Transform _visualRoot;
    [SerializeField] private float _squashY = 0.55f;
    [SerializeField] private float _squashDuration = 0.45f;
    [SerializeField] private ParticleSystem _bounceVFX;
    [SerializeField] private float _trailDuration = 0.6f;

    [Header("--- Audio ---")]
    [SerializeField] private AudioClip _boingSfx;
    [SerializeField][Range(0f, 1f)] private float _sfxVolume = 0.9f;

    private float _cooldownTimer;
    private float _squashTimer = -1f;
    private float _trailTimer;
    private Vector3 _baseScale = Vector3.one;
    private TrailRenderer _activeTrail;
    private int _bounceCount;
    private int _sfxCount;

    public event Action<Rigidbody> OnBounced;

    public float BounceForce => _bounceForce;
    public float GroundPoundMultiplier => _groundPoundMultiplier;
    public float Cooldown => _cooldown;
    public bool IsOnCooldown => _cooldownTimer > 0f;
    public int BounceCount => _bounceCount;
    public int SfxPlayCount => _sfxCount;
    public Transform VisualRoot => _visualRoot;
    public ParticleSystem BounceVFX => _bounceVFX;
    public TrailRenderer ActiveTrail => _activeTrail;

    private void Awake()
    {
        if (_visualRoot == null) _visualRoot = transform;
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
        if (_visualRoot == null) { _visualRoot = transform; changed = true; }

        if (_bounceVFX == null)
        {
            GameObject go = new GameObject("BouncePadVFX");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.3f;
            _bounceVFX = go.AddComponent<ParticleSystem>();
            _bounceVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _bounceVFX.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.startLifetime = 0.7f;
            main.startSpeed = 4f;
            main.startSize = 0.18f;
            main.gravityModifier = 0.4f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.5f, 0.1f));
            var emission = _bounceVFX.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
            var shape = _bounceVFX.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = new Material(Shader.Find("Sprites/Default"));
            changed = true;
        }

        if (_boingSfx == null)
        {
            int rate = 22050;
            int len = (int)(rate * 0.3f);
            float[] data = new float[len];
            float phase = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float freq = 220f + 380f * Mathf.Sin(t * Mathf.PI);
                phase += 2f * Mathf.PI * freq / rate;
                data[i] = Mathf.Sin(phase) * (1f - t) * 0.5f;
            }
            _boingSfx = AudioClip.Create("BouncePadBoing", len, 1, rate, false);
            _boingSfx.SetData(data, 0);
            changed = true;
        }
        return changed;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Rigidbody rb = collision.rigidbody;
        if (rb != null && IsAbove(collision.collider)) ApplyBounce(rb);
    }

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb != null && IsAbove(other)) ApplyBounce(rb);
    }

    private bool IsAbove(Collider other)
    {
        return other.bounds.center.y >= transform.position.y;
    }

    public bool ApplyBounce(Rigidbody rb)
    {
        if (rb == null) return false;
        ChickPeckAttack attack = rb.GetComponent<ChickPeckAttack>();
        return ApplyBounce(rb, attack != null && attack.IsGroundPounding);
    }

    public bool ApplyBounce(Rigidbody rb, bool groundPound)
    {
        if (rb == null || _cooldownTimer > 0f) return false;

        float force = _bounceForce * (groundPound ? _groundPoundMultiplier : 1f);
        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        v.y = force;
        rb.linearVelocity = v;

        ChickGlideController glide = rb.GetComponent<ChickGlideController>();
        if (glide != null) glide.ResetAirJumps();

        _cooldownTimer = _cooldown;
        _bounceCount++;
        PlayFeedback(rb);
        OnBounced?.Invoke(rb);
        return true;
    }

    private void PlayFeedback(Rigidbody rb)
    {
        EnsureVFXSetup();
        _squashTimer = 0f;
        ApplySquashPose(0f);

        if (_bounceVFX != null) _bounceVFX.Play(true);

        TrailRenderer trail = rb.GetComponentInChildren<TrailRenderer>();
        if (trail == null)
        {
            GameObject tg = new GameObject("BounceTrail");
            tg.transform.SetParent(rb.transform, false);
            trail = tg.AddComponent<TrailRenderer>();
            trail.time = 0.4f;
            trail.startWidth = 0.35f;
            trail.endWidth = 0f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = new Color(1f, 0.85f, 0.2f, 0.9f);
            trail.endColor = new Color(1f, 0.45f, 0.1f, 0f);
        }
        trail.emitting = true;
        _activeTrail = trail;
        _trailTimer = _trailDuration;

        _sfxCount++;
        SettingsUI.PlaySFX(_boingSfx, _sfxVolume);
    }

    public void StepFeedback(float dt)
    {
        if (_cooldownTimer > 0f) _cooldownTimer = Mathf.Max(0f, _cooldownTimer - dt);

        if (_trailTimer > 0f)
        {
            _trailTimer -= dt;
            if (_trailTimer <= 0f && _activeTrail != null) _activeTrail.emitting = false;
        }

        if (_squashTimer < 0f || _visualRoot == null) return;
        _squashTimer += dt;
        float n = _squashTimer / Mathf.Max(0.01f, _squashDuration);
        if (n >= 1f)
        {
            _squashTimer = -1f;
            _visualRoot.localScale = _baseScale;
            return;
        }
        ApplySquashPose(n);
    }

    private void ApplySquashPose(float n)
    {
        if (_visualRoot == null) return;
        float y;
        if (n < 0.2f)
        {
            y = Mathf.Lerp(1f, _squashY, n / 0.2f);
        }
        else
        {
            float k = (n - 0.2f) / 0.8f;
            y = 1f + (_squashY - 1f) * Mathf.Exp(-5f * k) * Mathf.Cos(k * Mathf.PI * 3f);
        }
        if (n == 0f) y = _squashY;
        float xz = 1f / Mathf.Sqrt(Mathf.Max(0.1f, y));
        _visualRoot.localScale = new Vector3(_baseScale.x * xz, _baseScale.y * y, _baseScale.z * xz);
    }
}
