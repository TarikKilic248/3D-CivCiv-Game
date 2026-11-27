using UnityEngine;

/// <summary>
/// Karakterin hareket olaylarına (Zıplama, İniş, Kayma, Dash) bağlanıp
/// ObjectPoolManager üzerinden toz, tüy ve darbe efektleri tetikleyen kontrolcü.
/// Eksik prefab durumlarında hata fırlatmaz, güvenli fallback sunar.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerVFXController : MonoBehaviour
{
    [Header("--- References ---")]
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private DashController _dashController;
    [SerializeField] private Transform _vfxSpawnPoint;

    [Header("--- VFX Prefabs (Pooled) ---")]
    [Tooltip("Zıplama anında ayak altında beliren toz/tüy efekti")]
    [SerializeField] private GameObject _jumpVFXPrefab;

    [Tooltip("Yere iniş anında yayılan toz/darbe halkası efekti")]
    [SerializeField] private GameObject _landVFXPrefab;

    [Tooltip("Kayma (Slide) anında zeminde oluşan toz/sürtünme izi efekti")]
    [SerializeField] private GameObject _slideVFXPrefab;

    [Tooltip("Dash anında arkada beliren rüzgar/tüy efekti")]
    [SerializeField] private GameObject _dashVFXPrefab;

    [Header("--- Fallback / Procedural VFX ---")]
    [SerializeField] private ParticleSystem _fallbackDustParticles;

    [Header("--- VFX Settings ---")]
    [SerializeField] private float _defaultVFXLifetime = 1.2f;

    [Header("--- Audio Hooks ---")]
    [SerializeField] private AudioClip _jumpSfx;
    [SerializeField] private AudioClip _landSfx;
    [SerializeField] private AudioClip _slideSfx;
    [SerializeField] private AudioClip _dashSfx;
    [SerializeField][Range(0f, 1f)] private float _sfxVolume = 0.8f;

    // Telemetri Sayaçları (Doğrulama ve davranış kontrolü için)
    private int _jumpVFXCount;
    private int _landVFXCount;
    private int _slideVFXCount;
    private int _dashVFXCount;

    private bool _isSubscribed;

    // Public Özellikler
    public PlayerController PlayerController => _playerController;
    public DashController DashController => _dashController;
    public GameObject JumpVFXPrefab { get => _jumpVFXPrefab; set => _jumpVFXPrefab = value; }
    public GameObject LandVFXPrefab { get => _landVFXPrefab; set => _landVFXPrefab = value; }
    public GameObject SlideVFXPrefab { get => _slideVFXPrefab; set => _slideVFXPrefab = value; }
    public GameObject DashVFXPrefab { get => _dashVFXPrefab; set => _dashVFXPrefab = value; }
    public ParticleSystem FallbackDustParticles => _fallbackDustParticles;

    public int JumpVFXCount => _jumpVFXCount;
    public int LandVFXCount => _landVFXCount;
    public int SlideVFXCount => _slideVFXCount;
    public int DashVFXCount => _dashVFXCount;
    public int TotalVFXCount => _jumpVFXCount + _landVFXCount + _slideVFXCount + _dashVFXCount;

    private void Awake()
    {
        ResolveDependencies();
        EnsureVFXSetup();
    }

    private void OnEnable()
    {
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    public void ResolveDependencies()
    {
        if (_playerController == null)
            _playerController = GetComponent<PlayerController>();

        if (_dashController == null)
            _dashController = GetComponent<DashController>();

        if (_vfxSpawnPoint == null)
        {
            var feet = transform.Find("FeetPoint");
            if (feet != null)
            {
                _vfxSpawnPoint = feet;
            }
        }
    }

    public void SubscribeEvents()
    {
        if (_isSubscribed) return;
        ResolveDependencies();

        if (_playerController != null)
        {
            _playerController.OnJump += HandleJump;
            _playerController.OnLanded += HandleLanded;
            _playerController.OnSlideStarted += HandleSlide;
        }

        if (_dashController != null)
        {
            _dashController.OnDashStarted += HandleDash;
        }

        _isSubscribed = true;
    }

    public void UnsubscribeEvents()
    {
        if (!_isSubscribed) return;

        if (_playerController != null)
        {
            _playerController.OnJump -= HandleJump;
            _playerController.OnLanded -= HandleLanded;
            _playerController.OnSlideStarted -= HandleSlide;
        }

        if (_dashController != null)
        {
            _dashController.OnDashStarted -= HandleDash;
        }

        _isSubscribed = false;
    }

    public void HandleJump() => TriggerJumpVFX();
    public void HandleLanded() => TriggerLandVFX();
    public void HandleSlide() => TriggerSlideVFX();
    public void HandleDash() => TriggerDashVFX();

    public Vector3 GetSpawnPosition()
    {
        if (_vfxSpawnPoint != null)
            return _vfxSpawnPoint.position;

        var col = GetComponent<Collider>();
        if (col != null)
        {
            return new Vector3(col.bounds.center.x, col.bounds.min.y, col.bounds.center.z);
        }

        return transform.position;
    }

    public GameObject TriggerJumpVFX(Vector3? customPos = null)
    {
        _jumpVFXCount++;
        if (_jumpSfx != null) SettingsUI.PlaySFX(_jumpSfx, _sfxVolume);

        Vector3 pos = customPos ?? GetSpawnPosition();
        return SpawnOrFallback(_jumpVFXPrefab, pos, Quaternion.identity);
    }

    public GameObject TriggerLandVFX(Vector3? customPos = null)
    {
        _landVFXCount++;
        if (_landSfx != null) SettingsUI.PlaySFX(_landSfx, _sfxVolume);

        Vector3 pos = customPos ?? GetSpawnPosition();
        return SpawnOrFallback(_landVFXPrefab, pos, Quaternion.identity);
    }

    public GameObject TriggerSlideVFX(Vector3? customPos = null)
    {
        _slideVFXCount++;
        if (_slideSfx != null) SettingsUI.PlaySFX(_slideSfx, _sfxVolume);

        Vector3 pos = customPos ?? GetSpawnPosition();
        return SpawnOrFallback(_slideVFXPrefab, pos, transform.rotation);
    }

    public GameObject TriggerDashVFX(Vector3? customPos = null)
    {
        _dashVFXCount++;
        if (_dashSfx != null) SettingsUI.PlaySFX(_dashSfx, _sfxVolume);

        Vector3 pos = customPos ?? GetSpawnPosition();
        return SpawnOrFallback(_dashVFXPrefab, pos, transform.rotation);
    }

    private GameObject SpawnOrFallback(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab != null)
        {
            GameObject instance = ObjectPoolManager.Spawn(prefab, position, rotation);
            if (instance != null)
            {
                ObjectPoolManager.Return(instance, _defaultVFXLifetime);
            }
            return instance;
        }

        // Eksik prefab durumu: Hata fırlatma, varsa fallback parçacığı oynat
        if (_fallbackDustParticles != null)
        {
            _fallbackDustParticles.transform.position = position;
            _fallbackDustParticles.Play();
        }

        return null;
    }

    public bool EnsureVFXSetup()
    {
        bool changed = false;

        if (_vfxSpawnPoint == null)
        {
            Transform existingFeet = transform.Find("FeetPoint");
            if (existingFeet == null)
            {
                GameObject feetObj = new GameObject("FeetPoint");
                feetObj.transform.SetParent(transform, false);
                feetObj.transform.localPosition = Vector3.zero;
                _vfxSpawnPoint = feetObj.transform;
            }
            else
            {
                _vfxSpawnPoint = existingFeet;
            }
            changed = true;
        }

        if (_fallbackDustParticles == null)
        {
            Transform existingFallback = transform.Find("MovementDustFallback");
            if (existingFallback == null)
            {
                GameObject dustObj = new GameObject("MovementDustFallback");
                dustObj.transform.SetParent(transform, false);
                dustObj.transform.localPosition = Vector3.zero;

                var ps = dustObj.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 0.5f;
                main.startLifetime = 0.4f;
                main.startSpeed = 1.2f;
                main.startSize = 0.35f;
                main.startColor = new Color(0.95f, 0.85f, 0.45f, 0.8f); // Warm cartoon golden dust

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 0f;
                emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 8) });

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.2f;

                _fallbackDustParticles = ps;
            }
            else
            {
                _fallbackDustParticles = existingFallback.GetComponent<ParticleSystem>();
            }
            changed = true;
        }

        return changed;
    }

    public void ResetCounters()
    {
        _jumpVFXCount = 0;
        _landVFXCount = 0;
        _slideVFXCount = 0;
        _dashVFXCount = 0;
    }
}
