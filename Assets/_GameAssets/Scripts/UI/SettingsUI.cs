using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// CivCiv 3D - Ayarlar ve Oyun İçi Menü Yöneticisi
/// Editörde oluşturulan Settings butonu, PopUp paneli, Müzik/Ses butonları ve kısayolları tam işlevsel hale getirir.
/// </summary>
public class SettingsUI : MonoBehaviour
{
    public static SettingsUI Instance { get; private set; }

    /// <summary>Ayarlar menüsünün açık olup olmadığını bildirir (Kamera ve diğer kontroller için)</summary>
    public static bool IsOpen => Instance != null && Instance._isMenuOpen;

    /// <summary>Müziğin açık/kapalı durumu</summary>
    public static bool IsMusicEnabled { get; private set; } = true;

    /// <summary>Ses efektlerinin (SFX) açık/kapalı durumu</summary>
    public static bool IsSoundEnabled { get; private set; } = true;

    public static event Action<bool> OnMusicToggled;
    public static event Action<bool> OnSoundToggled;
    public static event Action<bool> OnMenuStateChanged;

    private const string PREF_MUSIC = "CivCiv_MusicEnabled";
    private const string PREF_SOUND = "CivCiv_SoundEnabled";

    [Header("--- UI References (Paneller ve Butonlar) ---")]
    [Tooltip("HUD üzerindeki Ayarlar Menüsü Açma/Kapatma butonu (SettingsButtonUI)")]
    [SerializeField] private Button _settingsButton;

    [Tooltip("Açılıp kapanan ayarlar paneli (SettingPopUpUI)")]
    [SerializeField] private GameObject _popUpPanel;

    [Tooltip("PopUp içindeki kapatma / geri dön butonu (varsa)")]
    [SerializeField] private Button _closeButton;

    [Header("--- Menu Buttons (Ayar Butonları) ---")]
    [Tooltip("Müzik açma/kapama butonu")]
    [SerializeField] private Button _musicButton;

    [Tooltip("Ses efektleri (SFX) açma/kapama butonu")]
    [SerializeField] private Button _soundButton;

    [Tooltip("Yeniden Başlat butonu (isteğe bağlı)")]
    [SerializeField] private Button _restartButton;

    [Tooltip("Oyundan Çıkış butonu (isteğe bağlı)")]
    [SerializeField] private Button _quitButton;

    [Header("--- Visual State (Buton Görselleri ve Renkler) ---")]
    [Tooltip("Müzik butonunun Image bileşeni")]
    [SerializeField] private Image _musicImage;

    [Tooltip("Ses butonunun Image bileşeni")]
    [SerializeField] private Image _soundImage;

    [Tooltip("Özellik aktifken buton rengi / şeffaflığı")]
    [SerializeField] private Color _activeColor = Color.white;

    [Tooltip("Özellik kapalıyken / sessizdeyken buton rengi")]
    [SerializeField] private Color _mutedColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);

    [Tooltip("Müzik açıkken kullanılacak özel ikon (boş bırakılabilir)")]
    [SerializeField] private Sprite _musicOnSprite;

    [Tooltip("Müzik kapalıyken kullanılacak özel ikon (boş bırakılabilir)")]
    [SerializeField] private Sprite _musicOffSprite;

    [Tooltip("Ses açıkken kullanılacak özel ikon (boş bırakılabilir)")]
    [SerializeField] private Sprite _soundOnSprite;

    [Tooltip("Ses kapalıyken kullanılacak özel ikon (boş bırakılabilir)")]
    [SerializeField] private Sprite _soundOffSprite;

    [Header("--- Audio Settings (Müzik ve Ses Efektleri) ---")]
    [Tooltip("Arka plan müziği AudioSource bileşeni")]
    [SerializeField] private AudioSource _musicSource;

    [Tooltip("Menü ve UI ses efektleri AudioSource bileşeni")]
    [SerializeField] private AudioSource _sfxSource;

    [Tooltip("Varsayılan arka plan müziği")]
    [SerializeField] private AudioClip _backgroundMusicClip;

    [Tooltip("Butonlara tıklandığında çalacak ses efekti")]
    [SerializeField] private AudioClip _buttonClickClip;

    [Header("--- Gameplay & Cursor Controls ---")]
    [Tooltip("Menü açıldığında oyunu duraklat (Time.timeScale = 0)")]
    [SerializeField] private bool _pauseGameWhenOpen = true;

    [Tooltip("Menü açıldığında fare imlecini serbest bırak ve görünür yap")]
    [SerializeField] private bool _manageCursor = true;

    [Tooltip("Menüyü açıp kapatacak klavye kısayolu (Varsayılan: Escape)")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.Escape;

    [Tooltip("PopUp açılış ve kapanışında yumuşak ölçek animasyonu kullan")]
    [SerializeField] private bool _useAnimation = true;

    [Tooltip("Animasyon süresi (saniye)")]
    [SerializeField] private float _animationDuration = 0.22f;

    private bool _isMenuOpen = false;
    private Coroutine _animationRoutine;
    private Vector3 _originalPopUpScale = Vector3.one;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Otomatik referans bulma ve eksik Button bileşenlerini tamamlama
        AutoResolveReferences();

        // AudioSource'ları hazırla
        SetupAudioSources();

        // Kaydedilmiş ayarları yükle
        LoadPreferences();

        // Buton dinleyicilerini bağla
        BindButtonEvents();

        // Orijinal ölçeği kaydet
        if (_popUpPanel != null)
        {
            _originalPopUpScale = _popUpPanel.transform.localScale;
            if (_originalPopUpScale == Vector3.zero)
                _originalPopUpScale = Vector3.one;
        }
    }

    private void Start()
    {
        // Buton görsel durumlarını güncelle
        UpdateVisuals();

        // Arka plan müziğini başlat (açıksa)
        if (IsMusicEnabled && _musicSource != null && _backgroundMusicClip != null)
        {
            if (!_musicSource.isPlaying)
            {
                _musicSource.clip = _backgroundMusicClip;
                _musicSource.loop = true;
                _musicSource.Play();
            }
        }

        // Oyun başladığında ayarlar paneli kapalı olmalı
        if (_popUpPanel != null)
        {
            _popUpPanel.SetActive(false);
            _isMenuOpen = false;
        }
    }

    private void Update()
    {
        HandleKeyboardInput();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        // Sahne değişirse zamanı geri getir
        if (_isMenuOpen && _pauseGameWhenOpen)
        {
            Time.timeScale = 1f;
        }
    }

    #region Input & Control

    private void HandleKeyboardInput()
    {
        bool toggleRequested = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.escapeKey.wasPressedThisFrame)
                toggleRequested = true;
        }
#endif

        try
        {
            if (!toggleRequested && Input.GetKeyDown(_toggleKey))
                toggleRequested = true;
        }
        catch { }

        if (toggleRequested)
        {
            ToggleSettings();
        }
    }

    #endregion

    #region Open / Close Logic

    /// <summary>
    /// Menüyü açar veya kapatır (Toggle)
    /// </summary>
    public void ToggleSettings()
    {
        if (_isMenuOpen)
            CloseSettings();
        else
            OpenSettings();
    }

    /// <summary>
    /// Ayarlar PopUp menüsünü açar
    /// </summary>
    public void OpenSettings()
    {
        if (_isMenuOpen) return;
        _isMenuOpen = true;

        PlayClickSound();

        if (_popUpPanel != null)
        {
            _popUpPanel.SetActive(true);

            if (_useAnimation)
            {
                if (_animationRoutine != null) StopCoroutine(_animationRoutine);
                _animationRoutine = StartCoroutine(AnimatePopUp(0f, 1f));
            }
            else
            {
                _popUpPanel.transform.localScale = _originalPopUpScale;
            }
        }

        // Zamanı duraklat
        if (_pauseGameWhenOpen)
        {
            Time.timeScale = 0f;
        }

        // Fare imlecini aç
        if (_manageCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        UpdateVisuals();
        OnMenuStateChanged?.Invoke(true);
    }

    /// <summary>
    /// Ayarlar PopUp menüsünü kapatır
    /// </summary>
    public void CloseSettings()
    {
        if (!_isMenuOpen) return;
        _isMenuOpen = false;

        PlayClickSound();

        if (_popUpPanel != null)
        {
            if (_useAnimation && gameObject.activeInHierarchy)
            {
                if (_animationRoutine != null) StopCoroutine(_animationRoutine);
                _animationRoutine = StartCoroutine(AnimatePopUp(1f, 0f, () =>
                {
                    _popUpPanel.SetActive(false);
                    _popUpPanel.transform.localScale = _originalPopUpScale;
                }));
            }
            else
            {
                _popUpPanel.SetActive(false);
            }
        }

        // Zamanı devam ettir
        if (_pauseGameWhenOpen)
        {
            Time.timeScale = 1f;
        }

        // Fare imlecini kilitle (karakter kontrolü için)
        if (_manageCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        OnMenuStateChanged?.Invoke(false);
    }

    private IEnumerator AnimatePopUp(float fromFactor, float toFactor, Action onComplete = null)
    {
        if (_popUpPanel == null) yield break;

        float timer = 0f;
        Transform tr = _popUpPanel.transform;

        while (timer < _animationDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / _animationDuration);

            // Yumuşak yaylanma / pop efekti (Overshoot curve)
            float scaleFactor;
            if (toFactor > fromFactor)
            {
                // Açılış yaylanması
                scaleFactor = EaseOutBack(t);
            }
            else
            {
                // Kapanış yumuşaması
                scaleFactor = EaseInQuad(1f - t);
            }

            tr.localScale = _originalPopUpScale * scaleFactor;
            yield return null;
        }

        tr.localScale = _originalPopUpScale * toFactor;
        _animationRoutine = null;
        onComplete?.Invoke();
    }

    private float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    private float EaseInQuad(float x)
    {
        return x * x;
    }

    #endregion

    #region Audio & Toggles

    /// <summary>
    /// Müziği açar veya kapatır
    /// </summary>
    public void ToggleMusic()
    {
        IsMusicEnabled = !IsMusicEnabled;
        PlayerPrefs.SetInt(PREF_MUSIC, IsMusicEnabled ? 1 : 0);
        PlayerPrefs.Save();

        if (_musicSource != null)
        {
            _musicSource.mute = !IsMusicEnabled;
            if (IsMusicEnabled && !_musicSource.isPlaying && _backgroundMusicClip != null)
            {
                _musicSource.clip = _backgroundMusicClip;
                _musicSource.loop = true;
                _musicSource.Play();
            }
        }

        PlayClickSound();
        UpdateVisuals();
        OnMusicToggled?.Invoke(IsMusicEnabled);
    }

    /// <summary>
    /// Ses efektlerini açar veya kapatır
    /// </summary>
    public void ToggleSound()
    {
        IsSoundEnabled = !IsSoundEnabled;
        PlayerPrefs.SetInt(PREF_SOUND, IsSoundEnabled ? 1 : 0);
        PlayerPrefs.Save();

        if (_sfxSource != null)
        {
            _sfxSource.mute = !IsSoundEnabled;
        }

        PlayClickSound();
        UpdateVisuals();
        OnSoundToggled?.Invoke(IsSoundEnabled);
    }

    /// <summary>
    /// Geçerli sahneyi yeniden başlatır
    /// </summary>
    public void RestartGame()
    {
        PlayClickSound();
        if (_pauseGameWhenOpen)
        {
            Time.timeScale = 1f;
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Oyundan çıkar
    /// </summary>
    public void QuitGame()
    {
        PlayClickSound();
        if (_pauseGameWhenOpen)
        {
            Time.timeScale = 1f;
        }

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Tıklama sesini çalar
    /// </summary>
    public void PlayClickSound()
    {
        if (IsSoundEnabled && _sfxSource != null && _buttonClickClip != null)
        {
            _sfxSource.PlayOneShot(_buttonClickClip);
        }
    }

    /// <summary>
    /// Herhangi bir genel ses efektini SFX ayarına göre çalar
    /// </summary>
    public static void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null || !IsSoundEnabled) return;

        if (Instance != null && Instance._sfxSource != null)
        {
            Instance._sfxSource.PlayOneShot(clip, volume);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, volume);
        }
    }

    private void UpdateVisuals()
    {
        // Müzik Butonu Görünümü
        if (_musicImage != null)
        {
            _musicImage.color = IsMusicEnabled ? _activeColor : _mutedColor;
            if (_musicOnSprite != null && _musicOffSprite != null)
            {
                _musicImage.sprite = IsMusicEnabled ? _musicOnSprite : _musicOffSprite;
            }
        }

        // Ses Butonu Görünümü
        if (_soundImage != null)
        {
            _soundImage.color = IsSoundEnabled ? _activeColor : _mutedColor;
            if (_soundOnSprite != null && _soundOffSprite != null)
            {
                _soundImage.sprite = IsSoundEnabled ? _soundOnSprite : _soundOffSprite;
            }
        }
    }

    private void LoadPreferences()
    {
        IsMusicEnabled = PlayerPrefs.GetInt(PREF_MUSIC, 1) == 1;
        IsSoundEnabled = PlayerPrefs.GetInt(PREF_SOUND, 1) == 1;

        if (_musicSource != null)
        {
            _musicSource.mute = !IsMusicEnabled;
        }
        if (_sfxSource != null)
        {
            _sfxSource.mute = !IsSoundEnabled;
        }
    }

    #endregion

    #region Auto Setup & Binding

    private void BindButtonEvents()
    {
        if (_settingsButton != null)
        {
            _settingsButton.onClick.RemoveListener(ToggleSettings);
            _settingsButton.onClick.AddListener(ToggleSettings);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(CloseSettings);
            _closeButton.onClick.AddListener(CloseSettings);
        }

        if (_musicButton != null)
        {
            _musicButton.onClick.RemoveListener(ToggleMusic);
            _musicButton.onClick.AddListener(ToggleMusic);
        }

        if (_soundButton != null)
        {
            _soundButton.onClick.RemoveListener(ToggleSound);
            _soundButton.onClick.AddListener(ToggleSound);
        }

        if (_restartButton != null)
        {
            _restartButton.onClick.RemoveListener(RestartGame);
            _restartButton.onClick.AddListener(RestartGame);
        }

        if (_quitButton != null)
        {
            _quitButton.onClick.RemoveListener(QuitGame);
            _quitButton.onClick.AddListener(QuitGame);
        }
    }

    private void SetupAudioSources()
    {
        // Müzik AudioSource
        if (_musicSource == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length > 0)
            {
                _musicSource = sources[0];
            }
            else
            {
                _musicSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (_musicSource != null)
        {
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.ignoreListenerPause = true; // Oyun duraklatıldığında müzik çalmaya devam edebilir
        }

        // SFX AudioSource
        if (_sfxSource == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length > 1)
            {
                _sfxSource = sources[1];
            }
            else
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (_sfxSource != null)
        {
            _sfxSource.playOnAwake = false;
            _sfxSource.loop = false;
            _sfxSource.ignoreListenerPause = true; // Menü sesleri unscaled çalsın
        }
    }

    /// <summary>
    /// Sahnede veya çocuk nesnelerde editörde oluşturulmuş tüm buton ve panelleri akıllıca bulur.
    /// Eğer buton bileşeni eklenmemişse (sadece Image varsa) otomatik Button ekler!
    /// </summary>
    public void AutoResolveReferences()
    {
        Transform rootCanvas = transform.root;

        // 1. SettingsButtonUI
        if (_settingsButton == null)
        {
            _settingsButton = FindOrAddButton(transform, "SettingsButtonUI", "SettingsButton", "SettingButton", "Settings", "MenuButton");
            if (_settingsButton == null && rootCanvas != null)
            {
                _settingsButton = FindOrAddButton(rootCanvas, "SettingsButtonUI", "SettingsButton", "SettingButton", "Settings", "MenuButton");
            }
        }

        // 2. SettingPopUpUI Paneli
        if (_popUpPanel == null)
        {
            Transform popupTr = FindChildRecursive(transform, "SettingPopUpUI", "SettingPopUp", "SettingsPopUp", "SettingsPanel", "MenuPopUp");
            if (popupTr == null && rootCanvas != null)
            {
                popupTr = FindChildRecursive(rootCanvas, "SettingPopUpUI", "SettingPopUp", "SettingsPopUp", "SettingsPanel", "MenuPopUp");
            }

            if (popupTr != null)
            {
                _popUpPanel = popupTr.gameObject;
            }
        }

        Transform searchRoot = _popUpPanel != null ? _popUpPanel.transform : transform;

        // 3. MusicButton
        if (_musicButton == null)
        {
            _musicButton = FindOrAddButton(searchRoot, "MusicButton", "MusicButtonUI", "MusicToggle", "Music", "Btn_Music");
        }
        if (_musicImage == null && _musicButton != null)
        {
            _musicImage = _musicButton.GetComponent<Image>() ?? _musicButton.GetComponentInChildren<Image>();
        }

        // 4. SoundButton
        if (_soundButton == null)
        {
            _soundButton = FindOrAddButton(searchRoot, "SoundButton", "SoundButtonUI", "SoundToggle", "Sound", "Btn_Sound", "SFXButton");
        }
        if (_soundImage == null && _soundButton != null)
        {
            _soundImage = _soundButton.GetComponent<Image>() ?? _soundButton.GetComponentInChildren<Image>();
        }

        // 5. CloseButton / ResumeButton
        if (_closeButton == null)
        {
            _closeButton = FindOrAddButton(searchRoot, "CloseButton", "CloseButtonUI", "Close", "ResumeButton", "BackButton", "ExitButton", "X");
        }

        // 6. RestartButton
        if (_restartButton == null)
        {
            _restartButton = FindOrAddButton(searchRoot, "RestartButton", "RestartButtonUI", "Restart", "RetryButton", "Retry");
        }

        // 7. QuitButton
        if (_quitButton == null)
        {
            _quitButton = FindOrAddButton(searchRoot, "QuitButton", "QuitButtonUI", "Quit", "Exit");
        }

        // 8. Ses dosyaları boşsa projeden otomatik bulmayı dene
        AutoFindAudioClips();
    }

    private Button FindOrAddButton(Transform parent, params string[] possibleNames)
    {
        Transform target = FindChildRecursive(parent, possibleNames);
        if (target == null) return null;

        Button btn = target.GetComponent<Button>();
        if (btn == null)
        {
            // Sadece Image veya RectTransform varsa bileşeni dinamik ekle
            btn = target.gameObject.AddComponent<Button>();
            Debug.Log($"[SettingsUI] '{target.name}' nesnesine Button bileşeni eklendi.");
        }
        return btn;
    }

    private Transform FindChildRecursive(Transform parent, params string[] possibleNames)
    {
        if (parent == null) return null;

        foreach (string n in possibleNames)
        {
            if (string.Equals(parent.name, n, StringComparison.OrdinalIgnoreCase))
                return parent;
        }

        foreach (Transform child in parent)
        {
            foreach (string n in possibleNames)
            {
                if (string.Equals(child.name, n, StringComparison.OrdinalIgnoreCase))
                    return child;
            }

            Transform subChild = FindChildRecursive(child, possibleNames);
            if (subChild != null)
                return subChild;
        }

        return null;
    }

    private void AutoFindAudioClips()
    {
#if UNITY_EDITOR
        if (_backgroundMusicClip == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip Gymnopédie");
            if (guids.Length == 0) guids = AssetDatabase.FindAssets("t:AudioClip Minuet");
            if (guids.Length == 0) guids = AssetDatabase.FindAssets("t:AudioClip Tales");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _backgroundMusicClip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
        }

        if (_buttonClickClip == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip cozy_fx_1");
            if (guids.Length == 0) guids = AssetDatabase.FindAssets("t:AudioClip cozy_fx");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _buttonClickClip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
        }
#endif
    }

    #endregion

#if UNITY_EDITOR
    private void Reset()
    {
        AutoResolveReferences();
    }

    [ContextMenu("Auto Find References")]
    public void ContextAutoFindReferences()
    {
        Undo.RecordObject(this, "Auto Find SettingsUI References");
        AutoResolveReferences();
        EditorUtility.SetDirty(this);
        Debug.Log("<color=green>[SettingsUI] Referanslar başarıyla otomatik olarak tarandı ve bağlandı!</color>");
    }

    [ContextMenu("Build Missing Popup Buttons (Music, Sound, Close)")]
    public void BuildMissingPopupButtons()
    {
        AutoResolveReferences();

        if (_popUpPanel == null)
        {
            Debug.LogError("[SettingsUI] SettingPopUpUI paneli bulunamadı! Lütfen önce popup panelini oluşturun veya atayın.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(_popUpPanel, "Build Missing Popup Buttons");

        // Sprite'ları bul
        Sprite musicSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_GameAssets/Sprites/MusicButton.png");
        Sprite soundSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_GameAssets/Sprites/SoundButton.png");

        // 1. Music Button oluştur (yoksa)
        if (_musicButton == null)
        {
            GameObject musicObj = new GameObject("MusicButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            musicObj.transform.SetParent(_popUpPanel.transform, false);

            RectTransform rt = musicObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-15f, -5f);
            rt.sizeDelta = new Vector2(20f, 22f);

            Image img = musicObj.GetComponent<Image>();
            img.sprite = musicSprite;
            img.preserveAspect = true;

            _musicButton = musicObj.GetComponent<Button>();
            _musicImage = img;
            Debug.Log("[SettingsUI] MusicButton başarıyla oluşturuldu.");
        }

        // 2. Sound Button oluştur (yoksa)
        if (_soundButton == null)
        {
            GameObject soundObj = new GameObject("SoundButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            soundObj.transform.SetParent(_popUpPanel.transform, false);

            RectTransform rt = soundObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(15f, -5f);
            rt.sizeDelta = new Vector2(21f, 21f);

            Image img = soundObj.GetComponent<Image>();
            img.sprite = soundSprite;
            img.preserveAspect = true;

            _soundButton = soundObj.GetComponent<Button>();
            _soundImage = img;
            Debug.Log("[SettingsUI] SoundButton başarıyla oluşturuldu.");
        }

        // 3. Close Button oluştur (yoksa)
        if (_closeButton == null)
        {
            GameObject closeObj = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            closeObj.transform.SetParent(_popUpPanel.transform, false);

            RectTransform rt = closeObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-6f, -6f);
            rt.sizeDelta = new Vector2(12f, 12f);

            Image img = closeObj.GetComponent<Image>();
            img.color = new Color(0.85f, 0.25f, 0.25f, 0.8f);

            _closeButton = closeObj.GetComponent<Button>();
            Debug.Log("[SettingsUI] CloseButton başarıyla oluşturuldu.");
        }

        EditorUtility.SetDirty(this);
        EditorUtility.SetDirty(_popUpPanel);
        Debug.Log("<color=green>[SettingsUI] Eksik tüm menü butonları başarıyla oluşturuldu ve bağlandı!</color>");
    }
#endif
}
