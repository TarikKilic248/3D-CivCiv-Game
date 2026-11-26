using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CivCiv 3D - Oyun İçi Arayüz ve Oyun Sonu Paneli (GameplayHUD)
/// Stamina barını canlı günceller, Victory/GameOver durumlarında EndGamePanel'i açar.
/// </summary>
public class GameplayHUD : MonoBehaviour
{
    public static GameplayHUD Instance { get; private set; }

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
        if (_staminaSlider != null)
        {
            _staminaSlider.minValue = 0f;
            _staminaSlider.maxValue = maxStamina > 0f ? maxStamina : 100f;
            _staminaSlider.value = Mathf.Clamp(currentStamina, 0f, _staminaSlider.maxValue);
        }

        if (_staminaText != null)
        {
            _staminaText.text = $"{Mathf.RoundToInt(currentStamina)} / {Mathf.RoundToInt(maxStamina)}";
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
