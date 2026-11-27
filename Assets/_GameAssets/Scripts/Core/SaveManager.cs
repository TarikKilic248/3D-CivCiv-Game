using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// CivCiv 3D - Bölüm Kayıt Girdisi (Serileştirilebilir)
/// </summary>
[Serializable]
public class LevelSaveEntry
{
    public string levelId;
    public int starsEarned;
    public int highScore;
    public float bestRemainingTime;
    public bool isCompleted;

    public LevelSaveEntry() { }

    public LevelSaveEntry(string id, int stars, int score, float remainingTime, bool completed = true)
    {
        levelId = id;
        starsEarned = stars;
        highScore = score;
        bestRemainingTime = remainingTime;
        isCompleted = completed;
    }
}

/// <summary>
/// CivCiv 3D - Ana Kalıcı Kayıt Verisi (GameSaveData)
/// JSON serileştirmeye uygun cüzdan altını, rekor skor, kurtarılan civcivler ve bölüm ilerlemesi.
/// </summary>
[Serializable]
public class GameSaveData
{
    public int walletCoins = 0;
    public int highScore = 0;
    public int totalRescuedChicks = 0;
    public List<LevelSaveEntry> levels = new List<LevelSaveEntry>();
    public List<string> unlockedAchievements = new List<string>();

    public int GetLevelStars(string levelId)
    {
        if (string.IsNullOrEmpty(levelId) || levels == null) return 0;
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i] != null && levels[i].levelId == levelId)
            {
                return levels[i].starsEarned;
            }
        }
        return 0;
    }

    public LevelSaveEntry GetLevelEntry(string levelId)
    {
        if (string.IsNullOrEmpty(levelId) || levels == null) return null;
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i] != null && levels[i].levelId == levelId)
            {
                return levels[i];
            }
        }
        return null;
    }

    public bool SetLevelStars(string levelId, int stars)
    {
        if (string.IsNullOrEmpty(levelId)) return false;
        if (levels == null) levels = new List<LevelSaveEntry>();

        LevelSaveEntry entry = GetLevelEntry(levelId);
        if (entry == null)
        {
            entry = new LevelSaveEntry(levelId, Mathf.Clamp(stars, 0, 3), 0, 0f, true);
            levels.Add(entry);
            return true;
        }

        if (stars > entry.starsEarned)
        {
            entry.starsEarned = Mathf.Clamp(stars, 0, 3);
            entry.isCompleted = true;
            return true;
        }
        return false;
    }

    public bool UpdateLevelProgress(string levelId, int stars, int score, float remainingTime)
    {
        if (string.IsNullOrEmpty(levelId)) return false;
        if (levels == null) levels = new List<LevelSaveEntry>();

        LevelSaveEntry entry = GetLevelEntry(levelId);
        bool modified = false;

        if (entry == null)
        {
            entry = new LevelSaveEntry(levelId, Mathf.Clamp(stars, 0, 3), score, remainingTime, true);
            levels.Add(entry);
            return true;
        }

        if (stars > entry.starsEarned)
        {
            entry.starsEarned = Mathf.Clamp(stars, 0, 3);
            modified = true;
        }

        if (score > entry.highScore)
        {
            entry.highScore = score;
            modified = true;
        }

        if (remainingTime > entry.bestRemainingTime)
        {
            entry.bestRemainingTime = remainingTime;
            modified = true;
        }

        entry.isCompleted = true;
        return modified;
    }
}

/// <summary>
/// CivCiv 3D - Kalıcı Kayıt Yöneticisi (SaveManager)
/// Cüzdan altını, rekor skor, kurtarılan civcivler ve bölüm yıldızlarını JSON formatında kaydeder ve yükler.
/// Testler ve özel senaryolar için özel dosya yolu (CustomSavePath) destekler.
/// </summary>
[DisallowMultipleComponent]
public class SaveManager : MonoBehaviour
{
    public const string DefaultFileName = "civciv_save.json";

    public static SaveManager Instance { get; set; }

    [Header("--- Save Configuration ---")]
    [Tooltip("Özel kayıt dosya yolu (Testler için boş bırakılırsa persistentDataPath kullanılır)")]
    [SerializeField] private string _customSavePath = string.Empty;

    [Tooltip("Uygulama açılışında otomatik yükleme yapılsın mı?")]
    [SerializeField] private bool _autoLoadOnAwake = true;

    // Çalışma zamanı veri nesnesi
    private GameSaveData _currentData = new GameSaveData();

    // Olaylar (Events)
    public event Action<GameSaveData> OnSaveDataLoaded;
    public event Action<GameSaveData> OnSaveDataSaved;
    public event Action OnSaveDataReset;

    public GameSaveData CurrentData
    {
        get => _currentData;
        set => _currentData = value ?? new GameSaveData();
    }

    public string CustomSavePath
    {
        get => _customSavePath;
        set => _customSavePath = value;
    }

    public static string DefaultSavePath => Path.Combine(Application.persistentDataPath, DefaultFileName);

    public string CurrentSavePath => !string.IsNullOrEmpty(_customSavePath) ? _customSavePath : DefaultSavePath;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_autoLoadOnAwake)
        {
            Load();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Geçerli kayıt verilerini JSON formatında diske yazar.
    /// </summary>
    public bool Save(string pathOverride = null)
    {
        string targetPath = !string.IsNullOrEmpty(pathOverride) ? pathOverride : CurrentSavePath;

        try
        {
            string dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonUtility.ToJson(_currentData, true);
            File.WriteAllText(targetPath, json);

            OnSaveDataSaved?.Invoke(_currentData);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Kayıt dosyası yazılamadı: {targetPath}\nHata: {ex.Message}", this);
            return false;
        }
    }

    /// <summary>
    /// Diskteki JSON dosyasından verileri okur. Dosya yoksa temiz veri üretir.
    /// </summary>
    public GameSaveData Load(string pathOverride = null)
    {
        string targetPath = !string.IsNullOrEmpty(pathOverride) ? pathOverride : CurrentSavePath;

        try
        {
            if (File.Exists(targetPath))
            {
                string json = File.ReadAllText(targetPath);
                GameSaveData loaded = JsonUtility.FromJson<GameSaveData>(json);
                _currentData = loaded ?? new GameSaveData();
            }
            else
            {
                _currentData = new GameSaveData();

                // PlayerPrefs'ten eski HighScore varsa aktar
                if (PlayerPrefs.HasKey(CoinManager.HighScoreKey))
                {
                    _currentData.highScore = PlayerPrefs.GetInt(CoinManager.HighScoreKey, 0);
                }
            }

            OnSaveDataLoaded?.Invoke(_currentData);
            return _currentData;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Kayıt dosyası okunamadı: {targetPath}\nHata: {ex.Message}", this);
            _currentData = new GameSaveData();
            return _currentData;
        }
    }

    /// <summary>
    /// Kayıt verilerini sıfırlar. İsteğe bağlı olarak diskteki dosyayı da silebilir.
    /// </summary>
    public void ResetData(bool deleteDiskFile = false, string pathOverride = null)
    {
        _currentData = new GameSaveData();

        if (deleteDiskFile)
        {
            string targetPath = !string.IsNullOrEmpty(pathOverride) ? pathOverride : CurrentSavePath;
            try
            {
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Kayıt dosyası silinirken hata oluştu: {ex.Message}", this);
            }
        }

        OnSaveDataReset?.Invoke();
    }

    /// <summary>
    /// Kayıt dosyasının diskte bulunup bulunmadığını kontrol eder.
    /// </summary>
    public bool SaveExists(string pathOverride = null)
    {
        string targetPath = !string.IsNullOrEmpty(pathOverride) ? pathOverride : CurrentSavePath;
        return File.Exists(targetPath);
    }

    // --- Yardımcı Kalıcı Veri Metotları ---

    public void AddWalletCoins(int amount)
    {
        if (amount <= 0) return;
        _currentData.walletCoins += amount;
    }

    public void AddRescuedChicks(int count)
    {
        if (count <= 0) return;
        _currentData.totalRescuedChicks += count;
    }

    public void UpdateHighScore(int score)
    {
        if (score > _currentData.highScore)
        {
            _currentData.highScore = score;
        }
    }

    public int GetLevelStars(string levelId)
    {
        return _currentData.GetLevelStars(levelId);
    }

    public void SetLevelStars(string levelId, int stars)
    {
        _currentData.SetLevelStars(levelId, stars);
    }

    /// <summary>
    /// Bölüm tamamlanma sonucunu kaydeder ve diske yazar.
    /// </summary>
    public void SaveLevelCompletion(string levelId, int stars, int coinsEarned, float remainingTime, int rescuedChicks = 0)
    {
        _currentData.UpdateLevelProgress(levelId, stars, coinsEarned, remainingTime);
        AddWalletCoins(coinsEarned);
        UpdateHighScore(coinsEarned);
        AddRescuedChicks(rescuedChicks);
        Save();
    }
}
