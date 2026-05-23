FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Shared/Contracts/Contracts.csproj     Shared/Contracts/
COPY ShipmentService/ShipmentService.csproj ShipmentService/
RUN dotnet restore ShipmentService/ShipmentService.csproj

COPY Shared/ Shared/
COPY ShipmentService/ ShipmentService/
RUN dotnet publish ShipmentService/ShipmentService.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ShipmentService.dll"]
