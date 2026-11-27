using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CivCiv 3D - Konumsal ve Rastgele Pitch'li 3D Ses Yöneticisi (SpatialAudioManager)
/// 3D ortamda dinamik AudioSource havuzu yönetir, SettingsUI.IsSoundEnabled ile tam senkron çalışır.
/// </summary>
public class SpatialAudioManager : MonoBehaviour
{
    public static SpatialAudioManager Instance { get; private set; }

    [Header("--- Library Reference ---")]
    [Tooltip("Merkezi ses kütüphanesi varlığı (İsteğe bağlı)")]
    [SerializeField] private AudioLibrarySO _audioLibrary;

    [Header("--- Pool Settings ---")]
    [Tooltip("Başlangıçta oluşturulacak 3D AudioSource havuz kapasitesi")]
    [SerializeField] private int _poolInitialSize = 10;

    [Tooltip("Seslerin maksimum duyulma mesafesi")]
    [SerializeField] private float _maxSoundDistance = 35f;

    [Tooltip("Seslerin minimum tam duyulma mesafesi")]
    [SerializeField] private float _minSoundDistance = 1.5f;

    [Header("--- Default Pitch Range ---")]
    [SerializeField] private float _defaultMinPitch = 0.9f;
    [SerializeField] private float _defaultMaxPitch = 1.1f;

    // Çalışma Zamanı Havuz ve Takip Değişkenleri
    private readonly List<AudioSource> _sourcePool = new List<AudioSource>();
    private Transform _poolRoot;

    // Test ve Davranışsal Doğrulama Metrikleri
    public int TotalSoundsPlayed { get; private set; } = 0;
    public AudioClip LastPlayedClip { get; private set; }
    public float LastPlayedPitch { get; private set; } = 1f;
    public float LastPlayedVolume { get; private set; } = 1f;
    public Vector3 LastPlayedPosition { get; private set; }
    public AudioLibrarySO Library { get => _audioLibrary; set => _audioLibrary = value; }

    public static SpatialAudioManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        SpatialAudioManager existing = FindAnyObjectByType<SpatialAudioManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject go = new GameObject("SpatialAudioManager");
        Instance = go.AddComponent<SpatialAudioManager>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializePool();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void InitializePool()
    {
        if (_poolRoot == null)
        {
            Transform existing = transform.Find("AudioSourcePool");
            if (existing != null)
            {
                _poolRoot = existing;
            }
            else
            {
                GameObject rootObj = new GameObject("AudioSourcePool");
                rootObj.transform.SetParent(transform);
                _poolRoot = rootObj.transform;
            }
        }

        // Mevcut kaynakları topla
        _sourcePool.Clear();
        AudioSource[] existingSources = _poolRoot.GetComponentsInChildren<AudioSource>(true);
        _sourcePool.AddRange(existingSources);

        while (_sourcePool.Count < _poolInitialSize)
        {
            _sourcePool.Add(CreateNewPooledSource());
        }
    }

    private AudioSource CreateNewPooledSource()
    {
        GameObject sourceObj = new GameObject($"SpatialAudioSource_{_sourcePool.Count}");
        sourceObj.transform.SetParent(_poolRoot != null ? _poolRoot : transform);

        AudioSource src = sourceObj.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f; // 3D uzamsal ses
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = _minSoundDistance;
        src.maxDistance = _maxSoundDistance;
        src.dopplerLevel = 0f;
        sourceObj.SetActive(false);

        return src;
    }

    private AudioSource GetAvailableSource()
    {
        for (int i = 0; i < _sourcePool.Count; i++)
        {
            AudioSource src = _sourcePool[i];
            if (src != null && !src.isPlaying && !src.gameObject.activeSelf)
            {
                src.gameObject.SetActive(true);
                return src;
            }
        }

        AudioSource newSrc = CreateNewPooledSource();
        newSrc.gameObject.SetActive(true);
        return newSrc;
    }

    /// <summary>
    /// Belirtilen 3D konumda rastgele pitch ile ses çalar.
    /// SettingsUI.IsSoundEnabled kapalı ise ses çalmaz.
    /// </summary>
    public AudioSource PlaySoundAtPoint(AudioClip clip, Vector3 position, float volume = 1f, float minPitch = -1f, float maxPitch = -1f, float spatialBlend = 1f)
    {
        if (clip == null) return null;

        // SettingsUI.IsSoundEnabled kontrolü: Kapalıysa ASLA çalma
        if (!SettingsUI.IsSoundEnabled)
        {
            return null;
        }

        float pMin = minPitch > 0f ? minPitch : _defaultMinPitch;
        float pMax = maxPitch > 0f ? maxPitch : _defaultMaxPitch;
        if (pMax < pMin) pMax = pMin;

        float randomPitch = UnityEngine.Random.Range(pMin, pMax);

        AudioSource source = GetAvailableSource();
        if (source == null) return null;

        source.transform.position = position;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = randomPitch;
        source.spatialBlend = Mathf.Clamp01(spatialBlend);
        source.loop = false;
        source.Play();

        TotalSoundsPlayed++;
        LastPlayedClip = clip;
        LastPlayedPitch = randomPitch;
        LastPlayedVolume = volume;
        LastPlayedPosition = position;

        return source;
    }

    /// <summary>
    /// Hedef Transform'a bağlayarak veya hedef konumunda ses çalar.
    /// </summary>
    public AudioSource PlaySound(AudioClip clip, Transform followTarget = null, float volume = 1f, float minPitch = -1f, float maxPitch = -1f)
    {
        Vector3 pos = followTarget != null ? followTarget.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        AudioSource src = PlaySoundAtPoint(clip, pos, volume, minPitch, maxPitch);
        if (src != null && followTarget != null)
        {
            src.transform.SetParent(followTarget);
            src.transform.localPosition = Vector3.zero;
        }
        return src;
    }

    /// <summary>
    /// Kütüphanedeki anahtara göre ses çalar.
    /// </summary>
    public AudioSource PlayFromLibrary(string key, Vector3 position, float volume = 1f)
    {
        if (_audioLibrary == null) return null;
        AudioClip clip = _audioLibrary.GetRandomClip(key);
        if (clip == null) return null;

        AudioEntry entry = _audioLibrary.GetEntry(key);
        float minPitch = entry != null ? entry.minPitch : _defaultMinPitch;
        float maxPitch = entry != null ? entry.maxPitch : _defaultMaxPitch;
        float vol = entry != null ? entry.volume * volume : volume;

        return PlaySoundAtPoint(clip, position, vol, minPitch, maxPitch);
    }

    public void ResetCounters()
    {
        TotalSoundsPlayed = 0;
        LastPlayedClip = null;
        LastPlayedPitch = 1f;
        LastPlayedVolume = 1f;
    }

    private void Update()
    {
        // Çalması biten ses nesnelerini havuza geri kazandır
        for (int i = 0; i < _sourcePool.Count; i++)
        {
            AudioSource src = _sourcePool[i];
            if (src != null && src.gameObject.activeSelf && !src.isPlaying)
            {
                if (src.transform.parent != _poolRoot && _poolRoot != null)
                {
                    src.transform.SetParent(_poolRoot);
                }
                src.gameObject.SetActive(false);
            }
        }
    }
}
