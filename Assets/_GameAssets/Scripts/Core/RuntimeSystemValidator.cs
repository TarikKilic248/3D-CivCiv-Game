using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CivCiv 3D - Çalışma Zamanı Sistem Bütünlük Doğrulayıcı (RuntimeSystemValidator)
/// Oyun açılışında kritik yöneticilerin (GameManager, CoinManager, SaveManager, AchievementManager,
/// DayNightCycleController) sahnede bulunduğunu doğrular; eksik olanları otomatik tamamlar ve
/// otomatik oluşturulamayan sistemleri (ClockUI, SettingsUI, PlayerController) raporlar.
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public class RuntimeSystemValidator : MonoBehaviour
{
    [Serializable]
    public class ValidationReport
    {
        public List<string> present = new List<string>();
        public List<string> created = new List<string>();
        public List<string> missing = new List<string>();

        public bool IsHealthy => missing.Count == 0;
        public override string ToString() =>
            $"Present=[{string.Join(", ", present)}] Created=[{string.Join(", ", created)}] Missing=[{string.Join(", ", missing)}]";
    }

    [SerializeField] private bool _validateOnAwake = true;
    [SerializeField] private bool _autoCreateMissing = true;

    public ValidationReport LastReport { get; private set; }
    public event Action<ValidationReport> OnValidated;

    private void Awake()
    {
        if (_validateOnAwake) ValidateAndRepair(_autoCreateMissing);
    }

    /// <summary>Kritik sistemleri doğrular; createMissing true ise eksik olanları oluşturur.</summary>
    public ValidationReport ValidateAndRepair(bool createMissing)
    {
        ValidationReport report = new ValidationReport();

        // Otomatik tamamlanabilir çekirdek yöneticiler
        Ensure<GameManager>("GameManager", createMissing, report);
        Ensure<CoinManager>("CoinManager", createMissing, report);
        Ensure<SaveManager>("SaveManager", createMissing, report);
        Ensure<AchievementManager>("AchievementManager", createMissing, report);
        DayNightCycleController dayNight = Ensure<DayNightCycleController>("DayNightCycle", createMissing, report);
        if (dayNight != null) dayNight.ResolveSun();

        // Sahne kurulumu gerektiren sistemler (yalnızca raporlanır)
        Check<ClockUI>(report);
        Check<SettingsUI>(report);
        Check<PlayerController>(report);

        LastReport = report;
        if (!report.IsHealthy)
            Debug.LogWarning($"[RuntimeSystemValidator] Eksik kritik sistemler: {report}", this);
        else if (report.created.Count > 0)
            Debug.Log($"[RuntimeSystemValidator] Eksik sistemler tamamlandı: {report}", this);

        OnValidated?.Invoke(report);
        return report;
    }

    public static T FindSystem<T>() where T : Component
    {
        T[] found = FindObjectsByType<T>(FindObjectsInactive.Include);
        return found != null && found.Length > 0 ? found[0] : null;
    }

    private T Ensure<T>(string objectName, bool createMissing, ValidationReport report) where T : Component
    {
        string typeName = typeof(T).Name;
        T existing = FindSystem<T>();
        if (existing != null)
        {
            report.present.Add(typeName);
            return existing;
        }

        if (!createMissing)
        {
            report.missing.Add(typeName);
            return null;
        }

        GameObject go = new GameObject(objectName);
        T created = go.AddComponent<T>();
        report.created.Add(typeName);
        return created;
    }

    private static void Check<T>(ValidationReport report) where T : Component
    {
        if (FindSystem<T>() != null) report.present.Add(typeof(T).Name);
        else report.missing.Add(typeof(T).Name);
    }
}
