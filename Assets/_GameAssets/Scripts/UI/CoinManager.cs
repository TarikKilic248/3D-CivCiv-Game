using UnityEngine;
using System;

public class CoinManager : MonoBehaviour
{
    public static CoinManager Instance { get; private set; }

    private const string HighScorePrefKey = "CivCiv_HighScore";

    public int Coins { get; private set; }
    public int HighScore { get; private set; }
    public event Action<int> CoinsChanged;
    public event Action<int> CoinsCollected;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadHighScore();
    }

    public void LoadHighScore()
    {
        HighScore = PlayerPrefs.GetInt(HighScorePrefKey, 0);
    }

    public void SaveHighScore()
    {
        if (Coins > HighScore)
        {
            HighScore = Coins;
        }
        PlayerPrefs.SetInt(HighScorePrefKey, HighScore);
        PlayerPrefs.Save();
    }

    public bool AddCoins(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"Coin amount must be greater than zero. Received: {amount}", this);
            return false;
        }

        Coins += amount;
        if (Coins > HighScore)
        {
            SaveHighScore();
        }
        CoinsChanged?.Invoke(Coins);
        CoinsCollected?.Invoke(amount);
        Debug.Log($"Coins: {Coins}", this);
        return true;
    }
}
