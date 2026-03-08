# TrueNAS Deployment Guide

This guide covers three ways to deploy the Financial Sentiment API on TrueNAS SCALE so that it appears in the **Applications** section with start/stop controls and resource monitoring.

## Prerequisites

- TrueNAS SCALE Dragonfish (24.04) or later
- A dedicated dataset for app data (e.g., `pool/apps/financial-sentiment-api`)
- Network access to GHCR (`ghcr.io`) for pulling the container image
- (Optional) Ollama instance for AI sentiment analysis

## Option 1: Docker Compose via TrueNAS (Recommended)

TrueNAS SCALE Dragonfish and later support Docker Compose natively. Apps deployed this way appear in the Applications section with full start/stop/restart controls.

### 1. Create a dataset for the app

In the TrueNAS UI, go to **Storage > Datasets** and create:

```
pool/apps/financial-sentiment-api
```

### 2. Copy project files to the dataset

SSH into your TrueNAS server and copy the compose file:

```bash
APP_DIR="/mnt/pool/apps/financial-sentiment-api"
mkdir -p "$APP_DIR"
```

Create `docker-compose.yml` in that directory:

```yaml
services:

  api:
    image: ghcr.io/abhishekmandhare/financial-sentiment-api:latest
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__DefaultConnection: >-
        Host=db;Database=sentiment;Username=sentiment;Password=${DB_PASSWORD}
      AI__Provider: ${AI_PROVIDER:-Mock}
      Anthropic__ApiKey: ${ANTHROPIC_API_KEY:-}
      Anthropic__Model: claude-haiku-4-5-20251001
      Anthropic__MaxTokens: 512
      Ollama__BaseUrl: ${OLLAMA_BASE_URL:-}
      Ollama__Model: ${OLLAMA_MODEL:-llama3}
      Ollama__MaxTokens: ${OLLAMA_MAX_TOKENS:-512}
      Ingestion__TrackedSymbols__0: AAPL
      Ingestion__TrackedSymbols__1: MSFT
      Ingestion__TrackedSymbols__2: GOOGL
      Ingestion__TrackedSymbols__3: TSLA
      Ingestion__TrackedSymbols__4: NVDA
      Ingestion__TrackedSymbols__5: AMZN
      Ingestion__TrackedSymbols__6: META
      Ingestion__TrackedSymbols__7: NFLX
      Ingestion__TrackedSymbols__8: AMD
      Ingestion__TrackedSymbols__9: INTC
      Ingestion__TrackedSymbols__10: JPM
      Ingestion__TrackedSymbols__11: BAC
      Ingestion__TrackedSymbols__12: SPY
      Ingestion__TrackedSymbols__13: QQQ
      Ingestion__TrackedSymbols__14: BTC-USD
      Ingestion__PollingIntervalMinutes: 5
      Ingestion__MaxConcurrentAnalyses: 3
    depends_on:
      db:
        condition: service_healthy
    restart: unless-stopped
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/health/live || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s

  db:
    image: postgres:17-alpine
    environment:
      POSTGRES_DB: sentiment
      POSTGRES_USER: sentiment
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    restart: unless-stopped
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U sentiment -d sentiment"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres_data:
```

### 3. Create the `.env` file

```bash
cat > "$APP_DIR/.env" <<'EOF'
DB_PASSWORD=<your-secure-password>
AI_PROVIDER=Ollama
OLLAMA_BASE_URL=http://server.home:30068
OLLAMA_MODEL=llama3
OLLAMA_MAX_TOKENS=512
EOF
```

Available `AI_PROVIDER` values:
| Value | Description | Required variables |
|-------|-------------|--------------------|
| `Mock` | Returns dummy sentiment (no AI calls) | None |
| `Ollama` | Self-hosted LLM via Ollama | `OLLAMA_BASE_URL` |
| `Anthropic` | Anthropic Claude API | `ANTHROPIC_API_KEY` |

### 4. Register with TrueNAS

In the TrueNAS UI:

1. Go to **Apps > Discover Apps > Custom App** (or **Apps > Manage Docker Compose** on Dragonfish+)
2. Set the compose file path to `/mnt/pool/apps/financial-sentiment-api/docker-compose.yml`
3. Save and start the app

The app now appears in the Applications list with start/stop/restart controls.

### 5. Verify

```bash
curl http://<truenas-ip>:8080/health/live
# Expected: Healthy

curl http://<truenas-ip>:8080/health/ready
# Expected: Healthy (once DB migration completes)
```

---

## Option 2: TrueNAS Custom App (UI-Only)

Use this if you prefer configuring everything through the TrueNAS web UI without SSH.

### 1. Add the API container

1. Go to **Apps > Discover Apps > Custom App**
2. Fill in:
   - **Application Name**: `financial-sentiment-api`
   - **Image Repository**: `ghcr.io/abhishekmandhare/financial-sentiment-api`
   - **Image Tag**: `latest`

### 2. Configure port mapping

| Container Port | Host Port | Protocol |
|---------------|-----------|----------|
| 8080 | 8080 | TCP |

### 3. Set environment variables

Add the following environment variables in the UI:

| Variable | Value |
|----------|-------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | `Host=<postgres-host>;Database=sentiment;Username=sentiment;Password=<password>` |
| `AI__Provider` | `Ollama` |
| `Ollama__BaseUrl` | `http://server.home:30068` |
| `Ollama__Model` | `llama3` |

For this option, you need a separate Postgres instance. You can either:
- Install the **PostgreSQL** app from the TrueNAS app catalog
- Use an existing Postgres server on your network

Set `<postgres-host>` to the IP/hostname of your Postgres instance.

### 4. Configure storage

No persistent storage is needed for the API container itself (it is stateless). Postgres data persistence is handled by whichever Postgres deployment you chose above.

---

## Option 3: Custom TrueNAS App Catalog

For a native TrueNAS app experience with a configuration UI, you can create a custom catalog. This is more effort but provides the best integration.

This approach requires creating a Helm chart or ix-chart manifest. See the [TrueNAS custom app catalog documentation](https://www.truenas.com/docs/scale/scaletutorials/apps/usingcustomapp/) for details. The Docker Compose option (Option 1) is recommended for most users.

---

## Persistent Storage for Postgres

Regardless of which option you choose, Postgres data must survive container restarts.

**Option 1 (Docker Compose)**: The `postgres_data` named volume in the compose file handles this automatically. Docker stores it under `/var/lib/docker/volumes/` on the TrueNAS host.

For explicit dataset mapping, replace the named volume with a bind mount to a TrueNAS dataset:

```yaml
  db:
    volumes:
      - /mnt/pool/apps/financial-sentiment-api/pgdata:/var/lib/postgresql/data
```

Make sure the directory exists and has correct permissions:

```bash
mkdir -p /mnt/pool/apps/financial-sentiment-api/pgdata
chown 70:70 /mnt/pool/apps/financial-sentiment-api/pgdata
```

UID 70 is the `postgres` user inside the `postgres:17-alpine` image.

---

## Health Check Endpoints

The API exposes two health check endpoints:

| Endpoint | Purpose |
|----------|---------|
| `/health/live` | Liveness probe -- confirms the process is running |
| `/health/ready` | Readiness probe -- confirms the app can serve requests (DB connected) |

Use `/health/live` for container health checks (already configured in the compose file). Use `/health/ready` for load balancer or monitoring checks.

---

## Updating the Container Image

### Automated (via deploy script)

From your development machine:

```bash
./deploy.sh        # pulls latest image and restarts
./deploy.sh logs   # same, then tails logs
```

### Manual (via SSH)

```bash
cd /mnt/pool/apps/financial-sentiment-api
sudo docker compose pull api
sudo docker compose up -d api
```

### Via TrueNAS UI

If using the Custom App option, go to **Apps > financial-sentiment-api > Edit** and change the image tag, or click **Update** if TrueNAS detects a newer `latest` tag.

The GHCR image is built automatically by GitHub Actions on pushes to `main`:
```
ghcr.io/abhishekmandhare/financial-sentiment-api:latest
```

---

## Troubleshooting

### API container exits immediately

Check logs for connection string or configuration errors:

```bash
sudo docker compose logs api --tail 50
```

Common causes:
- Missing or incorrect `DB_PASSWORD` in `.env`
- Postgres container not healthy yet (the API waits for the `service_healthy` condition)

### Database connection refused

- Verify Postgres is running: `sudo docker compose ps db`
- Check Postgres health: `sudo docker compose exec db pg_isready -U sentiment`
- Ensure the connection string uses `Host=db` (the Docker Compose service name), not `localhost`

### Ollama sentiment analysis times out

- Verify Ollama is reachable from TrueNAS: `curl http://server.home:30068/api/tags`
- The first request may take longer as the model loads into memory
- Check that `OLLAMA_BASE_URL` does not have a trailing slash

### Port 8080 already in use

Another app or service is using port 8080. Change the host port mapping:

```yaml
    ports:
      - "9090:8080"  # access via port 9090 instead
```

### Health check failing

```bash
# Test from inside the container
sudo docker compose exec api curl -f http://localhost:8080/health/live

# Test from the host
curl -f http://localhost:8080/health/live
```

If the liveness probe passes but readiness fails, the database connection is likely the issue. Check the `ConnectionStrings__DefaultConnection` environment variable.

### Checking API version

```bash
curl http://<truenas-ip>:8080/health/live
```

### Viewing tracked symbols and ingestion status

```bash
curl http://<truenas-ip>:8080/api/sentiments/trending
```
