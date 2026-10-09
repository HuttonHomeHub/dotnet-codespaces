#!/usr/bin/env bash
# Starts a deployment bundle with plain HTTP on a local port, checks it through the proxy, then removes it
# (containers and volumes). Uses throwaway settings in place of the server's site.env.
# Usage: deploy/smoke-test.sh <bundle-dir>   (the bundle needs docker-compose.yaml, compose.proxy.yaml, Caddyfile, .env)
set -euo pipefail

cd "$1"
port=8090
cookies=$(mktemp)
cat > smoke.env <<ENV
SITE_ADDRESS=:80
PROXY_HTTP_PORT=$port
PROXY_HTTPS_PORT=8453
POSTGRES_PASSWORD=$(head -c 24 /dev/urandom | od -An -tx1 | tr -d ' \n')
MAIL=Endpoint=smtp://localhost:25
MAIL_FROM=PhotoMapper <no-reply@example.com>
ENV
compose=(docker compose --project-name photomapper-smoke --env-file .env --env-file smoke.env -f docker-compose.yaml -f compose.proxy.yaml)
trap '"${compose[@]}" logs --no-color > smoke.log 2>&1 || true; "${compose[@]}" down --volumes > /dev/null 2>&1 || true; rm -f smoke.env "$cookies"' EXIT

"${compose[@]}" up --detach --quiet-pull

base="http://localhost:$port"
for _ in $(seq 90); do
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
check 200 GET /Account/Login

# Registering only redirects to the confirmation page once the account is saved, so this proves the migrations
# ran and the web app can write to PostgreSQL. (Sending the email fails here, which the app logs and survives.)
token=$(curl --silent --cookie-jar "$cookies" --cookie "$cookies" "$base/Account/Register" \
  | grep -oE 'name="__RequestVerificationToken" value="[^"]+"' | head -1 | sed -E 's/.*value="([^"]+)"/\1/')
location=$(curl --silent --output /dev/null --write-out '%{redirect_url}' --cookie-jar "$cookies" --cookie "$cookies" \
  --data-urlencode _handler=register --data-urlencode "__RequestVerificationToken=$token" \
  --data-urlencode Input.Email=smoke-test@example.com \
  --data-urlencode Input.Password=Smoke-Test-Passw0rd! --data-urlencode Input.ConfirmPassword=Smoke-Test-Passw0rd! \
  "$base/Account/Register")
if [[ "$location" != *"/Account/RegisterConfirmation"* ]]; then
  echo "FAIL: POST /Account/Register redirected to '$location', expected /Account/RegisterConfirmation"
  exit 1
fi
echo "ok:   POST /Account/Register -> account saved"
