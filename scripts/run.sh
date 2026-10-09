#!/usr/bin/env bash
# Runs PhotoMapper with `aspire run` (hot reload on) and guarantees nothing outlives it.
#
# Clears leftovers from an earlier run first, then stops the app cleanly whichever way this script is
# stopped: Ctrl+C, closing the terminal, or VS Code terminating the task. Arguments go to `aspire run`.
# If this script itself is killed with SIGKILL nothing can clean up, but the next run (or scripts/stop.sh) will.
# Usage: scripts/run.sh [aspire run options]
set -uo pipefail

repo=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$repo" || exit 1

app=""

# shellcheck disable=SC2317,SC2329 # only called from the traps below
stop_app() {
  trap - INT TERM HUP
  if [[ -n "$app" ]] && kill -0 "$app" 2>/dev/null; then
    kill -INT "$app" 2>/dev/null
    for _ in $(seq 60); do
      kill -0 "$app" 2>/dev/null || break
      sleep 1
    done
  fi
  scripts/stop.sh --quiet
}
# Installed before anything else, so a stop request during the start-up cleanup isn't lost.
trap 'stop_app; exit 130' INT
trap 'stop_app; exit 143' TERM
trap 'stop_app; exit 129' HUP

scripts/stop.sh --quiet || exit 1

# Own session and process group, so a terminal Ctrl+C reaches Aspire exactly once (via the trap above).
# `env --default-signal` matters: bash starts background commands with SIGINT ignored, and .NET keeps that, so
# without it Aspire would ignore the Ctrl+C we forward and never shut down (or stop its containers) gracefully.
setsid env --default-signal=INT,QUIT aspire run "$@" &
app=$!

wait "$app"
status=$?
scripts/stop.sh --quiet
exit "$status"
