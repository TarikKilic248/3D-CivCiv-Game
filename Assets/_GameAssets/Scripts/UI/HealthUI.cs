using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _healthText;
    [SerializeField] private Image[] _heartIcons;
    [SerializeField] private Color _fullHeartColor = new Color(0.95f, 0.22f, 0.28f, 1f);
    [SerializeField] private Color _emptyHeartColor = new Color(0.25f, 0.25f, 0.25f, 0.45f);

    private PlayerHealth _playerHealth;
    private bool _isSubscribed;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
        if (_playerHealth != null)
        {
            HandleHealthChanged(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);
        }
    }

    private void OnDisable()
    {
        if (_isSubscribed && _playerHealth != null)
        {
            _playerHealth.OnHealthChanged -= HandleHealthChanged;
            _isSubscribed = false;
        }
    }

    private void TrySubscribe()
    {
        if (_isSubscribed)
            return;

        if (_playerHealth == null)
        {
            _playerHealth = FindAnyObjectByType<PlayerHealth>();
        }

        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged += HandleHealthChanged;
            _isSubscribed = true;
        }
    }

    private void HandleHealthChanged(int current, int max)
    {
        if (_healthText != null)
        {
            _healthText.text = $"{current}/{max}";
        }

        if (_heartIcons == null)
            return;

        for (int i = 0; i < _heartIcons.Length; i++)
        {
            Image heart = _heartIcons[i];
            if (heart == null)
                continue;

            bool isActiveSlot = i < max;
            heart.gameObject.SetActive(isActiveSlot);

            if (isActiveSlot)
            {
                bool isFilled = i < current;
                heart.color = isFilled ? _fullHeartColor : _emptyHeartColor;
            }
        }
    }
}
