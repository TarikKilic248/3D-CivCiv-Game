using UnityEngine;

/// <summary>
/// CivCiv 3D - Bölüm Verisi ve Yıldız Eşikleri (LevelDataSO)
/// Bölüm hedeflerini, süre limitini ve 1-3 yıldız kazanma kriterlerini ScriptableObject olarak tanımlar.
/// </summary>
[CreateAssetMenu(fileName = "DefaultLevelData", menuName = "CivCiv/Level Data")]
public class LevelDataSO : ScriptableObject
{
    public const string DefaultAssetPath = "Assets/_GameAssets/Data/DefaultLevelData.asset";

    [Header("--- Level Identification ---")]
    [Tooltip("Bölüm benzersiz kimliği (SaveManager anahtarı)")]
    [SerializeField] private string _levelId = "Level_Demo";

    [Tooltip("Bölümün ekranda görünen başlığı")]
    [SerializeField] private string _levelDisplayName = "Demo Çiftlik";

    [Tooltip("Bölümün sahne dosya adı")]
    [SerializeField] private string _sceneName = "Demo";

    [Header("--- Level Goals ---")]
    [Tooltip("Bölümün tamamlanması veya tam başarı için hedeflenen altın sayısı")]
    [SerializeField] private int _targetCoins = 29;

    [Tooltip("Bölümde kurtarılması gereken hedef civciv sayısı")]
    [SerializeField] private int _targetChicks = 2;

    [Tooltip("Bölüm için varsayılan süre limiti (saniye)")]
    [SerializeField] private float _timeLimit = 60f;

    [Header("--- Star Thresholds (Coin & Remaining Time) ---")]
    [Tooltip("1 Yıldız için minimum altın miktarı")]
    [SerializeField] private int _star1MinCoins = 10;

    [Tooltip("1 Yıldız için minimum kalan süre (saniye)")]
    [SerializeField] private float _star1MinRemainingTime = 0f;

    [Tooltip("2 Yıldız için minimum altın miktarı")]
    [SerializeField] private int _star2MinCoins = 20;

    [Tooltip("2 Yıldız için minimum kalan süre (saniye)")]
    [SerializeField] private float _star2MinRemainingTime = 15f;

    [Tooltip("3 Yıldız için minimum altın miktarı")]
    [SerializeField] private int _star3MinCoins = 28;

    [Tooltip("3 Yıldız için minimum kalan süre (saniye)")]
    [SerializeField] private float _star3MinRemainingTime = 30f;

    // Public Properties
    public string LevelId { get => _levelId; set => _levelId = value; }
    public string LevelDisplayName { get => _levelDisplayName; set => _levelDisplayName = value; }
    public string SceneName { get => _sceneName; set => _sceneName = value; }
    public int TargetCoins { get => _targetCoins; set => _targetCoins = value; }
    public int TargetChicks { get => _targetChicks; set => _targetChicks = value; }
    public float TimeLimit { get => _timeLimit; set => _timeLimit = value; }

    public int Star1MinCoins { get => _star1MinCoins; set => _star1MinCoins = value; }
    public float Star1MinRemainingTime { get => _star1MinRemainingTime; set => _star1MinRemainingTime = value; }
    public int Star2MinCoins { get => _star2MinCoins; set => _star2MinCoins = value; }
    public float Star2MinRemainingTime { get => _star2MinRemainingTime; set => _star2MinRemainingTime = value; }
    public int Star3MinCoins { get => _star3MinCoins; set => _star3MinCoins = value; }
    public float Star3MinRemainingTime { get => _star3MinRemainingTime; set => _star3MinRemainingTime = value; }

    /// <summary>
    /// Toplanan altın ve kalan süreye göre 0-3 arası kazanılan yıldız sayısını hesaplar.
    /// </summary>
    public int CalculateStars(int coinsCollected, float remainingSeconds)
    {
        if (coinsCollected >= _star3MinCoins && remainingSeconds >= _star3MinRemainingTime)
            return 3;
        if (coinsCollected >= _star2MinCoins && remainingSeconds >= _star2MinRemainingTime)
            return 2;
        if (coinsCollected >= _star1MinCoins && remainingSeconds >= _star1MinRemainingTime)
            return 1;
        return 0;
    }

    /// <summary>
    /// Belirtilen yıldız için gereken gereksinimleri metin olarak döndürür.
    /// </summary>
    public string GetStarRequirementDescription(int starIndex)
    {
        switch (starIndex)
        {
            case 1:
                return $"{_star1MinCoins} Altın topla";
            case 2:
                return $"{_star2MinCoins} Altın topla, {_star2MinRemainingTime:0}s kala bitir";
            case 3:
                return $"{_star3MinCoins} Altın topla, {_star3MinRemainingTime:0}s kala bitir";
            default:
                return string.Empty;
        }
    }
}
