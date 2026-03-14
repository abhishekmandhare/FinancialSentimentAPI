#!/usr/bin/env bash
# dev-local.sh — Copy TrueNAS DB to local Postgres and run the API locally
#
# Usage:
#   ./dev-local.sh          # dump remote DB, restore locally, build & run
#   ./dev-local.sh dump     # only dump + restore (skip build/run)
#   ./dev-local.sh run      # only build + run (skip dump, use existing local DB)

set -euo pipefail

HOST="truenas_admin@server.home"
APP_DIR="/mnt/immich-pool/apps/financial-sentiment-api"
DUMP_FILE="sentiment_dump.sql"

LOCAL_DB_HOST="localhost"
LOCAL_DB_PORT="5432"
LOCAL_DB_NAME="sentiment"
LOCAL_DB_USER="sentiment"
LOCAL_DB_PASS="localdev123"

# ── Dump remote DB and restore locally ────────────────────────────
dump_and_restore() {
  local remote_dump="/tmp/sentiment_dump.sql"

  echo "==> Dumping TrueNAS database (sudo will prompt for password)..."
  ssh -tt "$HOST" "cd $APP_DIR && sudo docker compose exec -T db pg_dump -U sentiment -d sentiment -Fc > $remote_dump && echo DUMP_OK"

  echo "==> Copying dump file..."
  scp "$HOST:$remote_dump" "$DUMP_FILE"
  ssh "$HOST" "rm -f $remote_dump"

  local size
  size=$(wc -c < "$DUMP_FILE")
  if [[ "$size" -lt 100 ]]; then
    echo "ERROR: Dump file is too small (${size} bytes) — something went wrong"
    rm -f "$DUMP_FILE"
    exit 1
  fi
  echo "    Dump complete: $(du -h "$DUMP_FILE" | cut -f1)"

  echo "==> Starting local Postgres..."
  docker compose up -d db
  echo "    Waiting for Postgres to be ready..."
  for i in $(seq 1 30); do
    if docker compose exec -T db pg_isready -U "$LOCAL_DB_USER" -d "$LOCAL_DB_NAME" &>/dev/null; then
      break
    fi
    sleep 1
  done

  echo "==> Dropping and recreating local database..."
  docker compose exec -T db psql -U "$LOCAL_DB_USER" -d postgres -c "
    SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$LOCAL_DB_NAME' AND pid <> pg_backend_pid();
  " &>/dev/null || true
  docker compose exec -T db dropdb -U "$LOCAL_DB_USER" --if-exists "$LOCAL_DB_NAME"
  docker compose exec -T db createdb -U "$LOCAL_DB_USER" "$LOCAL_DB_NAME"

  echo "==> Restoring dump into local database..."
  docker compose exec -T db pg_restore -U "$LOCAL_DB_USER" -d "$LOCAL_DB_NAME" --no-owner --no-acl < "$DUMP_FILE"

  local count
  count=$(docker compose exec -T db psql -U "$LOCAL_DB_USER" -d "$LOCAL_DB_NAME" -tAc "SELECT COUNT(*) FROM \"SentimentAnalyses\"" 2>/dev/null || echo "?")
  echo "    Restored. SentimentAnalyses rows: $count"

  rm -f "$DUMP_FILE"
  echo "==> Database sync complete."
}

# ── Build and run the API locally ─────────────────────────────────
build_and_run() {
  echo "==> Building API..."
  dotnet build API/API.csproj

  echo "==> Starting API (Development mode)..."
  echo "    http://localhost:5000"
  echo "    Press Ctrl+C to stop"
  echo ""

  ASPNETCORE_ENVIRONMENT=Development \
  ASPNETCORE_URLS="http://+:5000" \
  ConnectionStrings__DefaultConnection="Host=$LOCAL_DB_HOST;Port=$LOCAL_DB_PORT;Database=$LOCAL_DB_NAME;Username=$LOCAL_DB_USER;Password=$LOCAL_DB_PASS" \
  dotnet run --project API/API.csproj --no-build
}

# ── Main ──────────────────────────────────────────────────────────
case "${1:-all}" in
  dump)
    dump_and_restore
    ;;
  run)
    build_and_run
    ;;
  all|*)
    dump_and_restore
    build_and_run
    ;;
esac
