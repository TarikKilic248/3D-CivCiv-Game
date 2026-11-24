# 3D CivCiv Game - Kapsamlı Geliştirme Yol Planı ve Otonom Görev Listesi (Faz 1 - Faz 20)

## Proje Analizi (Mevcut Durum & Eksikler)

1. **Oyun Döngüsü (Game Loop) Eksikliği:** `ClockUI.cs` içinde süre (`remainingSeconds`) 0'a ulaştığında oyun bitmiyor, kazanma veya kaybetme (Game Over / Victory) durumu tetiklenmiyor. Merkezi bir `GameManager` ve oyun durumu (`GameState`) yönetimi yok.
2. **Veri Odaklı Mimari (`ScriptableObject`) Eksikliği:** `PlayerController.cs` içindeki hız, sürtünme, zıplama ve kayma değerleri doğrudan MonoBehaviour üzerinde tutuluyor; `ScriptableObject` tabanlı veri mimarisi bulunmuyor.
3. **Hareket Çeşitliliği (Dash, Çift Zıplama, Süzülme & Stamina):** Karakterde yürüme, zıplama ve slide var ancak 3D platform/civciv oyunlarına uygun ileri atılma (`Dash`), çift zıplama, kanat çırparak süzülme (`Glide`) ve enerji (`Stamina`) sistemi yok.
4. **Görsel/İşitsel Geri Bildirim (Juice & Polish):** `CoinCollect.cs` objeyi anında `Destroy(gameObject)` ile yok ediyor; dönme/süzülme animasyonu, toplama sesi (`SettingsUI.PlaySFX`) veya ölçek küçülme efekti yok.
5. **Skor & İlerleme Kaydı:** `CoinManager.cs` sadece anlık sahne içi altın sayısını tutuyor, `PlayerPrefs` veya JSON tabanlı kalıcı kayıt, kombo çarpanı ve başarımlar yapılmıyor.
6. **Seviye İçi Etkileşim, Yapay Zeka ve Dünyalar:** Sadece `CoinCollect` ve `TargetJump` mevcut. Hareketli platformlar, tehlike alanları, düşman devriyeleri, görev/NPC sistemleri, envanter, kozmetik mağaza ve gece/gündüz döngüsü bulunmuyor.

---

## Faz 1: Veri Mimarisi (ScriptableObject) ve Karakter Yetenekleri (Dash & Stamina)

- [ ] **Görev 1.1: Oyuncu İstatistikleri ScriptableObject (`PlayerStatsSO`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Data/PlayerStatsSO.cs`
  - **Sınıf Adı:** `PlayerStatsSO : ScriptableObject` (`[CreateAssetMenu(fileName = "PlayerStats", menuName = "CivCiv/Data/PlayerStatsSO")]`)
  - **Alanlar ve Metodlar:**
    - `public float MoveSpeed = 45f;`, `public float MaxSpeed = 7f;`, `public float GroundDrag = 4f;`, `public float AirDrag = 0.2f;`, `public float AirMultiplier = 0.4f;`
    - `public float JumpForce = 7f;`, `public float JumpCooldown = 0.25f;`
    - `public float MaxSlideSpeed = 13f;`, `public float SlideImpulseForce = 12f;`, `public float SlideSustainForce = 15f;`, `public float SlideDrag = 0.3f;`
    - `public float DashForce = 18f;`, `public float DashCooldown = 1.2f;`, `public float DashDuration = 0.2f;`, `public float DashStaminaCost = 35f;`
    - `public float MaxStamina = 100f;`, `public float StaminaRegenRate = 20f;`
    - `public void ValidateStats()` : Negatif veya sıfır değerleri güvenli alt sınırlarla (`Mathf.Max`) sınırlar (`OnValidate` içinde çağrılır).

- [ ] **Görev 1.2: `PlayerState` Enum Güncellemesi ve `PlayerController` SO Entegrasyonu**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Enums/PlayerState.cs` ve `Assets/_GameAssets/Scripts/PlayerController.cs`
  - **Sınıf Adı:** `PlayerState` (Enum) ve `PlayerController : MonoBehaviour`
  - **Metodlar ve Değişiklikler:**
    - `PlayerState.cs` içine `Dash` ve `Glide` durumlarını ekle.
    - `PlayerController.cs` içine `[SerializeField] private PlayerStatsSO _playerStats;` referansı ekle.
    - `public void ApplyStatsFromSO(PlayerStatsSO stats)` : Eğer `_playerStats != null` ise `Awake()` içinde hareket, zıplama ve slide değişkenlerini SO'dan yükler (atanmamışsa mevcut Inspector varsayılanlarını korur).
    - `public Rigidbody GetRigidbody()`, `public Transform GetOrientation()` ve `public bool IsGrounded()` yardımcı getter metodlarını dış bileşenler için ekle.
    - `public void SetSpeedMultiplier(float multiplier)` : Geçici hız güçlendirmeleri için hız çarpanını günceller.

- [ ] **Görev 1.3: İleri Atılma ve Enerji Kontrolcüsü (`DashController`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/DashController.cs`
  - **Sınıf Adı:** `DashController : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private PlayerStatsSO _playerStats;`
    - `[SerializeField] private KeyCode _dashKey = KeyCode.LeftShift;`
    - `public event System.Action<float, float> OnStaminaChanged;` (`currentStamina`, `maxStamina`)
    - `public event System.Action OnDashExecuted;`
    - `public bool IsDashing { get; private set; }`
  - **Metodlar:**
    - `private void HandleDashInput()` : Yeni Input System (`Keyboard.current.leftShiftKey.wasPressedThisFrame`) ve eski Input (`Input.GetKeyDown(_dashKey)`) üzerinden girdi alır.
    - `public bool CanDash()` : Cooldown süresi dolmuş mu, `currentStamina >= dashStaminaCost` mu ve `SettingsUI.IsOpen == false` mu kontrol eder.
    - `public void ExecuteDash()` : `PlayerController.GetOrientation()` yönünde `Rigidbody.AddForce(dashDir * dashForce, ForceMode.Impulse)` uygular, `StateController.ChangeState(PlayerState.Dash)` çağırır, staminayı düşürür ve `OnStaminaChanged` event'ini tetikler.
    - `public void RefillStamina(float amount)` : Dışarıdan stamina doldurur ve `OnStaminaChanged` tetikler.
    - `public bool TryConsumeStamina(float amount)` : Belirtilen miktarda stamina varsa harcar ve `true` döner.
    - `private void RegenerateStamina()` : `Update` içinde karakter dash atmıyorken staminayı saniyede `StaminaRegenRate` kadar doldurur.

---

## Faz 2: Çekirdek Oyun Döngüsü (GameManager, GameState ve Zamanlayıcı Entegrasyonu)

- [ ] **Görev 2.1: Oyun Durumu Enum Tanımı (`GameState`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Enums/GameState.cs`
  - **Sınıf Adı:** `GameState` (Enum)
  - **Değerler:** `Playing`, `Paused`, `Victory`, `GameOver`

- [ ] **Görev 2.2: Merkezi Oyun Yöneticisi (`GameManager`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Core/GameManager.cs`
  - **Sınıf Adı:** `GameManager : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `public static GameManager Instance { get; private set; }`
    - `public GameState CurrentState { get; private set; } = GameState.Playing;`
    - `public static event System.Action<GameState> OnGameStateChanged;`
    - `[SerializeField] private int _targetCoinsForVictory = 0;` (0 ise sahnedeki tüm `CoinCollect` sayısı `Start` içinde otomatik hesaplanır).
  - **Metodlar:**
    - `public void SetGameState(GameState newState)` : Durumu günceller, `OnGameStateChanged` event'ini tetikler. `Victory` veya `GameOver` durumunda `Cursor.lockState = CursorLockMode.None`, `Cursor.visible = true` yapar.
    - `private void HandleCoinsChanged(int currentCoins)` : `CoinManager.Instance.CoinsChanged` event'ini dinler; `_targetCoinsForVictory > 0 && currentCoins >= _targetCoinsForVictory` olduğunda `TriggerVictory()` çağırır.
    - `public void TriggerVictory()` : `SetGameState(GameState.Victory)` çağırır ve `CoinManager.Instance.SaveHighScore()` tetikler.
    - `public void TriggerGameOver()` : `SetGameState(GameState.GameOver)` çağırır.
    - `public void RestartLevel()` : `Time.timeScale = 1f;` yapıp aktif sahneyi `SceneManager.LoadScene` ile yeniden yükler.

- [ ] **Görev 2.3: `ClockUI` Süre Bitimi Eventi ve `CoinManager` HighScore Kaydı**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/UI/ClockUI.cs` ve `Assets/_GameAssets/Scripts/UI/CoinManager.cs`
  - **Sınıf Adları:** `ClockUI : MonoBehaviour`, `CoinManager : MonoBehaviour`
  - **Metodlar:**
    - `ClockUI.cs` -> `public event System.Action OnTimerExpired;` ve `public float RemainingSeconds => remainingSeconds;` ekle.
    - `ClockUI.cs` -> `public void AddBonusTime(float seconds)` : Dışarıdan süre eklemek için public metod.
    - `ClockUI.cs` -> `Update()` içinde `remainingSeconds <= 0f` olduğunda bir kez `OnTimerExpired?.Invoke()` çağır ve `GameManager.Instance != null` ise `GameManager.Instance.TriggerGameOver()` tetikle.
    - `CoinManager.cs` -> `public int HighScore { get; private set; }` alanı ekle.
    - `CoinManager.cs` -> `public void LoadHighScore()` ve `public void SaveHighScore()` metodlarını `PlayerPrefs` (`"CivCiv_HighScore"`) kullanarak yaz; `AddCoins` içinde yeni rekor kırılırsa otomatik kaydet.

---

## Faz 5: Gelişmiş CivCiv Hareket Mekanikleri (Kanat Çırpma, Süzülme & Gaga Hamlesi)

- [ ] **Görev 5.1: Çift Zıplama ve Kanat Çırparak Süzülme (`ChickGlideController`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Player/ChickGlideController.cs`
  - **Sınıf Adı:** `ChickGlideController : MonoBehaviour`
  - **Alanlar:**
    - `[SerializeField] private int _maxAirJumps = 1;`
    - `[SerializeField] private float _airJumpForce = 6.5f;`
    - `[SerializeField] private float _glideFallSpeedLimit = -2.2f;`
    - `[SerializeField] private float _glideStaminaDrainPerSec = 12f;`
    - `public bool IsGliding { get; private set; }`
  - **Metodlar:**
    - `private void HandleAirInput()` : Havadayken (`!_playerController.IsGrounded()`) Space tuşuna basıldığında `_remainingAirJumps > 0` ise `ExecuteAirJump()` çağırır; Space basılı tutulurken düşüş hızı negatifse `StartGlide()` başlatır.
    - `public void ExecuteAirJump()` : Dikey hızı sıfırlayıp `_airJumpForce` ile `ForceMode.Impulse` uygular ve `_remainingAirJumps` değerini 1 azaltır.
    - `private void ApplyGlidePhysics()` : `FixedUpdate` içinde `IsGliding` aktifken `Rigidbody.linearVelocity.y` değerini `Mathf.Max(vel.y, _glideFallSpeedLimit)` ile sınırlar ve `StateController.ChangeState(PlayerState.Glide)` yapar.
    - `public void StopGlide()` : Zemin temasında veya stamina bittiğinde süzülmeyi sonlandırır ve `_remainingAirJumps = _maxAirJumps` olarak sıfırlar.

- [ ] **Görev 5.2: Gaga Vuruşu ve Yere Çakılma (`ChickPeckAttack`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Player/ChickPeckAttack.cs`
  - **Sınıf Adı:** `ChickPeckAttack : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private float _peckRange = 1.8f;`, `[SerializeField] private float _peckRadius = 0.8f;`, `[SerializeField] private int _peckDamage = 1;`
    - `[SerializeField] private float _groundPoundForce = 25f;`
    - `public event System.Action OnPeckPerformed;`
    - `public event System.Action<Vector3> OnGroundPoundImpact;`
  - **Metodlar:**
    - `public void PerformPeck()` : Sol fare tuşu veya `F` tuşu ile karakterin önünde `Physics.OverlapSphere` taraması yapar, `IDamageable` veya kırılabilir nesnelere (`BreakableCrate`) hasar verir.
    - `public void StartGroundPound()` : Havadayken `LeftControl` tuşuna basıldığında yatay hızı sıfırlayıp aşağı yönlü `Vector3.down * _groundPoundForce` (`ForceMode.VelocityChange`) uygular; yere değdiği anda `OnGroundPoundImpact` tetikler.

---

## Faz 6: Can, Hasar ve Savunma Sistemi (Health & Damage Architecture)

- [ ] **Görev 6.1: Hasar Alabilirlik Arayüzü (`IDamageable`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Core/IDamageable.cs`
  - **Arayüz Adı:** `IDamageable`
  - **Metodlar:**
    - `void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitNormal);`
    - `bool IsAlive { get; }`

- [ ] **Görev 6.2: Oyuncu Can, Dokunulmazlık ve Geri Tepme Bileşeni (`PlayerHealth`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Player/PlayerHealth.cs`
  - **Sınıf Adı:** `PlayerHealth : MonoBehaviour, IDamageable`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private int _maxHealth = 3;`
    - `[SerializeField] private float _invincibilityDuration = 1.5f;`
    - `[SerializeField] private float _knockbackForce = 8f;`
    - `public int CurrentHealth { get; private set; }`
    - `public bool IsAlive => CurrentHealth > 0;`
    - `public bool IsInvincible { get; private set; }`
    - `public event System.Action<int, int> OnHealthChanged;` (`current`, `max`)
    - `public event System.Action OnPlayerDied;`
  - **Metodlar:**
    - `public void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitNormal)` : `IsInvincible` veya `!IsAlive` ise işlem yapmaz; canı düşürür, `ApplyKnockback(-hitNormal)` uygular, `StartCoroutine(InvincibilityFlashRoutine())` başlatır ve can 0 ise `Die()` çağırır.
    - `public void Heal(int amount)` : Canı `_maxHealth` sınırına kadar artırır ve `OnHealthChanged` tetikler.
    - `private void ApplyKnockback(Vector3 direction)` : `PlayerController.GetRigidbody()` üzerine geri savrulma kuvveti uygular.
    - `private void Die()` : `OnPlayerDied?.Invoke()` ve `GameManager.Instance?.TriggerGameOver()` çağırır.

- [ ] **Görev 6.3: Kalp / Can Göstergesi HUD Bileşeni (`HealthUI`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/UI/HealthUI.cs`
  - **Sınıf Adı:** `HealthUI : MonoBehaviour`
  - **Alanlar ve Metodlar:**
    - `[SerializeField] private TMPro.TextMeshProUGUI _healthText;`
    - `[SerializeField] private UnityEngine.UI.Image[] _heartIcons;`
    - `private void HandleHealthChanged(int current, int max)` : `PlayerHealth.OnHealthChanged` event'ini dinleyerek `_heartIcons` dizisindeki kalp görsellerinin doluluk/renk durumunu ve `_healthText` metnini günceller.

---

## Faz 7: Kombo, Çarpan ve Mıknatıs Sistemi (Score & Combo Mechanics)

- [ ] **Görev 7.1: Kombo ve Skor Çarpanı Yöneticisi (`ComboManager`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Core/ComboManager.cs`
  - **Sınıf Adı:** `ComboManager : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `public static ComboManager Instance { get; private set; }`
    - `[SerializeField] private float _comboResetTime = 3.5f;`
    - `[SerializeField] private int _maxMultiplier = 5;`
    - `public int CurrentStreak { get; private set; }`
    - `public int CurrentMultiplier { get; private set; } = 1;`
    - `public float RemainingComboTimeNormalized { get; private set; }`
    - `public event System.Action<int, int, float> OnComboUpdated;` (`streak`, `multiplier`, `normalizedTimer`)
  - **Metodlar:**
    - `private void HandleCoinCollected(int baseAmount)` : `CoinManager.Instance.CoinsCollected` event'ini dinler; `_comboTimer = _comboResetTime` sıfırlar, `CurrentStreak++` artırır, her 3 seride `CurrentMultiplier` değerini `_maxMultiplier` sınırına kadar yükseltir.
    - `public void ResetCombo()` : Süre bittiğinde veya oyuncu hasar aldığında (`PlayerHealth.OnHealthChanged`) çarpanı 1'e ve seriyi 0'a sıfırlar.

- [ ] **Görev 7.2: Kombo Arayüz Bileşeni (`ComboUI`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/UI/ComboUI.cs`
  - **Sınıf Adı:** `ComboUI : MonoBehaviour`
  - **Alanlar ve Metodlar:**
    - `[SerializeField] private TMPro.TextMeshProUGUI _comboText;`
    - `[SerializeField] private UnityEngine.UI.Image _comboTimerFill;`
    - `private void UpdateComboView(int streak, int multiplier, float normalizedTime)` : `ComboManager.Instance.OnComboUpdated` event'ini dinler; çarpan > 1 ise `"x{multiplier} COMBO ({streak})"` metnini gösterir ve ölçek sıçrama (punch scale) efekti uygular.

- [ ] **Görev 7.3: Altın Mıknatısı Alanı (`CoinMagnet`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Collectables/CoinMagnet.cs`
  - **Sınıf Adı:** `CoinMagnet : MonoBehaviour`
  - **Alanlar ve Metodlar:**
    - `[SerializeField] private float _defaultMagnetRadius = 3f;`, `[SerializeField] private float _pullSpeed = 14f;`
    - `private bool _isSuperMagnetActive;`
    - `public void ActivateSuperMagnet(float radius, float duration)` : Belirtilen süre boyunca çekim yarıçapını büyütür.
    - `private void PullNearbyCoins()` : `Update()` içinde `Physics.OverlapSphere(transform.position, currentRadius)` ile yakındaki `CoinCollect` nesnelerini bulur ve oyuncuya doğru `Vector3.MoveTowards` ile çeker.

---

## Faz 8: Dinamik Platformlar ve Fiziksel Seviye Öğeleri

- [ ] **Görev 8.1: Noktalar Arası Hareketli Platform (`MovingPlatform`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Gameplay/MovingPlatform.cs`
  - **Sınıf Adı:** `MovingPlatform : MonoBehaviour`
  - **Alanlar ve Metodlar:**
    - `[SerializeField] private Vector3[] _localWaypoints;`, `[SerializeField] private float _moveSpeed = 3f;`, `[SerializeField] private float _waitDuration = 1f;`
    - `private Vector3 _platformVelocity;`
    - `private void MovePlatform()` : `FixedUpdate()` içinde `Rigidbody.MovePosition` ile platformu sıradaki waypoint'e taşır ve `_platformVelocity` vektörünü hesaplar.
    - `private void OnCollisionStay(Collision collision)` : Üzerinde duran `PlayerController` nesnesinin `Rigidbody` bileşenine platformun yer değiştirme deltasını uygular (oyuncunun platformdan kaymasını önler).

- [ ] **Görev 8.2: Çöken ve Yeniden Oluşan Platform (`FallingPlatform`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Gameplay/FallingPlatform.cs`
  - **Sınıf Adı:** `FallingPlatform : MonoBehaviour`
  - **Alanlar ve Metodlar:**
    - `[SerializeField] private float _fallDelay = 0.8f;`, `[SerializeField] private float _respawnDelay = 4f;`, `[SerializeField] private float _shakeIntensity = 0.06f;`
    - `private void OnCollisionEnter(Collision collision)` : `PlayerController` temas ettiğinde `StartCoroutine(FallSequenceRoutine())` başlatır.
    - `private System.Collections.IEnumerator FallSequenceRoutine()` : `_fallDelay` süresince görseli titretir, ardından `Collider` ve `Renderer` bileşenlerini kapatır, `_respawnDelay` sonra başlangıç konumunda tekrar aktif eder.

- [ ] **Görev 8.3: Rüzgar Akımı / Hava Yükseltici (`WindCurrentZone`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Gameplay/WindCurrentZone.cs`
  - **Sınıf Adı:** `WindCurrentZone : MonoBehaviour`
  - **Alanlar ve Metodlar:**
    - `[SerializeField] private Vector3 _windDirection = Vector3.up;`, `[SerializeField] private float _windForce = 18f;`, `[SerializeField] private float _glidingMultiplier = 2.2f;`
    - `private void OnTriggerStay(Collider other)` : İçerideki `PlayerController` nesnesinin `Rigidbody` bileşenine `_windDirection.normalized * _windForce` kuvveti uygular; eğer karakterde `ChickGlideController.IsGliding == true` ise kuvveti `_glidingMultiplier` ile çarpar.

---

## Faz 9: Kırılabilir Nesneler, Yumurtalar ve Ganimet (Loot) Sistemi

- [ ] **Görev 9.1: Ganimet Tablosu ScriptableObject (`LootTableSO`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Data/LootTableSO.cs`
  - **Sınıf Adı:** `LootTableSO : ScriptableObject` (`[CreateAssetMenu(fileName = "LootTable", menuName = "CivCiv/Data/LootTableSO")]`)
  - **Alanlar ve Metodlar:**
    - `[System.Serializable] public struct LootEntry { public GameObject Prefab; [Range(0f, 100f)] public float DropWeight; public int MinCount; public int MaxCount; }`
    - `public LootEntry[] Entries;`
    - `public void SpawnLoot(Vector3 origin)` : Ağırlıklı olasılık hesabına göre seçilen prefabları `origin` noktasında hafif yukarı/dışarı rastgele fiziksel sıçrama (`Rigidbody.AddForce`) ile oluşturur.

- [ ] **Görev 9.2: Kırılabilir Sandık ve Sürpriz Yumurta (`BreakableCrate`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Gameplay/BreakableCrate.cs`
  - **Sınıf Adı:** `BreakableCrate : MonoBehaviour, IDamageable`
  - **Alanlar ve Metodlar:**
    - `[SerializeField] private int _health = 1;`, `[SerializeField] private int _directCoinReward = 3;`, `[SerializeField] private LootTableSO _lootTable;`
    - `public bool IsAlive => _health > 0;`
    - `public void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitNormal)` : Hasar aldığında `_health -= amount` yapar, ölçek elastik sarsıntı efekti oynatır, `_health <= 0` ise `Break()` çağırır.
    - `private void OnCollisionEnter(Collision collision)` : Oyuncu `DashController.IsDashing` durumunda veya `ChickPeckAttack` (GroundPound) ile çarptığında `TakeDamage(_health, ...)` çağırarak sandığı kırar.
    - `private void Break()` : `_lootTable != null` ise `_lootTable.SpawnLoot(transform.position)` çağırır, ayrıca `_directCoinReward > 0` ise `CoinManager.Instance?.AddCoins(_directCoinReward)` ekler ve nesneyi yok eder.

---

## Faz 10: Düşman Yapay Zekası ve Tehditler (Predator & Hazard AI)

- [ ] **Görev 10.1: Düşman İstatistikleri ScriptableObject (`EnemyStatsSO`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Data/EnemyStatsSO.cs`
  - **Sınıf Adı:** `EnemyStatsSO : ScriptableObject` (`[CreateAssetMenu(fileName = "EnemyStats", menuName = "CivCiv/Data/EnemyStatsSO")]`)
  - **Alanlar:**
    - `public float PatrolSpeed = 2.5f;`, `public float ChaseSpeed = 5.2f;`, `public float DetectionRadius = 8f;`, `public float AttackRange = 1.4f;`, `public int ContactDamage = 1;`, `public int MaxHealth = 2;`

- [ ] **Görev 10.2: Devriye ve Takipçi Düşman Kontrolcüsü (`PatrolEnemyAI`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/AI/PatrolEnemyAI.cs`
  - **Sınıf Adı:** `PatrolEnemyAI : MonoBehaviour, IDamageable` (ve `public enum EnemyAIState { Patrol, Chase, Stunned, Dead }`)
  - **Alanlar ve Metodlar:**
    - `[SerializeField] private EnemyStatsSO _stats;`, `[SerializeField] private Transform[] _patrolPoints;`
    - `public EnemyAIState CurrentAIState { get; private set; } = EnemyAIState.Patrol;`
    - `public bool IsAlive => _currentHealth > 0;`
    - `private void UpdateAIState()` : Oyuncu ile mesafeyi ölçer; `DetectionRadius` içindeyse `EnemyAIState.Chase`, dışındaysa `EnemyAIState.Patrol` durumuna geçer.
    - `private void ExecutePatrol()` ve `private void ExecuteChase()` : Hedefe doğru yönelip hareket eder.
    - `private void OnCollisionEnter(Collision collision)` : Oyuncu düşmanın tam üstüne zıpladıysa (`collision.contacts[0].normal.y < -0.5f`) düşmana `TakeDamage(1, ...)` uygular ve oyuncuyu yukarı zıplatır; yandan çarptıysa `PlayerHealth.TakeDamage(_stats.ContactDamage, ...)` çağırır.
    - `public void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitNormal)` : Canı azaltır, `Stunned` durumuna sokar, can 0 ise `Die()` çağırır.

---

## Faz 11: Görev, Bulmaca ve Etkileşimli Çevre (Triggers & Quests)

- [ ] **Görev 11.1: Etkileşim Arayüzü ve Oyuncu Etkileşim Tarayıcısı (`IInteractable` & `PlayerInteractor`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Core/IInteractable.cs` ve `Assets/_GameAssets/Scripts/Player/PlayerInteractor.cs`
  - **Sınıflar:** `public interface IInteractable` ve `public class PlayerInteractor : MonoBehaviour`
  - **Metodlar:**
    - `IInteractable` -> `string GetInteractionPrompt();`, `void Interact(PlayerController player);`
    - `PlayerInteractor` -> `private void ScanForInteractables()` : Karakterin önünde `Physics.OverlapSphere` ile en yakın `IInteractable` nesneyi bulur, `OnInteractableFound` event'ini tetikler ve `E` tuşuna basıldığında `Interact(player)` çağırır.

- [ ] **Görev 11.2: Basınç Plakası ve Kapı/Köprü Tetikleyicisi (`PressurePlateSwitch`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Gameplay/PressurePlateSwitch.cs`
  - **Sınıf Adı:** `PressurePlateSwitch : MonoBehaviour`
  - **Alanlar ve Metodlar:**
    - `[SerializeField] private Transform _targetDoorOrBridge;`, `[SerializeField] private Vector3 _openPositionOffset = new Vector3(0f, 3f, 0f);`, `[SerializeField] private bool _stayOpen = true;`
    - `public bool IsActivated { get; private set; }`
    - `public event System.Action<bool> OnSwitchStateChanged;`
    - `private void OnTriggerEnter(Collider other)` ve `private void OnTriggerExit(Collider other)` : `PlayerController` veya `Rigidbody` plaka üzerine çıktığında `ActivateSwitch(true)` çağırarak hedef kapıyı/köprüyü `Vector3.Lerp` ile açar.

- [ ] **Görev 11.3: Kayıp CivCiv Kurtarma Görevi (`LostChickNPC` & `ChickRescueManager`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Gameplay/LostChickNPC.cs` ve `Assets/_GameAssets/Scripts/Core/ChickRescueManager.cs`
  - **Sınıf Adları:** `LostChickNPC : MonoBehaviour, IInteractable`, `ChickRescueManager : MonoBehaviour`
  - **Metodlar:**
    - `LostChickNPC.cs` -> `public void Interact(PlayerController player)` : Civcivi kurtarır (`_isRescued = true`), oyuncuyu belirli bir takip mesafesinden (`_followDistance = 1.8f`) kuyruk halinde takip etmeye başlar ve `ChickRescueManager.Instance.RegisterRescue(this)` çağırır.
    - `ChickRescueManager.cs` -> `public static ChickRescueManager Instance { get; private set; }`, `public int RescuedCount { get; private set; }`, `public int TotalChicksInLevel { get; private set; }`
    - `ChickRescueManager.cs` -> `public event System.Action<int, int> OnChickRescued;` (`rescued`, `total`)
    - `ChickRescueManager.cs` -> `public void RegisterRescue(LostChickNPC chick)` : Kurtarılan civciv sayısını artırır, bonus altın/süre verir ve event tetikler.

---

## Faz 12: Envanter, Kozmetik Mağaza ve Kalıcı Kayıt (Save & Shop System)

- [ ] **Görev 12.1: JSON Tabanlı Kalıcı Kayıt Yöneticisi (`SaveManager`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Core/SaveManager.cs`
  - **Sınıf Adı:** `SaveManager` (Static Class) ve `[System.Serializable] public class PlayerSaveData`
  - **Alanlar ve Metodlar:**
    - `PlayerSaveData` -> `public int TotalWalletCoins;`, `public int HighScore;`, `public string EquippedItemId;`, `public System.Collections.Generic.List<string> UnlockedItemIds = new();`, `public System.Collections.Generic.List<string> UnlockedAchievementIds = new();`
    - `public static PlayerSaveData CurrentData { get; private set; }`
    - `public static void Save()` : `JsonUtility.ToJson(CurrentData)` çıktısını `PlayerPrefs` (`"CivCiv_SaveDataJson"`) ve `Application.persistentDataPath + "/civciv_save.json"` dosyasına yazar.
    - `public static PlayerSaveData Load()` : Kayıtlı veriyi okur, yoksa varsayılan `PlayerSaveData` oluşturur.
    - `public static void AddToWallet(int amount)` ve `public static bool SpendFromWallet(int amount)`

- [ ] **Görev 12.2: Kozmetik Eşya Tanımı ve Mağaza Yöneticisi (`CosmeticItemSO` & `CosmeticShopManager`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Data/CosmeticItemSO.cs` ve `Assets/_GameAssets/Scripts/Core/CosmeticShopManager.cs`
  - **Sınıf Adları:** `CosmeticItemSO : ScriptableObject`, `CosmeticShopManager : MonoBehaviour`
  - **Alanlar ve Metodlar:**
    - `CosmeticItemSO` -> `public string ItemId;`, `public string DisplayName;`, `public int Price;`, `public GameObject HatAccessoryPrefab;`, `public Color ChickTintColor = Color.white;`
    - `CosmeticShopManager` -> `public static CosmeticShopManager Instance { get; private set; }`
    - `public bool TryPurchaseItem(CosmeticItemSO item)` : Eşya kilitliyse ve `SaveManager.SpendFromWallet(item.Price)` başarılıysa eşyayı `UnlockedItemIds` listesine ekler ve `EquipItem(item)` çağırır.
    - `public void EquipItem(CosmeticItemSO item)` : Karakterin kafa soketine (`HeadSocket`) şapka/aksesuar prefabını takar veya materyal rengini uygular.

---

## Faz 13: Başarım ve Meydan Okuma Sistemi (Achievements & Challenges)

- [ ] **Görev 13.1: Başarım Veri Yapısı ve Yöneticisi (`AchievementSO` & `AchievementManager`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Data/AchievementSO.cs` ve `Assets/_GameAssets/Scripts/Core/AchievementManager.cs`
  - **Sınıf Adları:** `AchievementSO : ScriptableObject`, `AchievementManager : MonoBehaviour`
  - **Alanlar ve Metodlar:**
    - `AchievementSO` -> `public string AchievementId;`, `public string Title;`, `public string Description;`, `public int TargetValue;`, `public int CoinReward;`
    - `AchievementManager` -> `public static AchievementManager Instance { get; private set; }`
    - `public static event System.Action<AchievementSO> OnAchievementUnlocked;`
    - `public void ReportProgress(string achievementId, int currentValue)` : Hedef değere ulaşıldığında başarımı `SaveManager.CurrentData.UnlockedAchievementIds` içine kaydeder, `SaveManager.AddToWallet(CoinReward)` verir ve `OnAchievementUnlocked` tetikler.
    - `private void SubscribeGameplayEvents()` : `CoinManager.CoinsChanged`, `ComboManager.OnComboUpdated` ve `ChickRescueManager.OnChickRescued` event'lerini dinleyerek başarımları otomatik kontrol eder.

- [ ] **Görev 13.2: Başarım Bildirim Pop-Up Arayüzü (`AchievementPopupUI`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/UI/AchievementPopupUI.cs`
  - **Sınıf Adı:** `AchievementPopupUI : MonoBehaviour`
  - **Alanlar ve Metodlar:**
    - `[SerializeField] private RectTransform _popupContainer;`, `[SerializeField] private TMPro.TextMeshProUGUI _titleText;`, `[SerializeField] private TMPro.TextMeshProUGUI _rewardText;`
    - `private void EnqueueAchievement(AchievementSO achievement)` : `AchievementManager.OnAchievementUnlocked` event'ini dinler ve ekranda 3 saniye kalıp kaybolan bildirim kuyruğunu (`Coroutine`) çalıştırır.

---

## Faz 14: Nesne Havuzu ve Parçacık / Efekt Yöneticisi (Object Pooling & VFX)

- [ ] **Görev 14.1: Genel Nesne Havuzu Yöneticisi (`ObjectPoolManager`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Core/ObjectPoolManager.cs`
  - **Sınıf Adı:** `ObjectPoolManager : MonoBehaviour`
  - **Alanlar ve Metodlar:**
    - `public static ObjectPoolManager Instance { get; private set; }`
    - `private readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.Queue<GameObject>> _pools = new();`
    - `public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)` : Havuzda pasif nesne varsa çıkarıp aktif eder, yoksa `Instantiate` ile üretir.
    - `public void Despawn(GameObject instance, int prefabInstanceId, float delay = 0f)` : Nesneyi pasif hale getirip tekrar havuz kuyruğuna ekler.

- [ ] **Görev 14.2: Karakter Toz, Tüy ve Darbe Efekt Kontrolcüsü (`PlayerVFXController`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Player/PlayerVFXController.cs`
  - **Sınıf Adı:** `PlayerVFXController : MonoBehaviour`
  - **Alanlar ve Metodlar:**
    - `[SerializeField] private ParticleSystem _footstepDustParticles;`, `[SerializeField] private ParticleSystem _jumpFeatherParticles;`, `[SerializeField] private TrailRenderer _dashTrailRenderer;`
    - `private void HandleStateVFX(PlayerState state)` : Karakter `Move` durumundayken ayak tozu, `Jump` ve `Glide` durumunda tüy efekti, `Dash` ve `Slide` durumunda `_dashTrailRenderer.emitting = true` aktif eder; sahnede prefab atanmamışsa `Awake` içinde prosedürel `TrailRenderer` ve `ParticleSystem` bileşenlerini kodla oluşturur.

---

## Faz 15: Kamera Sarsıntısı, Dinamik FOV ve Görsel Cilalama (Camera Feel)

- [ ] **Görev 15.1: Kamera Sarsıntısı ve Dinamik FOV Efektleri (`CameraJuiceController`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Camera/CameraJuiceController.cs`
  - **Sınıf Adı:** `CameraJuiceController : MonoBehaviour`
  - **Alanlar ve Metodlar:**
    - `public static CameraJuiceController Instance { get; private set; }`
    - `[SerializeField] private float _baseFov = 60f;`, `[SerializeField] private float _dashFov = 74f;`, `[SerializeField] private float _fovLerpSpeed = 10f;`
    - `public void TriggerShake(float intensity, float duration)` : Aktif kamera veya `ThirdPersonCameraController` hedefi üzerinde sönümlenen (`Mathf.PerlinNoise`) sarsıntı ofseti uygular.
    - `private void UpdateDynamicFOV()` : Karakter `DashController.IsDashing` veya `PlayerState.Slide` durumundayken `CinemachineCamera.Lens.FieldOfView` (veya `Camera.main.fieldOfView`) değerini `_dashFov` değerine yumuşakça genişletir.
    - `private void SubscribeEvents()` : `DashController.OnDashExecuted`, `PlayerHealth.OnHealthChanged` ve `ChickPeckAttack.OnGroundPoundImpact` olaylarında otomatik kamera sarsıntısı tetikler.

---

## Faz 16: Gelişmiş Ses Mimarisi ve Yüzey Adım Sesleri (Audio & Footsteps)

- [ ] **Görev 16.1: Ses Kütüphanesi ScriptableObject (`AudioLibrarySO`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Data/AudioLibrarySO.cs`
  - **Sınıf Adı:** `AudioLibrarySO : ScriptableObject` (`[CreateAssetMenu(fileName = "AudioLibrary", menuName = "CivCiv/Data/AudioLibrarySO")]`)
  - **Alanlar ve Metodlar:**
    - `public AudioClip[] FootstepClips;`, `public AudioClip[] JumpClips;`, `public AudioClip[] ChickChirpClips;`, `public AudioClip[] DashClips;`, `public AudioClip[] CoinClips;`, `public AudioClip[] HurtClips;`
    - `public AudioClip GetRandomClip(AudioClip[] clips)` : Diziden rastgele bir `AudioClip` döndürür.

- [ ] **Görev 16.2: 3D konumsal Ses Havuzu ve Adım Sesi Tetikleyicisi (`SpatialAudioManager` & `FootstepAudioTrigger`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Core/SpatialAudioManager.cs` ve `Assets/_GameAssets/Scripts/Player/FootstepAudioTrigger.cs`
  - **Sınıf Adları:** `SpatialAudioManager : MonoBehaviour`, `FootstepAudioTrigger : MonoBehaviour`
  - **Metodlar:**
    - `SpatialAudioManager.cs` -> `public static SpatialAudioManager Instance { get; private set; }`
    - `SpatialAudioManager.cs` -> `public void Play3DSound(AudioClip clip, Vector3 position, float volume = 1f, float pitchVariance = 0.08f)` : `SettingsUI.IsSoundEnabled` aktifse rastgele pitch (`1f + Random.Range(-pitchVariance, pitchVariance)`) ile ses çalar.
    - `FootstepAudioTrigger.cs` -> `private void UpdateFootstepTimer()` : Karakter yerde hareket ederken (`PlayerState.Move`) hızla orantılı aralıklarla (`_stepInterval = 0.35f`) adım sesi ve zıplarken civciv ötme (`ChickChirpClips`) sesi çalar.

---

## Faz 17: Dinamik Gece/Gündüz Döngüsü ve Hava Durumu (Environment System)

- [ ] **Görev 17.1: Gece/Gündüz ve Işık Döngüsü Kontrolcüsü (`DayNightCycleController`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Environment/DayNightCycleController.cs`
  - **Sınıf Adı:** `DayNightCycleController : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private Light _directionalLight;`
    - `[SerializeField] private float _dayDurationInSeconds = 180f;`
    - `[SerializeField][Range(0f, 1f)] private float _timeOfDay = 0.3f;` (0: Gece yarısı, 0.25: Gün doğumu, 0.5: Öğle, 0.75: Gün batımı)
    - `public bool IsNightTime => _timeOfDay < 0.22f || _timeOfDay > 0.78f;`
    - `public event System.Action<bool> OnNightStateChanged;`
  - **Metodlar:**
    - `private void UpdateSunRotationAndLighting()` : `Update()` içinde `_timeOfDay` değerini ilerletir, `_directionalLight.transform.rotation` açısını, ışık yoğunluğunu (`intensity`) ve `RenderSettings.ambientLight` rengini zamana göre yumuşakça günceller.
    - `public void SetTimeOfDay(float normalizedTime)` : Zamanı manuel ayarlar ve gece/gündüz değişiminde `OnNightStateChanged` tetikler.

---

## Faz 18: Mini Harita, Pusula ve Hedef İşaretleyici (Minimap & Waypoint HUD)

- [ ] **Görev 18.1: 3D Hedef ve Kayıp CivCiv Ekran İşaretleyicisi (`WaypointIndicatorUI`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/UI/WaypointIndicatorUI.cs`
  - **Sınıf Adı:** `WaypointIndicatorUI : MonoBehaviour`
  - **Alanlar ve Metodlar:**
    - `[SerializeField] private RectTransform _indicatorIcon;`, `[SerializeField] private TMPro.TextMeshProUGUI _distanceText;`, `[SerializeField] private Vector3 _worldOffset = new Vector3(0f, 1.5f, 0f);`
    - `private Transform _currentTarget;`
    - `public void FindNearestObjective()` : Sahnedeki kurtarılmamış en yakın `LostChickNPC` veya `CoinCollect` nesnesini hedef olarak seçer.
    - `private void UpdateScreenPosition()` : `LateUpdate()` içinde `Camera.main.WorldToScreenPoint(_currentTarget.position + _worldOffset)` ile hedefin ekran koordinatını hesaplar, kamera arkasındaysa ekran kenarına (`Screen.width`, `Screen.height` sınırlarına) sabitler ve mesafeyi metre (`"14m"`) olarak yazar.

---

## Faz 19: Seviye İlerleme, Bölüm Seçimi ve Ana Menü (Level Progression)

- [ ] **Görev 19.1: Seviye Bilgisi ScriptableObject ve Bölüm Yöneticisi (`LevelDataSO` & `LevelProgressionManager`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Data/LevelDataSO.cs` ve `Assets/_GameAssets/Scripts/Core/LevelProgressionManager.cs`
  - **Sınıf Adları:** `LevelDataSO : ScriptableObject`, `LevelProgressionManager : MonoBehaviour`
  - **Alanlar ve Metodlar:**
    - `LevelDataSO` -> `public string LevelId;`, `public string LevelDisplayName;`, `public string SceneName;`, `public int OneStarScore = 5;`, `public int TwoStarScore = 15;`, `public int ThreeStarScore = 30;`
    - `LevelProgressionManager` -> `public static LevelProgressionManager Instance { get; private set; }`
    - `public int CalculateStarsEarned(int coinsCollected, float remainingSeconds)` : Toplanan altın ve kalan süreye göre 1-3 arası yıldız puanı hesaplar ve `PlayerPrefs` (`"CivCiv_Stars_" + LevelId`) olarak kaydeder.
    - `public void LoadNextLevel()` : Sıradaki sahneyi yükler; tek sahne varsa zorluk çarpanını artırarak sahneyi yeniden başlatır.

---

## Faz 20: Bütünsel Entegrasyon, Otomatik Sahne Kurulumu ve Gece Vardiyası Raporu

- [ ] **Görev 20.1: `CivCivAutoSetup` Tam Sistem Kurulum Sihirbazı (`SetupCompleteGameArchitecture`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/CivCivAutoSetup.cs`
  - **Sınıf Adı:** `CivCivAutoSetup` (Static Editor Class)
  - **Metodlar:**
    - `[MenuItem("Tools/CivCiv/Tüm Oyun Sistemlerini (Faz 1-20) Otomatik Kur %#m")] public static void SetupCompleteGameArchitecture()` :
      - `Assets/_GameAssets/Data/` altında varsayılan `PlayerStatsSO`, `EnemyStatsSO`, `LootTableSO`, `AudioLibrarySO` ve `LevelDataSO` asset'lerini otomatik oluşturur.
      - Sahnedeki `PlayerController` nesnesine `DashController`, `ChickGlideController`, `ChickPeckAttack`, `PlayerHealth`, `CoinMagnet`, `PlayerInteractor`, `PlayerVFXController` ve `FootstepAudioTrigger` bileşenlerini eksiksiz ekler ve referanslarını bağlar.
      - Sahnede `_Managers` kök nesnesi altında `GameManager`, `ComboManager`, `ChickRescueManager`, `CosmeticShopManager`, `AchievementManager`, `ObjectPoolManager`, `SpatialAudioManager`, `LevelProgressionManager` ve `DayNightCycleController` yöneticilerini kurar.
      - `Canvas` altında `GameplayHUD`, `HealthUI`, `ComboUI`, `AchievementPopupUI` ve `WaypointIndicatorUI` bileşenlerini oluşturur.

- [ ] **Görev 20.2: Çalışma Zamanı Hata ve Referans Doğrulayıcı (`RuntimeSystemValidator`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Core/RuntimeSystemValidator.cs`
  - **Sınıf Adı:** `RuntimeSystemValidator : MonoBehaviour`
  - **Metodlar:**
    - `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] private static void EnsureCriticalSystemsOnLoad()` : Sahne yüklendiğinde temel yöneticiler (`GameManager`, `CoinManager`, `ComboManager`, `ObjectPoolManager`) sahnede eksikse çalışma zamanında otomatik oluşturarak `NullReferenceException` oluşmasını tamamen engeller.

- [ ] **Görev 20.3: Son Derleme Testi, Mimari Harita Güncellemesi ve Gece Vardiyası Raporu**
  - **İşlem:**
    1. `dotnet build Assembly-CSharp.csproj` ve `dotnet build Assembly-CSharp-Editor.csproj` komutlarıyla tüm çalışma zamanı ve editör scriptlerinin sıfır hatayla derlendiğini doğrula.
    2. `ARCHITECTURE.md` dosyasını Faz 1 - Faz 20 arasında eklenen tüm yeni sınıflarla güncelle.
    3. `NIGHT_REPORT.md` dosyasına tamamlanan fazların ve sistemlerin özet raporunu oluştur.
