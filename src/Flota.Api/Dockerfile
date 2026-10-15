# Obraz Flota.Api z aplikacją przeglądarkową (Uno WebAssembly). Kontekst budowania: katalog główny repozytorium.
#   docker build -f src/Flota.Api/Dockerfile -t flota-api .

# Aplikacja przeglądarkowa: build Release z profilowaną kompilacją AOT (docs/WEB.md)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS web
# Obciążenie wasm-tools (natywne zasoby SkiaSharp, kompilacja AOT); emscripten wymaga python3
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 \
    && rm -rf /var/lib/apt/lists/* \
    && dotnet workload install wasm-tools
WORKDIR /src

COPY global.json Directory.Build.props Directory.Build.targets Directory.Packages.props ./
COPY src/Flota.Core/Flota.Core.csproj src/Flota.Core/
COPY src/Flota.Client/Flota.Client.csproj src/Flota.Client/
COPY src/Flota.Presentation/Flota.Presentation.csproj src/Flota.Presentation/
COPY src/Flota.App/Flota.App.csproj src/Flota.App/
# Przywracanie w konfiguracji Release (w Debug Uno.Sdk dołącza pakiet DevServer, niedozwolony w buildzie Release)
RUN dotnet restore src/Flota.App/Flota.App.csproj -p:FlotaWebOnly=true -p:Configuration=Release

COPY src/Flota.Core/ src/Flota.Core/
COPY src/Flota.Client/ src/Flota.Client/
COPY src/Flota.Presentation/ src/Flota.Presentation/
COPY src/Flota.App/ src/Flota.App/
RUN dotnet publish src/Flota.App/Flota.App.csproj -f net10.0-browserwasm -c Release -o /web --no-restore -p:FlotaWebOnly=true

# Flota.Api
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Przywracanie pakietów w osobnej warstwie (pliki projektów i konfiguracja centralna)
COPY global.json Directory.Build.props Directory.Build.targets Directory.Packages.props ./
COPY src/Flota.Core/Flota.Core.csproj src/Flota.Core/
COPY src/Flota.Api.Infrastructure/Flota.Api.Infrastructure.csproj src/Flota.Api.Infrastructure/
COPY src/Flota.Api/Flota.Api.csproj src/Flota.Api/
RUN dotnet restore src/Flota.Api/Flota.Api.csproj

COPY src/Flota.Core/ src/Flota.Core/
COPY src/Flota.Api.Infrastructure/ src/Flota.Api.Infrastructure/
COPY src/Flota.Api/ src/Flota.Api/
RUN dotnet publish src/Flota.Api/Flota.Api.csproj -c Release -o /app --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Katalogi danych zapisywalne dla użytkownika bez uprawnień administratora:
# App_Data — baza SQLite (dostawca domyślny), keys — klucze Data Protection
RUN mkdir -p /app/App_Data /app/keys && chown -R $APP_UID /app/App_Data /app/keys

COPY --from=build /app .
COPY --from=web /web/wwwroot /app/wwwroot

USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DataProtection__KeysPath=/app/keys \
    WebApp__RootPath=/app/wwwroot
EXPOSE 8080
ENTRYPOINT ["dotnet", "Flota.Api.dll"]
