using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MovingPlatform : MonoBehaviour
{
    [Header("--- Movement ---")]
    [SerializeField] private Vector3 _localOffset = new Vector3(4f, 0f, 0f);
    [SerializeField] private float _speed = 2f;
    [SerializeField] private float _waitTime = 0.4f;

    [Header("--- Feedback ---")]
    [SerializeField] private Transform _visualRoot;
    [SerializeField] private float _tiltAmount = 4f;
    [SerializeField] private ParticleSystem _dustVFX;
    [SerializeField] private AudioClip _moveSfx;
    [SerializeField] private float _sfxVolume = 0.5f;

    private Vector3 _pointA;
    private Vector3 _pointB;
    private bool _toB = true;
    private float _waitTimer;
    private Rigidbody _rb;
    private readonly HashSet<Rigidbody> _riders = new HashSet<Rigidbody>();
    private bool _initialized;

    public Vector3 PointA => _pointA;
    public Vector3 PointB => _pointB;
    public Vector3 LastDelta { get; private set; }
    public int SfxPlayCount { get; private set; }
    public int RiderCount => _riders.Count;
    public ParticleSystem DustVFX => _dustVFX;
    public Transform VisualRoot => _visualRoot != null ? _visualRoot : transform;

    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        _pointA = transform.position;
        _pointB = _pointA + transform.TransformVector(_localOffset);
        _rb = GetComponent<Rigidbody>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity = false;
        EnsureVFXSetup();
    }

    public bool EnsureVFXSetup()
    {
        if (_dustVFX != null) return true;
        GameObject go = new GameObject("MovingPlatformDust");
        go.transform.SetParent(transform, false);
        _dustVFX = go.AddComponent<ParticleSystem>();
        _dustVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = _dustVFX.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.5f;
        main.startSpeed = 1f;
        main.startSize = 0.2f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.5f, 0.1f));
        var emission = _dustVFX.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });
        return true;
    }

    private void FixedUpdate()
    {
        Step(Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f);
    }

    public void RegisterRider(Rigidbody rb) { if (rb != null) _riders.Add(rb); }
    public void UnregisterRider(Rigidbody rb) { if (rb != null) _riders.Remove(rb); }

    private void OnCollisionEnter(Collision c)
    {
        if (c.rigidbody != null && c.rigidbody.GetComponent<PlayerController>() != null) RegisterRider(c.rigidbody);
    }

    private void OnCollisionExit(Collision c)
    {
        if (c.rigidbody != null) UnregisterRider(c.rigidbody);
    }

    public void Step(float dt)
    {
        Initialize();
        Vector3 before = transform.position;
        if (_waitTimer > 0f)
        {
            _waitTimer -= dt;
            LastDelta = Vector3.zero;
            StepTilt(dt, 0f);
            return;
        }

        Vector3 target = _toB ? _pointB : _pointA;
        Vector3 next = Vector3.MoveTowards(before, target, _speed * dt);
        transform.position = next;
        _rb.position = next;
        LastDelta = next - before;

        foreach (Rigidbody rider in _riders)
        {
            if (rider != null) rider.position += LastDelta;
        }

        if ((next - target).sqrMagnitude < 0.0001f)
        {
            _toB = !_toB;
            _waitTimer = _waitTime;
            if (_dustVFX != null) _dustVFX.Play();
            SettingsUI.PlaySFX(_moveSfx, _sfxVolume);
            SfxPlayCount++;
        }
        StepTilt(dt, LastDelta.magnitude / Mathf.Max(0.0001f, dt));
    }

    private void StepTilt(float dt, float speed)
    {
        Transform v = VisualRoot;
        if (v == transform) return;
        float t = Mathf.Clamp01(speed / Mathf.Max(0.01f, _speed));
        Quaternion goal = Quaternion.Euler(0f, 0f, (_toB ? -1f : 1f) * _tiltAmount * t);
        v.localRotation = Quaternion.Slerp(v.localRotation, goal, Mathf.Clamp01(dt * 8f));
    }
}
