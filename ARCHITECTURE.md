# Proje Mimari Haritası (3D CivCiv Game)

## Teknik Altyapı ve Paketler
- **Motor Sürümü:** Unity 6 (`6000.6.8f1`)
- **Render Pipeline:** Universal Render Pipeline (URP `17.6.0`) & Shader Graph
- **Kamera Sistemi:** Unity Cinemachine (`6.6.0` - `Unity.Cinemachine`)
- **Girdi (Input) Sistemi:** Unity New Input System (`1.20.1` - `ENABLE_INPUT_SYSTEM`) + Eski `UnityEngine.Input` Fallback Desteği
- **Arayüz (UI) & Animasyon:** uGUI (`UnityEngine.UI`), TextMeshPro (`TMPro`), DOTween

## Klasör Yapısı
- `Assets/_GameAssets/Scripts/` : Ana oyun mekanikleri, karakter, kamera ve etkileşim scriptleri.
- `Assets/_GameAssets/Scripts/Enums/` : Durum ve tip numaralandırmaları (`enum`).
- `Assets/_GameAssets/Scripts/UI/` : Oyun içi arayüz (HUD), sayaç, toplanabilir obje yöneticileri ve ayarlar menüsü.
- `Assets/_GameAssets/Scripts/Editor/` : Sahne, kamera, karakter ve UI otomatik kurulum araçları (`UnityEditor`).
- `Assets/_GameAssets/Sprites/` : UI ikonları ve arayüz görselleri (`MusicButton.png`, `SoundButton.png` vb.).
- `Assets/Synty/` : 3D çevre modelleri, shader'lar, yardımcı scriptler ve aktif sahne (`Assets/Synty/PolygonStarter/Scenes/Demo.unity`).

## Mevcut Scriptler ve Sorumlulukları

### 1. Karakter, Kamera ve Oynanış (`Assets/_GameAssets/Scripts/`)
- `PlayerController.cs` : `Rigidbody` tabanlı 3D karakter hareket kontrolcüsü (`PlayerStatsSO` entegrasyonu, yürüme, hız limitleri ve çarpanı, zemin/hava sürtünmesi `linearDamping`, `SphereCastAll` tabanlı zemin kontrolü, zıplama, collider küçültmeli kayma/slide mekaniği ve `Animator` parametre/state senkronizasyonu).
- `ThirdPersonCameraController.cs` : Cinemachine 3. Şahıs (`CM_TPS_Camera`) ve 1. Şahıs (`CM_FPS_Camera`) kamera perspektif yöneticisi (`CameraPerspective`), fare ile bakış/dönüş (`_rotateWithMouse`), perspektif geçişi (`V` tuşu), FPS modunda karakter gövdesini `ShadowsOnly` yapma ve imleç kilitleme (`CursorLockMode`).
- `StateController.cs` : Karakterin anlık durumunu (`PlayerState`) tutan ve durum geçişlerini yöneten bileşen.
- `TargetJump.cs` : Oyuncu (`PlayerController`) tetikleyici alana (`OnTriggerEnter`) girdiğinde karakterin `Rigidbody` bileşenine dikey zıplama kuvveti (`ForceMode.Impulse`) uygulayan zıplatma platformu.
- `ICollectable.cs` : Toplanabilir nesneler için temel arayüz (`void Collect()`).
- `Data/PlayerStatsSO.cs` : Karakterin hareket, zıplama, kayma (slide), ileri atılma (dash) ve stamina parametrelerini tutan `ScriptableObject` veri sınıfı.

### 2. Durum Tanımları (`Assets/_GameAssets/Scripts/Enums/`)
- `PlayerState.cs` : Karakter hareket durumlarını tanımlayan enum (`Idle`, `Move`, `Jump`, `SlideIdle`, `Slide`, `Dash`, `Glide`).

### 3. Kullanıcı Arayüzü ve Oyun Yönetimi (`Assets/_GameAssets/Scripts/UI/`)
- `CoinManager.cs` : Toplanan altın/puan miktarını yöneten Singleton sınıfı (`CoinManager.Instance`). `CoinsChanged` ve `CoinsCollected` event'lerini tetikler.
- `CoinCollect.cs` : `ICollectable` arayüzünü uygulayan toplanabilir obje scripti. Oyuncu temas ettiğinde `CoinManager.Instance.AddCoins(value)` çağırır ve nesneyi yok eder.
- `CoinUI.cs` : `CoinManager.Instance.CoinsChanged` event'ini dinleyerek ekrandaki `TextMeshProUGUI` (`CoinValueText`) metnini güncelleyen HUD bileşeni.
- `ClockUI.cs` : Geri sayım sayacını (`MM:SS`) ve saat ibresi (`Stick`) rotasyonunu yöneten HUD bileşeni. `CoinManager.Instance.CoinsCollected` event'ini dinleyerek toplanan coin başına ekstra süre ekler.
- `SettingsUI.cs` : Oyun içi ayarlar menüsü ve ses/müzik yöneticisi Singleton sınıfı (`SettingsUI.Instance`). PopUp açma/kapama animasyonu, oyun duraklatma (`Time.timeScale`), `PlayerPrefs` ile müzik/SFX kaydetme (`CivCiv_MusicEnabled`, `CivCiv_SoundEnabled`), `SettingsUI.PlaySFX` genel ses çalma, yeniden başlatma ve çıkış işlevlerini barındırır.

### 4. Editör Araçları (`Assets/_GameAssets/Scripts/Editor/`)
- `CivCivAutoSetup.cs` : `Tools/CivCiv/` menüsü altında karakter, fizik, TPS/FPS Cinemachine kameraları (`Ctrl+Shift+K`) ve `SettingsUI` buton/referans hiyerarşisini (`Ctrl+Shift+U`) sahnede otomatik kuran editör aracı.

## Mimari Kurallar ve İletişim Akışı
- **İsim Alanı (Namespace):** Mevcut scriptler global namespace (root) altında tanımlıdır.
- **Singleton & Statik Erişimler:**
  - `CoinManager.Instance` (Altın/Skor yönetimi)
  - `SettingsUI.Instance` (Ayarlar menüsü, `SettingsUI.IsOpen`, `SettingsUI.IsMusicEnabled`, `SettingsUI.IsSoundEnabled`, `SettingsUI.PlaySFX()`)
- **Olay Güdümlü (Event-Driven) İletişim:**
  - `CoinCollect` -> `CoinManager.AddCoins()` -> `CoinsChanged` (`CoinUI` güncellenir) & `CoinsCollected` (`ClockUI` süre ekler).
  - `SettingsUI` -> `OnMenuStateChanged`, `OnMusicToggled`, `OnSoundToggled` event'leri ve `SettingsUI.IsOpen` üzerinden `ThirdPersonCameraController` imleç kilidi koordinasyonu.
