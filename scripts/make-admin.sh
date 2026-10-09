#!/usr/bin/env bash
# Gives an existing, confirmed account the Admin role in the local development database.
# The app must be running (scripts/run.sh or F5), since its PostgreSQL container holds the database.
# On a deployed server, run the same command through the migrations image instead (see deploy/README.md).
# Usage: scripts/make-admin.sh <email>
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: scripts/make-admin.sh <email>" >&2
  exit 2
fi

repo=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)

# The PostgreSQL container Aspire started for the current run.
container=$(docker ps --filter label=com.microsoft.developer.usvc-dev.persistent=false --filter name=postgres- --format '{{.ID}}' | head -1)
if [[ -z "$container" ]]; then
  echo "The app's database isn't running. Start the app first (scripts/run.sh or F5)." >&2
  exit 1
fi

port=$(docker port "$container" 5432/tcp | head -1 | sed 's/.*://')
password=$(docker inspect "$container" --format '{{range .Config.Env}}{{println .}}{{end}}' | sed -n 's/^POSTGRES_PASSWORD=//p')

ConnectionStrings__photomapperdb="Host=localhost;Port=$port;Username=postgres;Password=$password;Database=photomapperdb" \
  dotnet run --project "$repo/src/PhotoMapper.MigrationService" --verbosity quiet -- make-admin "$1"
