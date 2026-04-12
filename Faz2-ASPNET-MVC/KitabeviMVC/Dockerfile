# Gün 37: Multi-stage Dockerfile — build + runtime ayrı.
# Stage 1 (SDK ~800MB) sadece derleme için; production image'ına girmez.
# Stage 2 (aspnet ~200MB) sadece çalıştırma için — küçük, güvenli image.

# ─── Stage 1: Build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

# Önce sadece .csproj kopyala — layer cache için kritik.
# .csproj değişmezse restore adımı cache'den gelir (1-2 dk kazanım).
# Önce tüm kodu kopyalasaydık: her .cs değişikliğinde restore tekrar çalışırdı.
COPY KitabeviMVC.csproj ./
RUN dotnet restore KitabeviMVC.csproj

# Kaynak kodu kopyala
COPY . .

# Release modunda yayınla
RUN dotnet publish KitabeviMVC.csproj \
    -c Release \
    -o /app/publish \
    --no-restore
# --no-restore: restore zaten yapıldı, tekrar yapma → hız kazanımı
# -c Release: debug sembolleri yok, JIT optimize

# ─── Stage 2: Runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
# aspnet:9.0 = ASP.NET Core runtime (~200MB)
# sdk:9.0 yazsaydık: production image ~600MB daha büyük + gereksiz build araçları

WORKDIR /app

# Sadece publish çıktısını kopyala — kaynak kod, SDK, NuGet cache girmez
COPY --from=build /app/publish .
# --from=build: "build" isimli stage'den al

# Güvenlik: root olmayan kullanıcıyla çalıştır
RUN adduser --disabled-password --gecos "" appuser
USER appuser
# root kullanırsaydın: container ele geçirilirse host dosya sistemine erişim riski

EXPOSE 8080
# Docker metadata: "bu container 8080 kullanıyor"
# ASPNETCORE_URLS environment variable ile port ayarlanır (docker-compose'da)

ENTRYPOINT ["dotnet", "KitabeviMVC.dll"]
# Container başladığında çalışacak komut — override edilemez
