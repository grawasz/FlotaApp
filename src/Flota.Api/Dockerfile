# Obraz Flota.Api. Kontekst budowania: katalog główny repozytorium.
#   docker build -f src/Flota.Api/Dockerfile -t flota-api .

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

USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DataProtection__KeysPath=/app/keys
EXPOSE 8080
ENTRYPOINT ["dotnet", "Flota.Api.dll"]
