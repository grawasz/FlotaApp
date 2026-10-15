#!/bin/sh
# Odtworzenie bazy Flota.Api z kopii (kopia-bazy.sh). Flota.Api zatrzymywana na czas odtwarzania.
# Użycie (katalog wdrożenia na serwerze): ./odtworz-baze.sh kopie/flota-RRRRMMDD-GGMMSS.dump
set -eu
cd "$(dirname "$0")"
plik="${1:?Podaj plik kopii, np. kopie/flota-20261015-120000.dump}"
test -s "$plik"
docker compose stop flota-api
docker compose exec -T db pg_restore -U flota -d flota --clean --if-exists --no-owner < "$plik"
docker compose start flota-api
echo "Odtworzono z: $plik"
