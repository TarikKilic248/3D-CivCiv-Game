# Unity Otonom Gece Vardiyası Kuralları

## Bağlam ve Arama Kuralları
- Klasör taraması (`Get-ChildItem`, `tree` vb.) YAPMA; proje yapısı ve mevcut sınıflar için sadece `ARCHITECTURE.md` dosyasını referans al.
- `.unity`, `.prefab` ve binary asset dosyalarını OKUMA ve doğrudan metin düzenlemesiyle müdahale etme.
- Derleme ve log kontrollerinde çıktının tamamını basma, sadece hata satırlarını filtrele: `dotnet build Assembly-CSharp.csproj | Select-String "error"`

## Görev Akışı ve Kota Sınırı (Kritik)
- Her vardiyada `TASKS.md` içinden **en fazla 9 görev** tamamla ve oturumu sonlandır.
- Tamamladığın her görevi (ve tüm maddeleri biten faz başlıklarını) `TASKS.md` dosyasından tamamen sil.
- Tamamladığın görevi ve değişen dosya adlarını `NIGHT_REPORT.md` dosyasına 1 madde olarak ekle; yeni eklediğin scripti `ARCHITECTURE.md` dosyasına 1 satır olarak işle.

## Unity ve C# Standartları
- Unity 6 (`6000.6.8f1`) C# standartlarına ve SOLID prensiplerine uy; yeni sistemleri temiz C# scriptleri ve `ScriptableObject` yapılarıyla kur.

## İki Aşamalı Derleme Testi ve Geri Sarma (Rollback) Kilidi
1. **Hızlı Sözdizimi Kontrolü (Her Görevde):** C# kodu yazdıktan sonra önce `dotnet build Assembly-CSharp.csproj` (ve Editor scripti varsa `dotnet build Assembly-CSharp-Editor.csproj`) çalıştır.
2. **Sıfır Hata ve Rollback Kuralı:** Hata çıkarsa en fazla **2 kez** düzeltmeyi dene. **2 denemede çözülmezse** `git checkout -- .` ve `git clean -fd` komutlarıyla son çalışan temiz commit'e geri dön! Bozuk kodu asla bırakma; görevi `TASKS.md` içinde tut, `NIGHT_REPORT.md` dosyasına "Atlandı (Derleme Hatası)" notu düş ve sıradaki göreve geç.
3. **Temiz Commit:** Sadece **0 hata (0 errors)** alındığında `git add . && git commit -m "feat: <kısa özet>"` çalıştır.
4. **Tam Unity Doğrulaması (Vardiya Sonunda 1 Kez):** Vardiyadaki görevler tamamlandığında `.meta` dosyalarının üretimi ve sahne/referans kontrolü için en sonda 1 kez şu komutu çalıştır:
   `"C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Unity.exe" -batchmode -quit -projectPath . -logFile build.log`
   *(Not: Masaüstünde Unity Editör açıksa bu komutu çalıştırma.)*

## Çıktı Formatı
- Sohbet ekranına uzun açıklamalar veya kod blokları yazma; sadece araçları kullan ve 1 cümlelik durum bildirimi ver.