# Unity Otonom Geliştirme ve Gece Vardiyası Kuralları (Gerçek Sahne & Hibrit Köprü)

## 1. Bağlam, Token Tasarrufu ve Sahne Güvenliği (Kritik)
- Klasör taraması (`Get-ChildItem`, `tree`) YAPMA; kod ve sahne hiyerarşisi için sadece `ARCHITECTURE.md` ve `TestScreenshots/scene_hierarchy.txt` dosyalarını referans al.
- `.unity`, `.prefab` ve `.asset` dosyalarını metin (YAML) olarak OKUMA ve DEĞİŞTİRME (token israfını ve YAML bozulmasını önlemek için).
- Tüm sahne (`Assets/Synty/PolygonStarter/Scenes/Demo.unity`) ve prefab kurulumlarını **yalnızca C# Unity Editor API** (`EditorSceneManager`, `PrefabUtility`, `Undo`, `SerializedObject`) üzerinden `NightAgentMenu.cs` içinde yap ve sahneyi kaydet (`EditorSceneManager.SaveScene`).
- `.csproj` dosyalarına elle `<Reference>` veya paket referansı EKLEME. Unity'nin kendi derleyicisinde (`Assembly-CSharp.dll`) hata oluşmaması için tüm editör ve doğrulama kodlarını `Assets/_GameAssets/Scripts/Editor/` altında tut.
- Derleme ve test loglarının (`Editor.log`, `test.log`) tamamını OKUMA; sadece `TestScreenshots/night_agent_result.txt` özet çıktısını veya hata satırlarını filtrele.

## 2. Görev Akışı ve Vardiya Kotası
- Her gece vardiyasında `TASKS.md` içinden **en fazla 3 tam Faz** tamamla ve oturumu sonlandır.
- Bir faz yalnızca şu 4 adım eksiksiz tamamlandığında bitmiş sayılır:
  1. Fazın C# çalışma zamanı scriptlerinin (`Assets/_GameAssets/Scripts/...`) yazılması veya güncellenmesi.
  2. `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` içine `Uygula`, `Geri çek`, `Detay` menülerinin ve `NightAgentVerifier.cs` içine faz doğrulama kodunun eklenmesi.
  3. Fazın **gerçek sahneye (`Demo.unity`) uygulanması**, doğrulanması ve `TestScreenshots/faz<No>_sonucu.png` ekran görüntüsünün alınması.
  4. Tamamlanan fazın `TASKS.md` dosyasından silinmesi, `ARCHITECTURE.md` ve `NIGHT_REPORT.md` dosyalarına 1'er madde olarak işlenmesi ve **0 hata** ile `git add . ; git commit -m "feat: complete Phase <No> <kısa özet>"` atılması.
- Hata 2 denemede çözülmezse `git checkout -- . ; git clean -fd` ile geri al, fazı atla ve `NIGHT_REPORT.md` dosyasına yaz.

## 3. Night Agent Editör Menü Standardı (`NightAgentMenu.cs`)
- Tamamlanan her faz için `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` dosyasına aşağıdaki menü yapısını ekle:
  - `[MenuItem("Tools/Night Agent/Faz <No>: <Faz Başlığı>/Uygula")]` -> Aktif sahnede (`Demo.unity`) gerçek karakter (`PlayerController`), kamera ve `Canvas` objelerini bularak ilgili fazın bileşenlerini, `ScriptableObject` asset'lerini (`Assets/_GameAssets/Data/`) ve `Prefab` dosyalarını (`Assets/_GameAssets/Prefabs/`) kurar, referanslarını bağlar ve sahneyi kaydeder (`EditorSceneManager.SaveScene`).
  - `[MenuItem("Tools/Night Agent/Faz <No>: <Faz Başlığı>/Geri çek")]` -> İlgili fazın sahneye eklediği bileşenleri (`DestroyImmediate`) ve oluşturduğu UI/sahne objelerini temizleyip sahneyi kaydeder.
  - `[MenuItem("Tools/Night Agent/Faz <No>: <Faz Başlığı>/Detay")]` -> `EditorUtility.DisplayDialog` ile bu fazda hangi scriptlerin eklendiğini, `Demo.unity` sahnesinde nelerin değiştiğini ve nasıl test edileceğini özetleyen Türkçe bir pencere açar.

## 4. Hibrit Köprü ile Gerçek Sahne Uygulama ve Görsel Doğrulama (`NightAgentBridge` & `NightAgentVerifier`)
- Her fazın sonunda sahne kurulumunu ve doğrulamayı çalıştırmak için **Hibrit Köprü** komutunu kullan:
  1. `TestScreenshots/night_agent_cmd.txt` dosyasına `VERIFY:<FazNo>` yaz (ör. `VERIFY:1`).
  2. Unity Editör açık mı kontrol et:
     - **Unity Açık İse:** `NightAgentBridge` (`[InitializeOnLoad]`) komutu otomatik yakalar, scriptleri derler, `ApplyPhase<No>()` ile `Demo.unity` sahnesine uygular, `NightAgentVerifier.VerifyPhase(<No>)` çalıştırır, `TestScreenshots/faz<No>_sonucu.png` görüntüsünü alır ve sonucu `TestScreenshots/night_agent_result.txt` dosyasına yazar. (Unity arka plandaysa tetiklemek için PowerShell ile Unity penceresine kısa odak gönder veya `AssetDatabase.Refresh` tetiklenmesini birkaç saniye bekle).
     - **Unity Kapalı İse:** Şu komutu çalıştır:
       `& "C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Unity.exe" -batchmode -quit -projectPath . -executeMethod NightAgentBridge.RunFromCommandLine -logFile Logs/night_batch.log`
  3. `TestScreenshots/night_agent_result.txt` dosyasını oku (`PASS` döndüğünü doğrula) ve `TestScreenshots/faz<No>_sonucu.png` görselini incele. Sahne veya UI'da eksik/hata varsa düzeltip tekrar doğrula.

## 5. Çıktı Formatı
- Sohbet ekranına uzun açıklamalar yazma; sadece araçları kullan ve 1 cümlelik durum bildirimi ver.
