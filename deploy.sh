#!/usr/bin/env bash
set -e

# Zero-downtime-on-failure deploy (same pattern as rzprime/frontend/app/setup.sh):
# publish + build a new image while the live container keeps serving, boot it as
# a throwaway canary via a docker-compose override (reuses the same .env-driven
# environment as the real service - just on a private name/port), health-check
# it, and only then swap it into the live container. A bad publish/build/boot
# leaves production untouched - the job just fails.

SERVICE="api.sltcargopay.com"
IMAGE_NAME="slt.api"
LIVE_NAME="api.sltcargopay.com"
CANARY_NAME="${LIVE_NAME}_canary"
CANARY_PORT="13009"
INTERNAL_PORT="80"

cleanup_canary() {
  docker rm -f "$CANARY_NAME" >/dev/null 2>&1 || true
  rm -f docker-compose.canary.yml
}
trap cleanup_canary EXIT

echo "Publishing..."
dotnet build SLT.Api/SLT.Api.csproj -c Release
dotnet publish SLT.Api/SLT.Api.csproj -c Release -o publish

echo "Building new image (production container keeps serving)..."
docker build -t "$IMAGE_NAME" .

echo "Starting canary on 127.0.0.1:${CANARY_PORT} for a health check..."
cleanup_canary
cat > docker-compose.canary.yml <<CANARYEOF
services:
  ${SERVICE}:
    container_name: ${CANARY_NAME}
    ports:
      - "127.0.0.1:${CANARY_PORT}:${INTERNAL_PORT}"
CANARYEOF
docker-compose -f docker-compose.yml -f docker-compose.canary.yml up -d --no-deps "$SERVICE"

echo "Health-checking canary..."
ok=0
for i in $(seq 1 15); do
  code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "http://127.0.0.1:${CANARY_PORT}/" || echo 000)
  echo "  attempt $i: HTTP $code"
  case "$code" in
    000|5[0-9][0-9]) ;;
    *) ok=1; break ;;
  esac
  sleep 4
done

if [ "$ok" -ne 1 ]; then
  echo "Canary failed its health check - leaving the production container untouched."
  docker logs --tail 100 "$CANARY_NAME" || true
  exit 1
fi

echo "Canary healthy - swapping into production (brief blip)..."
docker ps -aq --filter "name=${LIVE_NAME}" | xargs -r docker rm -f >/dev/null 2>&1 || true
cleanup_canary
docker-compose up -d

echo "Container status:"
docker-compose ps
