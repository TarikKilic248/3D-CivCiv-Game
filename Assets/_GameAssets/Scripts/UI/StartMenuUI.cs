using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// CivCiv 3D - Baslangic Menusu ve Kontrol Rehberi (StartMenuUI)
/// Oyun acilisinda 3D dunya arka plani uzerinde sicak altin/kehribar temali ana menuyu gosterir,
/// yeni oyunculara kontrolleri sunar ve OYNA basildiginda oyunu baslatir.
/// </summary>
[DisallowMultipleComponent]
public class StartMenuUI : MonoBehaviour
{
    public static StartMenuUI Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance._isMenuOpen;

    private static bool _skipMenuOnNextLoad = false;

    [Header("--- Theme & Roots ---")]
    [SerializeField] private UIThemeSO _theme;
    [SerializeField] private GameObject _menuRoot;
    [SerializeField] private GameObject _mainCard;
    [SerializeField] private GameObject _controlsCard;
    [SerializeField] private GameObject _controlsHintBar;

    [Header("--- Labels & Buttons ---")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _subtitleText;
    [SerializeField] private TextMeshProUGUI _statsText;
    [SerializeField] private TextMeshProUGUI _controlsBodyText;
    [SerializeField] private TextMeshProUGUI _hintBarText;
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _controlsButton;
    [SerializeField] private Button _controlsBackButton;
    [SerializeField] private Button _quitButton;

    private bool _isMenuOpen = false;

    public GameObject MenuRoot => _menuRoot;
    public GameObject ControlsCard => _controlsCard;
    public GameObject ControlsHintBar => _controlsHintBar;
    public Button PlayButton => _playButton;
    public Button ControlsButton => _controlsButton;
    public Button QuitButton => _quitButton;
    public UIThemeSO Theme => _theme;

    /// <summary>Yeniden baslatmada baslangic menusunu atlayip dogrudan oyunu baslatir.</summary>
    public static void PrepareQuickRestart()
    {
        _skipMenuOnNextLoad = true;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        AutoResolveReferences();
        BindEvents();
        ApplyTheme(_theme);

        if (_skipMenuOnNextLoad)
        {
            _skipMenuOnNextLoad = false;
            HideMenuImmediate();
        }
        else
        {
            ShowMenu();
        }
    }

    private void Start()
    {
        RefreshStatsSummary();
        if (_isMenuOpen)
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Update()
    {
        if (!_isMenuOpen) return;

        bool confirmPressed = false;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
        {
            confirmPressed = true;
        }
#endif
        try
        {
            if (!confirmPressed && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                confirmPressed = true;
            }
        }
        catch { }

        if (confirmPressed)
        {
            StartGame();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            if (_isMenuOpen) Time.timeScale = 1f;
            Instance = null;
        }
    }

    public void ShowMenu()
    {
        _isMenuOpen = true;
        if (_menuRoot != null) _menuRoot.SetActive(true);
        if (_mainCard != null) _mainCard.SetActive(true);
        if (_controlsCard != null) _controlsCard.SetActive(false);
        if (_controlsHintBar != null) _controlsHintBar.SetActive(false);

        RefreshStatsSummary();
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void StartGame()
    {
        if (SettingsUI.Instance != null)
        {
            SettingsUI.Instance.PlayClickSound();
        }

        HideMenuImmediate();
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void HideMenuImmediate()
    {
        _isMenuOpen = false;
        if (_menuRoot != null) _menuRoot.SetActive(false);
        if (_controlsCard != null) _controlsCard.SetActive(false);
        if (_controlsHintBar != null) _controlsHintBar.SetActive(true);
    }

    public void ShowControls()
    {
        if (SettingsUI.Instance != null) SettingsUI.Instance.PlayClickSound();
        if (_mainCard != null) _mainCard.SetActive(false);
        if (_controlsCard != null) _controlsCard.SetActive(true);
    }

    public void ShowMainCard()
    {
        if (SettingsUI.Instance != null) SettingsUI.Instance.PlayClickSound();
        if (_controlsCard != null) _controlsCard.SetActive(false);
        if (_mainCard != null) _mainCard.SetActive(true);
    }

    public void QuitGame()
    {
        if (SettingsUI.Instance != null) SettingsUI.Instance.PlayClickSound();
        Time.timeScale = 1f;
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void RefreshStatsSummary()
    {
        if (_statsText == null) return;

        int highScore = PlayerPrefs.GetInt(CoinManager.HighScoreKey, 0);
        int bestStars = 0;
        int totalRescued = 0;

        SaveManager saveMgr = SaveManager.Instance ?? FindAnyObjectByType<SaveManager>();
        if (saveMgr != null && saveMgr.CurrentData != null)
        {
            var data = saveMgr.CurrentData;
            highScore = Mathf.Max(highScore, data.highScore);
            totalRescued = data.totalRescuedChicks;
            bestStars = data.GetLevelStars("Level_Demo");
        }

        _statsText.text = $"Rekor Altin: {highScore}   |   En Iyi Yildiz: {bestStars}/3   |   Kurtarilan: {totalRescued}";
    }

    public void ApplyTheme(UIThemeSO themeOverride = null)
    {
        if (themeOverride != null) _theme = themeOverride;
        UIThemeSO t = UIStyleHelper.ResolveTheme(_theme);

        // Koyu sicak kehribar arka plan + altin cerceve ile yuksek okunakli kart stili
        ApplyDarkAmberCardStyle(_mainCard, t);
        ApplyDarkAmberCardStyle(_controlsCard, t);
        ApplyDarkAmberCardStyle(_controlsHintBar, t);

        if (_titleText != null) UIStyleHelper.ApplyTextStyle(_titleText, t, true);
        if (_subtitleText != null) UIStyleHelper.ApplyTextStyle(_subtitleText, t, false);
        if (_statsText != null) UIStyleHelper.ApplyTextStyle(_statsText, t, false);
        if (_controlsBodyText != null) UIStyleHelper.ApplyTextStyle(_controlsBodyText, t, false);
        if (_hintBarText != null) UIStyleHelper.ApplyTextStyle(_hintBarText, t, false);

        if (_playButton != null) UIStyleHelper.ApplyButtonStyle(_playButton, t);
        if (_controlsButton != null) UIStyleHelper.ApplyButtonStyle(_controlsButton, t);
        if (_controlsBackButton != null) UIStyleHelper.ApplyButtonStyle(_controlsBackButton, t);
        if (_quitButton != null) UIStyleHelper.ApplyButtonStyle(_quitButton, t);
    }

    private static void ApplyDarkAmberCardStyle(GameObject card, UIThemeSO t)
    {
        if (card == null) return;
        UIStyleHelper.ApplyPanelStyle(card, t, false);
        Image img = card.GetComponent<Image>();
        if (img != null) img.color = t.barBackgroundColor;
        Outline outline = card.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = t.primaryGold;
            outline.effectDistance = new Vector2(3f, -3f);
        }
    }

    public void BindEvents()
    {
        if (_playButton != null)
        {
            _playButton.onClick.RemoveListener(StartGame);
            _playButton.onClick.AddListener(StartGame);
        }
        if (_controlsButton != null)
        {
            _controlsButton.onClick.RemoveListener(ShowControls);
            _controlsButton.onClick.AddListener(ShowControls);
        }
        if (_controlsBackButton != null)
        {
            _controlsBackButton.onClick.RemoveListener(ShowMainCard);
            _controlsBackButton.onClick.AddListener(ShowMainCard);
        }
        if (_quitButton != null)
        {
            _quitButton.onClick.RemoveListener(QuitGame);
            _quitButton.onClick.AddListener(QuitGame);
        }
    }

    public void AutoResolveReferences()
    {
        if (_menuRoot == null)
        {
            Transform r = transform.Find("StartMenuRoot");
            _menuRoot = r != null ? r.gameObject : gameObject;
        }
        if (_mainCard == null && _menuRoot != null)
            _mainCard = _menuRoot.transform.Find("MainCard")?.gameObject;
        if (_controlsCard == null && _menuRoot != null)
            _controlsCard = _menuRoot.transform.Find("ControlsCard")?.gameObject;
        if (_controlsHintBar == null)
            _controlsHintBar = transform.Find("ControlsHintBar")?.gameObject;

        if (_mainCard != null)
        {
            if (_titleText == null) _titleText = _mainCard.transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
            if (_subtitleText == null) _subtitleText = _mainCard.transform.Find("SubtitleText")?.GetComponent<TextMeshProUGUI>();
            if (_statsText == null) _statsText = _mainCard.transform.Find("StatsText")?.GetComponent<TextMeshProUGUI>();
            if (_playButton == null) _playButton = _mainCard.transform.Find("PlayButton")?.GetComponent<Button>();
            if (_controlsButton == null) _controlsButton = _mainCard.transform.Find("ControlsButton")?.GetComponent<Button>();
            if (_quitButton == null) _quitButton = _mainCard.transform.Find("QuitButton")?.GetComponent<Button>();
        }
        if (_controlsCard != null)
        {
            if (_controlsBodyText == null) _controlsBodyText = _controlsCard.transform.Find("ControlsBodyText")?.GetComponent<TextMeshProUGUI>();
            if (_controlsBackButton == null) _controlsBackButton = _controlsCard.transform.Find("BackButton")?.GetComponent<Button>();
        }
        if (_controlsHintBar != null && _hintBarText == null)
        {
            _hintBarText = _controlsHintBar.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }
}
