# Unity Otonom Gece Vardiyası Kuralları (Play Mode Test & Görsel Doğrulama)

## Bağlam ve Sahne Güvenliği (Kritik)
- Klasör taraması (`Get-ChildItem`, `tree`) YAPMA; sadece `ARCHITECTURE.md` dosyasını referans al.
- Mevcut `.unity` ve `.prefab` dosyalarını OKUMA, DEĞİŞTİRME ve ÜZERİNE KAYDETME. Tüm sahne kurulumları C# Editor/Test scriptleri üzerinden geçici sahnede yapılmalıdır.
- Derleme ve test loglarında çıktının tamamını basma; sadece hata veya test sonuç satırlarını filtrele.

## Görev Akışı ve Kota Sınırı
- Her vardiyada `TASKS.md` içinden **en fazla 9 görev** tamamla ve oturumu sonlandır.
- Tamamladığın görevi `TASKS.md` dosyasından sil; `NIGHT_REPORT.md` dosyasına 1 madde olarak ekle; yeni scripti `ARCHITECTURE.md` dosyasına 1 satır olarak işle.
- Ara adımlarda sadece `dotnet build Assembly-CSharp.csproj` çalıştır. Hata 2 denemede çözülmezse `git checkout -- . && git clean -fd` ile geri al, görevi atla ve `NIGHT_REPORT.md` dosyasına yaz.
- Sadece **0 hata** alındığında `git add . && git commit -m "feat: <kısa özet>"` çalıştır.

## Night Agent Editör Menü Standardı
- Tamamlanan her faz için `Assets/_GameAssets/Scripts/Editor/NightAgentMenu.cs` dosyasına aşağıdaki menü yapısını ekle:
  - `[MenuItem("Tools/Night Agent/Faz <No>: <Faz Başlığı>/Uygula")]` -> İlgili fazın bileşenlerini ve UI elemanlarını sahneye kurar/bağlar ve `EditorSceneManager.MarkSceneDirty` çağırır.
  - `[MenuItem("Tools/Night Agent/Faz <No>: <Faz Başlığı>/Geri çek")]` -> İlgili fazın sahneye eklediği bileşenleri (`DestroyImmediate`) ve oluşturduğu UI/sahne objelerini temizler.
  - `[MenuItem("Tools/Night Agent/Faz <No>: <Faz Başlığı>/Detay")]` -> `EditorUtility.DisplayDialog` ile bu fazda hangi scriptlerin eklendiğini, sahnede nelerin değiştiğini ve nasıl test edileceğini özetleyen Türkçe bir pencere açar.

## Faz Sonu Play Mode Test ve Görsel İnceleme Kuralı
- Her fazın en son görevinde `Assets/_GameAssets/Tests/PlayMode/` altında bir Unity Test Framework (`[UnityTest]`) scripti yaz.
- Test içinde `EditorSceneManager.NewScene` veya boş sahne kurulumuyla ilgili tüm bileşenleri (UI dahil) kodla oluştur, mekanikleri çalıştırarak `Assert` ile doğrula ve `ScreenCapture.CaptureScreenshot("TestScreenshots/faz_sonucu.png")` ile ekran görüntüsü al.
- Testi çalıştırmadan önce Unity Editör'ün açık olup olmadığını kontrol et ve şu komutu çalıştır:
  `if (Get-Process Unity -ErrorAction SilentlyContinue) { Write-Output "UNITY_ACIK_ATLANDI" } else { & "C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults TestResults.xml -logFile test.log }`
- Eğer çıktı `UNITY_ACIK_ATLANDI` dönerse testi zorlama; `NIGHT_REPORT.md` dosyasına "Unity açık olduğu için Play Mode testi gece vardiyasına bırakıldı" yaz ve sıradaki göreve geç.
- Test çalıştıysa `test.log` dosyasının tamamını OKUMA; sadece `TestResults.xml` dosyasını kontrol et. Başarısız olursa sadece `Select-String -Path test.log -Pattern "Exception|Error|Failed"` çıktısına bak.
- Test geçtikten sonra `TestScreenshots/faz_sonucu.png` görselini oku; UI hizalama hatası, eksik bar/panel veya görsel bozukluk varsa kodu düzeltip testi 1 kez daha tekrarla. Bulgularını `NIGHT_REPORT.md` dosyasına ekle.

## Çıktı Formatı
- Sohbet ekranına uzun açıklamalar yazma; sadece araçları kullan ve 1 cümlelik durum bildirimi ver.