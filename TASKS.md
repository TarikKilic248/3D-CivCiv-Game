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

## Faz 3: Toplanabilir Öğeler, Seviye Mekanikleri ve Etkileşimler

- [ ] **Görev 3.1: `CoinCollect` Görsel/İşitsel Efekt Geliştirmesi**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/UI/CoinCollect.cs`
  - **Sınıf Adı:** `CoinCollect : MonoBehaviour, ICollectable`
  - **Alanlar ve Metodlar:**
    - `[SerializeField] private float _rotationSpeed = 90f;`, `[SerializeField] private float _bobAmplitude = 0.15f;`, `[SerializeField] private float _bobFrequency = 2f;`, `[SerializeField] private AudioClip _collectSfx;`
    - `private void AnimateCoin()` : `Update()` içinde objeyi Y ekseninde döndürür ve `Mathf.Sin` ile yukarı-aşağı süzülme hareketi yaptırır.
    - `public void Collect()` : Çoklu tetiklenmeyi önlemek için `_isCollected` bayrağı kontrolü yapar, `SettingsUI.PlaySFX(_collectSfx)` çağırır ve `StartCoroutine(CollectAnimationRoutine())` ile objeyi 0.15 saniyede küçülterek (`Vector3.Lerp`) `Destroy(gameObject)` yapar.

- [ ] **Görev 3.2: Çok Amaçlı Güçlendirici ve Bonus Öğesi (`PowerUpCollectable`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Collectables/PowerUpCollectable.cs`
  - **Sınıf Adı:** `PowerUpCollectable : MonoBehaviour, ICollectable` (ve `public enum PowerUpType { ExtraTime, SpeedBoost, StaminaRefill }`)
  - **Alanlar ve Metodlar:**
    - `[SerializeField] private PowerUpType _powerUpType = PowerUpType.ExtraTime;`, `[SerializeField] private float _amount = 10f;`, `[SerializeField] private float _duration = 5f;`
    - `public void Collect()` : `_powerUpType` değerine göre `ApplyPowerUp(PlayerController player)` çağırır.
    - `private void ApplyPowerUp(PlayerController player)` : `ExtraTime` ise `FindAnyObjectByType<ClockUI>()?.AddBonusTime(_amount)`, `StaminaRefill` ise `player.GetComponent<DashController>()?.RefillStamina(_amount)`, `SpeedBoost` ise `player.SetSpeedMultiplier(_amount)` çağırır.

- [ ] **Görev 3.3: Tehlike Alanı ve Yeniden Doğma Noktası (`HazardZone` & `Checkpoint`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Gameplay/Checkpoint.cs` ve `Assets/_GameAssets/Scripts/Gameplay/HazardZone.cs`
  - **Sınıf Adları:** `Checkpoint : MonoBehaviour`, `HazardZone : MonoBehaviour`
  - **Metodlar:**
    - `Checkpoint.cs` -> `public static Vector3 ActiveRespawnPoint { get; private set; }`, `public static bool HasCheckpoint { get; private set; }`
    - `Checkpoint.cs` -> `public static void SetCheckpoint(Vector3 position)` ve `public static void ResetCheckpoints()`
    - `Checkpoint.cs` -> `private void OnTriggerEnter(Collider other)` : `PlayerController` temas ettiğinde `SetCheckpoint(transform.position + Vector3.up * 0.5f)` kaydeder.
    - `HazardZone.cs` -> `private void OnTriggerEnter(Collider other)` : Oyuncu suya, boşluğa veya tuzağa düştüğünde `RespawnPlayer(PlayerController player)` metodunu çağırır; karakterin `Rigidbody.linearVelocity` değerini sıfırlar ve `Checkpoint.ActiveRespawnPoint` noktasına ışınlar.

---

## Faz 4: Arayüz (HUD) Genişletmesi ve Editör Otomasyonu

- [ ] **Görev 4.1: Stamina Barı ve Oyun Sonu (Victory / GameOver) HUD Yöneticisi (`GameplayHUD`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/UI/GameplayHUD.cs`
  - **Sınıf Adı:** `GameplayHUD : MonoBehaviour`
  - **Alanlar:**
    - `[SerializeField] private UnityEngine.UI.Slider _staminaSlider;`
    - `[SerializeField] private GameObject _endGamePanel;`
    - `[SerializeField] private TMPro.TextMeshProUGUI _endGameTitleText;`
    - `[SerializeField] private TMPro.TextMeshProUGUI _endGameStatsText;`
    - `[SerializeField] private UnityEngine.UI.Button _restartButton;`
  - **Metodlar:**
    - `private void HandleStaminaChanged(float current, float max)` : `DashController.OnStaminaChanged` event'ine abone olur, `_staminaSlider.value = current / max` günceller.
    - `private void HandleGameStateChanged(GameState state)` : `GameManager.OnGameStateChanged` event'ine abone olur; `Victory` veya `GameOver` durumunda `_endGamePanel` nesnesini aktif eder, toplanan `CoinManager.Instance.Coins` ve `HighScore` bilgisini yazar.

- [ ] **Görev 4.2: `CivCivAutoSetup` Editör Aracının Yeni Sistemlerle Güncellenmesi**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/CivCivAutoSetup.cs`
  - **Sınıf Adı:** `CivCivAutoSetup` (Static Editor Class)
  - **Metodlar:**
    - `[MenuItem("Tools/CivCiv/Oyun Yöneticilerini ve PlayerStats SO Kur %#g")] public static void SetupCoreManagers()` :
      - `Assets/_GameAssets/Data/DefaultPlayerStats.asset` yolunda `PlayerStatsSO` yoksa `AssetDatabase.CreateAsset` ile otomatik oluşturur.
      - Sahnedeki `PlayerController` üzerine `DashController` bileşenini ekler ve `DefaultPlayerStats` asset'ini bağlar.
      - Sahnede `GameManager` yoksa oluşturur ve `GameplayHUD` bileşenini `Canvas` altına bağlar.

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
