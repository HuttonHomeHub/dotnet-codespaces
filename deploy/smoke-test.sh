#!/usr/bin/env bash
# Starts a deployment bundle with plain HTTP on a local port, checks it through the proxy, then removes it.
# Usage: deploy/smoke-test.sh <bundle-dir>   (the bundle needs docker-compose.yaml, compose.proxy.yaml, Caddyfile, .env)
set -euo pipefail

cd "$1"
port=8090
printf 'SITE_ADDRESS=:80\nPROXY_HTTP_PORT=%s\nPROXY_HTTPS_PORT=8453\n' "$port" > smoke.env
compose=(docker compose --project-name photomapper-smoke --env-file .env --env-file smoke.env -f docker-compose.yaml -f compose.proxy.yaml)
trap '"${compose[@]}" logs --no-color > smoke.log 2>&1 || true; "${compose[@]}" down --volumes > /dev/null 2>&1 || true; rm -f smoke.env' EXIT

"${compose[@]}" up --detach --quiet-pull

base="http://localhost:$port"
for _ in $(seq 60); do
  # --fail: wait for the app itself, not just the proxy (which answers 502 until the app is listening).
  curl --silent --fail --output /dev/null "$base/" && break
  sleep 1
done

check() {
  local expected=$1 method=$2 path=$3
  local actual
  actual=$(curl --silent --output /dev/null --write-out '%{http_code}' --request "$method" "$base$path")
  if [[ "$actual" != "$expected" ]]; then
    echo "FAIL: $method $path returned $actual, expected $expected"
    return 1
  fi
  echo "ok:   $method $path -> $actual"
}

check 200 GET /
check 404 GET /does-not-exist
check 200 GET /_framework/blazor.web.js
check 200 POST "/_blazor/negotiate?negotiateVersion=1"
check 404 GET /health
