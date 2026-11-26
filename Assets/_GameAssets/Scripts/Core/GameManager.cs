using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    [SerializeField] private GameState currentState = GameState.Playing;

    [Header("Goal & Level Settings")]
    [SerializeField] private int targetCoins = 0;
    [SerializeField] private bool autoDetectTargetCoins = true;

    public GameState CurrentState => currentState;

    public int TargetCoins
    {
        get => targetCoins;
        set => targetCoins = value;
    }

    public event Action<GameState> OnGameStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        currentState = GameState.Playing;
    }

    private void Start()
    {
        if (autoDetectTargetCoins && targetCoins <= 0)
        {
            CountTargetCoinsInScene();
        }

        SubscribeEvents();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        UnsubscribeEvents();
    }

    public void CountTargetCoinsInScene()
    {
        CoinCollect[] coins = UnityEngine.Object.FindObjectsByType<CoinCollect>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (coins != null && coins.Length > 0)
        {
            targetCoins = coins.Length;
        }
    }

    public void SubscribeEvents()
    {
        if (CoinManager.Instance != null)
        {
            SubscribeToCoinManager(CoinManager.Instance);
        }
        else
        {
            CoinManager cm = UnityEngine.Object.FindAnyObjectByType<CoinManager>();
            if (cm != null)
                SubscribeToCoinManager(cm);
        }

        if (ClockUI.Instance != null)
        {
            SubscribeToClock(ClockUI.Instance);
        }
        else
        {
            ClockUI clock = UnityEngine.Object.FindAnyObjectByType<ClockUI>();
            if (clock != null)
                SubscribeToClock(clock);
        }
    }

    public void SubscribeToCoinManager(CoinManager cm)
    {
        if (cm == null) return;
        cm.CoinsChanged -= HandleCoinsChanged;
        cm.CoinsChanged += HandleCoinsChanged;
    }

    public void SubscribeToClock(ClockUI clock)
    {
        if (clock == null) return;
        clock.OnTimerExpired -= HandleTimerExpired;
        clock.OnTimerExpired += HandleTimerExpired;
    }

    private void UnsubscribeEvents()
    {
        if (CoinManager.Instance != null)
        {
            CoinManager.Instance.CoinsChanged -= HandleCoinsChanged;
        }
        if (ClockUI.Instance != null)
        {
            ClockUI.Instance.OnTimerExpired -= HandleTimerExpired;
        }
    }

    private void HandleCoinsChanged(int currentCoins)
    {
        if (currentState != GameState.Playing) return;

        if (targetCoins > 0 && currentCoins >= targetCoins)
        {
            TriggerVictory();
        }
    }

    private void HandleTimerExpired()
    {
        if (currentState == GameState.Playing)
        {
            TriggerGameOver();
        }
    }

    public void SetState(GameState newState)
    {
        if (currentState == newState) return;

        currentState = newState;
        OnGameStateChanged?.Invoke(currentState);
    }

    public void TriggerVictory()
    {
        if (currentState == GameState.Victory || currentState == GameState.GameOver) return;
        SetState(GameState.Victory);
        Debug.Log("[GameManager] Victory achieved!");
    }

    public void TriggerGameOver()
    {
        if (currentState == GameState.GameOver || currentState == GameState.Victory) return;
        SetState(GameState.GameOver);
        Debug.Log("[GameManager] Game Over!");
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex >= 0)
        {
            SceneManager.LoadScene(activeScene.buildIndex);
        }
        else if (!string.IsNullOrEmpty(activeScene.name))
        {
            SceneManager.LoadScene(activeScene.name);
        }
        else
        {
            SceneManager.LoadScene(activeScene.path);
        }
    }
}
