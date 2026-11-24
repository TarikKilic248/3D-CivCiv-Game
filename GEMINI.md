# Unity Otonom Gece Vardiyası Kuralları

## Temel Görev Akışı
- Kök dizindeki `TASKS.md` dosyasını oku ve işaretlenmemiş (`[ ]`) görevleri sırayla uygula.
- Tamamladığın her görevi (ve tüm alt maddeleri tamamlanan faz başlıklarını) `TASKS.md` dosyasından tamamen sil; sadece sıradaki bekleyen görevler kalsın.
- Sildiğin ve tamamladığın her görevi, değişen dosya adlarıyla birlikte `NIGHT_REPORT.md` dosyasına madde olarak ekle.
- Her çalışan özellikten sonra anlamlı bir mesajla `git add .` ve `git commit -m "..."` at.

## Unity ve C# Kuralları
- Unity 6 (6000.x) C# standartlarına ve SOLID prensiplerine uy.
- `.unity` sahne veya `.prefab` dosyalarına doğrudan metin düzenlemesiyle müdahale etme; yeni sistemleri temiz C# scriptleri ve `ScriptableObject` yapılarıyla kur.

## Derleme Testi ve Kota Koruma Kilidi (Kritik)
- C# kodu yazdıktan veya değiştirdikten sonra mutlaka şu komutu çalıştırarak derleme kontrolü yap:
  `"C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Unity.exe" -batchmode -quit -projectPath . -logFile build.log`
- `build.log` dosyasını kontrol et. Aynı derleme hatasını en fazla **3 kez** çözmeyi dene. 3 denemede çözemezsen o görevi atla, `NIGHT_REPORT.md` dosyasına hatayı yaz ve sıradaki göreve geç. Sonsuz döngüye girme.

# Unity Otonom Gece Vardiyası Kuralları

## Temel Görev Akışı
- Kök dizindeki `TASKS.md` dosyasını oku ve işaretlenmemiş (`[ ]`) görevleri sırayla uygula.
- Tamamladığın her görevden sonra `TASKS.md` içindeki `[ ]` kutusunu `[x]` yap.
- Her çalışan özellikten sonra anlamlı bir mesajla `git add .` ve `git commit -m "..."` at.

## Unity ve C# Kuralları
- Unity 6 (6000.6.8f1) C# standartlarına ve SOLID prensiplerine uy.
- `.unity` sahne veya `.prefab` dosyalarına doğrudan metin düzenlemesiyle müdahale etme; yeni sistemleri temiz C# scriptleri ve `ScriptableObject` yapılarıyla kur.

## İki Aşamalı Derleme Testi ve Kota Koruma Kilidi (Kritik)
1. **Hızlı Sözdizimi Kontrolü (Öncelikli):** C# kodu yazdıktan veya düzenledikten sonra ilk olarak hızlı kontrol için şu komutu çalıştır:
   `dotnet build Assembly-CSharp.csproj`
   (Eğer projede Editor scripti varsa `dotnet build Assembly-CSharp-Editor.csproj` komutunu da çalıştır.)
2. **Tam Unity Doğrulaması (Görev Bitiminde):** Hızlı derleme hatasız geçtikten sonra, Unity'nin `.meta` dosyalarını üretmesi ve sahne/referans bütünlüğünü doğrulaması için görev sonunda şu komutu çalıştır:
   `"C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Unity.exe" -batchmode -quit -projectPath . -logFile build.log`
   *(Not: Eğer Unity Editör masaüstünde zaten açıksa bu komutu çalıştırma, `build.log` veya `Logs\Editor.log` dosyasını kontrol et.)*
3. **Sonsuz Döngü Koruması:** Aynı derleme hatasını en fazla **3 kez** çözmeyi dene. 3 denemede çözemezsen o görevi atla, `NIGHT_REPORT.md` dosyasına hatayı yaz ve sıradaki göreve geç.





# Otonom Geliştirme Kuralları (Token Tasarruf Modu)

## Bağlam ve Arama Kuralları
- Proje yapısını anlamak için klasör taraması (`Get-ChildItem`, `tree` vb.) YAPMA; sadece `ARCHITECTURE.md` dosyasını referans al.
- `.unity`, `.prefab` ve binary asset dosyalarını OKUMA.
- Komut çıktılarının tamamını ekrana basma; derleme ve log kontrollerinde sadece hata satırlarını filtrele (Örn: `dotnet build Assembly-CSharp.csproj | Select-String "error"`).

## Görev ve Kod Akışı
- `TASKS.md` içindeki `[ ]` görevlerini sırayla yap. Biteni `[x]` yap ve `ARCHITECTURE.md` dosyasına yeni eklediğin scriptin adını 1 satır olarak ekle.
- Unity 6 (6000.6.8f1) C# standartlarını ve SOLID prensiplerini kullan.
- Kod yazdıktan sonra sadece `dotnet build Assembly-CSharp.csproj` ile hızlı derleme testi yap. Unity Batchmode'u SADECE tüm görevler bittiğinde en sonda 1 kez çalıştır.
- Aynı hatayı en fazla 2 kez çözmeyi dene; çözemezsen atla ve `NIGHT_REPORT.md` dosyasına yaz.
- Her biten görevde `git add . && git commit -m "feat: <kısa özet>"` çalıştır.

## Çıktı Formatı
- Sohbet ekranına uzun açıklamalar veya kod blokları yazma; sadece dosya düzenleme araçlarını kullan ve 1 cümlelik durum bildirimi ver.