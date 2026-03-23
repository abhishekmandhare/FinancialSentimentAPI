# TrueNAS Deployment Guide

Deploy the Financial Sentiment API stack on TrueNAS SCALE as a managed custom app with auto-updates.

## Prerequisites

- TrueNAS SCALE Dragonfish (24.04) or later
- A dataset for app data (e.g., `ssd-pool/apps/financial-sentiment-api`)
- Network access to GHCR (`ghcr.io`)
- SSH access as `truenas_admin`

## Architecture

The stack runs as a TrueNAS custom app with 5 containers:

| Service | Image | Host Port | Purpose |
|---------|-------|-----------|---------|
| api | `ghcr.io/abhishekmandhare/financial-sentiment-api:latest` | 3100 | Main API |
| db | `postgres:17-alpine` | — | Database |
| prometheus | `prom/prometheus:latest` | 3200 | Metrics |
| tempo | `grafana/tempo:2.7.0` | — | Tracing |
| grafana | `grafana/grafana:latest` | 3300 | Dashboards |

FinBERT runs as a separate TrueNAS custom app:

| Service | Image | Host Port | Purpose |
|---------|-------|-----------|---------|
| finbert | `finbert-api:latest` (local) | 3400 | Sentiment model (GPU) |

## Initial Setup

### 1. Copy project files to TrueNAS

```bash
APP_DIR="/mnt/ssd-pool/apps/financial-sentiment-api"
sudo mkdir -p "$APP_DIR"
```

Copy the following files to `$APP_DIR`:
- `docker-compose.yml`
- `prometheus.yml`
- `tempo.yml`
- `grafana/` directory (provisioning + dashboards)
- `auto-update.sh`

Or use the deploy script from your dev machine:
```bash
./deploy.sh
```

### 2. Create the `.env` file on TrueNAS

```bash
cat > "$APP_DIR/.env" <<'EOF'
DB_PASSWORD=<your-secure-password>
AI_PROVIDER=FinBert
FINBERT_BASE_URL=http://192.168.1.179:3400
FINBERT_TIMEOUT=30
API_PORT=3100
PROMETHEUS_PORT=3200
GRAFANA_PORT=3300
EOF
```

### 3. Register as a TrueNAS custom app

The compose file uses `./` relative paths for config files, but TrueNAS custom apps need absolute paths. The `deploy.sh` script handles this automatically via the `midclt` API.

To register manually:
```bash
# Convert compose to JSON and create the app
COMPOSE_YAML=$(cat $APP_DIR/docker-compose.yml | python3 -c 'import sys,json; print(json.dumps(sys.stdin.read()))')
sudo midclt call -j app.create "{\"custom_app\": true, \"app_name\": \"financial-sentiment-api\", \"custom_compose_config_string\": $COMPOSE_YAML}"
```

**Note:** When using `midclt`, replace `./` paths with absolute paths (e.g., `./prometheus.yml` → `/mnt/ssd-pool/apps/financial-sentiment-api/prometheus.yml`) in the compose config.

### 4. Verify

```bash
curl http://<truenas-ip>:3100/health/live
# Expected: Healthy

curl http://<truenas-ip>:3100/health/ready
# Expected: Healthy (once DB migration completes)
```

## AI Provider Configuration

| Provider | `AI_PROVIDER` | Required variables |
|----------|---------------|--------------------|
| Mock | `Mock` | None |
| FinBERT | `FinBert` | `FINBERT_BASE_URL` |
| Ollama | `Ollama` | `OLLAMA_BASE_URL`, `OLLAMA_MODEL` |
| Anthropic | `Anthropic` | `ANTHROPIC_API_KEY` |

## Port Configuration

Ports are configurable via environment variables with these defaults:

| Variable | Default | Service |
|----------|---------|---------|
| `API_PORT` | 3100 | API |
| `PROMETHEUS_PORT` | 3200 | Prometheus |
| `GRAFANA_PORT` | 3300 | Grafana |

## Updating the Container Image

### Automatic (daily cron)

A cron job on TrueNAS runs `auto-update.sh` daily at 3 AM. It compares the running image digest against the latest on GHCR and restarts the app only if a new image is available.

To set up the cron job:
```bash
sudo midclt call cronjob.create '{
  "user": "root",
  "command": "/usr/bin/bash /mnt/ssd-pool/apps/financial-sentiment-api/auto-update.sh >> /var/log/fsa-auto-update.log 2>&1",
  "description": "Auto-update Financial Sentiment API",
  "schedule": {"minute": "0", "hour": "3", "dom": "*", "month": "*", "dow": "*"},
  "enabled": true
}'
```

Check logs: `cat /var/log/fsa-auto-update.log`

### Manual (via deploy script)

From your development machine:
```bash
./deploy.sh        # syncs config, pulls latest image, restarts via midclt
./deploy.sh logs   # same, then tails API logs
```

### Manual (via SSH)

```bash
sudo docker pull ghcr.io/abhishekmandhare/financial-sentiment-api:latest
sudo midclt call -j app.stop 'financial-sentiment-api'
sudo midclt call -j app.start 'financial-sentiment-api'
```

The GHCR image is built automatically by GitHub Actions on pushes to `main`.

## FinBERT Setup

FinBERT is a locally built Docker image running as a separate TrueNAS custom app with GPU access.

```yaml
services:
  finbert:
    image: finbert-api:latest
    ports:
      - "3400:8000"
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: all
              capabilities: [gpu]
    restart: unless-stopped
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8000/health || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 5
      start_period: 120s
```

**Note:** FinBERT takes ~2 minutes to start as it loads the transformer model weights.

## Persistent Storage

Postgres data is stored in a Docker named volume managed by TrueNAS at:
```
/mnt/.ix-apps/docker/volumes/ix-financial-sentiment-api_postgres_data/_data
```

Grafana, Prometheus, and Tempo data are similarly stored in named volumes under `/mnt/.ix-apps/docker/volumes/`.

## Troubleshooting

### API container exits immediately

```bash
sudo docker logs $(sudo docker ps -aqf name=ix-financial-sentiment-api.*api) --tail 50
```

Common causes:
- Missing or incorrect `DB_PASSWORD`
- Postgres container not healthy yet
- FinBERT not reachable (if `AI_PROVIDER=FinBert`)

### FinBERT not responding

- FinBERT takes ~2 minutes to load the model on startup
- Verify GPU access: `curl http://<truenas-ip>:3400/health` should show `"gpu": true`
- Check logs: `sudo docker logs $(sudo docker ps -qf name=ix-finbert)`

### Port conflicts

If ports 3100/3200/3300 are in use, change them in the `.env` file and redeploy.

### Health check endpoints

| Endpoint | Purpose |
|----------|---------|
| `/health/live` | Liveness probe — process is running |
| `/health/ready` | Readiness probe — DB connected, ready to serve |

### Auto-update not working

- Check cron exists: `sudo midclt call cronjob.query`
- Check logs: `cat /var/log/fsa-auto-update.log`
- Test manually: `sudo bash /mnt/ssd-pool/apps/financial-sentiment-api/auto-update.sh`
