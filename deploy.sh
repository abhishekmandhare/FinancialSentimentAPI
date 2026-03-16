#!/usr/bin/env bash
# deploy.sh — Redeploy the API on TrueNAS
#
# Usage:
#   ./deploy.sh          # pull latest and restart
#   ./deploy.sh logs     # pull, restart, and tail logs

set -euo pipefail

HOST="truenas_admin@server.home"
APP_DIR="/mnt/immich-pool/apps/financial-sentiment-api"

echo "Pulling latest image and restarting..."
ssh -t "$HOST" "cd $APP_DIR && sudo docker compose pull api && sudo docker compose up -d api"

echo "Deployed. Checking health..."
sleep 5
curl -sf http://server.home:8080/health/live && echo " — API is healthy" || echo " — API not responding yet, check logs"

# Print deployed image version (digest + creation date)
echo ""
echo "Deployed version:"
ssh -t "$HOST" "cd $APP_DIR && sudo docker inspect --format='Image: {{.Config.Image}}
Created: {{.Created}}
Digest: {{index .RepoDigests 0}}' \$(sudo docker compose ps -q api) 2>/dev/null" || echo "(could not read container info)"

if [[ "${1:-}" == "logs" ]]; then
  ssh -t "$HOST" "cd $APP_DIR && sudo docker compose logs -f --tail 50 api"
fi
