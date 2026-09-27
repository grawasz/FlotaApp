<#
.SYNOPSIS
    Uruchamia symulator UniAVL i Flota.Api skierowane do symulatora (docs/SYMULATOR.md).
.PARAMETER WebApp
    Katalog opublikowanej aplikacji przeglądarkowej (wwwroot) udostępnianej przez Flota.Api.
#>
param(
    [string]$WebApp
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$work = Join-Path ([IO.Path]::GetTempPath()) 'flota-symulacja'
New-Item -ItemType Directory -Force $work | Out-Null

$sim = Start-Process dotnet -PassThru -NoNewWindow -ArgumentList @(
    'run', '--project', (Join-Path $root 'tools/Flota.UniAvlSim'), '--', '--urls', 'http://localhost:5390')

# Parametry wiersza poleceń mają pierwszeństwo przed User Secrets: symulator zamiast UniAVL,
# osobna baza i klucze, geokodowanie Targeo wyłączone (pusty adres usługi — bez zapytań zewnętrznych)
$apiArgs = @(
    '--urls', 'http://localhost:5294',
    '--environment', 'Development',
    '--UniAvl:BaseUrl=http://localhost:5390/AVL/',
    '--Targeo:Endpoint=',
    "--ConnectionStrings:Flota=Data Source=$(Join-Path $work 'flota.db')",
    "--DataProtection:KeysPath=$(Join-Path $work 'keys')"
)
if ($WebApp) { $apiArgs += "--WebApp:RootPath=$((Resolve-Path $WebApp).Path)" }

try {
    dotnet run --project (Join-Path $root 'src/Flota.Api') --no-launch-profile -- @apiArgs
}
finally {
    Stop-Process -Id $sim.Id -ErrorAction SilentlyContinue
}
