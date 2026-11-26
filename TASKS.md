# 3D CivCiv Game - Faz ve Görev Yol Haritası

## Standart Faz Yapısı
Her faz aşağıdaki standart akışla tamamlanır:
1. **Amacı:** Fazın oyuna kattığı temel değer ve çözdüğü problem.
2. **İstenilenler (Görevler):** Kodlanacak veya entegre edilecek sistemlerin genel işlevsel tanımı.
3. **Faz Sonu Doğrulama (Menü & Test):**
   - `Tools/Night Agent/Faz <No>: <Faz Başlığı>/` altına **Uygula**, **Geri çek** ve **Detay** menülerinin eklenmesi.
   - Geçici sahnede mekanikleri ve UI görünümünü doğrulayan Play Mode testi (`Phase<No>Tests.cs`) ve ekran görüntüsü (`TestScreenshots/faz<No>_sonucu.png`) alınması.

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
  - [ ] **Görev 2.3: Faz 2 Night Agent Menüsü ve Play Mode Testi**
    - Faz 2 için `Uygula`, `Geri çek`, `Detay` editör menülerini ve oyun döngüsünü doğrulayan Play Mode testini oluştur.

---

## Faz 3: Toplanabilir Öğeler, Güçlendiriciler ve Kontrol Noktaları
- **Amacı:** Seviye içi etkileşimi zenginleştirmek için altınlara görsel/işitsel efekt kazandırmak, karaktere geçici güçlendiriciler sunmak ve tehlike anında kontrol noktasında yeniden doğmasını sağlamak.
- **İstenilenler:**
  - [ ] **Görev 3.1: Gelişmiş Altın ve Güçlendirici Sistemi (`CoinCollect` & `PowerUpCollectable`)**
    - Altınlara dönme, süzülme, ses ve küçülerek yok olma animasyonu ekle.
    - Oyuncuya ekstra süre, hız artışı ve enerji (stamina) yenilemesi sağlayan toplanabilir güçlendirici nesneler oluştur.
  - [ ] **Görev 3.2: Kontrol Noktası ve Tehlike Alanları (`Checkpoint` & `HazardZone`)**
    - Oyuncunun geçtiği son güvenli noktayı kaydeden kontrol noktaları ve tehlikeli alanlara düşüldüğünde oyuncuyu bu noktaya ışınlayan tehlike bölgeleri oluştur.
  - [ ] **Görev 3.3: Faz 3 Night Agent Menüsü ve Play Mode Testi**
    - Faz 3 için `Uygula`, `Geri çek`, `Detay` editör menülerini ve toplanabilir/checkpoint mekaniklerini doğrulayan Play Mode testini oluştur.

---

## Faz 4: Oyun İçi Arayüz (HUD) ve Oyun Sonu Ekranı
- **Amacı:** Oyuncuya anlık enerji (stamina) durumunu ve oyun bittiğinde kazanma/kaybetme istatistiklerini ekranda görsel olarak sunmak.
- **İstenilenler:**
  - [ ] **Görev 4.1: Enerji Çubuğu ve Oyun Sonu Paneli (`GameplayHUD`)**
    - Karakterin stamina değişimini anlık gösteren enerji barını ve oyun bittiğinde skor/rekor bilgisiyle birlikte yeniden başlatma butonu sunan oyun sonu ekranını oluştur.
  - [ ] **Görev 4.2: Faz 4 Night Agent Menüsü ve Play Mode Testi**
    - Faz 4 için `Uygula`, `Geri çek`, `Detay` editör menülerini ve HUD elemanlarının görünümünü doğrulayan Play Mode testini oluştur.

---

## Faz 5: Gelişmiş CivCiv Hareket ve Saldırı Yetenekleri
- **Amacı:** 3D platform oynanışını derinleştirmek için civciv karakterine havada çift zıplama, kanat çırparak süzülme, yakın mesafe gaga vuruşu ve havadan yere sert iniş yetenekleri kazandırmak.
- **İstenilenler:**
  - [ ] **Görev 5.1: Çift Zıplama ve Süzülme (`ChickGlideController`)**
    - Karakterin havadayken ikinci kez zıplayabilmesini ve zıplama tuşuna basılı tutulduğunda düşüş hızını yavaşlatarak süzülmesini sağla.
  - [ ] **Görev 5.2: Gaga Saldırısı ve Yere Çakılma (`ChickPeckAttack`)**
    - Karakterin önündeki hedeflere gaga vuruşu yapabilmesini ve havadayken hızla yere çakılarak çevresine darbe vurabilmesini sağla.
  - [ ] **Görev 5.3: Faz 5 Night Agent Menüsü ve Play Mode Testi**
    - Faz 5 için `Uygula`, `Geri çek`, `Detay` editör menülerini ve hareket/saldırı yeteneklerini doğrulayan Play Mode testini oluştur.

---

## Faz 6: Can, Hasar ve Savunma Sistemi
- **Amacı:** Oyuncunun ve dünyadaki hedeflerin hasar alabilmesini, oyuncunun can durumunun kalp ikonlarıyla takip edilmesini ve hasar sonrası geçici dokunulmazlık kazanmasını sağlamak.
- **İstenilenler:**
  - [ ] **Görev 6.1: Hasar Arayüzü ve Oyuncu Can Sistemi (`IDamageable` & `PlayerHealth`)**
    - Hasar alabilen tüm nesneler için ortak bir yapı kur; oyuncuya can, iyileşme, geri savrulma (knockback) ve hasar sonrası yanıp sönmeli dokunulmazlık süresi ekle.
  - [ ] **Görev 6.2: Kalp ve Can Göstergesi (`HealthUI`)**
    - Oyuncunun mevcut ve maksimum canını HUD üzerinde kalp ikonları ve metin olarak göster.
  - [ ] **Görev 6.3: Faz 6 Night Agent Menüsü ve Play Mode Testi**
    - Faz 6 için `Uygula`, `Geri çek`, `Detay` editör menülerini ve can/hasar döngüsünü doğrulayan Play Mode testini oluştur.

---

## Faz 7: Kombo Çarpanı ve Altın Mıknatısı
- **Amacı:** Hızlı ve kesintisiz altın toplamayı ödüllendiren bir kombo/çarpan sistemi kurmak ve yakınlardaki altınları karaktere doğru çeken mıknatıs özelliği eklemek.
- **İstenilenler:**
  - [ ] **Görev 7.1: Kombo Yöneticisi ve Arayüzü (`ComboManager` & `ComboUI`)**
    - Kısa süre içinde ardışık altın toplandığında kombo serisini ve skor çarpanını artıran, süre dolduğunda veya hasar alındığında sıfırlayan sistemi ve HUD göstergesini oluştur.
  - [ ] **Görev 7.2: Altın Mıknatısı (`CoinMagnet`)**
    - Karakterin belirli bir yarıçap içindeki altınları otomatik olarak kendine doğru çekmesini ve güçlendirici ile çekim alanının büyütülmesini sağla.
  - [ ] **Görev 7.3: Faz 7 Night Agent Menüsü ve Play Mode Testi**
    - Faz 7 için `Uygula`, `Geri çek`, `Detay` editör menülerini ve kombo/mıknatıs mekaniklerini doğrulayan Play Mode testini oluştur.

---

## Faz 8: Dinamik Platformlar ve Fiziksel Seviye Öğeleri
- **Amacı:** Haritada hareketli parkurlar oluşturmak için noktalar arası gezinen platformlar, basıldığında çöken bloklar ve süzülmeyi destekleyen rüzgar akımları eklemek.
- **İstenilenler:**
  - [ ] **Görev 8.1: Hareketli ve Çöken Platformlar (`MovingPlatform` & `FallingPlatform`)**
    - Belirlenen noktalar arasında hareket ederken üzerindeki oyuncuyu kaydırmadan taşıyan platformlar ve üzerine çıkıldığında titreyip kısa süre sonra çöken/yeniden oluşan platformlar oluştur.
  - [ ] **Görev 8.2: Rüzgar Akımı Bölgesi (`WindCurrentZone`)**
    - İçine giren oyuncuyu yukarı/belirli yöne iten ve kanatlarla süzülürken ekstra kaldırma kuvveti uygulayan hava akımı alanları oluştur.
  - [ ] **Görev 8.3: Faz 8 Night Agent Menüsü ve Play Mode Testi**
    - Faz 8 için `Uygula`, `Geri çek`, `Detay` editör menülerini ve platform/rüzgar fiziğini doğrulayan Play Mode testini oluştur.

---

## Faz 9: Kırılabilir Nesneler ve Ganimet (Loot) Sistemi
- **Amacı:** Oyuncunun gaga vuruşu, dash veya yere çakılma hamleleriyle kırabileceği sandıklar/yumurtalar ve içlerinden çıkan rastgele ödül sistemini kurmak.
- **İstenilenler:**
  - [ ] **Görev 9.1: Ganimet Tablosu ve Kırılabilir Sandıklar (`LootTableSO` & `BreakableCrate`)**
    - Ağırlıklı olasılıklara göre altın veya güçlendirici düşüren veri tablosu oluştur.
    - Saldırı veya dash ile vurulduğunda sarsılıp kırılan ve içinden ganimet/altın saçan kırılabilir sandık ve sürpriz yumurtalar ekle.
  - [ ] **Görev 9.2: Faz 9 Night Agent Menüsü ve Play Mode Testi**
    - Faz 9 için `Uygula`, `Geri çek`, `Detay` editör menülerini ve sandık kırma/ganimet düşürme mekaniğini doğrulayan Play Mode testini oluştur.

---

## Faz 10: Düşman Yapay Zekası ve Tehditler
- **Amacı:** Seviyelerde devriye gezen, oyuncuyu gördüğünde takip eden ve üstüne zıplandığında veya gagalandığında etkisiz hale getirilebilen düşmanlar oluşturmak.
- **İstenilenler:**
  - [ ] **Görev 10.1: Düşman Verisi ve Devriye Yapay Zekası (`EnemyStatsSO` & `PatrolEnemyAI`)**
    - Düşman hız, algılama mesafesi ve hasar verilerini tanımla.
    - Noktalar arası devriye gezen, oyuncu menzile girince kovalayan, yandan çarptığında hasar veren ancak üstüne zıplandığında hasar alıp oyuncuyu zıplatan düşman yapay zekasını kodla.
  - [ ] **Görev 10.2: Faz 10 Night Agent Menüsü ve Play Mode Testi**
    - Faz 10 için `Uygula`, `Geri çek`, `Detay` editör menülerini ve düşman takip/hasar davranışlarını doğrulayan Play Mode testini oluştur.

---

## Faz 11: Etkileşimli Çevre, Bulmacalar ve Kayıp CivCiv Görevi
- **Amacı:** Oyuncunun çevredeki nesnelerle etkileşime geçmesini, basınç plakalarıyla kapı/köprü açmasını ve haritadaki kayıp yavru civcivleri kurtarma görevini tamamlamasını sağlamak.
- **İstenilenler:**
  - [ ] **Görev 11.1: Etkileşim Sistemi ve Basınç Plakası (`IInteractable`, `PlayerInteractor` & `PressurePlateSwitch`)**
    - Yakındaki etkileşimli nesneleri algılayıp tuşla tetikleyen sistemi ve üzerine çıkıldığında bağlı kapı veya köprüyü açan basınç plakalarını oluştur.
  - [ ] **Görev 11.2: Kayıp CivCiv Kurtarma Sistemi (`LostChickNPC` & `ChickRescueManager`)**
    - Etkileşime girildiğinde kurtarılan, oyuncunun peşinden kuyruk halinde gelen kayıp civcivleri ve bölümdeki kurtarma ilerlemesini takip eden yöneticiyi oluştur.
  - [ ] **Görev 11.3: Faz 11 Night Agent Menüsü ve Play Mode Testi**
    - Faz 11 için `Uygula`, `Geri çek`, `Detay` editör menülerini ve bulmaca/kurtarma görevini doğrulayan Play Mode testini oluştur.

---

## Faz 12: Kalıcı Kayıt, Envanter ve Kozmetik Mağaza
- **Amacı:** Oyuncunun topladığı altınları kalıcı cüzdanda biriktirmesini ve bu altınlarla civciv için şapka/renk kozmetikleri satın alıp kuşanabilmesini sağlamak.
- **İstenilenler:**
  - [ ] **Görev 12.1: JSON Kayıt Yöneticisi (`SaveManager`)**
    - Toplam cüzdan altını, rekor skor, açılan kozmetikler ve başarımları JSON formatında kalıcı olarak kaydeden ve yükleyen sistemi kur.
  - [ ] **Görev 12.2: Kozmetik Eşyalar ve Mağaza (`CosmeticItemSO` & `CosmeticShopManager`)**
    - Fiyat, aksesuar modeli ve renk bilgisi tutan kozmetik eşyaları tanımla; satın alma ve karakter üzerinde kuşandırma işlemlerini yönet.
  - [ ] **Görev 12.3: Faz 12 Night Agent Menüsü ve Play Mode Testi**
    - Faz 12 için `Uygula`, `Geri çek`, `Detay` editör menülerini ve kayıt/mağaza işlemlerini doğrulayan Play Mode testini oluştur.

---

## Faz 13: Başarım ve Bildirim Sistemi
- **Amacı:** Oyuncunun altın toplama, kombo yapma ve civciv kurtarma gibi kilometre taşlarını takip ederek ödüllendirmek ve ekranda başarım bildirimi göstermek.
- **İstenilenler:**
  - [ ] **Görev 13.1: Başarım Yöneticisi ve Pop-Up Arayüzü (`AchievementSO`, `AchievementManager` & `AchievementPopupUI`)**
    - Oyun içi olayları dinleyerek hedef değere ulaşıldığında başarım kilidini açan, altın ödülü veren ve ekranda animasyonlu bildirim penceresi gösteren sistemi oluştur.
  - [ ] **Görev 13.2: Faz 13 Night Agent Menüsü ve Play Mode Testi**
    - Faz 13 için `Uygula`, `Geri çek`, `Detay` editör menülerini ve başarım kazanma/bildirim akışını doğrulayan Play Mode testini oluştur.

---

## Faz 14: Nesne Havuzu ve Görsel Efektler (Object Pooling & VFX)
- **Amacı:** Performanslı nesne üretimi için havuz sistemi kurmak ve karakterin koşma, zıplama, dash hareketlerini toz, tüy ve iz (trail) efektleriyle görsel olarak zenginleştirmek.
- **İstenilenler:**
  - [ ] **Görev 14.1: Nesne Havuzu ve Karakter Efektleri (`ObjectPoolManager` & `PlayerVFXController`)**
    - Efekt ve sık üretilen nesneler için yeniden kullanılabilir nesne havuzu oluştur.
    - Karakterin hareket durumuna göre ayak tozu, zıplama tüyü ve dash izi efektlerini otomatik tetikleyen kontrolcüyü ekle.
  - [ ] **Görev 14.2: Faz 14 Night Agent Menüsü ve Play Mode Testi**
    - Faz 14 için `Uygula`, `Geri çek`, `Detay` editör menülerini ve havuz/VFX çalışmasını doğrulayan Play Mode testini oluştur.

---

## Faz 15: Kamera Hissiyatı, Sarsıntı ve Dinamik FOV
- **Amacı:** Hızlanma, darbe ve hasar anlarında kameraya dinamik bakış açısı (FOV) değişimi ve sarsıntı (screen shake) ekleyerek vuruş/hız hissini güçlendirmek.
- **İstenilenler:**
  - [ ] **Görev 15.1: Kamera Efekt Kontrolcüsü (`CameraJuiceController`)**
    - Dash ve kayma sırasında kamera FOV açısını yumuşakça genişleten, yere çakılma ve hasar anlarında sönümlü kamera sarsıntısı uygulayan bileşeni oluştur.
  - [ ] **Görev 15.2: Faz 15 Night Agent Menüsü ve Play Mode Testi**
    - Faz 15 için `Uygula`, `Geri çek`, `Detay` editör menülerini ve kamera efektlerini doğrulayan Play Mode testini oluştur.

---

## Faz 16: 3D Ses Mimarisi ve Yüzey Adım Sesleri
- **Amacı:** Karakterin yürüme, zıplama, ötme ve etkileşim seslerini çeşitlendirmek ve 3D uzaysal ses/pitch varyasyonu ile doğal bir işitsel atmosfer kurmak.
- **İstenilenler:**
  - [ ] **Görev 16.1: Ses Kütüphanesi ve Adım Sesi Tetikleyicisi (`AudioLibrarySO`, `SpatialAudioManager` & `FootstepAudioTrigger`)**
    - Ses kliplerini kategorize eden veri yapısını, rastgele perde (pitch) değişimiyle 3D ses çalan yöneticiyi ve karakter hareket ettikçe adım/ötme sesi üreten bileşeni oluştur.
  - [ ] **Görev 16.2: Faz 16 Night Agent Menüsü ve Play Mode Testi**
    - Faz 16 için `Uygula`, `Geri çek`, `Detay` editör menülerini ve ses tetikleyicilerini doğrulayan Play Mode testini oluştur.

---

## Faz 17: Dinamik Gece/Gündüz Döngüsü ve Atmosfer
- **Amacı:** Oyun dünyasında zamanın akmasını sağlayarak güneşin açısını, ışık rengini ve ortam aydınlatmasını gündüzden geceye dinamik olarak değiştirmek.
- **İstenilenler:**
  - [ ] **Görev 17.1: Gece/Gündüz Işık Kontrolcüsü (`DayNightCycleController`)**
    - Güneş ışığının dönüşünü, yoğunluğunu ve ortam rengini zamana göre güncelleyen, gece/gündüz geçişlerinde olay tetikleyen çevre kontrolcüsünü oluştur.
  - [ ] **Görev 17.2: Faz 17 Night Agent Menüsü ve Play Mode Testi**
    - Faz 17 için `Uygula`, `Geri çek`, `Detay` editör menülerini ve ışık/zaman döngüsünü doğrulayan Play Mode testini oluştur.

---

## Faz 18: Hedef İşaretleyici ve Pusula Arayüzü (Waypoint HUD)
- **Amacı:** Oyuncunun haritadaki en yakın kayıp civcivi veya hedef noktayı kolayca bulabilmesi için ekran üzerinde mesafe gösteren dinamik bir hedef işaretleyici sunmak.
- **İstenilenler:**
  - [ ] **Görev 18.1: 3D Hedef ve Mesafe İşaretleyicisi (`WaypointIndicatorUI`)**
    - En yakın kurtarılmamış civcivi veya hedefi bulan, 3D konumunu ekran koordinatına çevirerek kenarlara sabitleyen ve aradaki mesafeyi metre cinsinden yazan HUD bileşenini oluştur.
  - [ ] **Görev 18.2: Faz 18 Night Agent Menüsü ve Play Mode Testi**
    - Faz 18 için `Uygula`, `Geri çek`, `Detay` editör menülerini ve hedef işaretleyici hesaplamalarını doğrulayan Play Mode testini oluştur.

---

## Faz 19: Seviye İlerleme ve Yıldız Puanlama Sistemi
- **Amacı:** Bölümleri toplanan altın ve kalan süreye göre 1-3 yıldız üzerinden değerlendirmek, kazanılan yıldızları kaydetmek ve sonraki seviyeye geçişi yönetmek.
- **İstenilenler:**
  - [ ] **Görev 19.1: Seviye Verisi ve İlerleme Yöneticisi (`LevelDataSO` & `LevelProgressionManager`)**
    - Bölüm hedeflerini ve yıldız eşiklerini tutan veri yapısını, bölüm sonunda yıldız hesabını yapıp kaydeden ve sıradaki bölümü yükleyen yöneticiyi oluştur.
  - [ ] **Görev 19.2: Faz 19 Night Agent Menüsü ve Play Mode Testi**
    - Faz 19 için `Uygula`, `Geri çek`, `Detay` editör menülerini ve yıldız puanlama sistemini doğrulayan Play Mode testini oluştur.

---

## Faz 20: Bütünsel Entegrasyon, Tek Tıkla Kurulum ve Son Doğrulama
- **Amacı:** Tüm fazlardaki sistemlerin tek tıkla sahneye kurulup geri çekilebilmesini, eksik yöneticilerin çalışma zamanında otomatik tamamlanmasını ve tüm oyunun uçtan uca test edilmesini sağlamak.
- **İstenilenler:**
  - [ ] **Görev 20.1: Toplu Faz Yönetimi ve Çalışma Zamanı Doğrulayıcı (`NightAgentMenu` & `RuntimeSystemValidator`)**
    - `NightAgentMenu` içerisine tüm fazları tek seferde uygulayan ve geri çeken toplu seçenekleri ekle.
    - Oyun başladığında kritik yöneticiler sahnede eksikse otomatik oluşturan güvenlik doğrulayıcısını kur.
  - [ ] **Görev 20.2: Bütünsel Play Mode Testi ve Final Raporu (`Phase20FinalTests`)**
    - Tüm sistemlerin aynı sahnede hatasız çalıştığını doğrulayan kapsamlı Play Mode testini çalıştır, final ekran görüntüsünü incele ve mimari dokümantasyonu tamamla.
