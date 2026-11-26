---
trigger: always_on
---
# AutoDev Token Tasarrufu ve Güvenli Geliştirme Kuralları

1. **Bağlam ve Token Disiplini**
   - Proje genelinde klasör taraması (`Get-ChildItem -Recurse`, `tree`) YAPMA; dosya ve API haritası için yalnızca `auto_dev/PROJECT_INDEX.md` ve `.autodev/scene_hierarchy.txt` özetlerini kullan.
   - `.unity`, `.prefab`, `.asset`, `.mat`, `.csproj`, `.sln`, `.slnx` ve medya/ikili dosyaları metin (YAML) olarak OKUMA veya DEĞİŞTİRME.
   - Uzun derleme/editör loglarının tamamını okuma; yalnızca orkestratörün verdiği filtrelenmiş hata satırlarına odaklan.

2. **Git Güvenliği (Orkestratör Sorumluluğu)**
   - Otonom görev yürütürken `git commit`, `git checkout`, `git branch`, `git reset` veya `git push` çalıştırma; tüm dal, merge ve geri alma işlemleri `auto_dev` orkestratörü tarafından deterministik olarak yönetilir.
   - `main` dalına doğrudan geliştirme yapılmaz; tüm otonom işler `auto-dev` ve geçici `task/<id>` dallarında yürütülür.

3. **Unity 6 Geliştirme ve Davranışsal Doğrulama**
   - Çalışma zamanı scriptlerini `Assets/_GameAssets/Scripts/` altına, mevcut projeyle uyumlu (global namespace) biçimde yaz.
   - Unity `6000.6` API uyumu için `FindObjectsByType<T>(FindObjectsInactive.Include)` kullan (`FindObjectsSortMode` parametresini KULLANMA; `CS0618` uyarısı üretir).
   - Gereksiz Editör menüsü (`Uygula`/`Geri çek`/`Detay`) şablon kodu üretme; geri alma işlemi Git üzerinden yapılır.
   - Sahne veya `ScriptableObject` bağlantıları gerekiyorsa yalnızca C# Unity Editor API (`SerializedObject`, `AssetDatabase`, `Undo`) kullan.
   - Her görev için `Assets/_GameAssets/Scripts/Editor/AutoDev/Checks/Check_<ID>.cs` altında `[AutoDevCheck("<ID>")]` ile işaretli kısa bir davranışsal doğrulama yaz. Sadece `GetComponent != null` kontrolü yeterli değildir; mekanik gerçekten tetiklenip (`Awake`/`FixedUpdate`/`Physics.Simulate`/event) sayısal veya durum bazlı doğrulanmalıdır.
