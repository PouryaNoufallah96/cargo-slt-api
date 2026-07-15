#!/usr/bin/env bash
set -e

# Zero-downtime-on-failure deploy (same pattern as cargo/frontend/app's deploy.sh):
# build a new image while the live container keeps serving, boot it as a throwaway
# canary via plain `docker run` (not `docker-compose up`), health-check it, and
# only then swap it into the live container. A bad publish/build/boot leaves
# production untouched - the job just fails.
#
# Canary/swap use `docker run`, not `docker-compose up`: compose matches an
# existing container by project+service label regardless of container_name
# overrides, and this project already had a container left renamed
# `<id>_api.sltcargopay.com` from an earlier failed compose recreate (the same
# v1.29.2 "recreate in place" bug class documented in infra/ci/deploy.yml) -
# compose kept trying to recreate that zombie (pointing at a since-deleted
# image) instead of creating the canary fresh. `docker-compose config` is still
# used once, only to resolve docker-compose.yml's .env-interpolated
# `environment:` block into plain `docker run --env-file` lines - compose
# itself never creates or touches a container.

SERVICE="api.sltcargopay.com"
IMAGE_NAME="slt.api"
LIVE_NAME="api.sltcargopay.com"
CANARY_NAME="${LIVE_NAME}_canary"
CANARY_PORT="13009"
INTERNAL_PORT="80"
LIVE_PORT="3009"

ENV_FILE=""
cleanup_canary() {
  docker rm -f "$CANARY_NAME" >/dev/null 2>&1 || true
  [ -n "$ENV_FILE" ] && rm -f "$ENV_FILE"
}
trap cleanup_canary EXIT

echo "Publishing..."
# dotnet publish builds internally - a separate `dotnet build` beforehand races it
# for the same obj/ files (MSBuild node reuse keeps handles open), causing
# IOException: process cannot access SLT.Utilities.dll.
dotnet publish SLT.Api/SLT.Api.csproj -c Release -o publish

echo "Building new image (production container keeps serving)..."
docker build -t "$IMAGE_NAME" .

echo "Resolving environment from docker-compose.yml + .env..."
ENV_FILE=$(mktemp)
docker-compose config | python3 -c "
import sys, yaml
d = yaml.safe_load(sys.stdin)
env = d['services']['${SERVICE}']['environment']
items = env.items() if isinstance(env, dict) else (e.split('=', 1) for e in env)
for k, v in items:
    print(f'{k}={v}')
" > "$ENV_FILE"

echo "Starting canary on 127.0.0.1:${CANARY_PORT} for a health check..."
cleanup_canary_container_only() { docker rm -f "$CANARY_NAME" >/dev/null 2>&1 || true; }
cleanup_canary_container_only
docker run -d --name "$CANARY_NAME" \
  --env-file "$ENV_FILE" \
  -v /etc/timezone:/etc/timezone:ro \
  -v /etc/localtime:/etc/localtime:ro \
  -p "127.0.0.1:${CANARY_PORT}:${INTERNAL_PORT}" \
  "$IMAGE_NAME"

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
cleanup_canary_container_only
docker run -d --name "$LIVE_NAME" --restart unless-stopped \
  --env-file "$ENV_FILE" \
  -v /etc/timezone:/etc/timezone:ro \
  -v /etc/localtime:/etc/localtime:ro \
  -p "127.0.0.1:${LIVE_PORT}:${INTERNAL_PORT}" \
  "$IMAGE_NAME"

echo "Container status:"
docker ps --filter "name=${LIVE_NAME}"
