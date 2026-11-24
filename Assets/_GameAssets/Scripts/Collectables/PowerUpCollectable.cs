using System.Collections;
using UnityEngine;

public enum PowerUpType
{
    ExtraTime,
    SpeedBoost,
    StaminaRefill
}

public class PowerUpCollectable : MonoBehaviour, ICollectable
{
    [SerializeField] private PowerUpType _powerUpType = PowerUpType.ExtraTime;
    [SerializeField] private float _amount = 10f;
    [SerializeField] private float _duration = 5f;
    [SerializeField] private AudioClip _collectSfx;

    private bool _isCollected;
    private PlayerController _targetPlayer;

    public void Collect()
    {
        if (_isCollected)
            return;

        _isCollected = true;

        if (_targetPlayer == null)
        {
            _targetPlayer = FindAnyObjectByType<PlayerController>();
        }

        SettingsUI.PlaySFX(_collectSfx);
        ApplyPowerUp(_targetPlayer);
    }

    private void ApplyPowerUp(PlayerController player)
    {
        switch (_powerUpType)
        {
            case PowerUpType.ExtraTime:
                FindAnyObjectByType<ClockUI>()?.AddBonusTime(_amount);
                Destroy(gameObject);
                break;

            case PowerUpType.StaminaRefill:
                if (player != null)
                {
                    player.GetComponent<DashController>()?.RefillStamina(_amount);
                }
                Destroy(gameObject);
                break;

            case PowerUpType.SpeedBoost:
                if (player != null)
                {
                    player.SetSpeedMultiplier(_amount);
                    if (_duration > 0f)
                    {
                        StartCoroutine(SpeedBoostDurationRoutine(player));
                        return;
                    }
                }
                Destroy(gameObject);
                break;
        }
    }

    private IEnumerator SpeedBoostDurationRoutine(PlayerController player)
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = false;
        }

        yield return new WaitForSeconds(_duration);

        if (player != null)
        {
            player.SetSpeedMultiplier(1f);
        }

        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isCollected)
            return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        _targetPlayer = player;
        Collect();
    }
}
