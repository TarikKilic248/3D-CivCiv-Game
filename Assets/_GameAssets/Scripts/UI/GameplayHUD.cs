using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameplayHUD : MonoBehaviour
{
    [SerializeField] private Slider _staminaSlider;
    [SerializeField] private GameObject _endGamePanel;
    [SerializeField] private TextMeshProUGUI _endGameTitleText;
    [SerializeField] private TextMeshProUGUI _endGameStatsText;
    [SerializeField] private Button _restartButton;

    private DashController _dashController;
    private bool _isSubscribedToDash;

    private void Awake()
    {
        if (_endGamePanel != null)
        {
            _endGamePanel.SetActive(false);
        }

        if (_restartButton != null)
        {
            _restartButton.onClick.RemoveListener(OnRestartClicked);
            _restartButton.onClick.AddListener(OnRestartClicked);
        }
    }

    private void OnEnable()
    {
        GameManager.OnGameStateChanged += HandleGameStateChanged;
        TrySubscribeToDash();
    }

    private void Start()
    {
        TrySubscribeToDash();
        if (_dashController != null)
        {
            HandleStaminaChanged(_dashController.CurrentStamina, _dashController.MaxStamina);
        }
    }

    private void OnDisable()
    {
        GameManager.OnGameStateChanged -= HandleGameStateChanged;
        if (_isSubscribedToDash && _dashController != null)
        {
            _dashController.OnStaminaChanged -= HandleStaminaChanged;
            _isSubscribedToDash = false;
        }
    }

    private void TrySubscribeToDash()
    {
        if (_isSubscribedToDash)
            return;

        if (_dashController == null)
        {
            _dashController = FindAnyObjectByType<DashController>();
        }

        if (_dashController != null)
        {
            _dashController.OnStaminaChanged += HandleStaminaChanged;
            _isSubscribedToDash = true;
        }
    }

    private void HandleStaminaChanged(float current, float max)
    {
        if (_staminaSlider == null)
            return;

        _staminaSlider.value = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }

    private void HandleGameStateChanged(GameState state)
    {
        if (state == GameState.Victory || state == GameState.GameOver)
        {
            if (_endGamePanel != null)
            {
                _endGamePanel.SetActive(true);
            }

            if (_endGameTitleText != null)
            {
                _endGameTitleText.text = state == GameState.Victory ? "VICTORY!" : "GAME OVER";
            }

            if (_endGameStatsText != null)
            {
                int coins = CoinManager.Instance != null ? CoinManager.Instance.Coins : 0;
                int highScore = CoinManager.Instance != null ? CoinManager.Instance.HighScore : 0;
                _endGameStatsText.text = $"Coins: {coins}\nHigh Score: {highScore}";
            }
        }
        else if (_endGamePanel != null)
        {
            _endGamePanel.SetActive(false);
        }
    }

    private void OnRestartClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartLevel();
        }
    }
}
