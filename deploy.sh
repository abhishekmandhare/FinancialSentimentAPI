#!/usr/bin/env bash
# deploy.sh — Redeploy the API on TrueNAS
#
# Usage:
#   ./deploy.sh          # pull latest and restart
#   ./deploy.sh logs     # pull, restart, and tail logs

set -euo pipefail

HOST="truenas_admin@server.home"
APP_DIR="/mnt/immich-pool/apps/financial-sentiment-api"

echo "Pulling latest changes and images..."
ssh -t "$HOST" "cd $APP_DIR && sudo git pull && sudo docker compose pull api && sudo docker compose up -d"

echo "Deployed. Checking health..."
sleep 5
curl -sf http://server.home:8080/health/live && echo " — API is healthy" || echo " — API not responding yet, check logs"

# Print deployed image version
echo ""
echo "Deployed version:"
ssh -t "$HOST" "cd $APP_DIR && sudo sh -c 'CID=\$(docker compose ps -q api) && docker inspect --format=\"Image: {{.Config.Image}}  Created: {{.Created}}\" \$CID'" || echo "(could not read container info)"

if [[ "${1:-}" == "logs" ]]; then
  ssh -t "$HOST" "cd $APP_DIR && sudo docker compose logs -f --tail 50 api"
fi
