# Gün 79 — Production Diagnostics: dotnet CLI Araçları

Production'da bir sorun var. Debugger bağlayamazsın, kodu değiştiremezsin, servis durdurulamaz. Ne yaparsın?

.NET bu senaryo için beş hafif CLI aracı sunar — çalışan process'e bağlanır, düşük overhead ile veri toplar.

---

## Araç Seçim Kılavuzu — Önce Bunu Oku

```
Belirti                    → Araç
─────────────────────────────────────────
CPU yüksek                 → dotnet-trace
Bellek sürekli artıyor     → dotnet-gcdump → dotnet-dump
Thread pool dolu / yavaş   → dotnet-counters
Crash / exception sonrası  → dotnet-dump (post-mortem)
Genel durum kontrolü       → dotnet-counters (başlangıç noktası)
```

---

## Kurulum

```bash
dotnet tool install --global dotnet-counters
dotnet tool install --global dotnet-trace
dotnet tool install --global dotnet-gcdump
dotnet tool install --global dotnet-dump

# Çalışan process'leri listele
dotnet-counters ps
# PID  Process
# 1234 KitabeviApi
```

---

## dotnet-counters — Canlı Metrik İzleme

**Ne işe yarar?** Çalışan uygulamanın CPU, GC, thread pool gibi runtime metriklerini anlık gösterir. İlk baktığın yer burası olmalı.

```bash
dotnet-counters monitor --process-id 1234 --counters System.Runtime
```

```
[System.Runtime]
    CPU Usage (%)                          87    ← yüksek, neden?
    GC Heap Size (MB)                     340    ← artıyor mu?
    Gen 0 GC Count (count / 1 sec)         12    ← sık GC → allocation fazla
    Gen 2 GC Count (count / 1 sec)          3    ← > 0 ise ciddi sorun
    Allocation Rate (B / 1 sec)     5,200,000    ← saniyede 5 MB — yüksek
    ThreadPool Thread Count                48    ← normal mi? max ne?
    ThreadPool Queue Length               120    ← kuyrukta bekleyen iş var
    Exception Count (count / 1 sec)         8    ← exception fırlatılıyor
```

- `Gen 2 GC Count > 0` → uzun yaşayan nesneler var, leak şüphesi → dotnet-gcdump'a geç
- `ThreadPool Queue Length` yüksek → thread'ler bloklanmış, muhtemelen sync-over-async
- `Exception Count` yüksek → fırlatılan exception'lar var, logları kontrol et

---

## dotnet-trace — CPU Profil

**Ne işe yarar?** CPU'yu en çok hangi metodun tükettiğini gösterir. "Nerede yavaş?" sorusunun cevabı.

```bash
# 30 saniye profil topla
dotnet-trace collect --process-id 1234 --duration 00:00:30
# → trace.nettrace dosyası oluşur
```

```bash
# SpeedScope formatına çevir — tarayıcıda aç
dotnet-trace convert trace.nettrace --format Speedscope
# → trace.nettrace.speedscope.json
# speedscope.app adresine yükle veya VS Code eklentisi ile aç
```

**SpeedScope'ta ne bakarsın?**
- "Left Heavy" görünümü → en uzun süren call stack'leri gösterir
- En tepedeki metodlar en fazla CPU tüketen yerler
- "KitapAramaHandler.Handle" → %60 CPU → buraya odaklan

---

## dotnet-gcdump — Hafif Heap Snapshot

**Ne işe yarar?** Full memory dump almak yerine sadece GC heap'teki nesne sayılarını ve referans zincirlerini alır. Boyutu küçük, etkisi az.

```bash
dotnet-gcdump collect --process-id 1234
# → 20240115_143022_1234.gcdump dosyası oluşur
# Dikkat: Gen2 GC tetikler — production'da kısa pause olabilir
```

```bash
# Visual Studio ile analiz — File → Open → .gcdump
# veya dotnet-gcdump report ile özet
dotnet-gcdump report 20240115_143022_1234.gcdump
```

**Çıktı özeti:**
```
Type                          Count    Size
─────────────────────────────────────────
String                        48,231   12.3 MB   ← çok fazla string?
KitapDto                      15,000    4.1 MB   ← 15k DTO neden bellekte?
byte[]                         3,102    8.7 MB
```

`KitapDto` sayısı her snapshot'ta artıyorsa → leak var → dotnet-dump ile derinleş.

---

## dotnet-dump — Full Memory Dump

**Ne işe yarar?** Process'in o anki tam bellek görüntüsünü alır. Sonra `dotnet-dump analyze` ile offline incelenir.

```bash
# Dump al — process durmaz ama kısa pause olur
dotnet-dump collect --process-id 1234
# → core_20240115_143022 dosyası oluşur (büyük olabilir, GB düzeyinde)

# Analiz — dump alınan makinede değil, geliştirici makinesinde yapılabilir
dotnet-dump analyze core_20240115_143022
```

**SOS komutları:**

```
> dumpheap -stat
# Heap'teki tüm nesne türlerini boyuta göre sıralar
MT       Count    TotalSize   Class Name
...
7f3a...  15,000   4,800,000   KitabeviApi.KitapDto   ← 15k nesne, neden?

> dumpheap -type KitapDto
# Tüm KitapDto örneklerinin adreslerini listeler
Address     MT        Size
0x7f3b1234  7f3a...   320

> gcroot 0x7f3b1234
# Bu nesneyi canlı tutan referans zincirini göster
→ static SomeCache._items → List<KitapDto> → KitapDto
# Bulundu: static cache içinde tutuluyormuş
```

---

## Linux Container'da Diagnostics

Kubernetes'te pod içine araç kurulumu:

```bash
# Pod'a bağlan
kubectl exec -it kitabevi-pod-xyz -- sh

# Araçları kur (global tool path'i ayarla)
export DOTNET_ROOT=/usr/share/dotnet
export PATH=$PATH:~/.dotnet/tools

dotnet tool install --global dotnet-gcdump
dotnet tool install --global dotnet-counters

# PID bul ve izle
dotnet-counters ps
dotnet-counters monitor --process-id 1
```

**Ephemeral container** (pod'u yeniden başlatmadan):
```bash
kubectl debug -it kitabevi-pod-xyz --image=mcr.microsoft.com/dotnet/sdk:8.0 --target=app
```

---

## Özet — Hangi Araç Ne Zaman?

| Araç | Ne Gösterir | Ne Zaman |
|---|---|---|
| `dotnet-counters` | CPU, GC, thread pool anlık | Her zaman ilk bak |
| `dotnet-trace` | Hangi metot CPU yiyor | CPU yüksek ama neden bilmiyorsun |
| `dotnet-gcdump` | Heap'te kaç nesne var | Bellek artıyor, hafif analiz |
| `dotnet-dump` | Tam heap, referans zinciri | Leak kesin, kök referansı bul |

---

## Kontrol Soruları

1. `dotnet-counters`'da `ThreadPool Queue Length` yüksek çıktığında muhtemel sebep nedir?
2. `dotnet-gcdump` ile `dotnet-dump` arasındaki fark nedir, hangisini önce denersin?
3. `gcroot <address>` komutu ne gösterir?
4. Linux container'da diagnostics aracı kurarken neden `DOTNET_ROOT` ayarlamak gerekebilir?
