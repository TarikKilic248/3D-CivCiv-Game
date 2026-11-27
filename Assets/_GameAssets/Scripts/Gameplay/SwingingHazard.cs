using System;
using UnityEngine;

public class SwingingHazard : MonoBehaviour
{
    [Header("--- Swing Settings ---")]
    [SerializeField] private Transform _pivot;
    [SerializeField] private float _maxAngle = 55f;
    [SerializeField] private float _speed = 2f;
    [SerializeField] private float _phase = 0f;

    [Header("--- Hit Settings ---")]
    [SerializeField] private int _damage = 15;
    [SerializeField] private float _knockbackForce = 10f;
    [SerializeField] private float _knockbackUp = 5f;
    [SerializeField] private float _hitCooldown = 0.8f;

    [Header("--- Visuals ---")]
    [SerializeField] private Transform _visualRoot;
    [SerializeField] private float _squashAmount = 0.12f;
    [SerializeField] private float _shakeDuration = 0.2f;
    [SerializeField] private float _shakeMagnitude = 0.12f;
    [SerializeField] private ParticleSystem _hitVFX;
    [SerializeField] private TrailRenderer _trail;

    [Header("--- Audio ---")]
    [SerializeField] private AudioClip _whooshSfx;
    [SerializeField] private AudioClip _thudSfx;
    [SerializeField][Range(0f, 1f)] private float _sfxVolume = 0.9f;

    private float _time;
    private float _cooldownTimer;
    private float _shakeTimer;
    private float _lastVelocitySign = 1f;
    private Vector3 _baseScale = Vector3.one;
    private Vector3 _baseLocalPos;
    private int _hitCount;
    private int _sfxCount;
    private int _signChanges;

    public event Action<GameObject> OnPlayerHit;

    public float MaxAngle => _maxAngle;
    public float Speed => _speed;
    public float HitCooldown { get => _hitCooldown; set => _hitCooldown = value; }
    public float KnockbackForce => _knockbackForce;
    public int Damage => _damage;
    public bool IsOnCooldown => _cooldownTimer > 0f;
    public int HitCount => _hitCount;
    public int SfxPlayCount => _sfxCount;
    public int SignChanges => _signChanges;
    public float CurrentAngle { get; private set; }
    public ParticleSystem HitVFX => _hitVFX;
    public TrailRenderer Trail => _trail;
    public Transform Pivot => _pivot != null ? _pivot : transform;

    private void Awake()
    {
        EnsureVFXSetup();
        CacheBase();
    }

    private void Update()
    {
        Step(Time.deltaTime > 0f ? Time.deltaTime : 0.016f);
    }

    private void CacheBase()
    {
        Transform v = _visualRoot != null ? _visualRoot : Pivot;
        _baseScale = v.localScale;
        _baseLocalPos = v.localPosition;
    }

    public void EnsureVFXSetup()
    {
        Transform parent = _visualRoot != null ? _visualRoot : Pivot;
        if (_trail == null)
        {
            _trail = parent.GetComponentInChildren<TrailRenderer>();
            if (_trail == null)
            {
                GameObject go = new GameObject("SwingTrail");
                go.transform.SetParent(parent, false);
                _trail = go.AddComponent<TrailRenderer>();
                _trail.time = 0.35f;
                _trail.startWidth = 0.35f;
                _trail.endWidth = 0f;
                _trail.material = new Material(Shader.Find("Sprites/Default"));
                _trail.startColor = new Color(1f, 0.75f, 0.2f, 0.8f);
                _trail.endColor = new Color(0.8f, 0.3f, 0.05f, 0f);
            }
        }
        if (_hitVFX == null)
        {
            _hitVFX = parent.GetComponentInChildren<ParticleSystem>();
            if (_hitVFX == null)
            {
                GameObject go = new GameObject("SwingHitVFX");
                go.transform.SetParent(parent, false);
                _hitVFX = go.AddComponent<ParticleSystem>();
                var main = _hitVFX.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 0.5f;
                main.startLifetime = 0.5f;
                main.startSpeed = 5f;
                main.startSize = 0.12f;
                main.startColor = new Color(1f, 0.7f, 0.2f);
                var emission = _hitVFX.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
                var shape = _hitVFX.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.2f;
                var r = go.GetComponent<ParticleSystemRenderer>();
                if (r != null) r.material = new Material(Shader.Find("Sprites/Default"));
            }
        }
    }

    public static float ComputeAngle(float maxAngle, float t, float speed, float phase)
    {
        return maxAngle * Mathf.Sin(t * speed + phase);
    }

    public float AngularVelocitySign()
    {
        float c = Mathf.Cos(_time * _speed + _phase);
        return c >= 0f ? 1f : -1f;
    }

    public void Step(float deltaTime)
    {
        if (_cooldownTimer > 0f) _cooldownTimer = Mathf.Max(0f, _cooldownTimer - deltaTime);

        _time += deltaTime;
        CurrentAngle = ComputeAngle(_maxAngle, _time, _speed, _phase);
        Transform p = Pivot;
        Vector3 e = p.localEulerAngles;
        p.localRotation = Quaternion.Euler(e.x, e.y, CurrentAngle);

        float sign = AngularVelocitySign();
        if (!Mathf.Approximately(sign, _lastVelocitySign))
        {
            _lastVelocitySign = sign;
            _signChanges++;
            PlaySfx(_whooshSfx);
        }

        StepFeedback(deltaTime);
    }

    private void StepFeedback(float dt)
    {
        Transform v = _visualRoot != null ? _visualRoot : Pivot;
        if (_baseScale.sqrMagnitude < 0.0001f) CacheBase();

        float extremity = _maxAngle > 0.01f ? Mathf.Abs(CurrentAngle) / _maxAngle : 0f;
        float squash = Mathf.Pow(extremity, 4f) * _squashAmount;
        v.localScale = new Vector3(_baseScale.x * (1f + squash), _baseScale.y * (1f - squash), _baseScale.z * (1f + squash));

        if (_shakeTimer > 0f)
        {
            _shakeTimer = Mathf.Max(0f, _shakeTimer - dt);
            float k = _shakeDuration > 0f ? _shakeTimer / _shakeDuration : 0f;
            v.localPosition = _baseLocalPos + UnityEngine.Random.insideUnitSphere * (_shakeMagnitude * k);
        }
        else
        {
            v.localPosition = _baseLocalPos;
        }
    }

    private void OnTriggerEnter(Collider other) => HitTarget(other);
    private void OnTriggerStay(Collider other) => HitTarget(other);
    private void OnCollisionEnter(Collision c) => HitTarget(c.collider);
    private void OnCollisionStay(Collision c) => HitTarget(c.collider);

    public bool HitTarget(Collider other)
    {
        return other != null && HitTarget(other.gameObject);
    }

    public bool HitTarget(GameObject target)
    {
        if (target == null || _cooldownTimer > 0f) return false;

        IDamageable dmg = target.GetComponent<IDamageable>();
        if (dmg == null) dmg = target.GetComponentInParent<IDamageable>();
        if (dmg == null) return false;

        Vector3 dir = GetTangentDirection();
        _cooldownTimer = _hitCooldown;
        _hitCount++;
        _shakeTimer = _shakeDuration;

        dmg.TakeDamage(_damage, dir);

        Component comp = dmg as Component;
        Rigidbody rb = comp != null ? comp.GetComponent<Rigidbody>() : null;
        if (rb == null) rb = target.GetComponentInParent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            Vector3 v = dir * _knockbackForce;
            v.y = _knockbackUp;
            rb.linearVelocity = v;
        }

        if (_hitVFX != null)
        {
            _hitVFX.transform.position = target.transform.position;
            _hitVFX.Play();
        }
        PlaySfx(_thudSfx);
        OnPlayerHit?.Invoke(target);
        return true;
    }

    private Vector3 GetTangentDirection()
    {
        float sign = AngularVelocitySign();
        float rad = CurrentAngle * Mathf.Deg2Rad;
        // Bob konumu (sin, -cos) * L; teget = d/dtheta = (cos, sin)
        Vector3 local = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * sign;
        Transform p = Pivot;
        Vector3 world = p.parent != null ? p.parent.TransformDirection(local) : local;
        world.y = 0f;
        if (world.sqrMagnitude < 0.0001f)
            world = (p.parent != null ? p.parent.right : Vector3.right) * sign;
        return world.normalized;
    }

    private void PlaySfx(AudioClip clip)
    {
        _sfxCount++;
        if (clip != null) SettingsUI.PlaySFX(clip, _sfxVolume);
    }
}
