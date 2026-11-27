# 3D CivCiv Game

**Unity 6 (`6000.6.8f1`, URP)** ile geliştirilmiş 3 boyutlu platform, aksiyon ve kurtarma oyunu.  
Altınları topla, engelleri ve devriye düşmanları aş, kayıp civcivleri kurtar ve süre dolmadan en yüksek yıldız puanıyla bölümü tamamla!

<p align="center">
  <img src="docs/start_menu.png" alt="3D CivCiv Game Başlangıç Menüsü" width="48%" />
  <img src="docs/gameplay_hud.png" alt="3D CivCiv Game Oyun İçi Görünüm" width="48%" />
</p>

---

## Hemen İndir ve Oyna (Unity Kurulumu Gerektirmez)

1. Sağdaki **[Releases](../../releases)** sayfasından **`3D-CivCiv-Game-Win64.zip`** dosyasını indir.
2. Zip dosyasını bir klasöre çıkart.
3. **`3D CivCiv Game.exe`** dosyasını çift tıklayarak hemen oyna.

---

## Özellikler

- **Hareket & Yetenekler:** Coyote Time & Jump Buffering destekli zıplama, havada **çift zıplama**, boşluk tuşunu basılı tutarak **kanatla süzülme (Glide)**, stamina kullanan **Dash**, eğimli zeminde **kayma (Slide)** ve havadan **yere çakılma (Ground Pound)**.
- **Dövüş & Etkileşim:** Yakın mesafe **Gaga Saldırısı**, parabolik zıpzıp **Yumurta Fırlatma** (cephane yenilenmeli), kırılabilir sandıklar ve ağırlıklı ganimet tablosu (`LootTableSO`), devriye/kovalama düşman yapay zekası (`PatrolEnemyAI`), basınç plakalı bulmacalar.
- **Parkur & Kurtarma Görevi:** Hareketli platformlar (`MovingPlatform`), düşüp yenilenen platformlar (`FallingPlatform`), süzülen oyuncuyu yükselten rüzgar akımları (`WindCurrentZone`), oyuncuyu takip eden **kayıp civcivler** ve mesafe pusulası (`WaypointIndicatorUI`).
- **Sistemler & Arayüz:** TPS/FPS kamera geçişi (`V`), Dash FOV genişlemesi & darbe sarsıntısı, dinamik gece/gündüz döngüsü, başarım sistemi, JSON kalıcı kayıt (`SaveManager`), 1–3 yıldız bölüm puanlama ve sıcak altın-kehribar temalı arayüz (`UIThemeSO`).

---

## Kontroller

| Tuş / Giriş | Eylem |
| --- | --- |
| `W A S D` / Ok Tuşları | Hareket |
| `Fare` | Kamera bakışı |
| `Space` | Zıpla / Havada Çift Zıpla |
| `Space` (Havada basılı tut) | Kanatlarla Süzül (Glide) |
| `Left Shift` | Hızlı Atılma (Dash) |
| `Left Ctrl` / `C` | Yerde Kayma / Havada Yere Çakılma (Ground Pound) |
| `F` | Gaga Saldırısı (Peck) |
| `Q` / `Sağ Tık` | Zıpzıp Yumurta Fırlatma |
| `E` | Etkileşim (Civciv kurtarma, basınç plakası) |
| `V` | TPS / FPS Kamera Geçişi |
| `ESC` | Ayarlar / Duraklat / İmleci Serbest Bırak |

---

## Unity Editor İle Açmak İsteyenler İçin

- **Unity Sürümü:** `Unity 6000.6.8f1` (Universal Render Pipeline)
- **Ana Sahne:** `Assets/Synty/PolygonStarter/Scenes/Demo.unity`

```bash
git clone https://github.com/TarikKilic248/3D-CivCiv-Game.git
```
Projeyi Unity Hub üzerinden açtıktan sonra `Assets/Synty/PolygonStarter/Scenes/Demo.unity` sahnesini yükleyip **Play** tuşuna basmanız yeterlidir.

---

## Otonom Geliştirme Altyapısı (`auto_dev/`)

Bu projenin oynanış sistemleri (`T001`–`T027`), Unity Editör köprüsü (`AutoDevBridge.cs`, `AutoDevVerifier.cs`) ve Git dal güvenliği ile birlikte [`auto_dev/`](auto_dev/README.md) orkestratörü üzerinden adım adım davranışsal testlerle (`Assets/_GameAssets/Scripts/Editor/AutoDev/Checks/`) doğrulanarak geliştirilmiştir. Oturum bazlı çalışma raporları [auto_dev/MORNING_BRIEF.md](auto_dev/MORNING_BRIEF.md) içinde yer alır.
