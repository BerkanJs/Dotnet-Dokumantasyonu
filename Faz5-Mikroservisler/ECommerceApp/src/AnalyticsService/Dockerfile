FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Shared/Contracts/Contracts.csproj       Shared/Contracts/
COPY AnalyticsService/AnalyticsService.csproj AnalyticsService/
RUN dotnet restore AnalyticsService/AnalyticsService.csproj

COPY Shared/ Shared/
COPY AnalyticsService/ AnalyticsService/
RUN dotnet publish AnalyticsService/AnalyticsService.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AnalyticsService.dll"]
