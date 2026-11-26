using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private int _maxHealth = 3;
    [SerializeField] private float _invincibilityDuration = 1.5f;
    [SerializeField] private float _knockbackForce = 8f;

    public int CurrentHealth { get; private set; }
    public int MaxHealth => _maxHealth;
    public bool IsAlive => CurrentHealth > 0;
    public bool IsInvincible { get; private set; }

    public event Action<int, int> OnHealthChanged;
    public event Action OnPlayerDied;

    private PlayerController _playerController;
    private Renderer[] _renderers;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _renderers = GetComponentsInChildren<Renderer>(true);
        CurrentHealth = _maxHealth;
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
    }

    public void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (amount <= 0 || IsInvincible || !IsAlive)
            return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);

        ApplyKnockback(-hitNormal);

        if (CurrentHealth <= 0)
        {
            Die();
        }
        else if (_invincibilityDuration > 0f && gameObject.activeInHierarchy)
        {
            StartCoroutine(InvincibilityFlashRoutine());
        }
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || !IsAlive)
            return;

        CurrentHealth = Mathf.Min(_maxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
    }

    private void ApplyKnockback(Vector3 direction)
    {
        if (_playerController == null)
            return;

        Rigidbody rb = _playerController.GetRigidbody();
        if (rb == null)
            return;

        Vector3 knockDir = direction;
        knockDir.y = 0.35f;
        if (knockDir.sqrMagnitude < 0.001f)
        {
            knockDir = -transform.forward + Vector3.up * 0.35f;
        }
        knockDir.Normalize();

        rb.AddForce(knockDir * _knockbackForce, ForceMode.Impulse);
    }

    private IEnumerator InvincibilityFlashRoutine()
    {
        IsInvincible = true;
        float elapsed = 0f;
        const float flashInterval = 0.12f;
        bool visible = true;

        while (elapsed < _invincibilityDuration)
        {
            elapsed += flashInterval;
            visible = !visible;
            SetRenderersVisible(visible);
            yield return new WaitForSeconds(flashInterval);
        }

        SetRenderersVisible(true);
        IsInvincible = false;
    }

    private void SetRenderersVisible(bool visible)
    {
        if (_renderers == null)
            return;

        foreach (var r in _renderers)
        {
            if (r != null)
            {
                r.enabled = visible;
            }
        }
    }

    private void Die()
    {
        OnPlayerDied?.Invoke();
        GameManager.Instance?.TriggerGameOver();
    }
}
