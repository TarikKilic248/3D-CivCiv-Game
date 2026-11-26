using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState CurrentState { get; private set; } = GameState.Playing;
    public static event Action<GameState> OnGameStateChanged;

    [SerializeField] private int _targetCoinsForVictory = 0;

    public int TargetCoinsForVictory => _targetCoinsForVictory;

    private bool _isSubscribedToCoins;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CurrentState = GameState.Playing;
    }

    private void OnEnable()
    {
        TrySubscribeToCoins();
    }

    private void Start()
    {
        if (_targetCoinsForVictory <= 0)
        {
            var allCoins = FindObjectsByType<CoinCollect>(FindObjectsSortMode.None);
            _targetCoinsForVictory = allCoins != null ? allCoins.Length : 0;
        }

        TrySubscribeToCoins();
    }

    private void Update()
    {
        if (!_isSubscribedToCoins)
        {
            TrySubscribeToCoins();
        }
    }

    public void SetTargetCoinsForVictory(int target)
    {
        _targetCoinsForVictory = Mathf.Max(0, target);
    }

    private void OnDisable()
    {
        if (_isSubscribedToCoins && CoinManager.Instance != null)
        {
            CoinManager.Instance.CoinsChanged -= HandleCoinsChanged;
            _isSubscribedToCoins = false;
        }
    }

    private void TrySubscribeToCoins()
    {
        if (_isSubscribedToCoins || CoinManager.Instance == null)
            return;

        CoinManager.Instance.CoinsChanged += HandleCoinsChanged;
        _isSubscribedToCoins = true;
    }

    public void SetGameState(GameState newState)
    {
        if (CurrentState == newState)
            return;

        CurrentState = newState;

        if (CurrentState == GameState.Victory || CurrentState == GameState.GameOver)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        OnGameStateChanged?.Invoke(CurrentState);
    }

    private void HandleCoinsChanged(int currentCoins)
    {
        if (CurrentState != GameState.Playing)
            return;

        if (_targetCoinsForVictory > 0 && currentCoins >= _targetCoinsForVictory)
        {
            TriggerVictory();
        }
    }

    public void TriggerVictory()
    {
        if (CurrentState == GameState.Victory || CurrentState == GameState.GameOver)
            return;

        if (CoinManager.Instance != null)
        {
            CoinManager.Instance.SaveHighScore();
        }

        SetGameState(GameState.Victory);
    }

    public void TriggerGameOver()
    {
        if (CurrentState == GameState.Victory || CurrentState == GameState.GameOver)
            return;

        SetGameState(GameState.GameOver);
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }
}
