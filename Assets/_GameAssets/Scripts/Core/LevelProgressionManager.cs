using System;
using UnityEngine;

/// <summary>
/// CivCiv 3D - Bölüm İlerleme ve Yıldız Yöneticisi (LevelProgressionManager)
/// Toplanan altın ve kalan süreye göre 0-3 arası yıldız hesaplar, SaveManager üzerinden kalıcı kaydeder,
/// ve görsel/işitsel kutlama geri bildirimi üretir.
/// </summary>
[DisallowMultipleComponent]
public class LevelProgressionManager : MonoBehaviour
{
    public static LevelProgressionManager Instance { get; set; }

    [Header("--- Level Data Configuration ---")]
    [Tooltip("Bu bölüme ait hedef ve yıldız eşiklerini barındıran ScriptableObject")]
    [SerializeField] private LevelDataSO _levelData;

    [Header("--- Audio & Visual Feedback ---")]
    [Tooltip("Yıldız ve zafer kazanıldığında çalacak ses efekti")]
    [SerializeField] private AudioClip _starEarnedSFX;

    [Tooltip("Kutlama parçacık efekti")]
    [SerializeField] private ParticleSystem _celebrationVFX;

    [Tooltip("Prosedürel hareket ve zıplama için görsel kök nesnesi")]
    [SerializeField] private Transform _visualFeedbackRoot;

    // Çalışma Zamanı Değişkenleri
    private int _earnedStars = 0;
    private bool _hasCompleted = false;
    private float _feedbackTimer = 0f;
    private Vector3 _originalRootScale = Vector3.one;

    // Olaylar (Events)
    public event Action<int> OnLevelCompleted; // Parametre: Kazanılan yıldız (0-3)
    public event Action<int, int, float> OnProgressionSaved; // (stars, coins, remainingTime)

    // Public Properties
    public LevelDataSO LevelData { get => _levelData; set => _levelData = value; }
    public int EarnedStars => _earnedStars;
    public bool HasCompleted => _hasCompleted;
    public ParticleSystem CelebrationVFX => _celebrationVFX;
    public Transform VisualFeedbackRoot { get => _visualFeedbackRoot; set => _visualFeedbackRoot = value; }
    public AudioClip StarEarnedSFX { get => _starEarnedSFX; set => _starEarnedSFX = value; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        ResolveDependencies();
        EnsureVFXSetup();

        if (_visualFeedbackRoot != null)
        {
            _originalRootScale = _visualFeedbackRoot.localScale;
        }
    }

    private void Start()
    {
        ResolveDependencies();
        SubscribeEvents();
    }

    private void Update()
    {
        StepFeedback(Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        UnsubscribeEvents();
    }

    /// <summary>
    /// Gerekli bileşenleri ve LevelData referansını güvenle bağlar.
    /// </summary>
    public void ResolveDependencies()
    {
        if (_visualFeedbackRoot == null)
        {
            _visualFeedbackRoot = transform;
            _originalRootScale = _visualFeedbackRoot.localScale;
        }

        if (_levelData == null)
        {
#if UNITY_EDITOR
            _levelData = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelDataSO>(LevelDataSO.DefaultAssetPath);
#endif
        }

        // SaveManager varlığından emin ol
        if (SaveManager.Instance == null)
        {
            SaveManager existing = FindAnyObjectByType<SaveManager>();
            if (existing == null)
            {
                GameObject saveObj = new GameObject("SaveManager");
                existing = saveObj.AddComponent<SaveManager>();
            }
            SaveManager.Instance = existing;
        }
    }

    /// <summary>
    /// GameManager olaylarını dinler; Victory tetiklendiğinde bölümü otomatik tamamlar.
    /// </summary>
    public void SubscribeEvents()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }
    }

    public void UnsubscribeEvents()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
    }

    private void HandleGameStateChanged(GameState newState)
    {
        if (newState == GameState.Victory && !_hasCompleted)
        {
            CompleteLevel();
        }
    }

    /// <summary>
    /// Toplanan altın ve kalan süreye göre 0-3 arası yıldız hesaplar.
    /// </summary>
    public int CalculateStars(int coins, float remainingTime)
    {
        if (_levelData != null)
        {
            return _levelData.CalculateStars(coins, remainingTime);
        }

        // LevelData atanmamışsa varsayılan güvenli eşik mantığı
        if (coins >= 28 && remainingTime >= 30f) return 3;
        if (coins >= 20 && remainingTime >= 15f) return 2;
        if (coins >= 10 && remainingTime >= 0f) return 1;
        return 0;
    }

    /// <summary>
    /// Sahnedeki CoinManager, ClockUI ve ChickRescueManager verilerini kullanarak bölümü tamamlar.
    /// </summary>
    public int CompleteLevel()
    {
        int coins = CoinManager.Instance != null ? CoinManager.Instance.Coins : 0;
        float remainingTime = ClockUI.Instance != null ? ClockUI.Instance.RemainingSeconds : 0f;
        int rescued = ChickRescueManager.Instance != null ? ChickRescueManager.Instance.RescuedChickCount : 0;

        return CompleteLevel(coins, remainingTime, rescued);
    }

    /// <summary>
    /// Belirtilen sayısal parametrelerle bölümü tamamlar, yıldızları hesaplar ve SaveManager'a yazar.
    /// </summary>
    public int CompleteLevel(int coins, float remainingTime, int rescuedChicks = 0)
    {
        _earnedStars = CalculateStars(coins, remainingTime);
        _hasCompleted = true;

        string levelId = _levelData != null ? _levelData.LevelId : "Level_Demo";

        // SaveManager üzerinden kalıcı kaydet
        SaveManager saveManager = SaveManager.Instance ?? FindAnyObjectByType<SaveManager>();
        if (saveManager != null)
        {
            saveManager.SaveLevelCompletion(levelId, _earnedStars, coins, remainingTime, rescuedChicks);
        }

        // Görsel ve işitsel kutlama geri bildirimi
        TriggerCelebrationFeedback();

        OnLevelCompleted?.Invoke(_earnedStars);
        OnProgressionSaved?.Invoke(_earnedStars, coins, remainingTime);

        return _earnedStars;
    }

    /// <summary>
    /// Zafer/yıldız kazanıldığında ses ve parçacık efektlerini tetikler.
    /// </summary>
    public void TriggerCelebrationFeedback()
    {
        if (_starEarnedSFX != null)
        {
            SettingsUI.PlaySFX(_starEarnedSFX);
        }

        if (_celebrationVFX != null)
        {
            _celebrationVFX.Play();
        }

        _feedbackTimer = 0.5f;
    }

    /// <summary>
    /// Prosedürel kutlama yaylanma ve pop hareketini adımlar.
    /// </summary>
    public void StepFeedback(float deltaTime)
    {
        if (_feedbackTimer > 0f && _visualFeedbackRoot != null)
        {
            _feedbackTimer = Mathf.Max(0f, _feedbackTimer - deltaTime);
            float t = 1f - (_feedbackTimer / 0.5f);
            float scalePop = 1f + 0.25f * Mathf.Sin(t * Mathf.PI);
            _visualFeedbackRoot.localScale = _originalRootScale * scalePop;

            if (_feedbackTimer <= 0f)
            {
                _visualFeedbackRoot.localScale = _originalRootScale;
            }
        }
    }

    /// <summary>
    /// Sahne veya nesne altında kutlama parçacık efektinin varlığını garanti eder.
    /// </summary>
    public bool EnsureVFXSetup()
    {
        if (_celebrationVFX != null) return false;

        Transform existing = transform.Find("CelebrationVFX");
        if (existing != null)
        {
            _celebrationVFX = existing.GetComponent<ParticleSystem>();
            if (_celebrationVFX != null) return false;
        }

        GameObject vfxObj = new GameObject("CelebrationVFX");
        vfxObj.transform.SetParent(transform, false);
        vfxObj.transform.localPosition = Vector3.up * 1.0f;

        _celebrationVFX = vfxObj.AddComponent<ParticleSystem>();
        var main = _celebrationVFX.main;
        main.duration = 1.0f;
        main.loop = false;
        main.startLifetime = 0.8f;
        main.startSpeed = 4f;
        main.startSize = 0.35f;
        main.startColor = new Color(1f, 0.85f, 0.2f, 1f); // Altın sarısı kutlama

        var emission = _celebrationVFX.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 30) });

        var shape = _celebrationVFX.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;

        ParticleSystemRenderer renderer = vfxObj.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        return true;
    }

    /// <summary>
    /// Durumu yeni bir test veya bölüm için sıfırlar.
    /// </summary>
    public void ResetState()
    {
        _earnedStars = 0;
        _hasCompleted = false;
        _feedbackTimer = 0f;
        if (_visualFeedbackRoot != null)
        {
            _visualFeedbackRoot.localScale = _originalRootScale;
        }
    }
}
