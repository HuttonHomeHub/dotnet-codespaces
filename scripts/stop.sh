#!/usr/bin/env bash
# Stops everything a local run of PhotoMapper started and frees its ports.
#
# `aspire run` cleans up after Ctrl+C or SIGTERM, but if its terminal is closed (SIGHUP) or it is killed, the
# `dotnet watch` hot-reload process survives and keeps ports open, and ignores a polite SIGTERM. This finds:
#   - `aspire run` processes started in this repository and `dotnet watch` processes for this AppHost,
#     plus every process they started;
#   - anything still listening on the app's fixed ports (read from the launchSettings.json files);
# then sends SIGTERM, waits, and sends SIGKILL to whatever is left.
# Usage: scripts/stop.sh [--quiet]
set -euo pipefail

repo=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
quiet=false
[[ "${1:-}" == "--quiet" ]] && quiet=true
say() { $quiet || echo "$@"; }

# Fixed ports: each project's applicationUrl, plus the AppHost's dashboard endpoints (…_URL variables).
mapfile -t ports < <(jq -r '.profiles[] | .applicationUrl, (.environmentVariables // {} | to_entries[] | select(.key | endswith("_URL")) | .value)' \
  "$repo"/src/*/Properties/launchSettings.json | grep -oE ':[0-9]+' | tr -d : | sort -un)

descendants() {
  local child
  for child in $(ps -o pid= --ppid "$1" 2>/dev/null); do
    echo "$child"
    descendants "$child"
  done
}

find_targets() {
  local pid cmd
  {
    while read -r pid cmd; do
      case "$cmd" in
        "aspire run"*)
          [[ "$(readlink "/proc/$pid/cwd" 2>/dev/null)" == "$repo"* ]] || continue ;;
        *"dotnet watch"*"$repo/src/PhotoMapper.AppHost/"*) ;;
        *) continue ;;
      esac
      echo "$pid"
      descendants "$pid"
    done < <(ps -eo pid=,args=)

    # Aspire's orchestrator (DCP) runs detached and watches the AppHost it serves (--monitor <pid>). Once that
    # AppHost is gone it lingers for minutes before exiting. A DCP whose AppHost is alive (another run, or the
    # integration tests) is left alone.
    while read -r pid cmd; do
      [[ "$cmd" =~ start-apiserver.*--monitor\ ([0-9]+) ]] || continue
      kill -0 "${BASH_REMATCH[1]}" 2>/dev/null && continue
      echo "$pid"
      descendants "$pid"
    done < <(ps -C dcp -o pid=,args= 2>/dev/null)

    for port in "${ports[@]}"; do
      ss -ltnpH "sport = :$port" 2>/dev/null | grep -oE 'pid=[0-9]+' | cut -d= -f2
    done
  } | sort -un | grep -vxE "$$|$PPID" || true
}

mapfile -t targets < <(find_targets)
if ((${#targets[@]} == 0)); then
  say "Nothing running. Ports ${ports[*]} are free."
  exit 0
fi

say "Stopping ${#targets[@]} process(es): ${targets[*]}"
# Re-scan each second: stopping the AppHost is what orphans its DCP.
for _ in $(seq 10); do
  kill -TERM "${targets[@]}" 2>/dev/null || true
  sleep 1
  mapfile -t targets < <(find_targets)
  ((${#targets[@]} == 0)) && break
done
if ((${#targets[@]} > 0)); then
  say "Still running after SIGTERM, sending SIGKILL: ${targets[*]}"
  kill -KILL "${targets[@]}" 2>/dev/null || true
  sleep 1
fi

mapfile -t targets < <(find_targets)
if ((${#targets[@]} > 0)); then
  echo "Could not stop: ${targets[*]}" >&2
  exit 1
fi
say "Stopped. Ports ${ports[*]} are free."
