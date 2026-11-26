using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CivCiv 3D - Oyun İçi Arayüz ve Oyun Sonu Paneli (GameplayHUD)
/// Stamina barını canlı günceller, UIThemeSO/UIStyleHelper temasını uygular ve Victory/GameOver durumlarında EndGamePanel'i açar.
/// </summary>
public class GameplayHUD : MonoBehaviour
{
    public static GameplayHUD Instance { get; private set; }

    [Header("--- Theme & Style ---")]
    [SerializeField] private UIThemeSO _theme;

    [Header("--- Stamina UI ---")]
    [SerializeField] private Slider _staminaSlider;
    [SerializeField] private Image _staminaFillImage;
    [SerializeField] private TextMeshProUGUI _staminaText;

    [Header("--- End Game Panel ---")]
    [SerializeField] private GameObject _endGamePanel;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _scoreText;
    [SerializeField] private TextMeshProUGUI _highScoreText;
    [SerializeField] private Button _restartButton;

    [Header("--- Target References ---")]
    [SerializeField] private DashController _dashController;
    [SerializeField] private GameManager _gameManager;

    private float _currentNormalizedStamina = 1f;
    private float _lowStaminaPulseTimer;

    public UIThemeSO Theme => _theme;
    public Slider StaminaSlider => _staminaSlider;
    public Image StaminaFillImage => _staminaFillImage;
    public TextMeshProUGUI StaminaText => _staminaText;
    public GameObject EndGamePanel => _endGamePanel;
    public TextMeshProUGUI TitleText => _titleText;
    public TextMeshProUGUI ScoreText => _scoreText;
    public TextMeshProUGUI HighScoreText => _highScoreText;
    public Button RestartButton => _restartButton;
    public DashController TargetDashController => _dashController;
    public GameManager TargetGameManager => _gameManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        AutoResolveReferences();
        ApplyTheme(_theme);

        if (_endGamePanel != null)
        {
            _endGamePanel.SetActive(false);
        }
    }

    private void Start()
    {
        SubscribeEvents();

        if (_dashController != null)
        {
            UpdateStaminaBar(_dashController.CurrentStamina, _dashController.MaxStamina);
        }
    }

    private void Update()
    {
        UIThemeSO activeTheme = UIStyleHelper.ResolveTheme(_theme);
        if (_staminaFillImage == null || _staminaSlider == null) return;

        if (_currentNormalizedStamina <= activeTheme.lowStaminaThreshold)
        {
            _lowStaminaPulseTimer += Time.unscaledDeltaTime * activeTheme.lowPulseSpeed;
            _staminaFillImage.color = UIStyleHelper.EvaluateBarFillColor(_currentNormalizedStamina, activeTheme, _lowStaminaPulseTimer);

            float scalePulse = 1f + 0.025f * Mathf.Sin(_lowStaminaPulseTimer);
            _staminaSlider.transform.localScale = new Vector3(scalePulse, scalePulse, 1f);
        }
        else if (_staminaSlider.transform.localScale != Vector3.one)
        {
            _staminaSlider.transform.localScale = Vector3.one;
            _lowStaminaPulseTimer = 0f;
        }
    }

    private void OnEnable()
    {
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
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
    /// GameplayHUD üzerindeki tüm bar, panel, metin ve butonlara merkezi UI temasını uygular.
    /// </summary>
    public void ApplyTheme(UIThemeSO themeOverride = null)
    {
        if (themeOverride != null)
        {
            _theme = themeOverride;
        }

        UIThemeSO activeTheme = UIStyleHelper.ResolveTheme(_theme);

        AutoResolveReferences();

        if (_staminaSlider != null)
        {
            UIStyleHelper.ApplyBarStyle(_staminaSlider, activeTheme, _staminaFillImage, _staminaText);
        }

        if (_endGamePanel != null)
        {
            UIStyleHelper.ApplyPanelStyle(_endGamePanel, activeTheme, true);
        }

        if (_titleText != null)
        {
            UIStyleHelper.ApplyTextStyle(_titleText, activeTheme, true);
        }

        if (_scoreText != null)
        {
            UIStyleHelper.ApplyTextStyle(_scoreText, activeTheme, false);
        }

        if (_highScoreText != null)
        {
            UIStyleHelper.ApplyTextStyle(_highScoreText, activeTheme, false);
        }

        if (_restartButton != null)
        {
            UIStyleHelper.ApplyButtonStyle(_restartButton, activeTheme);
        }
    }

    public void SubscribeEvents()
    {
        if (_dashController == null)
        {
            _dashController = UnityEngine.Object.FindAnyObjectByType<DashController>();
        }

        if (_dashController != null)
        {
            _dashController.OnStaminaChanged -= UpdateStaminaBar;
            _dashController.OnStaminaChanged += UpdateStaminaBar;
            UpdateStaminaBar(_dashController.CurrentStamina, _dashController.MaxStamina);
        }

        if (_gameManager == null)
        {
            _gameManager = GameManager.Instance ?? UnityEngine.Object.FindAnyObjectByType<GameManager>();
        }

        if (_gameManager != null)
        {
            _gameManager.OnGameStateChanged -= HandleGameStateChanged;
            _gameManager.OnGameStateChanged += HandleGameStateChanged;
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }

        if (_restartButton != null)
        {
            _restartButton.onClick.RemoveListener(OnRestartButtonClicked);
            _restartButton.onClick.AddListener(OnRestartButtonClicked);
        }
    }

    public void UnsubscribeEvents()
    {
        if (_dashController != null)
        {
            _dashController.OnStaminaChanged -= UpdateStaminaBar;
        }

        if (_gameManager != null)
        {
            _gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }

        if (_restartButton != null)
        {
            _restartButton.onClick.RemoveListener(OnRestartButtonClicked);
        }
    }

    public void UpdateStaminaBar(float currentStamina, float maxStamina)
    {
        float safeMax = maxStamina > 0f ? maxStamina : 100f;
        float clamped = Mathf.Clamp(currentStamina, 0f, safeMax);
        _currentNormalizedStamina = clamped / safeMax;

        if (_staminaSlider != null)
        {
            _staminaSlider.minValue = 0f;
            _staminaSlider.maxValue = safeMax;
            _staminaSlider.value = clamped;
        }

        if (_staminaFillImage != null)
        {
            UIThemeSO activeTheme = UIStyleHelper.ResolveTheme(_theme);
            _staminaFillImage.color = UIStyleHelper.EvaluateBarFillColor(_currentNormalizedStamina, activeTheme, _lowStaminaPulseTimer);
        }

        if (_staminaText != null)
        {
            _staminaText.text = $"{Mathf.RoundToInt(clamped)} / {Mathf.RoundToInt(safeMax)}";
        }
    }

    public void HandleGameStateChanged(GameState state)
    {
        if (state == GameState.Victory || state == GameState.GameOver)
        {
            if (_endGamePanel != null)
            {
                _endGamePanel.SetActive(true);
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_titleText != null)
            {
                _titleText.text = state == GameState.Victory ? "VICTORY!" : "GAME OVER!";
            }

            CoinManager coinManager = CoinManager.Instance ?? UnityEngine.Object.FindAnyObjectByType<CoinManager>();
            GameManager gameManager = GameManager.Instance ?? _gameManager ?? UnityEngine.Object.FindAnyObjectByType<GameManager>();

            int coins = coinManager != null ? coinManager.Coins : 0;
            int target = gameManager != null ? gameManager.TargetCoins : 0;
            int highScore = coinManager != null ? coinManager.HighScore : PlayerPrefs.GetInt(CoinManager.HighScoreKey, 0);

            if (_scoreText != null)
            {
                _scoreText.text = target > 0 ? $"Score: {coins} / {target}" : $"Score: {coins}";
            }

            if (_highScoreText != null)
            {
                _highScoreText.text = $"High Score: {highScore}";
            }
        }
        else if (state == GameState.Playing)
        {
            if (_endGamePanel != null)
            {
                _endGamePanel.SetActive(false);
            }
        }
    }

    public void OnRestartButtonClicked()
    {
        if (SettingsUI.Instance != null)
        {
            SettingsUI.Instance.PlayClickSound();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartLevel();
        }
        else if (_gameManager != null)
        {
            _gameManager.RestartLevel();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
            );
        }
    }

    public void AutoResolveReferences()
    {
        if (_staminaSlider == null)
        {
            _staminaSlider = GetComponentInChildren<Slider>(true);
        }

        if (_staminaSlider != null && _staminaFillImage == null && _staminaSlider.fillRect != null)
        {
            _staminaFillImage = _staminaSlider.fillRect.GetComponent<Image>();
        }

        if (_staminaSlider != null && _staminaText == null)
        {
            Transform stTr = _staminaSlider.transform.Find("StaminaText");
            if (stTr != null)
            {
                _staminaText = stTr.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_endGamePanel == null)
        {
            Transform panelTr = transform.Find("EndGamePanel");
            if (panelTr == null && transform.parent != null)
            {
                panelTr = transform.parent.Find("EndGamePanel");
            }
            if (panelTr != null)
            {
                _endGamePanel = panelTr.gameObject;
            }
        }

        if (_endGamePanel != null)
        {
            if (_titleText == null)
            {
                Transform tTr = _endGamePanel.transform.Find("TitleText");
                if (tTr != null) _titleText = tTr.GetComponent<TextMeshProUGUI>();
            }

            if (_scoreText == null)
            {
                Transform sTr = _endGamePanel.transform.Find("ScoreText");
                if (sTr != null) _scoreText = sTr.GetComponent<TextMeshProUGUI>();
            }

            if (_highScoreText == null)
            {
                Transform hsTr = _endGamePanel.transform.Find("HighScoreText");
                if (hsTr != null) _highScoreText = hsTr.GetComponent<TextMeshProUGUI>();
            }

            if (_restartButton == null)
            {
                Transform bTr = _endGamePanel.transform.Find("RestartButton");
                if (bTr != null) _restartButton = bTr.GetComponent<Button>();
            }
        }

        if (_dashController == null)
        {
            _dashController = UnityEngine.Object.FindAnyObjectByType<DashController>();
        }

        if (_gameManager == null)
        {
            _gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        }
    }
}
