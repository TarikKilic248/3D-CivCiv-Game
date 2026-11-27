using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// CivCiv 3D - Başarım Yöneticisi (AchievementManager)
/// Oyun içi kilometre taşlarını (altın, civciv kurtarma, gece hayatta kalma...) takip eder, hedefe ulaşıldığında
/// başarımı açar, OnAchievementUnlocked olayını tetikler, SaveManager ile senkron tutar ve
/// UIThemeSO / UIStyleHelper temalı açılır bildirim (pop + slide) gösterir.
/// </summary>
[DisallowMultipleComponent]
public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance { get; private set; }

    [Header("--- Achievements ---")]
    [SerializeField] private List<AchievementSO> _achievements = new List<AchievementSO>();
    [SerializeField] private bool _autoSaveOnUnlock = true;

    [Header("--- Notification UI ---")]
    [SerializeField] private UIThemeSO _theme;
    [SerializeField] private RectTransform _notificationPanel;
    [SerializeField] private TextMeshProUGUI _notificationTitle;
    [SerializeField] private TextMeshProUGUI _notificationDescription;
    [SerializeField] private float _notificationDuration = 3f;

    [Header("--- Feedback ---")]
    [SerializeField] private ParticleSystem _unlockVFX;
    [SerializeField] private AudioClip _defaultUnlockSfx;

    private readonly Dictionary<AchievementMetric, int> _progress = new Dictionary<AchievementMetric, int>();
    private readonly HashSet<string> _unlocked = new HashSet<string>();
    private SaveManager _saveManager;
    private CanvasGroup _notificationGroup;
    private float _notificationTimer;
    private float _notificationAge;
    private Vector2 _panelRestPosition;
    private bool _restCached;

    public event Action<AchievementSO> OnAchievementUnlocked;

    public IReadOnlyList<AchievementSO> Achievements => _achievements;
    public int UnlockedCount => _unlocked.Count;
    public bool IsNotificationShowing => _notificationTimer > 0f;
    public RectTransform NotificationPanel => _notificationPanel;
    public UIThemeSO Theme { get => _theme; set => _theme = value; }
    public SaveManager SaveManagerOverride { get => _saveManager; set => _saveManager = value; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        EnsureVFXSetup();
        if (_notificationPanel == null)
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null) EnsureNotificationUI(canvas.rootCanvas.transform);
        }
        HideNotificationImmediate();
    }

    private void Start()
    {
        LoadFromSave();
        if (CoinManager.Instance != null) CoinManager.Instance.CoinsCollected += HandleCoinsCollected;
        if (ChickRescueManager.Instance != null) ChickRescueManager.Instance.OnChickRescued += HandleChickRescued;
        if (DayNightCycleController.Instance != null) DayNightCycleController.Instance.OnDayStateChanged += HandleDayStateChanged;
    }

    private void OnDestroy()
    {
        if (CoinManager.Instance != null) CoinManager.Instance.CoinsCollected -= HandleCoinsCollected;
        if (ChickRescueManager.Instance != null) ChickRescueManager.Instance.OnChickRescued -= HandleChickRescued;
        if (DayNightCycleController.Instance != null) DayNightCycleController.Instance.OnDayStateChanged -= HandleDayStateChanged;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        StepNotification(Time.unscaledDeltaTime);
    }

    private void HandleCoinsCollected(int amount) => ReportProgress(AchievementMetric.CoinsCollected, amount);
    private void HandleChickRescued(LostChickNPC chick) => ReportProgress(AchievementMetric.ChicksRescued, 1);
    private void HandleDayStateChanged(bool isDay)
    {
        if (isDay) ReportProgress(AchievementMetric.NightsSurvived, 1);
    }

    private SaveManager ResolveSaveManager()
    {
        if (_saveManager == null) _saveManager = SaveManager.Instance != null ? SaveManager.Instance : FindAnyObjectByType<SaveManager>();
        return _saveManager;
    }

    public int GetProgress(AchievementMetric metric) => _progress.TryGetValue(metric, out int v) ? v : 0;

    public bool IsUnlocked(AchievementSO achievement) => achievement != null && _unlocked.Contains(achievement.AchievementId);

    public void SetAchievements(List<AchievementSO> list) => _achievements = list ?? new List<AchievementSO>();

    /// <summary>Metrik ilerlemesini artırır ve hedefe ulaşan başarımları açar. Açılan başarım sayısını döndürür.</summary>
    public int ReportProgress(AchievementMetric metric, int amount = 1)
    {
        if (amount <= 0) return 0;
        _progress[metric] = GetProgress(metric) + amount;
        return EvaluateMetric(metric);
    }

    public int SetProgress(AchievementMetric metric, int value)
    {
        _progress[metric] = Mathf.Max(0, value);
        return EvaluateMetric(metric);
    }

    private int EvaluateMetric(AchievementMetric metric)
    {
        int unlockedNow = 0;
        int value = GetProgress(metric);
        for (int i = 0; i < _achievements.Count; i++)
        {
            AchievementSO a = _achievements[i];
            if (a != null && a.Metric == metric && !IsUnlocked(a) && a.IsReached(value) && Unlock(a)) unlockedNow++;
        }
        return unlockedNow;
    }

    /// <summary>Başarımı açar, kaydı senkronlar, bildirim + VFX + SFX tetikler.</summary>
    public bool Unlock(AchievementSO achievement)
    {
        if (achievement == null || string.IsNullOrEmpty(achievement.AchievementId)) return false;
        if (!_unlocked.Add(achievement.AchievementId)) return false;

        SaveManager sm = ResolveSaveManager();
        if (sm != null)
        {
            if (sm.CurrentData.unlockedAchievements == null) sm.CurrentData.unlockedAchievements = new List<string>();
            if (!sm.CurrentData.unlockedAchievements.Contains(achievement.AchievementId))
                sm.CurrentData.unlockedAchievements.Add(achievement.AchievementId);
            if (_autoSaveOnUnlock) sm.Save();
        }

        ShowNotification(achievement);
        if (_unlockVFX != null) _unlockVFX.Play();
        SettingsUI.PlaySFX(achievement.UnlockSfx != null ? achievement.UnlockSfx : _defaultUnlockSfx, 0.9f);
        OnAchievementUnlocked?.Invoke(achievement);
        return true;
    }

    /// <summary>SaveManager'daki açık başarım listesini belleğe yükler.</summary>
    public void LoadFromSave()
    {
        SaveManager sm = ResolveSaveManager();
        if (sm == null || sm.CurrentData.unlockedAchievements == null) return;
        for (int i = 0; i < sm.CurrentData.unlockedAchievements.Count; i++)
        {
            string id = sm.CurrentData.unlockedAchievements[i];
            if (!string.IsNullOrEmpty(id)) _unlocked.Add(id);
        }
    }

    public void ResetProgress()
    {
        _progress.Clear();
        _unlocked.Clear();
        HideNotificationImmediate();
    }

    // --- Notification UI ---

    public void ShowNotification(AchievementSO achievement)
    {
        if (_notificationPanel == null) return;
        CacheRest();
        if (_notificationTitle != null) _notificationTitle.text = "Başarım Açıldı: " + achievement.DisplayName;
        if (_notificationDescription != null) _notificationDescription.text = achievement.Description;
        _notificationPanel.gameObject.SetActive(true);
        _notificationTimer = Mathf.Max(0.5f, _notificationDuration);
        _notificationAge = 0f;
        StepNotification(0f);
    }

    /// <summary>Pop (squash-stretch) + yukarıdan kayma + solma animasyonu.</summary>
    public void StepNotification(float deltaTime)
    {
        if (_notificationPanel == null || _notificationTimer <= 0f) return;
        _notificationTimer -= deltaTime;
        _notificationAge += deltaTime;
        float appear = Mathf.Clamp01(_notificationAge / 0.35f);
        float overshoot = 1f + Mathf.Sin(appear * Mathf.PI) * 0.15f;
        float fade = Mathf.Clamp01(_notificationTimer / 0.4f);
        _notificationPanel.localScale = new Vector3(appear * overshoot, appear * (2f - overshoot), 1f);
        _notificationPanel.anchoredPosition = _panelRestPosition + new Vector2(0f, (1f - appear) * 80f);
        if (_notificationGroup != null) _notificationGroup.alpha = Mathf.Min(appear, fade);
        if (_notificationTimer <= 0f) HideNotificationImmediate();
    }

    public void HideNotificationImmediate()
    {
        _notificationTimer = 0f;
        if (_notificationPanel == null) return;
        CacheRest();
        _notificationPanel.anchoredPosition = _panelRestPosition;
        _notificationPanel.localScale = Vector3.one;
        _notificationPanel.gameObject.SetActive(false);
    }

    private void CacheRest()
    {
        if (_restCached || _notificationPanel == null) return;
        _panelRestPosition = _notificationPanel.anchoredPosition;
        _restCached = true;
        _notificationGroup = _notificationPanel.GetComponent<CanvasGroup>();
    }

    /// <summary>Canvas altında temalı başarım bildirim panelini idempotent olarak kurar.</summary>
    public bool EnsureNotificationUI(Transform canvasRoot)
    {
        if (_notificationPanel != null || canvasRoot == null) return false;
        UIThemeSO t = UIStyleHelper.ResolveTheme(_theme);

        GameObject panel = new GameObject("AchievementNotification", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.SetParent(canvasRoot, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(520f, 130f);
        rt.anchoredPosition = new Vector2(0f, -40f);
        UIStyleHelper.ApplyPanelStyle(panel, t, true);
        _notificationPanel = rt;

        _notificationTitle = CreateText(rt, "Title", new Vector2(0f, 0.5f), new Vector2(1f, 1f), 34f);
        UIStyleHelper.ApplyTextStyle(_notificationTitle, t, true, 34f);
        _notificationDescription = CreateText(rt, "Description", new Vector2(0f, 0f), new Vector2(1f, 0.5f), 24f);
        UIStyleHelper.ApplyTextStyle(_notificationDescription, t, false, 24f);

        _restCached = false;
        HideNotificationImmediate();
        return true;
    }

    private static TextMeshProUGUI CreateText(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = new Vector2(24f, 8f);
        rt.offsetMax = new Vector2(-24f, -8f);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = size;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 14f;
        tmp.fontSizeMax = size;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>Altın konfeti başarım VFX'ini idempotent olarak kurar.</summary>
    public bool EnsureVFXSetup()
    {
        if (_unlockVFX != null) return false;
        Transform existing = transform.Find("AchievementUnlockVFX");
        if (existing != null && (_unlockVFX = existing.GetComponent<ParticleSystem>()) != null) return false;

        GameObject vfxObj = new GameObject("AchievementUnlockVFX");
        vfxObj.transform.SetParent(transform, false);
        _unlockVFX = vfxObj.AddComponent<ParticleSystem>();
        var main = _unlockVFX.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 1.2f;
        main.startSpeed = 5f;
        main.startSize = 0.25f;
        main.gravityModifier = 0.6f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.835f, 0.31f), new Color(1f, 0.6f, 0f));
        var emission = _unlockVFX.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
        var shape = _unlockVFX.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        ParticleSystemRenderer psr = vfxObj.GetComponent<ParticleSystemRenderer>();
        if (psr != null) psr.renderMode = ParticleSystemRenderMode.Billboard;
        return true;
    }

    private void LateUpdate()
    {
        if (_unlockVFX == null || !_unlockVFX.isPlaying) return;
        Transform follow = Camera.main != null ? Camera.main.transform : null;
        if (follow != null) _unlockVFX.transform.position = follow.position + follow.forward * 4f;
    }
}
