using UnityEngine;

[DisallowMultipleComponent]
public class FallingPlatform : MonoBehaviour
{
    public enum PlatformState { Idle, Shaking, Falling, Respawning }

    [Header("--- Timing ---")]
    [SerializeField] private float _fallDelay = 0.6f;
    [SerializeField] private float _respawnDelay = 3f;
    [SerializeField] private float _fallGravity = 20f;

    [Header("--- Feedback ---")]
    [SerializeField] private Transform _visualRoot;
    [SerializeField] private float _shakeAmplitude = 0.06f;
    [SerializeField] private ParticleSystem _crumbleVFX;
    [SerializeField] private AudioClip _shakeSfx;
    [SerializeField] private AudioClip _respawnSfx;
    [SerializeField] private float _sfxVolume = 0.6f;

    private PlatformState _state = PlatformState.Idle;
    private Vector3 _startPosition;
    private float _timer;
    private float _fallVelocity;
    private bool _initialized;

    public PlatformState State => _state;
    public Vector3 StartPosition => _startPosition;
    public float FallDelay => _fallDelay;
    public float RespawnDelay => _respawnDelay;
    public int SfxPlayCount { get; private set; }
    public ParticleSystem CrumbleVFX => _crumbleVFX;
    public Transform VisualRoot => _visualRoot != null ? _visualRoot : transform;

    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        _startPosition = transform.position;
        EnsureVFXSetup();
    }

    public bool EnsureVFXSetup()
    {
        if (_crumbleVFX != null) return true;
        GameObject go = new GameObject("FallingPlatformCrumble");
        go.transform.SetParent(transform, false);
        _crumbleVFX = go.AddComponent<ParticleSystem>();
        _crumbleVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = _crumbleVFX.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.7f;
        main.startSpeed = 1.5f;
        main.startSize = 0.15f;
        main.gravityModifier = 1f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.5f, 0.15f), new Color(1f, 0.75f, 0.25f));
        var emission = _crumbleVFX.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });
        return true;
    }

    private void OnCollisionEnter(Collision c)
    {
        if (c.rigidbody != null && c.rigidbody.GetComponent<PlayerController>() != null) Trigger();
    }

    private void FixedUpdate()
    {
        Step(Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f);
    }

    public bool Trigger()
    {
        Initialize();
        if (_state != PlatformState.Idle) return false;
        _state = PlatformState.Shaking;
        _timer = 0f;
        if (_crumbleVFX != null) _crumbleVFX.Play();
        SettingsUI.PlaySFX(_shakeSfx, _sfxVolume);
        SfxPlayCount++;
        return true;
    }

    public void Step(float dt)
    {
        Initialize();
        switch (_state)
        {
            case PlatformState.Shaking:
                _timer += dt;
                float phase = _timer * 60f;
                VisualRoot.localPosition = new Vector3(Mathf.Sin(phase), 0f, Mathf.Cos(phase * 1.3f)) * _shakeAmplitude;
                VisualRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 0.7f) * 2f);
                if (_timer >= _fallDelay)
                {
                    VisualRoot.localPosition = Vector3.zero;
                    VisualRoot.localRotation = Quaternion.identity;
                    _state = PlatformState.Falling;
                    _timer = 0f;
                    _fallVelocity = 0f;
                }
                break;
            case PlatformState.Falling:
                _timer += dt;
                _fallVelocity += _fallGravity * dt;
                transform.position += Vector3.down * (_fallVelocity * dt);
                if (_timer >= _respawnDelay)
                {
                    transform.position = _startPosition;
                    _fallVelocity = 0f;
                    _state = PlatformState.Idle;
                    if (_crumbleVFX != null) _crumbleVFX.Play();
                    SettingsUI.PlaySFX(_respawnSfx, _sfxVolume);
                    SfxPlayCount++;
                }
                break;
        }
    }
}
