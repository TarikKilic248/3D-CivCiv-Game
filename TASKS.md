# 3D CivCiv Game - Faz ve Görev Yol Haritası (Faz 1 - Faz 20)

## Standart Faz Yapısı
Her faz aşağıdaki standart akışla tamamlanır:
1. **Amacı:** Fazın oyuna kattığı temel değer ve çözdüğü problem.
2. **İstenilenler (Görevler):** Kodlanacak veya mevcut kodu doğrulanıp gerçek sahneye (`Demo.unity`) entegre edilecek sistemlerin işlevsel tanımı.
3. **Faz Sonu Gerçek Sahne Kurulumu & Doğrulama:**
   - `Tools/Night Agent/Faz <No>: <Faz Başlığı>/` altına **Uygula**, **Geri çek** ve **Detay** menülerinin eklenmesi.
   - `NightAgentBridge` (`VERIFY:<No>`) ile fazın **gerçek `Demo.unity` sahnesine uygulanıp kaydedilmesi**, `NightAgentVerifier` ile bileşen/referans kontrolünün (`PASS`) yapılması ve `TestScreenshots/faz<No>_sonucu.png` ekran görüntüsünün alınması.

---

## Faz 1: Veri Mimarisi (ScriptableObject) ve Karakter Yetenekleri (Dash & Stamina)
- **Amacı:** Karakterin hareket, zıplama, kayma (slide), ileri atılma (dash) ve enerji (stamina) verilerini ScriptableObject yapısına taşımak ve karaktere enerji harcayarak ileri atılma yeteneği kazandırmak.
- **İstenilenler:**
  - [ ] **Görev 1.1: Oyuncu İstatistikleri ve Karakter Entegrasyonu (`PlayerStatsSO`, `PlayerState` & `PlayerController`)**
    - Karakterin hareket, zıplama, slide, dash ve stamina değerlerini tutan `PlayerStatsSO` veri yapısını ve `DefaultPlayerStats.asset` dosyasını oluştur.
    - `PlayerState` içine `Dash` ve `Glide` durumlarını ekle; `PlayerController` bileşeninin verileri `PlayerStatsSO` üzerinden okumasını sağla.
  - [ ] **Görev 1.2: İleri Atılma ve Stamina Sistemi (`DashController`)**
    - Karakterin `LeftShift` tuşuyla baktığı yöne ileri atılmasını, stamina harcamasını ve dururken staminanın zamanla yenilenmesini sağla.
  - [ ] **Görev 1.3: Faz 1 Gerçek Sahne (`Demo.unity`) Kurulumu ve Görsel Doğrulama**
    - `NightAgentMenu` ve `NightAgentBridge` (`VERIFY:1`) üzerinden `DefaultPlayerStats.asset` ve `DashController` bileşenini `Demo.unity` sahnesindeki gerçek karaktere bağla, sahneyi kaydet ve `TestScreenshots/faz1_sonucu.png` ile doğrula.

---

## Faz 2: Çekirdek Oyun Döngüsü ve Skor Yönetimi
- **Amacı:** Oyunun başlama, duraklatılma, kazanma ve kaybetme akışını merkezi bir yapıya kavuşturmak ve en yüksek skoru kalıcı olarak saklamak.
- **İstenilenler:**
  - [ ] **Görev 2.1: Oyun Durumu ve Merkezi Yönetici (`GameState` & `GameManager`)**
    - Oyun durumlarını (`Playing`, `Paused`, `Victory`, `GameOver`) tanımla.
    - Sahnedeki hedef altın toplandığında zafer (`Victory`), süre veya can bittiğinde yenilgi (`GameOver`) durumunu tetikleyen ve bölümü yeniden başlatabilen merkezi yöneticiyi kur.
  - [ ] **Görev 2.2: Süre Bitimi ve Yüksek Skor Kaydı (`ClockUI` & `CoinManager`)**
    - Geri sayım sayacı sıfırlandığında oyun sonunu tetikle ve dışarıdan bonus süre eklenebilmesini sağla.
    - Toplanan en yüksek altın skorunu (`HighScore`) cihazda kalıcı olarak kaydet.
  - [ ] **Görev 2.3: Faz 2 Gerçek Sahne (`Demo.unity`) Kurulumu ve Görsel Doğrulama**
    - `NightAgentMenu` ve `NightAgentBridge` (`VERIFY:2`) üzerinden `GameManager` ve `CoinManager` yöneticilerini `Demo.unity` sahnesine kur, sahneyi kaydet ve `TestScreenshots/faz2_sonucu.png` ile doğrula.

---

## Faz 3: Toplanabilir Öğeler, Güçlendiriciler ve Kontrol Noktaları
- **Amacı:** Seviye içi etkileşimi zenginleştirmek için altınlara görsel/işitsel efekt kazandırmak, karaktere geçici güçlendiriciler sunmak ve tehlike anında kontrol noktasında yeniden doğmasını sağlamak.
- **İstenilenler:**
  - [ ] **Görev 3.1: Gelişmiş Altın ve Güçlendirici Sistemi (`CoinCollect` & `PowerUpCollectable`)**
    - Altınlara dönme, süzülme, ses ve küçülerek yok olma animasyonu ekle.
    - Oyuncuya ekstra süre, hız artışı ve enerji (stamina) yenilemesi sağlayan toplanabilir güçlendirici nesneler oluştur.
  - [ ] **Görev 3.2: Kontrol Noktası ve Tehlike Alanları (`Checkpoint` & `HazardZone`)**
    - Oyuncunun geçtiği son güvenli noktayı kaydeden kontrol noktaları ve tehlikeli alanlara düşüldüğünde oyuncuyu bu noktaya ışınlayan tehlike bölgeleri oluştur.
  - [ ] **Görev 3.3: Faz 3 Gerçek Sahne (`Demo.unity`) Kurulumu ve Görsel Doğrulama**
    - `NightAgentMenu` ve `NightAgentBridge` (`VERIFY:3`) üzerinden güçlendirici, kontrol noktası ve tehlike alanı prefab/objelerini `Demo.unity` sahnesine kur, sahneyi kaydet ve `TestScreenshots/faz3_sonucu.png` ile doğrula.

---

## Faz 4: Oyun İçi Arayüz (HUD) ve Oyun Sonu Ekranı
- **Amacı:** Oyuncuya anlık enerji (stamina) durumunu ve oyun bittiğinde kazanma/kaybetme istatistiklerini ekranda görsel olarak sunmak.
- **İstenilenler:**
  - [ ] **Görev 4.1: Enerji Çubuğu ve Oyun Sonu Paneli (`GameplayHUD`)**
    - Karakterin stamina değişimini anlık gösteren enerji barını ve oyun bittiğinde skor/rekor bilgisiyle birlikte yeniden başlatma butonu sunan oyun sonu ekranını oluştur.
  - [ ] **Görev 4.2: Faz 4 Gerçek Sahne (`Demo.unity`) Kurulumu ve Görsel Doğrulama**
    - `NightAgentMenu` ve `NightAgentBridge` (`VERIFY:4`) üzerinden `Demo.unity` içindeki `Canvas` altına görsel `StaminaSlider` ve `EndGamePanel` UI hiyerarşisini kurup `GameplayHUD` bileşenine bağla, sahneyi kaydet ve `TestScreenshots/faz4_sonucu.png` ile doğrula.

---

## Faz 5: Gelişmiş CivCiv Hareket ve Saldırı Yetenekleri
- **Amacı:** 3D platform oynanışını derinleştirmek için civciv karakterine havada çift zıplama, kanat çırparak süzülme, yakın mesafe gaga vuruşu ve havadan yere sert iniş yetenekleri kazandırmak.
- **İstenilenler:**
  - [ ] **Görev 5.1: Çift Zıplama ve Süzülme (`ChickGlideController`)**
    - Karakterin havadayken ikinci kez zıplayabilmesini ve zıplama tuşuna basılı tutulduğunda düşüş hızını yavaşlatarak süzülmesini sağla.
  - [ ] **Görev 5.2: Gaga Saldırısı ve Yere Çakılma (`ChickPeckAttack`)**
    - Karakterin önündeki hedeflere gaga vuruşu yapabilmesini ve havadayken hızla yere çakılarak çevresine darbe vurabilmesini sağla.
  - [ ] **Görev 5.3: Faz 5 Gerçek Sahne (`Demo.unity`) Kurulumu ve Görsel Doğrulama**
    - `NightAgentMenu` ve `NightAgentBridge` (`VERIFY:5`) üzerinden `ChickGlideController` ve `ChickPeckAttack` bileşenlerini `Demo.unity` sahnesindeki gerçek karaktere ekle, sahneyi kaydet ve `TestScreenshots/faz5_sonucu.png` ile doğrula.

---

## Faz 6: Can, Hasar ve Savunma Sistemi
- **Amacı:** Oyuncunun ve dünyadaki hedeflerin hasar alabilmesini, oyuncunun can durumunun kalp ikonlarıyla takip edilmesini ve hasar sonrası geçici dokunulmazlık kazanmasını sağlamak.
- **İstenilenler:**
  - [ ] **Görev 6.1: Hasar Arayüzü ve Oyuncu Can Sistemi (`IDamageable` & `PlayerHealth`)**
    - Hasar alabilen tüm nesneler için ortak bir yapı kur; oyuncuya can, iyileşme, geri savrulma (knockback) ve hasar sonrası yanıp sönmeli dokunulmazlık süresi ekle.
  - [ ] **Görev 6.2: Kalp ve Can Göstergesi (`HealthUI`)**
    - Oyuncunun mevcut ve maksimum canını HUD üzerinde kalp ikonları ve metin olarak göster.
  - [ ] **Görev 6.3: Faz 6 Gerçek Sahne (`Demo.unity`) Kurulumu ve Görsel Doğrulama**
    - `NightAgentMenu` ve `NightAgentBridge` (`VERIFY:6`) üzerinden karaktere `PlayerHealth` ve `Canvas` altına kalp ikonlu `HealthUI` panelini kur, sahneyi kaydet ve `TestScreenshots/faz6_sonucu.png` ile doğrula.

---

## Faz 7: Kombo Çarpanı ve Altın Mıknatısı
- **Amacı:** Hızlı ve kesintisiz altın toplamayı ödüllendiren bir kombo/çarpan sistemi kurmak ve yakınlardaki altınları karaktere doğru çeken mıknatıs özelliği eklemek.
- **İstenilenler:**
  - [ ] **Görev 7.1: Kombo Yöneticisi ve Arayüzü (`ComboManager` & `ComboUI`)**
    - Kısa süre içinde ardışık altın toplandığında kombo serisini ve skor çarpanını artıran, süre dolduğunda veya hasar alındığında sıfırlayan sistemi ve HUD göstergesini oluştur.
  - [ ] **Görev 7.2: Altın Mıknatısı (`CoinMagnet`)**
    - Karakterin belirli bir yarıçap içindeki altınları otomatik olarak kendine doğru çekmesini ve güçlendirici ile çekim alanının büyütülmesini sağla.
  - [ ] **Görev 7.3: Faz 7 Gerçek Sahne (`Demo.unity`) Kurulumu ve Görsel Doğrulama**
    - `NightAgentMenu` ve `NightAgentBridge` (`VERIFY:7`) üzerinden `ComboManager`, `CoinMagnet` ve `Canvas` altına `ComboUI` panelini `Demo.unity` sahnesine kur, sahneyi kaydet ve `TestScreenshots/faz7_sonucu.png` ile doğrula.

---

## Faz 8: Dinamik Platformlar ve Fiziksel Seviye Öğeleri
- **Amacı:** Haritada hareketli parkurlar oluşturmak için noktalar arası gezinen platformlar, basıldığında çöken bloklar ve süzülmeyi destekleyen rüzgar akımları eklemek.
- **İstenilenler:**
  - [ ] **Görev 8.1: Hareketli ve Çöken Platformlar (`MovingPlatform` & `FallingPlatform`)**
    - Belirlenen noktalar arasında hareket ederken üzerindeki oyuncuyu kaydırmadan taşıyan platformlar ve üzerine çıkıldığında titreyip kısa süre sonra çöken/yeniden oluşan platformlar oluştur.
  - [ ] **Görev 8.2: Rüzgar Akımı Bölgesi (`WindCurrentZone`)**
    - İçine giren oyuncuyu yukarı/belirli yöne iten ve kanatlarla süzülürken ekstra kaldırma kuvveti uygulayan hava akımı alanları oluştur.
  - [ ] **Görev 8.3: Faz 8 Gerçek Sahne (`Demo.unity`) Kurulumu ve Görsel Doğrulama**
    - `NightAgentMenu` ve `NightAgentBridge` (`VERIFY:8`) üzerinden platform ve rüzgar akımı prefab/objelerini `Demo.unity` sahnesine kur, sahneyi kaydet ve `TestScreenshots/faz8_sonucu.png` ile doğrula.

---

## Faz 9: Kırılabilir Nesneler ve Ganimet (Loot) Sistemi
- **Amacı:** Oyuncunun gaga vuruşu, dash veya yere çakılma hamleleriyle kırabileceği sandıklar/yumurtalar ve içlerinden çıkan rastgele ödül sistemini kurmak.
- **İstenilenler:**
  - [ ] **Görev 9.1: Ganimet Tablosu ve Kırılabilir Sandıklar (`LootTableSO` & `BreakableCrate`)**
    - Ağırlıklı olasılıklara göre altın veya güçlendirici düşüren veri tablosu oluştur.
    - Saldırı veya dash ile vurulduğunda sarsılıp kırılan ve içinden ganimet/altın saçan kırılabilir sandık ve sürpriz yumurtalar ekle.
  - [ ] **Görev 9.2: Faz 9 Gerçek Sahne (`Demo.unity`) Kurulumu ve Görsel Doğrulama**
    - `NightAgentMenu` ve `NightAgentBridge` (`VERIFY:9`) üzerinden `DefaultLootTable.asset` ve kırılabilir sandıkları `Demo.unity` sahnesine kur, sahneyi kaydet ve `TestScreenshots/faz9_sonucu.png` ile doğrula.

---

## Faz 10: Düşman Yapay Zekası ve Tehditler
- **Amacı:** Seviyelerde devriye gezen, oyuncuyu gördüğünde takip eden ve üstüne zıplandığında veya gagalandığında etkisiz hale getirilebilen düşmanlar oluşturmak.
- **İstenilenler:**
  - [ ] **Görev 10.1: Düşman Verisi ve Devriye Yapay Zekası (`EnemyStatsSO` & `PatrolEnemyAI`)**
    - Düşman hız, algılama mesafesi ve hasar verilerini tanımla.
    - Noktalar arası devriye gezen, oyuncu menzile girince kovalayan, yandan çarptığında hasar veren ancak üstüne zıplandığında hasar alıp oyuncuyu zıplatan düşman yapay zekasını kodla.
  - [ ] **Görev 10.2: Faz 10 Gerçek Sahne (`Demo.unity`) Kurulumu ve Görsel Doğrulama**
    - `NightAgentMenu` ve `NightAgentBridge` (`VERIFY:10`) üzerinden `DefaultEnemyStats.asset` ve örnek devriye düşmanını `Demo.unity` sahnesine kur, sahneyi kaydet ve `TestScreenshots/faz10_sonucu.png` ile doğrula.

---

## Faz 11: Etkileşimli Çevre, Bulmacalar ve Kayıp CivCiv Görevi
- **Amacı:** Oyuncunun çevredeki nesnelerle etkileşime geçmesini, basınç plakalarıyla kapı/köprü açmasını ve haritadaki kayıp yavru civcivleri kurtarma görevini tamamlamasını sağlamak.
- **İstenilenler:**
  - [ ] **Görev 11.1: Etkileşim Sistemi ve Basınç Plakası (`IInteractable`, `PlayerInteractor` & `PressurePlateSwitch`)**
    - Yakındaki etkileşimli nesneleri algılayıp tuşla tetikleyen sistemi ve üzerine çıkıldığında bağlı kapı veya köprüyü açan basınç plakalarını oluştur.
  - [ ] **Görev 11.2: Kayıp CivCiv Kurtarma Sistemi (`LostChickNPC` & `ChickRescueManager`)**
    - Etkileşime girildiğinde kurtarılan, oyuncunun peşinden kuyruk halinde gelen kayıp civcivleri ve bölümdeki kurtarma ilerlemesini takip eden yöneticiyi oluştur.
  - [ ] **Görev 11.3: Faz 11 Gerçek Sahne (`Demo.unity`) Kurulumu ve Görsel Doğrulama**
    - `NightAgentMenu` ve `NightAgentBridge` (`VERIFY:11`) üzerinden etkileşim ve kurtarma sistemlerini `Demo.unity` sahnesine kur, sahneyi kaydet ve `TestScreenshots/faz11_sonucu.png` ile doğrula.

---

## Faz 12: Kalıcı Kayıt, Envanter ve Kozmetik Mağaza
- **Amacı:** Oyuncunun topladığı altınları kalıcı cüzdanda biriktirmesini ve bu altınlarla civciv için şapka/renk kozmetikleri satın alıp kuşanabilmesini sağlamak.
- **İstenilenler:**
  - [ ] **Görev 12.1: JSON Kayıt Yöneticisi (`SaveManager`)**
    - Toplam cüzdan altını, rekor skor, açılan kozmetikler ve başarımları JSON formatında kalıcı olarak kaydeden ve yükleyen sistemi kur.
  - [ ] **Görev 12.2: Kozmetik Eşyalar ve Mağaza (`CosmeticItemSO` & `CosmeticShopManager`)**
    - Fiyat, aksesuar modeli ve renk bilgisi tutan kozmetik eşyaları tanımla; satın alma ve karakter üzerinde kuşandırma işlemlerini yönet.
  - [ ] **Görev 12.3: Faz 12 Gerçek Sahne (`Demo.unity`) Kurulumu ve Görsel Doğrulama**
    - `NightAgentMenu` ve `NightAgentBridge` (`VERIFY:12`) üzerinden `CosmeticShopManager` ve örnek kozmetik verisini `Demo.unity` sahnesine kur, sahneyi kaydet ve `TestScreenshots/faz12_sonucu.png` ile doğrula.

---

## Faz 13: Başarım ve Bildirim Sistemi
- **Amacı:** Oyuncunun altın toplama, kombo yapma ve civciv kurtarma gibi kilometre taşlarını takip ederek ödüllendirmek ve ekranda başarım bildirimi göstermek.
- **İstenilenler:**
  - [ ] **Görev 13.1: Başarım Yöneticisi ve Pop-Up Arayüzü (`AchievementSO`, `AchievementManager` & `AchievementPopupUI`)**
    - Oyun içi olayları dinleyerek hedef değere ulaşıldığında başarım kilidini açan, altın ödülü veren ve ekranda animasyonlu bildirim penceresi gösteren sistemi oluştur.
  - [ ] **Görev 13.2: Faz 13 Gerçek Sahne (`Demo.unity`) Kurulumu ve Görsel Doğrulama**
    - `NightAgentMenu` ve `NightAgentBridge` (`VERIFY:13`) üzerinden `AchievementManager` ve `AchievementPopupUI` panelini `Demo.unity` sahnesine kur, sahneyi kaydet ve `TestScreenshots/faz13_sonucu.png` ile doğrula.

---

## Faz 14: Nesne Havuzu ve Görsel Efektler (Object Pooling & VFX)
- **Amacı:** Performanslı nesne üretimi için havuz sistemi kurmak ve karakterin koşma, zıplama, dash hareketlerini toz, tüy ve iz (trail) efektleriyle görsel olarak zenginleştirmek.
- **İstenilenler:**
  - [ ] **Görev 14.1: Nesne Havuzu ve Karakter Efektleri (`ObjectPoolManager` & `PlayerVFXController`)**
    - Efekt ve sık üretilen nesneler için yeniden kullanılabilir nesne havuzu oluştur.
    - Karakterin hareket durumuna göre ayak tozu, zıplama tüyü ve dash izi efektlerini otomatik tetikleyen kontrolcüyü ekle.
  - [ ] **Görev 14.2: Faz 14 Gerçek Sahne (`Demo.unity`) Kurulumu ve Görsel Doğrulama**
    - `NightAgentMenu` ve `NightAgentBridge` (`VERIFY:14`) üzerinden `ObjectPoolManager` ve `PlayerVFXController` bileşenlerini `Demo.unity` sahnesine kur, sahneyi kaydet ve `TestScreenshots/faz14_sonucu.png` ile doğrula.

---

## Faz 15: Kamera Hissiyatı, Sarsıntı ve Dinamik FOV
- **Amacı:** Hızlanma, darbe ve hasar anlarında kameraya dinamik bakış açısı (FOV) değişimi ve sarsıntı (screen shake) ekleyerek vuruş/hız hissini güçlendirmek.
- **İstenilenler:**
  - [ ] **Görev 15.1: Kamera Efekt Kontrolcüsü (`CameraJuiceController`)**
    - Dash ve kayma sırasında kamera FOV açısını yumuşakça genişleten, yere çakılma ve hasar anlarında sönümlü kamera sarsıntısı uygulayan bileşeni oluştur.
  - [ ] **Görev 15.2: Faz 15 Gerçek Sahne (`Demo.unity`) Kurulumu ve Görsel Doğrulama**
    - `NightAgentMenu` ve `NightAgentBridge` (`VERIFY:15`) üzerinden `CameraJuiceController` bileşenini `Demo.unity` sahnesine kur, sahneyi kaydet ve `TestScreenshots/faz15_sonucu.png` ile doğrula.

---

## Faz 16: 3D Ses Mimarisi ve Yüzey Adım Sesleri
- **Amacı:** Karakterin yürüme, zıplama, ötme ve etkileşim seslerini çeşitlendirmek ve 3D uzaysal ses/pitch varyasyonu ile doğal bir işitsel atmosfer kurmak.
- **İstenilenler:**
  - [ ] **Görev 16.1: Ses Kütüphanesi ve Adım Sesi Tetikleyicisi (`AudioLibrarySO`, `SpatialAudioManager` & `FootstepAudioTrigger`)**
    - Ses kliplerini kategorize eden veri yapısını, rastgele perde (pitch) değişimiyle 3D ses çalan yöneticiyi ve karakter hareket ettikçe adım/ötme sesi üreten bileşeni oluştur.
  - [ ] **Görev 16.2: Faz 16 Gerçek Sahne (`Demo.unity`) Kurulumu ve Görsel Doğrulama**
    - `NightAgentMenu` ve `NightAgentBridge` (`VERIFY:16`) üzerinden ses kütüphanesi, 3D ses yöneticisi ve adım sesi tetikleyicisini `Demo.unity` sahnesine kur, sahneyi kaydet ve `TestScreenshots/faz16_sonucu.png` ile doğrula.

---

## Faz 17: Dinamik Gece/Gündüz Döngüsü ve Atmosfer
- **Amacı:** Oyun dünyasında zamanın akmasını sağlayarak güneşin açısını, ışık rengini ve ortam aydınlatmasını gündüzden geceye dinamik olarak değiştirmek.
- **İstenilenler:**
  - [ ] **Görev 17.1: Gece/Gündüz Işık Kontrolcüsü (`DayNightCycleController`)**
    - Güneş ışığının dönüşünü, yoğunluğunu ve ortam rengini zamana göre güncelleyen, gece/gündüz geçişlerinde olay tetikleyen çevre kontrolcüsünü oluştur.
  - [ ] **Görev 17.2: Faz 17 Gerçek Sahne (`Demo.unity`) Kurulumu ve Görsel Doğrulama**
    - `NightAgentMenu` ve `NightAgentBridge` (`VERIFY:17`) üzerinden `DayNightCycleController` bileşenini `Demo.unity` sahnesindeki `Directional Light` ile bağla, sahneyi kaydet ve `TestScreenshots/faz17_sonucu.png` ile doğrula.

---

## Faz 18: Hedef İşaretleyici ve Pusula Arayüzü (Waypoint HUD)
- **Amacı:** Oyuncunun haritadaki en yakın kayıp civcivi veya hedef noktayı kolayca bulabilmesi için ekran üzerinde mesafe gösteren dinamik bir hedef işaretleyici sunmak.
- **İstenilenler:**
  - [ ] **Görev 18.1: 3D Hedef ve Mesafe İşaretleyicisi (`WaypointIndicatorUI`)**
    - En yakın kurtarılmamış civcivi veya hedefi bulan, 3D konumunu ekran koordinatına çevirerek kenarlara sabitleyen ve aradaki mesafeyi metre cinsinden yazan HUD bileşenini oluştur.
  - [ ] **Görev 18.2: Faz 18 Gerçek Sahne (`Demo.unity`) Kurulumu ve Görsel Doğrulama**
    - `NightAgentMenu` ve `NightAgentBridge` (`VERIFY:18`) üzerinden `WaypointIndicatorUI` bileşenini `Demo.unity` içindeki `Canvas` altına kur, sahneyi kaydet ve `TestScreenshots/faz18_sonucu.png` ile doğrula.

---

## Faz 19: Seviye İlerleme ve Yıldız Puanlama Sistemi
- **Amacı:** Bölümleri toplanan altın ve kalan süreye göre 1-3 yıldız üzerinden değerlendirmek, kazanılan yıldızları kaydetmek ve sonraki seviyeye geçişi yönetmek.
- **İstenilenler:**
  - [ ] **Görev 19.1: Seviye Verisi ve İlerleme Yöneticisi (`LevelDataSO` & `LevelProgressionManager`)**
    - Bölüm hedeflerini ve yıldız eşiklerini tutan veri yapısını, bölüm sonunda yıldız hesabını yapıp kaydeden ve sıradaki bölümü yükleyen yöneticiyi oluştur.
  - [ ] **Görev 19.2: Faz 19 Gerçek Sahne (`Demo.unity`) Kurulumu ve Görsel Doğrulama**
    - `NightAgentMenu` ve `NightAgentBridge` (`VERIFY:19`) üzerinden `DefaultLevelData.asset` ve `LevelProgressionManager` bileşenini `Demo.unity` sahnesine kur, sahneyi kaydet ve `TestScreenshots/faz19_sonucu.png` ile doğrula.

---

## Faz 20: Bütünsel Entegrasyon, Tek Tıkla Kurulum ve Son Doğrulama
- **Amacı:** Tüm fazlardaki sistemlerin tek tıkla sahneye kurulup geri çekilebilmesini, eksik yöneticilerin çalışma zamanında otomatik tamamlanmasını ve tüm oyunun uçtan uca doğrulanmasını sağlamak.
- **İstenilenler:**
  - [ ] **Görev 20.1: Toplu Faz Yönetimi ve Çalışma Zamanı Doğrulayıcı (`NightAgentMenu` & `RuntimeSystemValidator`)**
    - `NightAgentMenu` içerisine tüm fazları tek seferde uygulayan ve geri çeken toplu seçenekleri ekle.
    - Oyun başladığında kritik yöneticiler sahnede eksikse otomatik oluşturan güvenlik doğrulayıcısını kur.
  - [ ] **Görev 20.2: Faz 20 Gerçek Sahne (`Demo.unity`) Bütünsel Doğrulaması ve Final Raporu**
    - Tüm sistemlerin `Demo.unity` sahnesinde eksiksiz kurulu olduğunu `NightAgentBridge` (`VERIFY:20`) ile doğrula, `TestScreenshots/faz20_sonucu.png` ekran görüntüsünü incele ve mimari dokümantasyonu tamamla.
