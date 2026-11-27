using System;
using UnityEngine;

public class CoinManager : MonoBehaviour
{
    public const string HighScoreKey = "CivCiv_HighScore";

    public static CoinManager Instance { get; private set; }

    public int Coins { get; private set; }
    public int HighScore { get; private set; }

    public event Action<int> CoinsChanged;
    public event Action<int> CoinsCollected;
    public event Action<int> HighScoreChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadHighScore();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SubscribeToCoinManager(this);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void LoadHighScore()
    {
        HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
    }

    public bool AddCoins(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"Coin amount must be greater than zero. Received: {amount}", this);
            return false;
        }

        Coins += amount;
        CoinsChanged?.Invoke(Coins);
        CoinsCollected?.Invoke(amount);

        if (Coins > HighScore)
        {
            HighScore = Coins;
            PlayerPrefs.SetInt(HighScoreKey, HighScore);
            PlayerPrefs.Save();
            HighScoreChanged?.Invoke(HighScore);
        }

        Debug.Log($"Coins: {Coins}, HighScore: {HighScore}", this);
        return true;
    }

    public void ResetCoins()
    {
        Coins = 0;
        CoinsChanged?.Invoke(Coins);
    }

    public void ResetHighScore()
    {
        HighScore = 0;
        PlayerPrefs.DeleteKey(HighScoreKey);
        PlayerPrefs.Save();
        HighScoreChanged?.Invoke(HighScore);
    }
}
