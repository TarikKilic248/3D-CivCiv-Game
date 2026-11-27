using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WindCurrentZone : MonoBehaviour
{
    [Header("--- Wind ---")]
    [SerializeField] private float _liftAcceleration = 18f;
    [SerializeField] private float _glideMultiplier = 2f;
    [SerializeField] private float _maxUpSpeed = 8f;

    [Header("--- Feedback ---")]
    [SerializeField] private ParticleSystem _windVFX;
    [SerializeField] private AudioClip _windSfx;
    [SerializeField] private float _sfxVolume = 0.4f;
    [SerializeField] private float _sfxInterval = 1.5f;

    private float _sfxTimer;

    public float LiftAcceleration => _liftAcceleration;
    public float GlideMultiplier => _glideMultiplier;
    public float MaxUpSpeed => _maxUpSpeed;
    public int SfxPlayCount { get; private set; }
    public ParticleSystem WindVFX => _windVFX;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
        EnsureVFXSetup();
    }

    public bool EnsureVFXSetup()
    {
        if (_windVFX != null) return true;
        GameObject go = new GameObject("WindCurrentVFX");
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        _windVFX = go.AddComponent<ParticleSystem>();
        _windVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = _windVFX.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = 1.2f;
        main.startSpeed = 4f;
        main.startSize = 0.12f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.7f, 0.8f), new Color(1f, 0.8f, 0.4f, 0.6f));
        var emission = _windVFX.emission;
        emission.rateOverTime = 14f;
        var shape = _windVFX.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1f, 1f, 0.1f);
        return true;
    }

    private void OnTriggerStay(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null || rb.GetComponent<PlayerController>() == null) return;
        ApplyWind(rb, Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f);
    }

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null || rb.GetComponent<PlayerController>() == null) return;
        if (_windVFX != null && !_windVFX.isPlaying) _windVFX.Play();
    }

    private void OnTriggerExit(Collider other)
    {
        if (_windVFX != null && other.attachedRigidbody != null && other.attachedRigidbody.GetComponent<PlayerController>() != null)
            _windVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    public float ApplyWind(Rigidbody rb, float dt)
    {
        if (rb == null) return 0f;
        ChickGlideController glide = rb.GetComponent<ChickGlideController>();
        float accel = _liftAcceleration * (glide != null && glide.IsGliding ? _glideMultiplier : 1f);

        Vector3 vel = rb.linearVelocity;
        vel.y = Mathf.Min(vel.y + accel * dt, Mathf.Max(vel.y, _maxUpSpeed));
        rb.linearVelocity = vel;

        if (_windVFX != null && !_windVFX.isPlaying) _windVFX.Play();
        _sfxTimer -= dt;
        if (_sfxTimer <= 0f)
        {
            _sfxTimer = _sfxInterval;
            SettingsUI.PlaySFX(_windSfx, _sfxVolume);
            SfxPlayCount++;
        }
        return accel;
    }
}
