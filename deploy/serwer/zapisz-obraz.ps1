<#
.SYNOPSIS
    Build obrazu Flota.Api (z aplikacją przeglądarkową) i zapis do pliku .tar do przeniesienia na serwer (docker load).
.DESCRIPTION
    Tag obrazu: skrót bieżącego zatwierdzenia git (flota-api:<skrót>). Wynik: <Katalog>\flota-api-<skrót>.tar.
    Na serwerze: docker load -i flota-api-<skrót>.tar, następnie FLOTA_API_IMAGE=flota-api:<skrót> w pliku .env (docs/WDROZENIE.md).
.PARAMETER Katalog
    Katalog pliku wynikowego (domyślnie bieżący).
#>
param(
    [string]$Katalog = '.'
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if (git -C $root status --porcelain --untracked-files=no) {
    throw 'Niezatwierdzone zmiany w repozytorium — obraz musi odpowiadać zatwierdzeniu (tag z git).'
}
$tag = (git -C $root rev-parse --short HEAD).Trim()
$image = "flota-api:$tag"

docker build -f (Join-Path $root 'src/Flota.Api/Dockerfile') -t $image $root
if ($LASTEXITCODE -ne 0) { throw 'Build obrazu nie powiódł się.' }

New-Item -ItemType Directory -Force $Katalog | Out-Null
$file = Join-Path (Resolve-Path $Katalog) "flota-api-$tag.tar"
docker save $image -o $file
if ($LASTEXITCODE -ne 0) { throw 'Zapis obrazu nie powiódł się.' }

$hash = (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $(Split-Path $file -Leaf)" | Set-Content "$file.sha256" -Encoding ascii
"Obraz: $image"
"Plik:  $file ($([math]::Round((Get-Item $file).Length / 1MB)) MB)"
"SHA-256: $hash (sprawdzenie na serwerze: sha256sum -c $(Split-Path $file -Leaf).sha256)"
