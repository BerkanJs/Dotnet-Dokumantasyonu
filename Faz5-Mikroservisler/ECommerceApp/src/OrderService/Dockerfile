FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Shared proje önce — OrderService buna bağımlı
COPY Shared/Contracts/Contracts.csproj Shared/Contracts/
COPY OrderService/OrderService.csproj  OrderService/
RUN dotnet restore OrderService/OrderService.csproj

COPY Shared/ Shared/
COPY OrderService/ OrderService/
RUN dotnet publish OrderService/OrderService.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrderService.dll"]
