using UnityEngine;

public interface IDamageable
{
    int CurrentHealth { get; }
    int MaxHealth { get; }
    bool IsAlive { get; }
    bool IsInvincible { get; }

    void TakeDamage(int damage);
    void TakeDamage(int damage, Vector3 hitDirection);
    void Heal(int amount);
}
