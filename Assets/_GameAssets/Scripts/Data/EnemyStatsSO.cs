using UnityEngine;

[CreateAssetMenu(fileName = "DefaultEnemyStats", menuName = "CivCiv/Enemy Stats")]
public class EnemyStatsSO : ScriptableObject
{
    public const string DefaultAssetPath = "Assets/_GameAssets/Data/DefaultEnemyStats.asset";

    [Header("--- Movement Settings ---")]
    [Tooltip("Devriye sirasindaki yurume hizi")]
    [SerializeField] private float _patrolSpeed = 3f;

    [Tooltip("Kovalama (Chase) sirasindaki kosu hizi")]
    [SerializeField] private float _chaseSpeed = 5.5f;

    [Tooltip("Noktaya ulasildiginda bekleme suresi (saniye)")]
    [SerializeField] private float _waypointWaitTime = 1f;

    [Header("--- Perception (Algilama) ---")]
    [Tooltip("Oyuncuyu fark etme / kovalama baslatma mesafesi")]
    [SerializeField] private float _detectionRange = 7f;

    [Tooltip("Kovalamayi birakma mesafesi")]
    [SerializeField] private float _loseAggroRange = 10f;

    [Header("--- Combat & Health ---")]
    [Tooltip("Maksimum can degeri")]
    [SerializeField] private int _maxHealth = 30;

    [Tooltip("Yandan temas halinde oyuncuya verilen hasar")]
    [SerializeField] private int _contactDamage = 20;

    [Tooltip("Oyuncuya arka arkaya temas hasari verme bekleme suresi (saniye)")]
    [SerializeField] private float _attackCooldown = 1.0f;

    [Tooltip("Ustten ziplandiginda dusmanin alacagi ezilme hasari")]
    [SerializeField] private int _stompDamage = 30;

    [Tooltip("Dusmanin ustune ziplandiginda oyuncuya uygulanan dikey ziplama kuvveti")]
    [SerializeField] private float _bounceForce = 11f;

    // Public properties with getters and setters
    public float PatrolSpeed { get => _patrolSpeed; set => _patrolSpeed = value; }
    public float ChaseSpeed { get => _chaseSpeed; set => _chaseSpeed = value; }
    public float WaypointWaitTime { get => _waypointWaitTime; set => _waypointWaitTime = value; }
    public float DetectionRange { get => _detectionRange; set => _detectionRange = value; }
    public float LoseAggroRange { get => _loseAggroRange; set => _loseAggroRange = value; }
    public int MaxHealth { get => _maxHealth; set => _maxHealth = value; }
    public int ContactDamage { get => _contactDamage; set => _contactDamage = value; }
    public float AttackCooldown { get => _attackCooldown; set => _attackCooldown = value; }
    public int StompDamage { get => _stompDamage; set => _stompDamage = value; }
    public float BounceForce { get => _bounceForce; set => _bounceForce = value; }

    private void OnValidate()
    {
        _patrolSpeed = Mathf.Max(0.1f, _patrolSpeed);
        _chaseSpeed = Mathf.Max(0.1f, _chaseSpeed);
        _waypointWaitTime = Mathf.Max(0f, _waypointWaitTime);
        _detectionRange = Mathf.Max(0.5f, _detectionRange);
        _loseAggroRange = Mathf.Max(_detectionRange, _loseAggroRange);
        _maxHealth = Mathf.Max(1, _maxHealth);
        _contactDamage = Mathf.Max(0, _contactDamage);
        _attackCooldown = Mathf.Max(0.05f, _attackCooldown);
        _stompDamage = Mathf.Max(1, _stompDamage);
        _bounceForce = Mathf.Max(1f, _bounceForce);
    }
}
