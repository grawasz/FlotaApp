#!/bin/sh
# Kopia zapasowa bazy Flota.Api (pg_dump, format własny PostgreSQL) do katalogu kopie/ obok docker-compose.yml.
# Użycie (katalog wdrożenia na serwerze): ./kopia-bazy.sh
set -eu
cd "$(dirname "$0")"
mkdir -p kopie
plik="kopie/flota-$(date +%Y%m%d-%H%M%S).dump"
docker compose exec -T db pg_dump -U flota -Fc flota > "$plik"
test -s "$plik"
echo "Kopia: $plik ($(du -h "$plik" | cut -f1))"
