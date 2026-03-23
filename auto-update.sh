#!/usr/bin/env bash
# auto-update.sh — Pull latest images and restart TrueNAS-managed app if changed
# Set up as a cron job on TrueNAS: */5 * * * *
set -euo pipefail

APP_NAME="financial-sentiment-api"
IMAGE="ghcr.io/abhishekmandhare/financial-sentiment-api:latest"
LOG_TAG="[auto-update]"

# Get current image digest
CURRENT_DIGEST=$(docker inspect --format="{{.Image}}" $(docker ps -qf name=ix-${APP_NAME}.*-api-) 2>/dev/null || echo "none")

# Pull latest
docker pull "$IMAGE" > /dev/null 2>&1
NEW_DIGEST=$(docker inspect --format="{{.Id}}" "$IMAGE" 2>/dev/null || echo "unknown")

if [ "$CURRENT_DIGEST" != "$NEW_DIGEST" ]; then
    echo "$LOG_TAG New image detected, restarting $APP_NAME..."
    midclt call -j app.stop "$APP_NAME" > /dev/null 2>&1
    midclt call -j app.start "$APP_NAME" > /dev/null 2>&1
    echo "$LOG_TAG $APP_NAME restarted with new image."
else
    echo "$LOG_TAG $APP_NAME is up to date."
fi
