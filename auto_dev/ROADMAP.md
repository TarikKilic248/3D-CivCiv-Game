# 3D CivCiv Game — Eksiklik Analizi ve 4 Aşamalı Geliştirme Yol Haritası

Bu belge mevcut kod tabanının (`auto-dev`, 11 çalışma zamanı scripti + `Demo.unity`) doğrudan incelenmesiyle çıkarılan gerçek eksiklikleri ve `auto_dev/tasks.json` üzerindeki 20 atomik görevin (`T001`–`T020`) ilerleme sırasını özetler.

## 1. Mevcut Kod Tabanında Tespit Edilen Eksiklikler ve Buglar

| Kategori | Mevcut Durum | Eksiklik / Risk | Çözüm Görevleri |
| --- | --- | --- | --- |
| **Oyun Döngüsü** | `ClockUI` 00:00'a iner, `CoinManager` sadece sayar | Kazanma (`Victory`), kaybetme (`GameOver`), yeniden başlatma akışı ve kalıcı HighScore yok | `T002`, `T004` |
| **Hareket Mimarisi & Buglar** | Parametreler `PlayerController` içine gömülü; `LimitSpeed`/`ApplyDrag` tüm yatay hızları 7 m/s'ye ezer | `SettingsUI.IsOpen` iken zıplama/slide girdisi sızıyor; harici yetenekler (Dash/Knockback/Boost) eziliyor; Coyote Time / Jump Buffer yok; `TargetJump` dikey hızı sıfırlamıyor | `T001` |
| **Platformer Yetenekleri** | Yürüme, tek zıplama, slide | Stamina'lı Dash, havada çift zıplama, kanatla süzülme (Glide) ve gaga/yere çakılma saldırısı yok | `T003`, `T007`, `T012` |
| **Risk, Can & Düşme** | Oyuncu ölemez, boşluğa düşerse sonsuza gider | `IDamageable`, `PlayerHealth`, i-frames, knockback, `HealthUI`, `Checkpoint` ve `HazardZone` yok | `T006`, `T008`, `T009` |
| **Toplanabilir & Ödül Döngüsü** | Altın temasta aniden `Destroy` olur | Dönme/süzülme/SFX geri bildirimi, güçlendiriciler, kombo çarpanı, altın mıknatısı ve kırılabilir sandıklar yok | `T005`, `T010`, `T013` |
| **Seviye, Düşman & Amaç** | Statik çevre + 29 altın | Hareketli/çöken platform, rüzgar akımı, devriye düşman AI, bulmaca/etkileşim ve kayıp civciv kurtarma + pusula yok | `T011`, `T014`, `T015`, `T016` |
| **İlerleme, Juice & Atmosfer** | Sadece müzik/ses toggle var | JSON kalıcı kayıt, 1-3 yıldız puanlama, nesne havuzu + VFX, kamera FOV/shake, 3D adım/ötme sesleri, gece/gündüz döngüsü ve başarım sistemi yok | `T017`, `T018`, `T019`, `T020` |

## 2. Aşama ve Bağımlılık Yapısı (DAG)

- **Aşama 1 — Çekirdek Mimari & Oynanabilir Döngü (`T001`–`T004`):** Önce `PlayerController` bugları, `PlayerStatsSO`, `GameManager`, `DashController` ve `GameplayHUD` kurulur; bu aşama bitince oyun baştan sona oynanıp kazanılabilen/kaybedilebilen sağlam bir çekirdeğe kavuşur.
- **Aşama 2 — Platformer Derinliği, Tehlike & Ödül (`T005`–`T010`):** Altın cilası, güçlendiriciler, checkpoint/düşme koruması, çift zıplama+süzülme, can/kalp HUD'ı, kombo çarpanı ve mıknatıs.
- **Aşama 3 — Dinamik Dünya, Savaş & Ana Görev (`T011`–`T016`):** Hareketli/çöken platformlar, rüzgar akımı, gaga saldırısı/ground pound, kırılabilir sandıklar, devriye düşmanlar, basınç plakası ve kayıp civciv kurtarma + ekran pusulası.
- **Aşama 4 — Kalıcı İlerleme, Game Feel & Atmosfer (`T017`–`T020`):** JSON kayıt & yıldız puanlama, havuzlu VFX, dinamik FOV/shake + 3D ses, gece/gündüz döngüsü, başarımlar ve açılış bütünlük doğrulayıcısı.

## 3. Token Tasarrufu Dağılımı
- **Akıllı model (`tier: smart`):** Yalnızca 2 görev (`T001` çekirdek hareket mimarisi ve `T020` bütünsel final entegrasyon).
- **Hızlı/ucuz model (`tier: fast`):** 18 görev (yalnızca 1. denemede doğrulamadan kalırsa 2. denemede akıllı modele yükselir).
- **Görsel doğrulama (`visual_check: true`):** Yalnızca 5 UI/görsel görev (`T004`, `T009`, `T010`, `T016`, `T020`); diğer 15 görevde görsel token maliyeti `0`.

## 4. Nasıl Başlanır?

```powershell
# 1) Aşama 1'i (T001-T004) onaylayıp sıraya al (hepsini açmak yerine aşama aşama gitmek en sağlıklısı!)
python auto_dev/cli.py approve T001 T002 T003 T004

# 2) İlk görevi tek tek canlı test et
python auto_dev/cli.py run --max-tasks 1

# 3) Sonucu Unity'de kontrol ettikten sonra sıradaki 3 görevi otomatik yürüt
python auto_dev/cli.py run --max-tasks 3
```
