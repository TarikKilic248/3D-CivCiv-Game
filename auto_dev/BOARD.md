# AutoDev Gorev Panosu (BOARD)

- Ozet: **Toplam 20** | Taslak (Onay Bekleyen): `16` | Sirada: `3` (Calismaya Hazir: `1`) | Tamamlanan: `0` | Bloklanan: `1`

## Onay Bekleyen Taslaklar (`draft`)
| ID | Baslik | Bagimlilik | Model | Gorsel | Durum / Not |
| --- | --- | --- | --- | --- | --- |
| `T005` | Gelişmiş Altın Geri Bildirimi ve Güçlendirici Sistemi (CoinCollect & PowerUpCollectable) | T001, T002, T003 | `fast` | - | `approve` bekliyor |
| `T006` | Kontrol Noktası ve Tehlike/Düşme Alanları (Checkpoint & HazardZone) | T001, T002 | `fast` | - | `approve` bekliyor |
| `T007` | Çift Zıplama ve Kanatla Süzülme (ChickGlideController) | T001 | `fast` | - | `approve` bekliyor |
| `T008` | Hasar Arayüzü ve Oyuncu Can Sistemi (IDamageable & PlayerHealth) | T001, T002 | `fast` | - | `approve` bekliyor |
| `T009` | Kalp ve Can Göstergesi HUD Bileşeni (HealthUI) | T008 | `fast` | Evet | `approve` bekliyor |
| `T010` | Kombo Çarpanı (ComboManager & ComboUI) ve Altın Mıknatısı (CoinMagnet) | T002, T005, T008 | `fast` | Evet | `approve` bekliyor |
| `T011` | Dinamik Parkur Öğeleri (MovingPlatform, FallingPlatform & WindCurrentZone) | T007 | `fast` | - | `approve` bekliyor |
| `T012` | CivCiv Gaga Saldırısı ve Havadan Yere Çakılma (ChickPeckAttack) | T001, T008 | `fast` | - | `approve` bekliyor |
| `T013` | Kırılabilir Sandık ve Ganimet Sistemi (LootTableSO & BreakableCrate) | T005, T012 | `fast` | - | `approve` bekliyor |
| `T014` | Devriye Düşman Yapay Zekası (EnemyStatsSO & PatrolEnemyAI) | T008, T012 | `fast` | - | `approve` bekliyor |
| `T015` | Etkileşim Sistemi ve Basınç Plakası Bulmacası (IInteractable, PlayerInteractor & PressurePlateSwitch) | T001 | `fast` | - | `approve` bekliyor |
| `T016` | Kayıp CivCiv Kurtarma Görevi ve Hedef Pusulası (LostChickNPC, ChickRescueManager & WaypointIndicatorUI) | T015 | `fast` | Evet | `approve` bekliyor |
| `T017` | JSON Kalıcı Kayıt ve Yıldız Puanlama Sistemi (SaveManager, LevelDataSO & LevelProgressionManager) | T002, T016 | `fast` | - | `approve` bekliyor |
| `T018` | Nesne Havuzu ve Karakter Hareket Efektleri (ObjectPoolManager & PlayerVFXController) | T003, T007 | `fast` | - | `approve` bekliyor |
| `T019` | Kamera Hissiyatı ve 3D Ses Mimarisi (CameraJuiceController & SpatialAudioManager) | T003, T008 | `fast` | - | `approve` bekliyor |
| `T020` | Dinamik Gece/Gündüz Döngüsü, Başarım Sistemi ve Bütünlük Doğrulayıcı | T017 | `smart` | Evet | `approve` bekliyor |

## Siradaki Gorevler (`pending`)
| ID | Baslik | Bagimlilik | Model | Gorsel | Durum / Not |
| --- | --- | --- | --- | --- | --- |
| `T002` | Merkezi Oyun Döngüsü (GameState, GameManager, ClockUI & HighScore) | - | `fast` | - | Hazir |
| `T003` | Stamina Tabanlı İleri Atılma Yeteneği (DashController) | T001 | `fast` | - | Bagimlilik bekliyor |
| `T004` | Oyun İçi Enerji Çubuğu ve Oyun Sonu Ekranı (GameplayHUD) | T002, T003 | `fast` | Evet | Bagimlilik bekliyor |

## Bloklanan Gorevler (`blocked`)
| ID | Baslik | Bagimlilik | Model | Gorsel | Durum / Not |
| --- | --- | --- | --- | --- | --- |
| `T001` | Veri Mimarisi (PlayerStatsSO) ve PlayerController Temel Düzeltmeleri | - | `smart` | - | Deneme: 2 — agent_exec: Ajan oturumu basarisiz (kod 1). |

## Tamamlanan Gorevler (`done`)
_Yok._
