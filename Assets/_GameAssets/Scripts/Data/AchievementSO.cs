using UnityEngine;

/// <summary>Başarımın takip ettiği oyun içi metrik türü.</summary>
public enum AchievementMetric
{
    CoinsCollected,
    ChicksRescued,
    EnemiesDefeated,
    CratesBroken,
    NightsSurvived,
    Custom
}

/// <summary>
/// CivCiv 3D - Başarım Tanımı (AchievementSO)
/// Kimlik, görünen ad, açıklama, metrik türü ve hedef değeri tutar.
/// </summary>
[CreateAssetMenu(fileName = "Achievement", menuName = "CivCiv/Achievement")]
public class AchievementSO : ScriptableObject
{
    public const string DefaultFolder = "Assets/_GameAssets/Data/Achievements";

    [SerializeField] private string _achievementId = "achievement_id";
    [SerializeField] private string _displayName = "Başarım";
    [SerializeField, TextArea] private string _description = "";
    [SerializeField] private AchievementMetric _metric = AchievementMetric.CoinsCollected;
    [SerializeField, Min(1)] private int _targetValue = 10;
    [SerializeField] private Sprite _icon;
    [SerializeField] private AudioClip _unlockSfx;

    public string AchievementId { get => _achievementId; set => _achievementId = value; }
    public string DisplayName { get => _displayName; set => _displayName = value; }
    public string Description { get => _description; set => _description = value; }
    public AchievementMetric Metric { get => _metric; set => _metric = value; }
    public int TargetValue { get => _targetValue; set => _targetValue = Mathf.Max(1, value); }
    public Sprite Icon { get => _icon; set => _icon = value; }
    public AudioClip UnlockSfx { get => _unlockSfx; set => _unlockSfx = value; }

    public bool IsReached(int progress) => progress >= _targetValue;

    public float GetNormalizedProgress(int progress) => Mathf.Clamp01(progress / (float)Mathf.Max(1, _targetValue));

    private void OnValidate()
    {
        _targetValue = Mathf.Max(1, _targetValue);
    }
}
