# 3D CivCiv Game - Kapsamlı Geliştirme Yol Planı ve Standart Otonom Görev Listesi (Faz 1 - Faz 20)

## Standart Faz ve Görev Şablonu (GEMINI.md Uyumlu)

Bu belgedeki tüm fazlar (**Faz 1 - Faz 20**) ve alt görevler eksiksiz olarak aşağıdaki standart yapıyı takip eder:
1. **Alt Görevler (`Görev X.1`, `Görev X.2`, ...):**
   - **Dosya Yolu:** Tam proje içi konum (`Assets/_GameAssets/Scripts/...`).
   - **Sınıf / Arayüz Adı:** Kalıtım ve attribute bilgileri.
   - **Alanlar ve Eventler:** `[SerializeField]` alanları, public property'ler ve C# `event` tanımları.
   - **Metodlar ve İşleyiş:** Her metodun imzası, parametreleri ve çalışma mantığı.
   - **Durum Notu:** Dün/önceki vardiyada kodlanmış scriptler `[KODLANDI - Menü & Test Doğrulaması Bekliyor]` etiketiyle işaretlenmiştir; eksik/atlanan kısımları varsa tamamlanacak, ardından faz sonu menü ve test görevleriyle doğrulanacaktır.
2. **Faz Sonu Standart Görevi (`Görev X.Son`):**
   - **Night Agent Editör Menüsü (`Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs`):**
     - `[MenuItem("Tools/Night Agent/Faz X: <Faz Başlığı>/Uygula")]` : İlgili fazın tüm bileşenlerini, ScriptableObject asset'lerini ve UI/sahne objelerini aktif sahneye kurar/bağlar ve `EditorSceneManager.MarkSceneDirty` çağırır.
     - `[MenuItem("Tools/Night Agent/Faz X: <Faz Başlığı>/Geri çek")]` : İlgili fazın sahneye eklediği bileşenleri (`DestroyImmediate`) ve oluşturduğu UI/sahne objelerini temizler.
     - `[MenuItem("Tools/Night Agent/Faz X: <Faz Başlığı>/Detay")]` : `EditorUtility.DisplayDialog` ile bu fazda hangi scriptlerin eklendiğini, sahnede nelerin değiştiğini ve nasıl test edileceğini özetleyen Türkçe bir pencere açar.
   - **Play Mode Test & Görsel Doğrulama (`Assets/_GameAssets/Tests/PlayMode/PhaseXTests.cs`):**
     - Geçici sahne kurulumuyla (`SceneManager.CreateScene` / boş sahne) ilgili fazın tüm bileşenlerini (UI dahil) kodla oluşturur, mekanikleri çalıştırarak `Assert` ile doğrular ve `ScreenCapture.CaptureScreenshot("TestScreenshots/fazX_sonucu.png")` ile ekran görüntüsü alır.

---

## Faz 2: Çekirdek Oyun Döngüsü (GameManager, GameState ve Zamanlayıcı Entegrasyonu)

- [ ] **Görev 2.1: Oyun Durumu Enum Tanımı (`GameState`)** `[KODLANDI - Kontrol & Doğrulama]`
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Enums/GameState.cs`
  - **Sınıf Adı:** `GameState` (Enum)
  - **Değerler:** `Playing`, `Paused`, `Victory`, `GameOver`

- [ ] **Görev 2.2: Merkezi Oyun Yöneticisi (`GameManager`)** `[KODLANDI - Kontrol & Doğrulama]`
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Core/GameManager.cs`
  - **Sınıf Adı:** `GameManager : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `public static GameManager Instance { get; private set; }`
    - `public GameState CurrentState { get; private set; } = GameState.Playing;`
    - `public static event System.Action<GameState> OnGameStateChanged;`
    - `[SerializeField] private int _targetCoinsForVictory = 0;` (0 ise sahnedeki tüm `CoinCollect` sayısı `Start` içinde otomatik hesaplanır).
  - **Metodlar ve İşleyiş:**
    - `public void SetGameState(GameState newState)` : Durumu günceller, `OnGameStateChanged` event'ini tetikler. `Victory` veya `GameOver` durumunda `Cursor.lockState = CursorLockMode.None`, `Cursor.visible = true` yapar.
    - `private void HandleCoinsChanged(int currentCoins)` : `CoinManager.Instance.CoinsChanged` event'ini dinler; `_targetCoinsForVictory > 0 && currentCoins >= _targetCoinsForVictory` olduğunda `TriggerVictory()` çağırır.
    - `public void TriggerVictory()` : `SetGameState(GameState.Victory)` çağırır ve `CoinManager.Instance.SaveHighScore()` tetikler.
    - `public void TriggerGameOver()` : `SetGameState(GameState.GameOver)` çağırır.
    - `public void RestartLevel()` : `Time.timeScale = 1f;` yapıp aktif sahneyi `SceneManager.LoadScene` ile yeniden yükler.

- [ ] **Görev 2.3: `ClockUI` Süre Bitimi Eventi ve `CoinManager` HighScore Kaydı** `[KODLANDI - Kontrol & Doğrulama]`
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/UI/ClockUI.cs` ve `Assets/_GameAssets/Scripts/UI/CoinManager.cs`
  - **Sınıf Adları:** `ClockUI : MonoBehaviour`, `CoinManager : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `ClockUI` -> `public event System.Action OnTimerExpired;`, `public float RemainingSeconds => remainingSeconds;`
    - `CoinManager` -> `public int HighScore { get; private set; }`
  - **Metodlar ve İşleyiş:**
    - `ClockUI.AddBonusTime(float seconds)` : Kalan süreye bonus saniye ekler ve zamanlayıcı UI metnini/ibresini günceller.
    - `ClockUI.Update()` : `remainingSeconds <= 0f` olduğunda bir kez `OnTimerExpired?.Invoke()` çağırır ve `GameManager.Instance?.TriggerGameOver()` tetikler.
    - `CoinManager.LoadHighScore()` ve `CoinManager.SaveHighScore()` : `PlayerPrefs` (`"CivCiv_HighScore"`) üzerinden rekor skoru yükler/kaydeder; `AddCoins` içinde yeni rekor kırılırsa otomatik kaydeder.

- [ ] **Görev 2.Son: Faz 2 Night Agent Menü Entegrasyonu, Play Mode Testi ve Görsel Doğrulama**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` ve `Assets/_GameAssets/Tests/PlayMode/Phase2Tests.cs`
  - **Menü Öğeleri:**
    - `[MenuItem("Tools/Night Agent/Faz 2: Çekirdek Oyun Döngüsü ve GameManager/Uygula")]` : Sahnede `GameManager` ve `CoinManager` yoksa oluşturur, `ClockUI` bağlantısını doğrular ve sahneyi dirty işaretler.
    - `[MenuItem("Tools/Night Agent/Faz 2: Çekirdek Oyun Döngüsü ve GameManager/Geri çek")]` : Night Agent tarafından eklenen `GameManager` nesnesini sahneden temizler.
    - `[MenuItem("Tools/Night Agent/Faz 2: Çekirdek Oyun Döngüsü ve GameManager/Detay")]` : `GameState`, `GameManager`, `ClockUI` süre bitimi ve `HighScore` sistemini açıklayan Türkçe diyalog penceresi açar.
  - **Play Mode Test & Görsel İnceleme:**
    - `Phase2Tests.cs` içinde geçici sahnede `GameManager`, `CoinManager` ve `ClockUI` kur; altın toplandığında `Victory` durumuna geçişi ve `HighScore` kaydını `Assert` ile doğrula; `TestScreenshots/faz2_sonucu.png` ekran görüntüsünü kaydet ve incele.

---

## Faz 3: Toplanabilir Öğeler, Seviye Mekanikleri ve Etkileşimler

- [ ] **Görev 3.1: `CoinCollect` Görsel/İşitsel Efekt Geliştirmesi** `[KODLANDI - Kontrol & Doğrulama]`
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/UI/CoinCollect.cs`
  - **Sınıf Adı:** `CoinCollect : MonoBehaviour, ICollectable`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private int value = 1;`
    - `[SerializeField] private float _rotationSpeed = 90f;`, `[SerializeField] private float _bobAmplitude = 0.15f;`, `[SerializeField] private float _bobFrequency = 2f;`, `[SerializeField] private AudioClip _collectSfx;`
  - **Metodlar ve İşleyiş:**
    - `private void AnimateCoin()` : `Update()` içinde objeyi Y ekseninde döndürür ve `Mathf.Sin` ile yukarı-aşağı süzülme hareketi yaptırır.
    - `public void Collect()` : Çoklu tetiklenmeyi önlemek için `_isCollected` bayrağı kontrolü yapar, `SettingsUI.PlaySFX(_collectSfx)` çağırır ve `StartCoroutine(CollectAnimationRoutine())` ile objeyi 0.15 saniyede küçülterek (`Vector3.Lerp`) `Destroy(gameObject)` yapar.

- [ ] **Görev 3.2: Çok Amaçlı Güçlendirici ve Bonus Öğesi (`PowerUpCollectable`)** `[KODLANDI - Kontrol & Doğrulama]`
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Collectables/PowerUpCollectable.cs`
  - **Sınıf Adı:** `PowerUpCollectable : MonoBehaviour, ICollectable` (ve `public enum PowerUpType { ExtraTime, SpeedBoost, StaminaRefill }`)
  - **Alanlar ve Eventler:**
    - `[SerializeField] private PowerUpType _powerUpType = PowerUpType.ExtraTime;`, `[SerializeField] private float _amount = 10f;`, `[SerializeField] private float _duration = 5f;`, `[SerializeField] private AudioClip _collectSfx;`
  - **Metodlar ve İşleyiş:**
    - `public void Collect()` : `_powerUpType` değerine göre `ApplyPowerUp(PlayerController player)` çağırır.
    - `private void ApplyPowerUp(PlayerController player)` : `ExtraTime` ise `ClockUI.AddBonusTime(_amount)`, `StaminaRefill` ise `DashController.RefillStamina(_amount)`, `SpeedBoost` ise `_duration` süresince `player.SetSpeedMultiplier(_amount)` uygular.

- [ ] **Görev 3.3: Tehlike Alanı ve Yeniden Doğma Noktası (`HazardZone` & `Checkpoint`)** `[KODLANDI - Kontrol & Doğrulama]`
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Gameplay/Checkpoint.cs` ve `Assets/_GameAssets/Scripts/Gameplay/HazardZone.cs`
  - **Sınıf Adları:** `Checkpoint : MonoBehaviour`, `HazardZone : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `Checkpoint` -> `public static Vector3 ActiveRespawnPoint { get; private set; }`, `public static bool HasCheckpoint { get; private set; }`
  - **Metodlar ve İşleyiş:**
    - `Checkpoint.SetCheckpoint(Vector3 position)` ve `Checkpoint.ResetCheckpoints()` : Aktif doğma noktasını günceller veya sıfırlar.
    - `Checkpoint.OnTriggerEnter(Collider other)` : `PlayerController` temas ettiğinde `SetCheckpoint(transform.position + Vector3.up * 0.5f)` kaydeder.
    - `HazardZone.RespawnPlayer(PlayerController player)` : Oyuncu tehlike alanına girdiğinde `Rigidbody.linearVelocity` değerini sıfırlar ve `Checkpoint.ActiveRespawnPoint` noktasına ışınlar.

- [ ] **Görev 3.Son: Faz 3 Night Agent Menü Entegrasyonu, Play Mode Testi ve Görsel Doğrulama**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` ve `Assets/_GameAssets/Tests/PlayMode/Phase3Tests.cs`
  - **Menü Öğeleri:**
    - `[MenuItem("Tools/Night Agent/Faz 3: Güçlendiriciler, Checkpoint ve HazardZone/Uygula")]` : Sahneye örnek `PowerUpCollectable`, `Checkpoint` ve `HazardZone` test objelerini (`NightAgent_Phase3_Objects` altında) kurar ve sahneyi dirty işaretler.
    - `[MenuItem("Tools/Night Agent/Faz 3: Güçlendiriciler, Checkpoint ve HazardZone/Geri çek")]` : `NightAgent_Phase3_Objects` grubunu sahneden kaldırır.
    - `[MenuItem("Tools/Night Agent/Faz 3: Güçlendiriciler, Checkpoint ve HazardZone/Detay")]` : `CoinCollect` animasyonu, `PowerUpCollectable`, `Checkpoint` ve `HazardZone` kullanımını açıklayan Türkçe diyalog penceresi açar.
  - **Play Mode Test & Görsel İnceleme:**
    - `Phase3Tests.cs` içinde geçici sahnede `Checkpoint` kaydını, `HazardZone` ışınlamasını ve `PowerUpCollectable` etkisini `[UnityTest]` ile doğrula; `TestScreenshots/faz3_sonucu.png` ekran görüntüsünü kaydet ve incele.

---

## Faz 4: Arayüz (HUD) Genişletmesi ve Editör Otomasyonu

- [ ] **Görev 4.1: Stamina Barı ve Oyun Sonu (Victory / GameOver) HUD Yöneticisi (`GameplayHUD`)** `[KODLANDI - Kontrol & Doğrulama]`
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/UI/GameplayHUD.cs`
  - **Sınıf Adı:** `GameplayHUD : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private UnityEngine.UI.Slider _staminaSlider;`
    - `[SerializeField] private GameObject _endGamePanel;`
    - `[SerializeField] private TMPro.TextMeshProUGUI _endGameTitleText;`
    - `[SerializeField] private TMPro.TextMeshProUGUI _endGameStatsText;`
    - `[SerializeField] private UnityEngine.UI.Button _restartButton;`
  - **Metodlar ve İşleyiş:**
    - `private void HandleStaminaChanged(float current, float max)` : `DashController.OnStaminaChanged` event'ine abone olur, `_staminaSlider.value = current / max` günceller.
    - `private void HandleGameStateChanged(GameState state)` : `GameManager.OnGameStateChanged` event'ine abone olur; `Victory` veya `GameOver` durumunda `_endGamePanel` nesnesini aktif eder, toplanan `CoinManager.Instance.Coins` ve `HighScore` bilgisini yazar.

- [ ] **Görev 4.2: `CivCivAutoSetup` Editör Aracının Yeni Sistemlerle Güncellenmesi** `[KODLANDI - Kontrol & Doğrulama]`
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/CivCivAutoSetup.cs`
  - **Sınıf Adı:** `CivCivAutoSetup` (Static Editor Class)
  - **Metodlar ve İşleyiş:**
    - `[MenuItem("Tools/CivCiv/Oyun Yöneticilerini ve PlayerStats SO Kur %#g")] public static void SetupCoreManagers()` : `DefaultPlayerStats.asset` oluşturur, `PlayerController` ve `DashController` referanslarını bağlar, `GameManager` ve `GameplayHUD` kurar.

- [ ] **Görev 4.Son: Faz 4 Night Agent Menü Entegrasyonu, Play Mode Testi ve Görsel Doğrulama**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` ve `Assets/_GameAssets/Tests/PlayMode/Phase4Tests.cs`
  - **Menü Öğeleri:**
    - `[MenuItem("Tools/Night Agent/Faz 4: Stamina Barı ve Oyun Sonu HUD (GameplayHUD)/Uygula")]` : Sahnedeki `Canvas` altına görsel `StaminaSlider` barını ve `EndGamePanel` (Başlık, İstatistik metni, Yeniden Başlat butonu) hiyerarşisini tam boyut/konumlarıyla kurup `GameplayHUD` bileşenine bağlar.
    - `[MenuItem("Tools/Night Agent/Faz 4: Stamina Barı ve Oyun Sonu HUD (GameplayHUD)/Geri çek")]` : `Canvas` altındaki `GameplayHUD` ve bağlı UI panellerini sahneden temizler.
    - `[MenuItem("Tools/Night Agent/Faz 4: Stamina Barı ve Oyun Sonu HUD (GameplayHUD)/Detay")]` : `GameplayHUD` ve `CivCivAutoSetup` özelliklerini açıklayan Türkçe diyalog penceresi açar.
  - **Play Mode Test & Görsel İnceleme:**
    - `Phase4Tests.cs` içinde geçici sahnede `Canvas` ve `GameplayHUD` oluştur; stamina değişiminde slider değerini ve `GameManager.TriggerVictory()` çağrıldığında oyun sonu panelinin açıldığını `Assert` ile doğrula; `TestScreenshots/faz4_sonucu.png` ekran görüntüsünü kaydet ve UI hizalamasını incele.

---

## Faz 5: Gelişmiş CivCiv Hareket Mekanikleri (Kanat Çırpma, Süzülme & Gaga Hamlesi)

- [ ] **Görev 5.1: Çift Zıplama ve Kanat Çırparak Süzülme (`ChickGlideController`)** `[KODLANDI - Kontrol & Doğrulama]`
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Player/ChickGlideController.cs`
  - **Sınıf Adı:** `ChickGlideController : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private int _maxAirJumps = 1;`
    - `[SerializeField] private float _airJumpForce = 6.5f;`
    - `[SerializeField] private float _glideFallSpeedLimit = -2.2f;`
    - `[SerializeField] private float _glideStaminaDrainPerSec = 12f;`
    - `public bool IsGliding { get; private set; }`
  - **Metodlar ve İşleyiş:**
    - `private void HandleAirInput()` : Havadayken (`!_playerController.IsGrounded()`) Space tuşuna basıldığında `_remainingAirJumps > 0` ise `ExecuteAirJump()` çağırır; Space basılı tutulurken düşüş hızı negatifse `StartGlide()` başlatır.
    - `public void ExecuteAirJump()` : Dikey hızı sıfırlayıp `_airJumpForce` ile `ForceMode.Impulse` uygular ve `_remainingAirJumps` değerini 1 azaltır.
    - `private void ApplyGlidePhysics()` : `FixedUpdate` içinde `IsGliding` aktifken `Rigidbody.linearVelocity.y` değerini `Mathf.Max(vel.y, _glideFallSpeedLimit)` ile sınırlar ve `StateController.ChangeState(PlayerState.Glide)` yapar.
    - `public void StopGlide()` : Zemin temasında veya stamina bittiğinde süzülmeyi sonlandırır ve `_remainingAirJumps = _maxAirJumps` olarak sıfırlar.

- [ ] **Görev 5.2: Gaga Vuruşu ve Yere Çakılma (`ChickPeckAttack`)** `[KODLANDI - Kontrol & Doğrulama]`
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Player/ChickPeckAttack.cs`
  - **Sınıf Adı:** `ChickPeckAttack : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private float _peckRange = 1.8f;`, `[SerializeField] private float _peckRadius = 0.8f;`, `[SerializeField] private int _peckDamage = 1;`
    - `[SerializeField] private float _groundPoundForce = 25f;`
    - `public event System.Action OnPeckPerformed;`
    - `public event System.Action<Vector3> OnGroundPoundImpact;`
    - `public bool IsGroundPounding { get; private set; }`
  - **Metodlar ve İşleyiş:**
    - `public void PerformPeck()` : Sol fare tuşu veya `F` tuşu ile karakterin önünde `Physics.OverlapSphere` taraması yapar, `IDamageable` nesnelere hasar verir ve `OnPeckPerformed` tetikler.
    - `public void StartGroundPound()` : Havadayken `LeftControl` tuşuna basıldığında hızı sıfırlayıp aşağı yönlü `Vector3.down * _groundPoundForce` (`ForceMode.VelocityChange`) uygular; yere değdiği anda çevredeki `IDamageable` hedeflere hasar verip `OnGroundPoundImpact` tetikler.

- [ ] **Görev 5.Son: Faz 5 Night Agent Menü Entegrasyonu, Play Mode Testi ve Görsel Doğrulama**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` ve `Assets/_GameAssets/Tests/PlayMode/Phase5Tests.cs`
  - **Menü Öğeleri:**
    - `[MenuItem("Tools/Night Agent/Faz 5: Çift Zıplama, Süzülme ve Gaga Saldırısı/Uygula")]` : Sahnedeki `PlayerController` nesnesine `ChickGlideController` ve `ChickPeckAttack` bileşenlerini ekler ve sahneyi dirty işaretler.
    - `[MenuItem("Tools/Night Agent/Faz 5: Çift Zıplama, Süzülme ve Gaga Saldırısı/Geri çek")]` : Karakter üzerindeki `ChickGlideController` ve `ChickPeckAttack` bileşenlerini kaldırır.
    - `[MenuItem("Tools/Night Agent/Faz 5: Çift Zıplama, Süzülme ve Gaga Saldırısı/Detay")]` : Çift zıplama, kanat çırparak süzülme, gaga vuruşu ve yere çakılma mekaniklerini açıklayan Türkçe diyalog penceresi açar.
  - **Play Mode Test & Görsel İnceleme:**
    - `Phase5Tests.cs` içinde geçici sahnede `ChickGlideController` çift zıplama (`ExecuteAirJump`) dikey hız artışını ve `ChickPeckAttack` (`PerformPeck`) hasar tetiklemesini `[UnityTest]` ile doğrula; `TestScreenshots/faz5_sonucu.png` ekran görüntüsünü kaydet ve incele.

---

## Faz 6: Can, Hasar ve Savunma Sistemi (Health & Damage Architecture)

- [ ] **Görev 6.1: Hasar Alabilirlik Arayüzü (`IDamageable`)** `[KODLANDI - Kontrol & Doğrulama]`
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Core/IDamageable.cs`
  - **Arayüz Adı:** `IDamageable`
  - **Metodlar:**
    - `void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitNormal);`
    - `bool IsAlive { get; }`

- [ ] **Görev 6.2: Oyuncu Can, Dokunulmazlık ve Geri Tepme Bileşeni (`PlayerHealth`)** `[KODLANDI - Kontrol & Doğrulama]`
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Player/PlayerHealth.cs`
  - **Sınıf Adı:** `PlayerHealth : MonoBehaviour, IDamageable`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private int _maxHealth = 3;`
    - `[SerializeField] private float _invincibilityDuration = 1.5f;`
    - `[SerializeField] private float _knockbackForce = 8f;`
    - `public int CurrentHealth { get; private set; }`, `public int MaxHealth => _maxHealth;`
    - `public bool IsAlive => CurrentHealth > 0;`, `public bool IsInvincible { get; private set; }`
    - `public event System.Action<int, int> OnHealthChanged;` (`current`, `max`)
    - `public event System.Action OnPlayerDied;`
  - **Metodlar ve İşleyiş:**
    - `public void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitNormal)` : `IsInvincible` veya `!IsAlive` ise işlem yapmaz; canı düşürür, `ApplyKnockback(-hitNormal)` uygular, `InvincibilityFlashRoutine()` başlatır ve can 0 ise `Die()` çağırır.
    - `public void Heal(int amount)` : Canı `_maxHealth` sınırına kadar artırır ve `OnHealthChanged` tetikler.
    - `private void Die()` : `OnPlayerDied?.Invoke()` ve `GameManager.Instance?.TriggerGameOver()` çağırır.

- [ ] **Görev 6.3: Kalp / Can Göstergesi HUD Bileşeni (`HealthUI`)** `[KODLANDI - Kontrol & Doğrulama]`
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/UI/HealthUI.cs`
  - **Sınıf Adı:** `HealthUI : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private TMPro.TextMeshProUGUI _healthText;`
    - `[SerializeField] private UnityEngine.UI.Image[] _heartIcons;`
  - **Metodlar ve İşleyiş:**
    - `private void HandleHealthChanged(int current, int max)` : `PlayerHealth.OnHealthChanged` event'ini dinleyerek `_heartIcons` dizisindeki kalp görsellerinin doluluk/renk durumunu ve `_healthText` metnini günceller.

- [ ] **Görev 6.Son: Faz 6 Night Agent Menü Entegrasyonu, Play Mode Testi ve Görsel Doğrulama**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` ve `Assets/_GameAssets/Tests/PlayMode/Phase6Tests.cs`
  - **Menü Öğeleri:**
    - `[MenuItem("Tools/Night Agent/Faz 6: Can, Hasar ve Kalp HUD Sistemi/Uygula")]` : Karaktere `PlayerHealth` ekler, `Canvas` sol üst köşesine 3 adet kalp ikonu ve can metni içeren `HealthUI` panelini oluşturup referanslarını bağlar.
    - `[MenuItem("Tools/Night Agent/Faz 6: Can, Hasar ve Kalp HUD Sistemi/Geri çek")]` : Karakterden `PlayerHealth` bileşenini ve `Canvas` altından `HealthUI` panelini kaldırır.
    - `[MenuItem("Tools/Night Agent/Faz 6: Can, Hasar ve Kalp HUD Sistemi/Detay")]` : `IDamageable`, `PlayerHealth` ve `HealthUI` çalışma mantığını açıklayan Türkçe diyalog penceresi açar.
  - **Play Mode Test & Görsel İnceleme:**
    - `Phase6Tests.cs` içinde geçici sahnede `PlayerHealth` ve `HealthUI` kur; `TakeDamage` ve `Heal` çağrılarında can değişimini, dokunulmazlık durumunu ve UI güncellemesini `Assert` ile doğrula; `TestScreenshots/faz6_sonucu.png` ekran görüntüsünü kaydet ve kalp HUD hizalamasını incele.

---

## Faz 7: Kombo, Çarpan ve Mıknatıs Sistemi (Score & Combo Mechanics)

- [ ] **Görev 7.1: Kombo ve Skor Çarpanı Yöneticisi (`ComboManager`)** `[KODLANDI - Kontrol & Doğrulama]`
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
  - **Metodlar ve İşleyiş:**
    - `private void HandleCoinCollected(int baseAmount)` : `CoinManager.Instance.CoinsCollected` event'ini dinler; `_comboTimer = _comboResetTime` sıfırlar, `CurrentStreak++` artırır, her 3 seride `CurrentMultiplier` değerini `_maxMultiplier` sınırına kadar yükseltir.
    - `public void ResetCombo()` : Süre bittiğinde veya oyuncu hasar aldığında (`PlayerHealth.OnHealthChanged`) çarpanı 1'e ve seriyi 0'a sıfırlar.

- [ ] **Görev 7.2: Kombo Arayüz Bileşeni (`ComboUI`)** `[KODLANDI - Kontrol & Doğrulama]`
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/UI/ComboUI.cs`
  - **Sınıf Adı:** `ComboUI : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private TMPro.TextMeshProUGUI _comboText;`
    - `[SerializeField] private UnityEngine.UI.Image _comboTimerFill;`
  - **Metodlar ve İşleyiş:**
    - `private void UpdateComboView(int streak, int multiplier, float normalizedTime)` : `ComboManager.Instance.OnComboUpdated` event'ini dinler; çarpan > 1 ise `"x{multiplier} COMBO ({streak})"` metnini gösterir ve ölçek sıçrama (punch scale) efekti uygular.

- [ ] **Görev 7.3: Altın Mıknatısı Alanı (`CoinMagnet`)** `[KODLANDI - Kontrol & Doğrulama]`
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Collectables/CoinMagnet.cs`
  - **Sınıf Adı:** `CoinMagnet : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private float _defaultMagnetRadius = 3f;`, `[SerializeField] private float _pullSpeed = 14f;`
    - `public float CurrentRadius => _isSuperMagnetActive ? _superMagnetRadius : _defaultMagnetRadius;`
  - **Metodlar ve İşleyiş:**
    - `public void ActivateSuperMagnet(float radius, float duration)` : Belirtilen süre boyunca çekim yarıçapını büyütür.
    - `private void PullNearbyCoins()` : `Update()` içinde `Physics.OverlapSphere` ile yakındaki `CoinCollect` nesnelerini bulur ve oyuncuya doğru `Vector3.MoveTowards` ile çeker.

- [ ] **Görev 7.Son: Faz 7 Night Agent Menü Entegrasyonu, Play Mode Testi ve Görsel Doğrulama**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` ve `Assets/_GameAssets/Tests/PlayMode/Phase7Tests.cs`
  - **Menü Öğeleri:**
    - `[MenuItem("Tools/Night Agent/Faz 7: Kombo Çarpanı ve Altın Mıknatısı/Uygula")]` : Sahneye `ComboManager` kurar, karaktere `CoinMagnet` ekler ve `Canvas` altına görsel `ComboUI` panelini oluşturup bağlar.
    - `[MenuItem("Tools/Night Agent/Faz 7: Kombo Çarpanı ve Altın Mıknatısı/Geri çek")]` : `ComboManager`, `CoinMagnet` ve `ComboUI` bileşenlerini sahneden temizler.
    - `[MenuItem("Tools/Night Agent/Faz 7: Kombo Çarpanı ve Altın Mıknatısı/Detay")]` : `ComboManager`, `ComboUI` ve `CoinMagnet` sistemlerini açıklayan Türkçe diyalog penceresi açar.
  - **Play Mode Test & Görsel İnceleme:**
    - `Phase7Tests.cs` içinde geçici sahnede ardışık altın toplandığında `ComboManager.CurrentMultiplier` artışını ve `CoinMagnet` çekim hareketini `[UnityTest]` ile doğrula; `TestScreenshots/faz7_sonucu.png` ekran görüntüsünü kaydet ve kombo UI görünümünü incele.

---

## Faz 8: Dinamik Platformlar ve Fiziksel Seviye Öğeleri

- [ ] **Görev 8.1: Noktalar Arası Hareketli Platform (`MovingPlatform`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Gameplay/MovingPlatform.cs`
  - **Sınıf Adı:** `MovingPlatform : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private Vector3[] _localWaypoints;`, `[SerializeField] private float _moveSpeed = 3f;`, `[SerializeField] private float _waitDuration = 1f;`
    - `private Vector3 _platformVelocity;`
  - **Metodlar ve İşleyiş:**
    - `private void MovePlatform()` : `FixedUpdate()` içinde `Rigidbody.MovePosition` ile platformu sıradaki waypoint'e taşır ve `_platformVelocity` vektörünü hesaplar.
    - `private void OnCollisionStay(Collision collision)` : Üzerinde duran `PlayerController` nesnesinin `Rigidbody` bileşenine platformun yer değiştirme deltasını uygular (oyuncunun platformdan kaymasını önler).

- [ ] **Görev 8.2: Çöken ve Yeniden Oluşan Platform (`FallingPlatform`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Gameplay/FallingPlatform.cs`
  - **Sınıf Adı:** `FallingPlatform : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private float _fallDelay = 0.8f;`, `[SerializeField] private float _respawnDelay = 4f;`, `[SerializeField] private float _shakeIntensity = 0.06f;`
    - `public bool IsCollapsed { get; private set; }`
  - **Metodlar ve İşleyiş:**
    - `private void OnCollisionEnter(Collision collision)` : `PlayerController` temas ettiğinde `StartCoroutine(FallSequenceRoutine())` başlatır.
    - `private System.Collections.IEnumerator FallSequenceRoutine()` : `_fallDelay` süresince görseli titretir, ardından `Collider` ve `Renderer` bileşenlerini kapatır (`IsCollapsed = true`), `_respawnDelay` sonra başlangıç konumunda tekrar aktif eder.

- [ ] **Görev 8.3: Rüzgar Akımı / Hava Yükseltici (`WindCurrentZone`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Gameplay/WindCurrentZone.cs`
  - **Sınıf Adı:** `WindCurrentZone : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private Vector3 _windDirection = Vector3.up;`, `[SerializeField] private float _windForce = 18f;`, `[SerializeField] private float _glidingMultiplier = 2.2f;`
  - **Metodlar ve İşleyiş:**
    - `private void OnTriggerStay(Collider other)` : İçerideki `PlayerController` nesnesinin `Rigidbody` bileşenine `_windDirection.normalized * _windForce` kuvveti uygular; eğer karakterde `ChickGlideController.IsGliding == true` ise kuvveti `_glidingMultiplier` ile çarpar.

- [ ] **Görev 8.Son: Faz 8 Night Agent Menü Entegrasyonu, Play Mode Testi ve Görsel Doğrulama**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` ve `Assets/_GameAssets/Tests/PlayMode/Phase8Tests.cs`
  - **Menü Öğeleri:**
    - `[MenuItem("Tools/Night Agent/Faz 8: Dinamik Platformlar ve Rüzgar Akımı/Uygula")]` : Sahneye `NightAgent_Phase8_Platforms` altında örnek `MovingPlatform`, `FallingPlatform` ve `WindCurrentZone` objeleri oluşturur.
    - `[MenuItem("Tools/Night Agent/Faz 8: Dinamik Platformlar ve Rüzgar Akımı/Geri çek")]` : `NightAgent_Phase8_Platforms` grubunu sahneden siler.
    - `[MenuItem("Tools/Night Agent/Faz 8: Dinamik Platformlar ve Rüzgar Akımı/Detay")]` : Dinamik platformları ve rüzgar yükselticiyi açıklayan Türkçe diyalog penceresi açar.
  - **Play Mode Test & Görsel İnceleme:**
    - `Phase8Tests.cs` içinde geçici sahnede platform hareketini, çökme döngüsünü ve rüzgar kaldırma kuvvetini `[UnityTest]` ile doğrula; `TestScreenshots/faz8_sonucu.png` ekran görüntüsünü kaydet ve incele.

---

## Faz 9: Kırılabilir Nesneler, Yumurtalar ve Ganimet (Loot) Sistemi

- [ ] **Görev 9.1: Ganimet Tablosu ScriptableObject (`LootTableSO`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Data/LootTableSO.cs`
  - **Sınıf Adı:** `LootTableSO : ScriptableObject` (`[CreateAssetMenu(fileName = "LootTable", menuName = "CivCiv/Data/LootTableSO")]`)
  - **Alanlar ve Eventler:**
    - `[System.Serializable] public struct LootEntry { public GameObject Prefab; [Range(0f, 100f)] public float DropWeight; public int MinCount; public int MaxCount; }`
    - `public LootEntry[] Entries;`
  - **Metodlar ve İşleyiş:**
    - `public void SpawnLoot(Vector3 origin)` : Ağırlıklı olasılık hesabına göre seçilen prefabları `origin` noktasında hafif yukarı/dışarı rastgele fiziksel sıçrama (`Rigidbody.AddForce`) ile oluşturur.

- [ ] **Görev 9.2: Kırılabilir Sandık ve Sürpriz Yumurta (`BreakableCrate`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Gameplay/BreakableCrate.cs`
  - **Sınıf Adı:** `BreakableCrate : MonoBehaviour, IDamageable`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private int _health = 1;`, `[SerializeField] private int _directCoinReward = 3;`, `[SerializeField] private LootTableSO _lootTable;`
    - `public bool IsAlive => _health > 0;`
  - **Metodlar ve İşleyiş:**
    - `public void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitNormal)` : Hasar aldığında `_health -= amount` yapar, ölçek elastik sarsıntı efekti oynatır, `_health <= 0` ise `Break()` çağırır.
    - `private void OnCollisionEnter(Collision collision)` : Oyuncu `DashController.IsDashing` durumunda veya `ChickPeckAttack` (GroundPound) ile çarptığında `TakeDamage(_health, ...)` çağırarak sandığı kırar.
    - `private void Break()` : `_lootTable != null` ise `_lootTable.SpawnLoot(transform.position)` çağırır, ayrıca `_directCoinReward > 0` ise `CoinManager.Instance?.AddCoins(_directCoinReward)` ekler ve nesneyi yok eder.

- [ ] **Görev 9.Son: Faz 9 Night Agent Menü Entegrasyonu, Play Mode Testi ve Görsel Doğrulama**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` ve `Assets/_GameAssets/Tests/PlayMode/Phase9Tests.cs`
  - **Menü Öğeleri:**
    - `[MenuItem("Tools/Night Agent/Faz 9: Kırılabilir Sandık ve Ganimet Sistemi/Uygula")]` : `Assets/_GameAssets/Data/DefaultLootTable.asset` oluşturur ve sahneye örnek `BreakableCrate` nesneleri yerleştirir.
    - `[MenuItem("Tools/Night Agent/Faz 9: Kırılabilir Sandık ve Ganimet Sistemi/Geri çek")]` : Sahnedeki örnek `BreakableCrate` nesnelerini temizler.
    - `[MenuItem("Tools/Night Agent/Faz 9: Kırılabilir Sandık ve Ganimet Sistemi/Detay")]` : `LootTableSO` ve `BreakableCrate` sistemini açıklayan Türkçe diyalog penceresi açar.
  - **Play Mode Test & Görsel İnceleme:**
    - `Phase9Tests.cs` içinde geçici sahnede sandığa hasar verildiğinde kırılmasını ve `CoinManager` altın artışını `[UnityTest]` ile doğrula; `TestScreenshots/faz9_sonucu.png` ekran görüntüsünü kaydet ve incele.

---

## Faz 10: Düşman Yapay Zekası ve Tehditler (Predator & Hazard AI)

- [ ] **Görev 10.1: Düşman İstatistikleri ScriptableObject (`EnemyStatsSO`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Data/EnemyStatsSO.cs`
  - **Sınıf Adı:** `EnemyStatsSO : ScriptableObject` (`[CreateAssetMenu(fileName = "EnemyStats", menuName = "CivCiv/Data/EnemyStatsSO")]`)
  - **Alanlar ve Eventler:**
    - `public float PatrolSpeed = 2.5f;`, `public float ChaseSpeed = 5.2f;`, `public float DetectionRadius = 8f;`, `public float AttackRange = 1.4f;`, `public int ContactDamage = 1;`, `public int MaxHealth = 2;`

- [ ] **Görev 10.2: Devriye ve Takipçi Düşman Kontrolcüsü (`PatrolEnemyAI`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/AI/PatrolEnemyAI.cs`
  - **Sınıf Adı:** `PatrolEnemyAI : MonoBehaviour, IDamageable` (ve `public enum EnemyAIState { Patrol, Chase, Stunned, Dead }`)
  - **Alanlar ve Eventler:**
    - `[SerializeField] private EnemyStatsSO _stats;`, `[SerializeField] private Transform[] _patrolPoints;`
    - `public EnemyAIState CurrentAIState { get; private set; } = EnemyAIState.Patrol;`
    - `public bool IsAlive => _currentHealth > 0;`
  - **Metodlar ve İşleyiş:**
    - `private void UpdateAIState()` : Oyuncu ile mesafeyi ölçer; `DetectionRadius` içindeyse `EnemyAIState.Chase`, dışındaysa `EnemyAIState.Patrol` durumuna geçer.
    - `private void ExecutePatrol()` ve `private void ExecuteChase()` : Hedefe doğru yönelip hareket eder.
    - `private void OnCollisionEnter(Collision collision)` : Oyuncu düşmanın tam üstüne zıpladıysa (`collision.contacts[0].normal.y < -0.5f`) düşmana `TakeDamage(1, ...)` uygular ve oyuncuyu yukarı zıplatır; yandan çarptıysa `PlayerHealth.TakeDamage(_stats.ContactDamage, ...)` çağırır.
    - `public void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitNormal)` : Canı azaltır, `Stunned` durumuna sokar, can 0 ise `Die()` çağırır.

- [ ] **Görev 10.Son: Faz 10 Night Agent Menü Entegrasyonu, Play Mode Testi ve Görsel Doğrulama**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` ve `Assets/_GameAssets/Tests/PlayMode/Phase10Tests.cs`
  - **Menü Öğeleri:**
    - `[MenuItem("Tools/Night Agent/Faz 10: Düşman Yapay Zekası (PatrolEnemyAI)/Uygula")]` : `Assets/_GameAssets/Data/DefaultEnemyStats.asset` oluşturur ve sahneye 2 devriye noktalı örnek `PatrolEnemyAI` kurar.
    - `[MenuItem("Tools/Night Agent/Faz 10: Düşman Yapay Zekası (PatrolEnemyAI)/Geri çek")]` : Sahnedeki örnek `PatrolEnemyAI` ve devriye noktalarını siler.
    - `[MenuItem("Tools/Night Agent/Faz 10: Düşman Yapay Zekası (PatrolEnemyAI)/Detay")]` : `EnemyStatsSO` ve `PatrolEnemyAI` davranışlarını açıklayan Türkçe diyalog penceresi açar.
  - **Play Mode Test & Görsel İnceleme:**
    - `Phase10Tests.cs` içinde geçici sahnede düşmanın oyuncuyu algılayıp `Chase` durumuna geçişini ve `TakeDamage` ile sersemleme/ölümünü `[UnityTest]` ile doğrula; `TestScreenshots/faz10_sonucu.png` ekran görüntüsünü kaydet ve incele.

---

## Faz 11: Görev, Bulmaca ve Etkileşimli Çevre (Triggers & Quests)

- [ ] **Görev 11.1: Etkileşim Arayüzü ve Oyuncu Etkileşim Tarayıcısı (`IInteractable` & `PlayerInteractor`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Core/IInteractable.cs` ve `Assets/_GameAssets/Scripts/Player/PlayerInteractor.cs`
  - **Sınıflar:** `public interface IInteractable` ve `public class PlayerInteractor : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private float _interactRadius = 2.2f;`
    - `public event System.Action<IInteractable> OnInteractableChanged;`
  - **Metodlar ve İşleyiş:**
    - `IInteractable` -> `string GetInteractionPrompt();`, `void Interact(PlayerController player);`
    - `PlayerInteractor.ScanForInteractables()` : Karakterin önünde `Physics.OverlapSphere` ile en yakın `IInteractable` nesneyi bulur ve `E` tuşuna basıldığında (veya `TriggerNearestInteraction()` çağrıldığında) `Interact(player)` çalıştırır.

- [ ] **Görev 11.2: Basınç Plakası ve Kapı/Köprü Tetikleyicisi (`PressurePlateSwitch`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Gameplay/PressurePlateSwitch.cs`
  - **Sınıf Adı:** `PressurePlateSwitch : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private Transform _targetDoorOrBridge;`, `[SerializeField] private Vector3 _openPositionOffset = new Vector3(0f, 3f, 0f);`, `[SerializeField] private bool _stayOpen = true;`
    - `public bool IsActivated { get; private set; }`
    - `public event System.Action<bool> OnSwitchStateChanged;`
  - **Metodlar ve İşleyiş:**
    - `private void OnTriggerEnter(Collider other)` ve `private void OnTriggerExit(Collider other)` : `PlayerController` veya `Rigidbody` plaka üzerine çıktığında `ActivateSwitch(true)` çağırarak hedef kapıyı/köprüyü `Vector3.Lerp` ile açar.

- [ ] **Görev 11.3: Kayıp CivCiv Kurtarma Görevi (`LostChickNPC` & `ChickRescueManager`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Gameplay/LostChickNPC.cs` ve `Assets/_GameAssets/Scripts/Core/ChickRescueManager.cs`
  - **Sınıf Adları:** `LostChickNPC : MonoBehaviour, IInteractable`, `ChickRescueManager : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `ChickRescueManager` -> `public static ChickRescueManager Instance { get; private set; }`, `public int RescuedCount { get; private set; }`, `public int TotalChicksInLevel { get; private set; }`
    - `ChickRescueManager` -> `public event System.Action<int, int> OnChickRescued;` (`rescued`, `total`)
  - **Metodlar ve İşleyiş:**
    - `LostChickNPC.Interact(PlayerController player)` : Civcivi kurtarır (`IsRescued = true`), oyuncuyu belirli bir takip mesafesinden (`_followDistance = 1.8f`) takip etmeye başlar ve `ChickRescueManager.Instance.RegisterRescue(this)` çağırır.
    - `ChickRescueManager.RegisterRescue(LostChickNPC chick)` : Kurtarılan civciv sayısını artırır, bonus altın/süre verir ve `OnChickRescued` tetikler.

- [ ] **Görev 11.Son: Faz 11 Night Agent Menü Entegrasyonu, Play Mode Testi ve Görsel Doğrulama**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` ve `Assets/_GameAssets/Tests/PlayMode/Phase11Tests.cs`
  - **Menü Öğeleri:**
    - `[MenuItem("Tools/Night Agent/Faz 11: Etkileşim, Basınç Plakası ve Kayıp CivCiv/Uygula")]` : Karaktere `PlayerInteractor` ekler, sahneye `ChickRescueManager`, örnek `PressurePlateSwitch` (ve kapı objesi) ile `LostChickNPC` kurar.
    - `[MenuItem("Tools/Night Agent/Faz 11: Etkileşim, Basınç Plakası ve Kayıp CivCiv/Geri çek")]` : `PlayerInteractor`, `ChickRescueManager` ve örnek Faz 11 sahne objelerini kaldırır.
    - `[MenuItem("Tools/Night Agent/Faz 11: Etkileşim, Basınç Plakası ve Kayıp CivCiv/Detay")]` : Etkileşim sistemi, basınç plakası ve kayıp civciv kurtarma görevini açıklayan Türkçe diyalog penceresi açar.
  - **Play Mode Test & Görsel İnceleme:**
    - `Phase11Tests.cs` içinde geçici sahnede `LostChickNPC.Interact` çağrısıyla kurtarma sayacının arttığını ve `PressurePlateSwitch` aktivasyonunu `[UnityTest]` ile doğrula; `TestScreenshots/faz11_sonucu.png` ekran görüntüsünü kaydet ve incele.

---

## Faz 12: Envanter, Kozmetik Mağaza ve Kalıcı Kayıt (Save & Shop System)

- [ ] **Görev 12.1: JSON Tabanlı Kalıcı Kayıt Yöneticisi (`SaveManager`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Core/SaveManager.cs`
  - **Sınıf Adı:** `SaveManager` (Static Class) ve `[System.Serializable] public class PlayerSaveData`
  - **Alanlar ve Eventler:**
    - `PlayerSaveData` -> `public int TotalWalletCoins;`, `public int HighScore;`, `public string EquippedItemId;`, `public System.Collections.Generic.List<string> UnlockedItemIds = new();`, `public System.Collections.Generic.List<string> UnlockedAchievementIds = new();`
    - `public static PlayerSaveData CurrentData { get; private set; }`
  - **Metodlar ve İşleyiş:**
    - `public static void Save()` : `JsonUtility.ToJson(CurrentData)` çıktısını `PlayerPrefs` (`"CivCiv_SaveDataJson"`) ve `Application.persistentDataPath + "/civciv_save.json"` dosyasına yazar.
    - `public static PlayerSaveData Load()` : Kayıtlı veriyi okur, yoksa varsayılan `PlayerSaveData` oluşturur.
    - `public static void AddToWallet(int amount)` ve `public static bool SpendFromWallet(int amount)` : Kalıcı cüzdan bakiyesini yönetir.

- [ ] **Görev 12.2: Kozmetik Eşya Tanımı ve Mağaza Yöneticisi (`CosmeticItemSO` & `CosmeticShopManager`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Data/CosmeticItemSO.cs` ve `Assets/_GameAssets/Scripts/Core/CosmeticShopManager.cs`
  - **Sınıf Adları:** `CosmeticItemSO : ScriptableObject`, `CosmeticShopManager : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `CosmeticItemSO` -> `public string ItemId;`, `public string DisplayName;`, `public int Price;`, `public GameObject HatAccessoryPrefab;`, `public Color ChickTintColor = Color.white;`
    - `CosmeticShopManager` -> `public static CosmeticShopManager Instance { get; private set; }`
  - **Metodlar ve İşleyiş:**
    - `public bool TryPurchaseItem(CosmeticItemSO item)` : Eşya kilitliyse ve `SaveManager.SpendFromWallet(item.Price)` başarılıysa eşyayı `UnlockedItemIds` listesine ekler ve `EquipItem(item)` çağırır.
    - `public void EquipItem(CosmeticItemSO item)` : Karakterin kafa soketine (`HeadSocket`) şapka/aksesuar prefabını takar veya materyal rengini uygular.

- [ ] **Görev 12.Son: Faz 12 Night Agent Menü Entegrasyonu, Play Mode Testi ve Görsel Doğrulama**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` ve `Assets/_GameAssets/Tests/PlayMode/Phase12Tests.cs`
  - **Menü Öğeleri:**
    - `[MenuItem("Tools/Night Agent/Faz 12: Kayıt Sistemi ve Kozmetik Mağaza/Uygula")]` : `Assets/_GameAssets/Data/DefaultCosmeticItem.asset` oluşturur ve sahneye `CosmeticShopManager` kurar.
    - `[MenuItem("Tools/Night Agent/Faz 12: Kayıt Sistemi ve Kozmetik Mağaza/Geri çek")]` : Sahnedeki `CosmeticShopManager` nesnesini temizler.
    - `[MenuItem("Tools/Night Agent/Faz 12: Kayıt Sistemi ve Kozmetik Mağaza/Detay")]` : `SaveManager`, `CosmeticItemSO` ve `CosmeticShopManager` işleyişini açıklayan Türkçe diyalog penceresi açar.
  - **Play Mode Test & Görsel İnceleme:**
    - `Phase12Tests.cs` içinde geçici sahnede cüzdana altın ekleme, kozmetik eşya satın alma ve JSON kayıt/yükleme döngüsünü `[UnityTest]` ile doğrula; `TestScreenshots/faz12_sonucu.png` ekran görüntüsünü kaydet ve incele.

---

## Faz 13: Başarım ve Meydan Okuma Sistemi (Achievements & Challenges)

- [ ] **Görev 13.1: Başarım Veri Yapısı ve Yöneticisi (`AchievementSO` & `AchievementManager`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Data/AchievementSO.cs` ve `Assets/_GameAssets/Scripts/Core/AchievementManager.cs`
  - **Sınıf Adları:** `AchievementSO : ScriptableObject`, `AchievementManager : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `AchievementSO` -> `public string AchievementId;`, `public string Title;`, `public string Description;`, `public int TargetValue;`, `public int CoinReward;`
    - `AchievementManager` -> `public static AchievementManager Instance { get; private set; }`
    - `public static event System.Action<AchievementSO> OnAchievementUnlocked;`
  - **Metodlar ve İşleyiş:**
    - `public void ReportProgress(string achievementId, int currentValue)` : Hedef değere ulaşıldığında başarımı `SaveManager.CurrentData.UnlockedAchievementIds` içine kaydeder, `SaveManager.AddToWallet(CoinReward)` verir ve `OnAchievementUnlocked` tetikler.
    - `private void SubscribeGameplayEvents()` : `CoinManager.CoinsChanged`, `ComboManager.OnComboUpdated` ve `ChickRescueManager.OnChickRescued` event'lerini dinleyerek başarımları otomatik kontrol eder.

- [ ] **Görev 13.2: Başarım Bildirim Pop-Up Arayüzü (`AchievementPopupUI`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/UI/AchievementPopupUI.cs`
  - **Sınıf Adı:** `AchievementPopupUI : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private RectTransform _popupContainer;`, `[SerializeField] private TMPro.TextMeshProUGUI _titleText;`, `[SerializeField] private TMPro.TextMeshProUGUI _rewardText;`
  - **Metodlar ve İşleyiş:**
    - `private void EnqueueAchievement(AchievementSO achievement)` : `AchievementManager.OnAchievementUnlocked` event'ini dinler ve ekranda 3 saniye kalıp kaybolan bildirim kuyruğunu (`Coroutine`) çalıştırır.

- [ ] **Görev 13.Son: Faz 13 Night Agent Menü Entegrasyonu, Play Mode Testi ve Görsel Doğrulama**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` ve `Assets/_GameAssets/Tests/PlayMode/Phase13Tests.cs`
  - **Menü Öğeleri:**
    - `[MenuItem("Tools/Night Agent/Faz 13: Başarım ve Bildirim Sistemi/Uygula")]` : `DefaultAchievement.asset` oluşturur, sahneye `AchievementManager` ve `Canvas` sağ üst köşesine `AchievementPopupUI` panelini kurar.
    - `[MenuItem("Tools/Night Agent/Faz 13: Başarım ve Bildirim Sistemi/Geri çek")]` : `AchievementManager` ve `AchievementPopupUI` nesnelerini sahneden temizler.
    - `[MenuItem("Tools/Night Agent/Faz 13: Başarım ve Bildirim Sistemi/Detay")]` : Başarım takibi ve bildirim pop-up sistemini açıklayan Türkçe diyalog penceresi açar.
  - **Play Mode Test & Görsel İnceleme:**
    - `Phase13Tests.cs` içinde geçici sahnede `ReportProgress` ile başarım kilidi açıldığında pop-up panelinin görünür olduğunu ve ödülün cüzdana eklendiğini `[UnityTest]` ile doğrula; `TestScreenshots/faz13_sonucu.png` ekran görüntüsünü kaydet ve incele.

---

## Faz 14: Nesne Havuzu ve Parçacık / Efekt Yöneticisi (Object Pooling & VFX)

- [ ] **Görev 14.1: Genel Nesne Havuzu Yöneticisi (`ObjectPoolManager`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Core/ObjectPoolManager.cs`
  - **Sınıf Adı:** `ObjectPoolManager : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `public static ObjectPoolManager Instance { get; private set; }`
    - `private readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.Queue<GameObject>> _pools = new();`
  - **Metodlar ve İşleyiş:**
    - `public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)` : Havuzda pasif nesne varsa çıkarıp aktif eder, yoksa `Instantiate` ile üretir.
    - `public void Despawn(GameObject instance, int prefabInstanceId, float delay = 0f)` : Nesneyi pasif hale getirip tekrar havuz kuyruğuna ekler.

- [ ] **Görev 14.2: Karakter Toz, Tüy ve Darbe Efekt Kontrolcüsü (`PlayerVFXController`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Player/PlayerVFXController.cs`
  - **Sınıf Adı:** `PlayerVFXController : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private ParticleSystem _footstepDustParticles;`, `[SerializeField] private ParticleSystem _jumpFeatherParticles;`, `[SerializeField] private TrailRenderer _dashTrailRenderer;`
  - **Metodlar ve İşleyiş:**
    - `private void HandleStateVFX(PlayerState state)` : Karakter `Move` durumundayken ayak tozu, `Jump` ve `Glide` durumunda tüy efekti, `Dash` ve `Slide` durumunda `_dashTrailRenderer.emitting = true` aktif eder; sahnede prefab atanmamışsa `Awake` içinde prosedürel `TrailRenderer` ve `ParticleSystem` bileşenlerini kodla oluşturur.

- [ ] **Görev 14.Son: Faz 14 Night Agent Menü Entegrasyonu, Play Mode Testi ve Görsel Doğrulama**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` ve `Assets/_GameAssets/Tests/PlayMode/Phase14Tests.cs`
  - **Menü Öğeleri:**
    - `[MenuItem("Tools/Night Agent/Faz 14: Nesne Havuzu ve Karakter VFX/Uygula")]` : Sahneye `ObjectPoolManager` kurar ve karaktere `PlayerVFXController` bileşenini ekler.
    - `[MenuItem("Tools/Night Agent/Faz 14: Nesne Havuzu ve Karakter VFX/Geri çek")]` : `ObjectPoolManager` ve `PlayerVFXController` bileşenlerini kaldırır.
    - `[MenuItem("Tools/Night Agent/Faz 14: Nesne Havuzu ve Karakter VFX/Detay")]` : `ObjectPoolManager` ve `PlayerVFXController` kullanımını açıklayan Türkçe diyalog penceresi açar.
  - **Play Mode Test & Görsel İnceleme:**
    - `Phase14Tests.cs` içinde geçici sahnede nesne havuzu `Spawn`/`Despawn` yeniden kullanımını ve `PlayerVFXController` prosedürel efekt üretimini `[UnityTest]` ile doğrula; `TestScreenshots/faz14_sonucu.png` ekran görüntüsünü kaydet ve incele.

---

## Faz 15: Kamera Sarsıntısı, Dinamik FOV ve Görsel Cilalama (Camera Feel)

- [ ] **Görev 15.1: Kamera Sarsıntısı ve Dinamik FOV Efektleri (`CameraJuiceController`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Camera/CameraJuiceController.cs`
  - **Sınıf Adı:** `CameraJuiceController : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `public static CameraJuiceController Instance { get; private set; }`
    - `[SerializeField] private float _baseFov = 60f;`, `[SerializeField] private float _dashFov = 74f;`, `[SerializeField] private float _fovLerpSpeed = 10f;`
    - `public float CurrentFov { get; private set; }`
  - **Metodlar ve İşleyiş:**
    - `public void TriggerShake(float intensity, float duration)` : Aktif kamera veya `ThirdPersonCameraController` hedefi üzerinde sönümlenen (`Mathf.PerlinNoise`) sarsıntı ofseti uygular.
    - `private void UpdateDynamicFOV()` : Karakter `DashController.IsDashing` veya `PlayerState.Slide` durumundayken `CinemachineCamera.Lens.FieldOfView` (veya `Camera.main.fieldOfView`) değerini `_dashFov` değerine yumuşakça genişletir.
    - `private void SubscribeEvents()` : `DashController.OnDashExecuted`, `PlayerHealth.OnHealthChanged` ve `ChickPeckAttack.OnGroundPoundImpact` olaylarında otomatik kamera sarsıntısı tetikler.

- [ ] **Görev 15.Son: Faz 15 Night Agent Menü Entegrasyonu, Play Mode Testi ve Görsel Doğrulama**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` ve `Assets/_GameAssets/Tests/PlayMode/Phase15Tests.cs`
  - **Menü Öğeleri:**
    - `[MenuItem("Tools/Night Agent/Faz 15: Kamera Sarsıntısı ve Dinamik FOV/Uygula")]` : Ana kameraya veya sahneye `CameraJuiceController` ekler ve referanslarını bağlar.
    - `[MenuItem("Tools/Night Agent/Faz 15: Kamera Sarsıntısı ve Dinamik FOV/Geri çek")]` : `CameraJuiceController` bileşenini sahneden kaldırır.
    - `[MenuItem("Tools/Night Agent/Faz 15: Kamera Sarsıntısı ve Dinamik FOV/Detay")]` : Kamera sarsıntısı ve dinamik FOV sistemini açıklayan Türkçe diyalog penceresi açar.
  - **Play Mode Test & Görsel İnceleme:**
    - `Phase15Tests.cs` içinde geçici sahnede `TriggerShake` ve Dash sırasında FOV artışını `[UnityTest]` ile doğrula; `TestScreenshots/faz15_sonucu.png` ekran görüntüsünü kaydet ve incele.

---

## Faz 16: Gelişmiş Ses Mimarisi ve Yüzey Adım Sesleri (Audio & Footsteps)

- [ ] **Görev 16.1: Ses Kütüphanesi ScriptableObject (`AudioLibrarySO`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Data/AudioLibrarySO.cs`
  - **Sınıf Adı:** `AudioLibrarySO : ScriptableObject` (`[CreateAssetMenu(fileName = "AudioLibrary", menuName = "CivCiv/Data/AudioLibrarySO")]`)
  - **Alanlar ve Eventler:**
    - `public AudioClip[] FootstepClips;`, `public AudioClip[] JumpClips;`, `public AudioClip[] ChickChirpClips;`, `public AudioClip[] DashClips;`, `public AudioClip[] CoinClips;`, `public AudioClip[] HurtClips;`
  - **Metodlar ve İşleyiş:**
    - `public AudioClip GetRandomClip(AudioClip[] clips)` : Diziden rastgele bir `AudioClip` döndürür (dizi boşsa `null` döner).

- [ ] **Görev 16.2: 3D Konumsal Ses Havuzu ve Adım Sesi Tetikleyicisi (`SpatialAudioManager` & `FootstepAudioTrigger`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Core/SpatialAudioManager.cs` ve `Assets/_GameAssets/Scripts/Player/FootstepAudioTrigger.cs`
  - **Sınıf Adları:** `SpatialAudioManager : MonoBehaviour`, `FootstepAudioTrigger : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `SpatialAudioManager` -> `public static SpatialAudioManager Instance { get; private set; }`
    - `FootstepAudioTrigger` -> `[SerializeField] private AudioLibrarySO _audioLibrary;`, `[SerializeField] private float _stepInterval = 0.35f;`
  - **Metodlar ve İşleyiş:**
    - `SpatialAudioManager.Play3DSound(AudioClip clip, Vector3 position, float volume = 1f, float pitchVariance = 0.08f)` : `SettingsUI.IsSoundEnabled` aktifse rastgele pitch (`1f + Random.Range(-pitchVariance, pitchVariance)`) ile ses çalar.
    - `FootstepAudioTrigger.UpdateFootstepTimer()` : Karakter yerde hareket ederken (`PlayerState.Move`) hızla orantılı aralıklarla adım sesi ve zıplarken civciv ötme (`ChickChirpClips`) sesi çalar.

- [ ] **Görev 16.Son: Faz 16 Night Agent Menü Entegrasyonu, Play Mode Testi ve Görsel Doğrulama**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` ve `Assets/_GameAssets/Tests/PlayMode/Phase16Tests.cs`
  - **Menü Öğeleri:**
    - `[MenuItem("Tools/Night Agent/Faz 16: 3D Ses Mimarisi ve Adım Sesleri/Uygula")]` : `Assets/_GameAssets/Data/DefaultAudioLibrary.asset` oluşturur, sahneye `SpatialAudioManager` ve karaktere `FootstepAudioTrigger` kurar.
    - `[MenuItem("Tools/Night Agent/Faz 16: 3D Ses Mimarisi ve Adım Sesleri/Geri çek")]` : `SpatialAudioManager` ve `FootstepAudioTrigger` bileşenlerini kaldırır.
    - `[MenuItem("Tools/Night Agent/Faz 16: 3D Ses Mimarisi ve Adım Sesleri/Detay")]` : `AudioLibrarySO`, `SpatialAudioManager` ve `FootstepAudioTrigger` sistemini açıklayan Türkçe diyalog penceresi açar.
  - **Play Mode Test & Görsel İnceleme:**
    - `Phase16Tests.cs` içinde geçici sahnede `SpatialAudioManager` ve `FootstepAudioTrigger` bileşenlerinin hatasız çalıştığını `[UnityTest]` ile doğrula; `TestScreenshots/faz16_sonucu.png` ekran görüntüsünü kaydet ve incele.

---

## Faz 17: Dinamik Gece/Gündüz Döngüsü ve Hava Durumu (Environment System)

- [ ] **Görev 17.1: Gece/Gündüz ve Işık Döngüsü Kontrolcüsü (`DayNightCycleController`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Environment/DayNightCycleController.cs`
  - **Sınıf Adı:** `DayNightCycleController : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private Light _directionalLight;`
    - `[SerializeField] private float _dayDurationInSeconds = 180f;`
    - `[SerializeField][Range(0f, 1f)] private float _timeOfDay = 0.3f;` (0: Gece yarısı, 0.25: Gün doğumu, 0.5: Öğle, 0.75: Gün batımı)
    - `public float TimeOfDay => _timeOfDay;`
    - `public bool IsNightTime => _timeOfDay < 0.22f || _timeOfDay > 0.78f;`
    - `public event System.Action<bool> OnNightStateChanged;`
  - **Metodlar ve İşleyiş:**
    - `private void UpdateSunRotationAndLighting()` : `Update()` içinde `_timeOfDay` değerini ilerletir, `_directionalLight.transform.rotation` açısını, ışık yoğunluğunu (`intensity`) ve `RenderSettings.ambientLight` rengini zamana göre yumuşakça günceller.
    - `public void SetTimeOfDay(float normalizedTime)` : Zamanı manuel ayarlar ve gece/gündüz değişiminde `OnNightStateChanged` tetikler.

- [ ] **Görev 17.Son: Faz 17 Night Agent Menü Entegrasyonu, Play Mode Testi ve Görsel Doğrulama**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` ve `Assets/_GameAssets/Tests/PlayMode/Phase17Tests.cs`
  - **Menü Öğeleri:**
    - `[MenuItem("Tools/Night Agent/Faz 17: Gece ve Gündüz Işık Döngüsü/Uygula")]` : Sahneye `DayNightCycleController` kurar ve sahnedeki `Directional Light` referansını bağlar.
    - `[MenuItem("Tools/Night Agent/Faz 17: Gece ve Gündüz Işık Döngüsü/Geri çek")]` : `DayNightCycleController` nesnesini sahneden kaldırır.
    - `[MenuItem("Tools/Night Agent/Faz 17: Gece ve Gündüz Işık Döngüsü/Detay")]` : Gece/gündüz döngüsü ve atmosferik ışık geçişlerini açıklayan Türkçe diyalog penceresi açar.
  - **Play Mode Test & Görsel İnceleme:**
    - `Phase17Tests.cs` içinde geçici sahnede `SetTimeOfDay(0.1f)` (Gece) ve `SetTimeOfDay(0.5f)` (Öğle) çağrılarıyla `IsNightTime` durumunu ve ışık açısını `[UnityTest]` ile doğrula; `TestScreenshots/faz17_sonucu.png` ekran görüntüsünü kaydet ve incele.

---

## Faz 18: Mini Harita, Pusula ve Hedef İşaretleyici (Minimap & Waypoint HUD)

- [ ] **Görev 18.1: 3D Hedef ve Kayıp CivCiv Ekran İşaretleyicisi (`WaypointIndicatorUI`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/UI/WaypointIndicatorUI.cs`
  - **Sınıf Adı:** `WaypointIndicatorUI : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `[SerializeField] private RectTransform _indicatorIcon;`, `[SerializeField] private TMPro.TextMeshProUGUI _distanceText;`, `[SerializeField] private Vector3 _worldOffset = new Vector3(0f, 1.5f, 0f);`
    - `public Transform CurrentTarget => _currentTarget;`
  - **Metodlar ve İşleyiş:**
    - `public void FindNearestObjective()` : Sahnedeki kurtarılmamış en yakın `LostChickNPC` veya `CoinCollect` nesnesini hedef olarak seçer.
    - `private void UpdateScreenPosition()` : `LateUpdate()` içinde `Camera.main.WorldToScreenPoint(_currentTarget.position + _worldOffset)` ile hedefin ekran koordinatını hesaplar, kamera arkasındaysa ekran kenarına (`Screen.width`, `Screen.height` sınırlarına) sabitler ve mesafeyi metre (`"14m"`) olarak yazar.

- [ ] **Görev 18.Son: Faz 18 Night Agent Menü Entegrasyonu, Play Mode Testi ve Görsel Doğrulama**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` ve `Assets/_GameAssets/Tests/PlayMode/Phase18Tests.cs`
  - **Menü Öğeleri:**
    - `[MenuItem("Tools/Night Agent/Faz 18: 3D Hedef ve Pusula İşaretleyicisi/Uygula")]` : `Canvas` altına ikon ve mesafe metni içeren `WaypointIndicatorUI` bileşenini oluşturur.
    - `[MenuItem("Tools/Night Agent/Faz 18: 3D Hedef ve Pusula İşaretleyicisi/Geri çek")]` : `Canvas` altındaki `WaypointIndicatorUI` nesnesini kaldırır.
    - `[MenuItem("Tools/Night Agent/Faz 18: 3D Hedef ve Pusula İşaretleyicisi/Detay")]` : `WaypointIndicatorUI` hedef takip sistemini açıklayan Türkçe diyalog penceresi açar.
  - **Play Mode Test & Görsel İnceleme:**
    - `Phase18Tests.cs` içinde geçici sahnede hedef seçimini (`FindNearestObjective`) ve ekran üzeri mesafe metni güncellemesini `[UnityTest]` ile doğrula; `TestScreenshots/faz18_sonucu.png` ekran görüntüsünü kaydet ve incele.

---

## Faz 19: Seviye İlerleme, Bölüm Seçimi ve Ana Menü (Level Progression)

- [ ] **Görev 19.1: Seviye Bilgisi ScriptableObject ve Bölüm Yöneticisi (`LevelDataSO` & `LevelProgressionManager`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Data/LevelDataSO.cs` ve `Assets/_GameAssets/Scripts/Core/LevelProgressionManager.cs`
  - **Sınıf Adları:** `LevelDataSO : ScriptableObject`, `LevelProgressionManager : MonoBehaviour`
  - **Alanlar ve Eventler:**
    - `LevelDataSO` -> `public string LevelId = "Level_1";`, `public string LevelDisplayName = "Çiftlik Bahçesi";`, `public string SceneName = "Demo";`, `public int OneStarScore = 5;`, `public int TwoStarScore = 15;`, `public int ThreeStarScore = 30;`
    - `LevelProgressionManager` -> `public static LevelProgressionManager Instance { get; private set; }`
  - **Metodlar ve İşleyiş:**
    - `public int CalculateStarsEarned(int coinsCollected, float remainingSeconds)` : Toplanan altın ve kalan süreye göre 1-3 arası yıldız puanı hesaplar ve `PlayerPrefs` (`"CivCiv_Stars_" + LevelId`) olarak kaydeder.
    - `public void LoadNextLevel()` : Sıradaki sahneyi yükler; tek sahne varsa zorluk çarpanını artırarak sahneyi yeniden başlatır.

- [ ] **Görev 19.Son: Faz 19 Night Agent Menü Entegrasyonu, Play Mode Testi ve Görsel Doğrulama**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` ve `Assets/_GameAssets/Tests/PlayMode/Phase19Tests.cs`
  - **Menü Öğeleri:**
    - `[MenuItem("Tools/Night Agent/Faz 19: Seviye İlerleme ve Yıldız Puanı Sistemi/Uygula")]` : `Assets/_GameAssets/Data/DefaultLevelData.asset` oluşturur ve sahneye `LevelProgressionManager` kurar.
    - `[MenuItem("Tools/Night Agent/Faz 19: Seviye İlerleme ve Yıldız Puanı Sistemi/Geri çek")]` : `LevelProgressionManager` nesnesini sahneden kaldırır.
    - `[MenuItem("Tools/Night Agent/Faz 19: Seviye İlerleme ve Yıldız Puanı Sistemi/Detay")]` : `LevelDataSO` ve yıldız puanı hesaplama sistemini açıklayan Türkçe diyalog penceresi açar.
  - **Play Mode Test & Görsel İnceleme:**
    - `Phase19Tests.cs` içinde geçici sahnede `CalculateStarsEarned` fonksiyonunun 1, 2 ve 3 yıldız eşiklerini doğru hesapladığını ve kaydettiğini `[UnityTest]` ile doğrula; `TestScreenshots/faz19_sonucu.png` ekran görüntüsünü kaydet ve incele.

---

## Faz 20: Bütünsel Entegrasyon, Tek Tıkla Tüm Fazları Kurma ve Son Doğrulama

- [ ] **Görev 20.1: `NightAgentMenu` Toplu İşlem Seçenekleri (`Tüm Fazları Uygula` / `Tümünü Geri Çek`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs`
  - **Sınıf Adı:** `NightAgentMenu` (Static Editor Class)
  - **Metodlar ve İşleyiş:**
    - `[MenuItem("Tools/Night Agent/Tüm Fazları Uygula (Faz 1-19)", false, 1)] public static void ApplyAllPhases()` : Faz 1'den Faz 19'a kadar tüm `ApplyPhaseX()` metodlarını sırayla çağırır ve sahneyi dirty işaretler.
    - `[MenuItem("Tools/Night Agent/Tüm Fazları Geri Çek (Faz 1-19)", false, 2)] public static void RevertAllPhases()` : Faz 19'dan Faz 1'e kadar tüm `RevertPhaseX()` metodlarını sırayla çağırarak sahneyi orijinal temiz haline döndürür.

- [ ] **Görev 20.2: Çalışma Zamanı Hata ve Referans Doğrulayıcı (`RuntimeSystemValidator`)**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Core/RuntimeSystemValidator.cs`
  - **Sınıf Adı:** `RuntimeSystemValidator : MonoBehaviour`
  - **Metodlar ve İşleyiş:**
    - `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] private static void EnsureCriticalSystemsOnLoad()` : Sahne yüklendiğinde temel yöneticiler (`GameManager`, `CoinManager`, `ComboManager`, `ObjectPoolManager`) sahnede eksikse çalışma zamanında otomatik oluşturarak `NullReferenceException` oluşmasını tamamen engeller.

- [ ] **Görev 20.Son: Faz 20 Menü Entegrasyonu, Bütünsel Play Mode Testi, Görsel İnceleme ve Raporlama**
  - **Dosya Yolu:** `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` ve `Assets/_GameAssets/Tests/PlayMode/Phase20FinalTests.cs`
  - **Menü Öğeleri:**
    - `[MenuItem("Tools/Night Agent/Faz 20: Bütünsel Sistem Doğrulayıcı/Uygula")]` : `ApplyAllPhases()` çağırır ve `RuntimeSystemValidator` doğrulamasını çalıştırır.
    - `[MenuItem("Tools/Night Agent/Faz 20: Bütünsel Sistem Doğrulayıcı/Geri çek")]` : `RevertAllPhases()` çağırır.
    - `[MenuItem("Tools/Night Agent/Faz 20: Bütünsel Sistem Doğrulayıcı/Detay")]` : Faz 1 - Faz 20 arasındaki tüm mimariyi özetleyen Türkçe diyalog penceresi açar.
  - **Play Mode Test & Görsel İnceleme:**
    1. `Phase20FinalTests.cs` içinde tüm sistemlerin (Hareket, Dash, Glide, Gaga Saldırısı, Can, Kombo, Platformlar, Sandık/Loot, Düşman AI, Kurtarma Görevi, Kayıt/Mağaza, Başarım, VFX, Kamera, Ses, Gece/Gündüz, Waypoint ve HUD) aynı geçici sahnede çakışmadan çalıştığını `[UnityTest]` ile doğrula ve `TestScreenshots/faz20_final.png` ekran görüntüsü alarak görseli incele.
    2. `ARCHITECTURE.md` ve `NIGHT_REPORT.md` dosyalarını son durumla güncelle.
