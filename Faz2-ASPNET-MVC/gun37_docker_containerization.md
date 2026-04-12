# Gün 37 — Docker ile Containerization

---

## 1. Neden Docker?

```
Problem: "Bende çalışıyor, sende çalışmıyor"
  Geliştirici A: .NET 8, SQL Server 2022, Windows
  Geliştirici B: .NET 9, LocalDB, macOS
  Sunucu:        .NET 8, SQL Server 2019, Linux
  → Her ortamda farklı davranış, farklı hata

Docker çözümü: Container
  Uygulama + .NET runtime + bağımlılıklar → tek paket (image)
  Bu image her makinede aynı şekilde çalışır
  Geliştirici A/B/Sunucu: aynı container → aynı davranış
```

```
Temel kavramlar:

Image      → şablon; "bu içerikte bir kutu istiyorum" tarifi
Container  → image'dan oluşturulan çalışan örnek
Dockerfile → image'ı nasıl oluşturacağını adım adım anlatan dosya
docker-compose → birden fazla container'ı (uygulama + DB) birlikte yöneten dosya
```

---

## 2. Dockerfile — Multi-Stage Build

Tek stage Dockerfile: SDK (~800MB) image'a dahil olur — production'da ihtiyaç yok.
Multi-stage: build aşamasında SDK kullanılır, son image'a sadece runtime (~200MB) kopyalanır.

```dockerfile
# Dockerfile (proje kök dizininde: KitabeviMVC/Dockerfile)

# ─── Stage 1: Build ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
# AS build: bu stage'e isim ver — sonraki stage buradan kopyalayacak
# sdk:8.0: derleme araçları dahil (~800MB) — sadece build için kullanılır

WORKDIR /src
# Sonraki komutlar /src dizininde çalışır
# bunu yazmasaydık: COPY ve RUN komutları container kök dizininde çalışırdı

# Önce sadece .csproj kopyala — bağımlılıkları restore et
COPY KitabeviMVC/KitabeviMVC.csproj KitabeviMVC/
RUN dotnet restore KitabeviMVC/KitabeviMVC.csproj
# .csproj önce kopyalanıp restore yapılmasının sebebi: Docker layer cache
# Eğer önce tüm kaynak kodu kopyalasaydık:
# → Her küçük .cs değişikliğinde restore tekrar çalışırdı (~1-2 dakika)
# → Sadece .csproj değişmezse restore adımı cache'den gelir (saniyeler)

# Şimdi tüm kaynak kodu kopyala
COPY KitabeviMVC/ KitabeviMVC/

# Uygulamayı yayınla (Release modunda)
RUN dotnet publish KitabeviMVC/KitabeviMVC.csproj \
    -c Release \
    -o /app/publish \
    --no-restore
# -c Release: optimize edilmiş, debug sembolleri yok
# -o /app/publish: çıktı dizini
# --no-restore: restore zaten yapıldı, tekrar yapma → hız kazanımı

# ─── Stage 2: Runtime ───────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
# aspnet:8.0: ASP.NET Core runtime (~200MB), SDK yok
# SDK'yı dahil etseydin: production image ~600MB daha büyük olurdu
# production image'ında build araçları güvenlik açığı da oluşturabilir

WORKDIR /app

# Build stage'inden sadece publish çıktısını al
COPY --from=build /app/publish .
# --from=build: "build" isimli stage'den kopyala
# Kaynak kodun tamamı, SDK, NuGet cache'i production image'ına girmez

# Uygulama güvenliği: root olmayan kullanıcıyla çalıştır
RUN adduser --disabled-password --gecos "" appuser
USER appuser
# root olarak çalıştırırsaydın: container ele geçirilirse host sistemine erişim riski artar

EXPOSE 8080
# Docker'a "bu container 8080 portunu kullanıyor" bilgisi
# EXPOSE yazmasan da uygulama çalışır, ama docker ps ve compose için meta bilgi

ENTRYPOINT ["dotnet", "KitabeviMVC.dll"]
# Container başladığında çalışacak komut
# CMD ile fark: ENTRYPOINT override edilemez (amaca kilitli), CMD override edilebilir
```

---

## 3. .dockerignore

Build context'e gönderilmeyecek dosyaları belirtir. Docker her `docker build` çağrısında tüm dizini build context olarak gönderir — büyükse yavaşlar.

```
# .dockerignore (Dockerfile yanında)

**/bin/
**/obj/
**/.vs/
**/*.user
Logs/
appsettings.Development.json
# appsettings.Development.json: geliştirme bağlantı dizesi container'a gitmesin
# production bağlantı dizesi environment variable ile verilir (aşağıda)

.git/
.gitignore
README.md
```

---

## 4. docker-compose — Uygulama + Veritabanı

Tek komutla hem SQL Server hem uygulama ayağa kalkar.

```yaml
# docker-compose.yml (solution kök dizininde)

version: "3.9"

services:

  # ─── SQL Server ────────────────────────────────────────────────────────────
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      SA_PASSWORD:         "Guclu_Sifre_123!"   # prod'da secret yönetimi kullan
      ACCEPT_EULA:         "Y"                   # lisans kabulü zorunlu
      MSSQL_PID:           "Express"             # Express: ücretsiz, 10GB limit
    ports:
      - "1433:1433"       # host:container — SSMS ile local bağlanmak için
    volumes:
      - sqlserver_data:/var/opt/mssql  # veri kalıcı olsun — container silinse de veriler durur
                                       # volumes yazmasaydık: container silinince DB tamamen kaybolur
    healthcheck:
      test: ["CMD", "/opt/mssql-tools/bin/sqlcmd", "-S", "localhost",
             "-U", "sa", "-P", "Guclu_Sifre_123!", "-Q", "SELECT 1"]
      interval: 10s
      retries:  5
      # healthcheck: uygulama service'i DB hazır olmadan başlamasın (depends_on condition ile)

  # ─── ASP.NET MVC Uygulama ──────────────────────────────────────────────────
  web:
    build:
      context:    .                        # build context: solution kök dizini
      dockerfile: KitabeviMVC/Dockerfile   # hangi Dockerfile kullanılacak
    ports:
      - "8080:8080"    # tarayıcıda localhost:8080 → container'ın 8080 portuna
    environment:
      ASPNETCORE_ENVIRONMENT: "Production"
      ConnectionStrings__Default: >-
        Server=sqlserver,1433;
        Database=KitabeviDb;
        User Id=sa;
        Password=Guclu_Sifre_123!;
        TrustServerCertificate=True
      # ConnectionStrings__Default: appsettings.json'daki ConnectionStrings:Default'u ezer
      # __ (çift alt tire): JSON hiyerarşisini environment variable'a çevirme kuralı
      # bağlantı dizesini kod içine gömseydik: her ortam için farklı image gerekir
    depends_on:
      sqlserver:
        condition: service_healthy   # SQL Server healthcheck geçene kadar web başlamaz
                                     # condition: service_started yazsaydık: DB henüz hazır değilken
                                     # web başlar → bağlantı hatası alırdı

volumes:
  sqlserver_data:   # named volume — docker tarafından yönetilen kalıcı depolama
```

---

## 5. Temel Docker Komutları

```bash
# Image oluştur
docker build -t kitabevi-mvc:1.0 -f KitabeviMVC/Dockerfile .
# -t: image adı ve tag (isim:versiyon)
# -f: Dockerfile yolu
# .: build context (hangi dizin gönderilecek)

# Container başlat (tek uygulama, DB ayrı)
docker run -d \
  -p 8080:8080 \
  -e "ConnectionStrings__Default=Server=host.docker.internal,1433;..." \
  --name kitabevi \
  kitabevi-mvc:1.0
# -d: detached (arka planda çalış)
# -p: port eşleştirme
# -e: environment variable
# host.docker.internal: container içinden host makinenin IP'si

# docker-compose ile tüm servisleri başlat
docker compose up -d
# -d: arka planda
# ilk çalışmada image build edilir, sonra her ikisi de başlar

# Logları izle
docker compose logs -f web
# -f: follow (canlı izleme) — Ctrl+C ile çık

# Durdurup sil
docker compose down
# down: container'ları durdur ve sil, volume'lar kalır

docker compose down -v
# -v: volume'ları da sil (DB verisi silinir — dikkat!)

# Çalışan container'a bağlan (debug için)
docker exec -it kitabevi bash
# -it: interactive terminal
# bash: hangi shell (alpine image'ında sh kullanılır)
```

---

## 6. Migration'ı Docker Ortamında Çalıştırma

Container başladığında migration otomatik uygulanabilir — ama Gün 32'den hatırlarsın: `Database.Migrate()` production'da race condition yaratır.

```yaml
# docker-compose.yml — migration için ayrı servis

  migrate:
    build:
      context:    .
      dockerfile: KitabeviMVC/Dockerfile
    environment:
      ConnectionStrings__Default: >-
        Server=sqlserver,1433;Database=KitabeviDb;
        User Id=sa;Password=Guclu_Sifre_123!;TrustServerCertificate=True
    command: ["dotnet", "ef", "database", "update", "--project", "KitabeviMVC.dll"]
    # command: Dockerfile'daki ENTRYPOINT'i geçersiz kılar
    # bu servis migration bundle veya ef CLI çalıştırır ve kapanır (one-shot)
    depends_on:
      sqlserver:
        condition: service_healthy
    # web servisi bu servisin bitmesini beklemeli — compose profiles ile yönetilir
```

```bash
# Pratikte en temiz yol: migration bundle (Gün 32)
# docker-compose'da web başlamadan önce bundle çalıştır:

docker compose run --rm migrate
# --rm: işi bittikten sonra container'ı sil
# migrate service'i bir kez çalışır, migration'ı uygular, kapanır
# sonra: docker compose up -d web
```

---

## 7. Production Kontrol Listesi

```
Image
  ✓ Multi-stage build: SDK production image'a girmiyor
  ✓ Non-root user: appuser ile çalışıyor
  ✓ .dockerignore: gereksiz dosyalar build context'ten çıkarıldı

Konfigürasyon
  ✓ Bağlantı dizesi environment variable — kod içinde yok
  ✓ appsettings.Development.json image'a girmiyor

Veritabanı
  ✓ Named volume: container silinse de veri kalıcı
  ✓ healthcheck: uygulama DB hazır olmadan başlamıyor
  ✓ Migration bundle: Database.Migrate() production'da yok

Güvenlik
  ✓ SA şifresi: gerçek prod'da Docker Secret veya env dosyasında
  ✓ TrustServerCertificate=True: geliştirme için, prod'da sertifika kur
```

---

## 8. Özet

```
Dockerfile
  Multi-stage: build (SDK) → runtime (aspnet) — image boyutu küçük, güvenli
  COPY .csproj + restore ÖNCE: layer cache → hız
  USER appuser: root olmayan kullanıcı

docker-compose
  sqlserver + web: tek komutla ayağa kalk
  depends_on + healthcheck: sıra garantisi
  volumes: veri kalıcılığı
  environment: bağlantı dizesi ve konfigürasyon dışarıdan

Migration
  docker compose run --rm migrate: tek seferlik migration container'ı
  Database.Migrate() production'da kullanma

Komutlar
  docker compose up -d     → başlat
  docker compose logs -f   → canlı log
  docker compose down      → durdur (veri kalır)
  docker compose down -v   → durdur + veriyi sil
```

---

## Sonraki Gün

Gün 38'de CI/CD: GitHub Actions ile otomatik build, test, Docker image push ve deployment pipeline.
