using System;
using UnityEngine;

/// <summary>
/// CivCiv 3D - Dinamik Gece/Gündüz Döngüsü (DayNightCycleController)
/// Directional Light açısını, yoğunluğunu ve rengini zamana göre değiştirir; gündüz/gece geçişlerinde
/// ateş böceği VFX'i, hafif güneş salınımı ve SettingsUI.PlaySFX ses kancası ile geri bildirim verir.
/// </summary>
[DisallowMultipleComponent]
public class DayNightCycleController : MonoBehaviour
{
    public static DayNightCycleController Instance { get; private set; }

    [Header("--- Sun ---")]
    [SerializeField] private Light _sun;
    [Tooltip("Güneşin yatay yön açısı (Y ekseni)")]
    [SerializeField] private float _sunYaw = -30f;

    [Header("--- Time ---")]
    [Tooltip("Tam bir gün (24 saat) kaç saniye sürer")]
    [SerializeField, Min(1f)] private float _dayLengthSeconds = 180f;
    [Tooltip("0 = gece yarısı, 0.25 = gün doğumu, 0.5 = öğle, 0.75 = gün batımı")]
    [SerializeField, Range(0f, 1f)] private float _timeOfDay = 0.35f;
    [SerializeField] private bool _autoAdvance = true;

    [Header("--- Lighting ---")]
    [SerializeField] private float _dayIntensity = 1.2f;
    [SerializeField] private float _nightIntensity = 0.08f;
    [SerializeField] private Color _dayColor = new Color(1f, 0.93f, 0.78f, 1f);
    [SerializeField] private Color _sunsetColor = new Color(1f, 0.55f, 0.2f, 1f);
    [SerializeField] private Color _nightColor = new Color(0.35f, 0.42f, 0.75f, 1f);
    [SerializeField] private bool _driveAmbient = true;
    [SerializeField] private Color _dayAmbient = new Color(0.85f, 0.78f, 0.62f, 1f);
    [SerializeField] private Color _nightAmbient = new Color(0.12f, 0.13f, 0.25f, 1f);

    [Header("--- Feedback ---")]
    [SerializeField] private ParticleSystem _nightFirefliesVFX;
    [SerializeField] private AudioClip _sunriseSfx;
    [SerializeField] private AudioClip _nightfallSfx;
    [Tooltip("Güneşin hafif salınım (wobble) genliği (derece)")]
    [SerializeField] private float _sunWobbleAmplitude = 1.5f;

    private bool _isDay;
    private bool _stateInitialized;
    private float _wobbleTime;

    public event Action<float> OnTimeOfDayChanged;
    public event Action<bool> OnDayStateChanged;

    public Light Sun { get => _sun; set => _sun = value; }
    public float TimeOfDay => _timeOfDay;
    public float DayLengthSeconds { get => _dayLengthSeconds; set => _dayLengthSeconds = Mathf.Max(1f, value); }
    public bool AutoAdvance { get => _autoAdvance; set => _autoAdvance = value; }
    public bool IsDay => _isDay;
    public int DaysElapsed { get; private set; }
    public ParticleSystem NightFirefliesVFX => _nightFirefliesVFX;

    /// <summary>Güneşin ufka göre normalize yüksekliği (-1 gece yarısı .. 1 öğle).</summary>
    public float SunElevation => Mathf.Sin((_timeOfDay - 0.25f) * Mathf.PI * 2f);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        ResolveSun();
        EnsureVFXSetup();
        ApplyLighting(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_autoAdvance) Step(Time.deltaTime);
    }

    /// <summary>Sahnedeki Directional Light'ı (RenderSettings.sun öncelikli) bulur.</summary>
    public Light ResolveSun()
    {
        if (_sun != null) return _sun;
        if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional)
        {
            _sun = RenderSettings.sun;
            return _sun;
        }
        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].type == LightType.Directional)
            {
                _sun = lights[i];
                break;
            }
        }
        return _sun;
    }

    /// <summary>Zamanı deltaTime kadar ilerletir ve ışığı günceller.</summary>
    public void Step(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        float advanced = _timeOfDay + deltaTime / Mathf.Max(1f, _dayLengthSeconds);
        if (advanced >= 1f) DaysElapsed += Mathf.FloorToInt(advanced);
        _timeOfDay = Mathf.Repeat(advanced, 1f);
        _wobbleTime += deltaTime;
        ApplyLighting(true);
        StepFeedback(deltaTime);
        OnTimeOfDayChanged?.Invoke(_timeOfDay);
    }

    public void SetTimeOfDay(float normalizedTime, bool triggerFeedback = false)
    {
        _timeOfDay = Mathf.Repeat(normalizedTime, 1f);
        ApplyLighting(triggerFeedback);
        OnTimeOfDayChanged?.Invoke(_timeOfDay);
    }

    /// <summary>Işık rotasyonu, yoğunluk, renk ve gündüz/gece durumunu uygular.</summary>
    public void ApplyLighting(bool triggerFeedback)
    {
        float elevation = SunElevation;
        float dayFactor = Mathf.Clamp01(elevation * 2.5f + 0.15f);

        if (ResolveSun() != null)
        {
            float wobble = Mathf.Sin(_wobbleTime * 0.7f) * _sunWobbleAmplitude;
            float pitch = _timeOfDay * 360f - 90f;
            _sun.transform.rotation = Quaternion.Euler(pitch, _sunYaw + wobble, 0f);
            _sun.intensity = Mathf.Lerp(_nightIntensity, _dayIntensity, dayFactor);
            float sunsetBlend = 1f - Mathf.Clamp01(Mathf.Abs(elevation) * 4f);
            Color baseColor = Color.Lerp(_nightColor, _dayColor, dayFactor);
            _sun.color = Color.Lerp(baseColor, _sunsetColor, sunsetBlend * dayFactor);
        }

        if (_driveAmbient)
        {
            RenderSettings.ambientLight = Color.Lerp(_nightAmbient, _dayAmbient, dayFactor);
        }

        bool newIsDay = elevation > 0f;
        if (!_stateInitialized)
        {
            _stateInitialized = true;
            _isDay = newIsDay;
            UpdateFireflies(false);
            return;
        }

        if (newIsDay != _isDay)
        {
            _isDay = newIsDay;
            UpdateFireflies(triggerFeedback);
            if (triggerFeedback)
            {
                SettingsUI.PlaySFX(_isDay ? _sunriseSfx : _nightfallSfx, 0.8f);
            }
            OnDayStateChanged?.Invoke(_isDay);
        }
    }

    private void UpdateFireflies(bool animate)
    {
        if (_nightFirefliesVFX == null) return;
        var emission = _nightFirefliesVFX.emission;
        emission.enabled = !_isDay;
        if (!_isDay && animate && Application.isPlaying && !_nightFirefliesVFX.isPlaying)
        {
            _nightFirefliesVFX.Play();
        }
    }

    /// <summary>Ateş böceklerini oyuncu/kamera etrafında yumuşak salınımla taşır.</summary>
    private void StepFeedback(float deltaTime)
    {
        if (_nightFirefliesVFX == null) return;
        Transform follow = Camera.main != null ? Camera.main.transform : null;
        Vector3 basePos = follow != null ? follow.position : transform.position;
        Vector3 target = basePos + new Vector3(0f, 1.5f + Mathf.Sin(_wobbleTime * 1.3f) * 0.4f, 0f);
        Transform vfxTr = _nightFirefliesVFX.transform;
        vfxTr.position = Vector3.Lerp(vfxTr.position, target, 1f - Mathf.Exp(-3f * deltaTime));
        vfxTr.Rotate(Vector3.up, 12f * deltaTime, Space.World);
    }

    /// <summary>Gece ateş böceği partikül sistemini idempotent olarak kurar.</summary>
    public bool EnsureVFXSetup()
    {
        if (_nightFirefliesVFX != null) return false;
        Transform existing = transform.Find("NightFirefliesVFX");
        if (existing != null && (_nightFirefliesVFX = existing.GetComponent<ParticleSystem>()) != null) return false;

        GameObject vfxObj = new GameObject("NightFirefliesVFX");
        vfxObj.transform.SetParent(transform, false);
        _nightFirefliesVFX = vfxObj.AddComponent<ParticleSystem>();
        var main = _nightFirefliesVFX.main;
        main.loop = true;
        main.startLifetime = 3.5f;
        main.startSpeed = 0.3f;
        main.startSize = 0.12f;
        main.maxParticles = 120;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(1f, 0.85f, 0.3f, 1f);
        var emission = _nightFirefliesVFX.emission;
        emission.rateOverTime = 18f;
        emission.enabled = false;
        var shape = _nightFirefliesVFX.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 12f;
        var noise = _nightFirefliesVFX.noise;
        noise.enabled = true;
        noise.strength = 0.6f;
        noise.frequency = 0.4f;
        ParticleSystemRenderer psr = vfxObj.GetComponent<ParticleSystemRenderer>();
        if (psr != null) psr.renderMode = ParticleSystemRenderMode.Billboard;
        return true;
    }
}
