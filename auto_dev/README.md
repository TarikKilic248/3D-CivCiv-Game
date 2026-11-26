# AutoDev — Düşük Token Maliyetli Otonom Geliştirme Sistemi

`auto_dev/`, projeden bağımsız **taşınabilir Python çekirdeği** (`auto_dev/core/`) ile projeye özel **adaptör katmanını** (`auto_dev/adapters/`) ayıran, Git güvenli ve düşük token maliyetli bir otonom geliştirme orkestratörüdür.

## Neden Token Maliyeti Düşük?
1. **0-Token Deterministik Orkestrasyon:** Git dallanması, görev kuyruğu, bağımlılık (DAG) takibi, kapsam/diff koruması, kompakt kod indeksleme (`PROJECT_INDEX.md`), derleme/test doğrulaması ve raporlama tamamen Python ile çalışır — **hiç LLM tokenı harcamaz**.
2. **Görev Başına Taze Oturum:** Her atomik görev temiz bir `agy -p` oturumunda çalışır; sohbet geçmişi şişmez.
3. **Kademeli Model Yönlendirme:**
   - Planlama (`plan`): Güçlü model (`gemini-4-neon-medium`)
   - Rutin atomik görevler (1. deneme): Hızlı/ucuz model (`gemini-3.8-flash-medium`)
   - Yalnızca 1. deneme hata alırsa (2. deneme): Sadece filtrelenmiş hata satırlarıyla güçlü modele yükseltme (`gemini-4-neon-medium`)
4. **Koşullu Görsel Kontrol:** Ekran görüntüsü ve görsel model yalnızca `visual_check: true` olan UI/sahne görevlerinde devreye girer.

## Günlük Kullanım Akışı

```powershell
# 1. Durumu ve hazır görevleri gör (0 token)
python auto_dev/cli.py status

# 2. Kod indeksini ve panoyu güncelle (0 token)
python auto_dev/cli.py index

# 3. Yeni bir hedefi küçük atomik taslak görevlere böl (1 akıllı model çağrısı)
python auto_dev/cli.py plan "Karaktere stamina tabanli ileri atilma (Dash) yetenegi ekle"

# 4. auto_dev/BOARD.md ve auto_dev/tasks.json dosyalarını incele, onaylayıp sıraya al
python auto_dev/cli.py approve --all
# veya tek tek: python auto_dev/cli.py approve T001 T002

# 5. Çalışacak görevleri önceden gör (dry-run, 0 token)
python auto_dev/cli.py run --dry-run

# 6. Otonom geliştirme koşusunu başlat (varsayılan max 3 görev)
python auto_dev/cli.py run --max-tasks 3

# 7. Unity köprü / derleme doğrulamasını elle test et (0 token)
python auto_dev/cli.py verify --check SMOKE

# 8. Bloklanan görevi tekrar sıraya al
python auto_dev/cli.py unblock T003
```

## Git Güvenliği
- `main` dalı korumalıdır; orkestratör `main` üzerinde çalışmayı reddeder.
- Her görev `auto-dev` dalından açılan geçici `task/<id>` dalında çalışır.
- Tüm kalite kapıları (diff koruması + derleme + davranışsal test + varsa görsel kontrol) geçerse `auto-dev` dalına tek temiz commit ile birleştirilir ve geçici dal silinir.
- 2 denemede başarısız olan görev tamamen geri alınır (`git reset --hard && git clean -fd`), geçici dal silinir ve görev `blocked` işaretlenir.
- Commitler lokalde `auto-dev` dalında birikir; `git push` ve `main`'e merge/PR kararı her zaman sendedir.

## Gece Vardiyası (Windows Task Scheduler)

```powershell
# Gece 02:30'da her gün max 3 görev çalışacak şekilde kur
powershell -ExecutionPolicy Bypass -File auto_dev\scripts\setup_scheduler.ps1 -Action Install -Time 02:30 -MaxTasks 3

# Durumu kontrol et
powershell -ExecutionPolicy Bypass -File auto_dev\scripts\setup_scheduler.ps1 -Action Status

# Kaldır
powershell -ExecutionPolicy Bypass -File auto_dev\scripts\setup_scheduler.ps1 -Action Remove
```

## Başka Bir Projeye Taşıma (Taşınabilirlik)
1. Yeni projeye yalnızca `auto_dev/` klasörünü (ve istersen `.agents/rules/autodev.md` dosyasını) kopyala.
2. Yeni projede `.gitignore` dosyasına `/.autodev/` ekle.
3. Genel şablon yapılandırmasını üret:
   ```powershell
   python auto_dev/cli.py init-generic --out auto_dev/config.json
   ```
4. `auto_dev/config.json` içindeki `compile_cmd`, `test_cmd` ve `allowed_write_prefixes` alanlarını yeni projene göre düzenle (`pytest`, `npm test`, `dotnet test`, `go test` vb.) ve `auto-dev` dalında kullanmaya başla!
