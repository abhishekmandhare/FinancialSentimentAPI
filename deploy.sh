#!/usr/bin/env bash
# deploy.sh — Redeploy the API on TrueNAS
#
# Usage:
#   ./deploy.sh          # pull latest and restart
#   ./deploy.sh logs     # pull, restart, and tail logs

set -euo pipefail

HOST="truenas_admin@server.home"
APP_DIR="/mnt/ssd-pool/apps/financial-sentiment-api"
APP_NAME="financial-sentiment-api"

# Sync compose files, Prometheus config, and Grafana provisioning to TrueNAS
echo "Syncing config files..."
scp docker-compose.yml prometheus.yml tempo.yml "$HOST:/tmp/"
ssh "$HOST" "rm -rf /tmp/grafana && mkdir -p /tmp/grafana" && scp -r grafana/* "$HOST:/tmp/grafana/"
ssh -t "$HOST" "sudo mv /tmp/docker-compose.yml /tmp/prometheus.yml /tmp/tempo.yml $APP_DIR/ && sudo rm -rf $APP_DIR/grafana && sudo mv /tmp/grafana $APP_DIR/"

echo "Updating TrueNAS custom app compose config..."
ssh -t "$HOST" "COMPOSE_YAML=\$(cat $APP_DIR/docker-compose.yml | python3 -c 'import sys,json; print(json.dumps(sys.stdin.read()))') && sudo midclt call -j app.update '$APP_NAME' \"{\\\"custom_compose_config_string\\\": \$COMPOSE_YAML}\""

echo "Restarting app..."
ssh -t "$HOST" "sudo midclt call -j app.stop '$APP_NAME' && sudo midclt call -j app.start '$APP_NAME'"

echo "Deployed. Checking health..."
sleep 10
curl -sf http://server.home:3100/health/live && echo " — API is healthy" || echo " — API not responding yet, check logs"

# Print deployed image version
echo ""
echo "Deployed version:"
ssh -t "$HOST" "sudo docker inspect --format='Image: {{.Config.Image}}  Created: {{.Created}}' \$(sudo docker ps -qf name=ix-financial-sentiment-api.*api)" || echo "(could not read container info)"

if [[ "${1:-}" == "logs" ]]; then
  ssh -t "$HOST" "sudo docker logs -f --tail 50 \$(sudo docker ps -qf name=ix-financial-sentiment-api.*api)"
fi
