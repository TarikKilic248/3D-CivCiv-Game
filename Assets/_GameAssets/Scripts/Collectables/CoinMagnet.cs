using System;
using UnityEngine;

/// <summary>
/// CivCiv 3D - Altın Mıknatısı (CoinMagnet)
/// Belirli yarıçaptaki CoinCollect nesnelerini karaktere doğru çeker ve geçici Süper Mıknatıs modunu destekler.
/// Görsel geri bildirim (altın parçacık halesi) ve SFX kancaları içerir.
/// </summary>
public class CoinMagnet : MonoBehaviour
{
    private const string VFXRootName = "CoinMagnetVFX";

    [Header("--- Magnet Settings ---")]
    [Tooltip("Temel altın çekim yarıçapı (metre)")]
    [SerializeField] private float _magnetRadius = 5.5f;

    [Tooltip("Altınların karaktere doğru çekilme hızı (m/sn)")]
    [SerializeField] private float _pullSpeed = 12f;

    [Tooltip("Karakter merkezine göre dikey hedef ofseti")]
    [SerializeField] private float _targetHeightOffset = 0.8f;

    [Tooltip("Bu mesafeye ulaşan altın otomatik toplanır")]
    [SerializeField] private float _autoCollectDistance = 0.75f;

    [Header("--- Super Magnet Settings ---")]
    [Tooltip("Süper mıknatıs varsayılan yarıçap çarpanı")]
    [SerializeField] private float _defaultSuperRadiusMultiplier = 2.2f;

    [Tooltip("Süper mıknatıs varsayılan çekim hızı çarpanı")]
    [SerializeField] private float _defaultSuperSpeedMultiplier = 1.6f;

    [Header("--- VFX & Audio ---")]
    [SerializeField] private ParticleSystem _magnetAuraParticles;
    [SerializeField] private AudioClip _superMagnetSound;
    [SerializeField] private AudioClip _magnetPullSound;
    [SerializeField][Range(0f, 1f)] private float _sfxVolume = 0.75f;

    private bool _isSuperMagnetActive;
    private float _superMagnetTimer;
    private float _superRadiusMultiplier = 1f;
    private float _superSpeedMultiplier = 1f;
    private float _cacheRefreshTimer;
    private float _pullSoundCooldown;
    private CoinCollect[] _cachedCoins;

    public event Action<bool> OnSuperMagnetChanged;
    public event Action<CoinCollect> OnCoinPulled;

    public float BaseRadius { get => _magnetRadius; set => _magnetRadius = Mathf.Max(0.5f, value); }
    public float BasePullSpeed { get => _pullSpeed; set => _pullSpeed = Mathf.Max(0.5f, value); }
    public bool IsSuperMagnetActive => _isSuperMagnetActive;
    public float SuperMagnetTimeRemaining => _superMagnetTimer;
    public float EffectiveRadius => _isSuperMagnetActive ? _magnetRadius * _superRadiusMultiplier : _magnetRadius;
    public float EffectivePullSpeed => _isSuperMagnetActive ? _pullSpeed * _superSpeedMultiplier : _pullSpeed;
    public ParticleSystem MagnetAuraParticles => _magnetAuraParticles;
    public int PulledCoinCountLastStep { get; private set; }

    private void Awake()
    {
        EnsureVFXSetup();
        RefreshCoinCache();
    }

    private void Update()
    {
        if (SettingsUI.IsOpen || Time.timeScale <= 0f)
            return;

        StepMagnet(Time.deltaTime);
    }

    /// <summary>
    /// Geçici Süper Mıknatıs modunu başlatır (daha geniş yarıçap ve daha hızlı çekim + altın hale VFX).
    /// </summary>
    public void ActivateSuperMagnet(float duration, float radiusMultiplier = -1f, float speedMultiplier = -1f)
    {
        if (duration <= 0f)
            return;

        bool wasActive = _isSuperMagnetActive;
        _isSuperMagnetActive = true;
        _superMagnetTimer = Mathf.Max(_superMagnetTimer, duration);
        _superRadiusMultiplier = radiusMultiplier > 1f ? radiusMultiplier : _defaultSuperRadiusMultiplier;
        _superSpeedMultiplier = speedMultiplier > 1f ? speedMultiplier : _defaultSuperSpeedMultiplier;

        EnsureVFXSetup();
        if (_magnetAuraParticles != null && !_magnetAuraParticles.isPlaying)
        {
            _magnetAuraParticles.Play();
        }

        if (_superMagnetSound != null)
        {
            SettingsUI.PlaySFX(_superMagnetSound, _sfxVolume);
        }

        if (!wasActive)
        {
            OnSuperMagnetChanged?.Invoke(true);
        }
    }

    public void DeactivateSuperMagnet()
    {
        if (!_isSuperMagnetActive)
            return;

        _isSuperMagnetActive = false;
        _superMagnetTimer = 0f;
        _superRadiusMultiplier = 1f;
        _superSpeedMultiplier = 1f;

        if (_magnetAuraParticles != null && _magnetAuraParticles.isPlaying)
        {
            _magnetAuraParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        OnSuperMagnetChanged?.Invoke(false);
    }

    public void RefreshCoinCache()
    {
        _cachedCoins = FindObjectsByType<CoinCollect>(FindObjectsInactive.Include);
        _cacheRefreshTimer = 0.25f;
    }

    /// <summary>
    /// Tek adım mıknatıs güncellemesi: süper mıknatıs süresini azaltır ve menzildeki altınları çeker.
    /// </summary>
    public int StepMagnet(float deltaTime)
    {
        PulledCoinCountLastStep = 0;
        if (deltaTime <= 0f)
            return 0;

        if (_isSuperMagnetActive)
        {
            _superMagnetTimer -= deltaTime;
            if (_superMagnetTimer <= 0f)
            {
                DeactivateSuperMagnet();
            }
        }

        if (_pullSoundCooldown > 0f)
        {
            _pullSoundCooldown -= deltaTime;
        }

        _cacheRefreshTimer -= deltaTime;
        if (_cachedCoins == null || _cacheRefreshTimer <= 0f)
        {
            RefreshCoinCache();
        }

        if (_cachedCoins == null || _cachedCoins.Length == 0)
            return 0;

        Vector3 targetPos = transform.position + Vector3.up * _targetHeightOffset;
        float radius = EffectiveRadius;
        float radiusSq = radius * radius;
        float speed = EffectivePullSpeed;
        int pulledCount = 0;

        for (int i = 0; i < _cachedCoins.Length; i++)
        {
            CoinCollect coin = _cachedCoins[i];
            if (coin == null || coin.IsCollected || !coin.gameObject.activeInHierarchy)
                continue;

            Vector3 coinPos = coin.transform.position;
            Vector3 toTarget = targetPos - coinPos;
            float distSq = toTarget.sqrMagnitude;
            if (distSq > radiusSq)
                continue;

            float dist = Mathf.Sqrt(distSq);
            float proximityBoost = 1f + (1f - Mathf.Clamp01(dist / Mathf.Max(0.01f, radius))) * 0.75f;
            float step = speed * proximityBoost * deltaTime;

            coin.transform.position = Vector3.MoveTowards(coinPos, targetPos, step);
            pulledCount++;
            OnCoinPulled?.Invoke(coin);

            if (Vector3.Distance(coin.transform.position, targetPos) <= _autoCollectDistance)
            {
                coin.Collect();
            }
        }

        PulledCoinCountLastStep = pulledCount;

        if (pulledCount > 0)
        {
            if (_magnetAuraParticles != null && !_magnetAuraParticles.isPlaying)
            {
                _magnetAuraParticles.Play();
            }

            if (_magnetPullSound != null && _pullSoundCooldown <= 0f)
            {
                SettingsUI.PlaySFX(_magnetPullSound, _sfxVolume * 0.6f);
                _pullSoundCooldown = 0.35f;
            }
        }
        else if (!_isSuperMagnetActive && _magnetAuraParticles != null && _magnetAuraParticles.isPlaying)
        {
            _magnetAuraParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        return pulledCount;
    }

    public bool EnsureVFXSetup()
    {
        if (_magnetAuraParticles != null)
            return false;

        Transform existingRoot = transform.Find(VFXRootName);
        GameObject vfxObj;
        if (existingRoot != null)
        {
            vfxObj = existingRoot.gameObject;
            _magnetAuraParticles = vfxObj.GetComponent<ParticleSystem>();
            if (_magnetAuraParticles != null)
                return false;
        }
        else
        {
            vfxObj = new GameObject(VFXRootName);
            vfxObj.transform.SetParent(transform, false);
            vfxObj.transform.localPosition = new Vector3(0f, _targetHeightOffset, 0f);
        }

        _magnetAuraParticles = vfxObj.AddComponent<ParticleSystem>();
        var main = _magnetAuraParticles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = 0.55f;
        main.startSpeed = 1.4f;
        main.startSize = 0.16f;
        main.startColor = new Color(1f, 0.835f, 0.310f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = _magnetAuraParticles.emission;
        emission.rateOverTime = 18f;

        var shape = _magnetAuraParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.1f;

        _magnetAuraParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return true;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        EnsureVFXSetup();
    }

    private void OnValidate()
    {
        _magnetRadius = Mathf.Max(0.5f, _magnetRadius);
        _pullSpeed = Mathf.Max(0.5f, _pullSpeed);
        _autoCollectDistance = Mathf.Max(0.1f, _autoCollectDistance);
        _defaultSuperRadiusMultiplier = Mathf.Max(1.1f, _defaultSuperRadiusMultiplier);
        _defaultSuperSpeedMultiplier = Mathf.Max(1.0f, _defaultSuperSpeedMultiplier);
    }
#endif
}
