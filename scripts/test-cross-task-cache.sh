#!/usr/bin/env bash
#
# Local validation of FusionCache cross-task coordination.
#
# Prerequisites:
#   1. Redis running on localhost:6379 (docker run -p 6379:6379 redis:7)
#   2. A valid .env / appsettings.Development.json so the app can boot with real FRC credentials.
#   3. `dotnet build` succeeds (uses bin/Debug/net10.0/gatool-api.dll).
#
# Usage:
#   ./scripts/test-cross-task-cache.sh [endpoint]
#   endpoint defaults to: /v3/2024/events
#
set -euo pipefail

# Use distinct years per test so each test starts with a truly cold cache
# (FLUSHDB clears L2 only — L1 stays warm in each task's memory). Years
# 2018-2023 are all valid in the FRC API.
ENDPOINT_T1="${1:-/v3/2023/events}"
ENDPOINT_T2="${2:-/v3/2022/events}"
PORT_A=8080
PORT_B=8081
DLL="bin/Debug/net10.0/gatool-api.dll"

cd "$(dirname "$0")/.."

[[ -f "$DLL" ]] || { echo "Build first: dotnet build"; exit 1; }
command -v redis-cli >/dev/null || { echo "Install redis-cli"; exit 1; }
command -v jq >/dev/null || { echo "Install jq"; exit 1; }

echo "==> Flushing Redis"
redis-cli -h localhost -p 6379 FLUSHDB >/dev/null

echo "==> Starting Task A on :$PORT_A"
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS="http://localhost:$PORT_A" \
  dotnet "$DLL" > /tmp/gatool-a.log 2>&1 &
PID_A=$!

echo "==> Starting Task B on :$PORT_B"
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS="http://localhost:$PORT_B" \
  dotnet "$DLL" > /tmp/gatool-b.log 2>&1 &
PID_B=$!

cleanup() {
  echo "==> Cleaning up"
  kill "$PID_A" "$PID_B" 2>/dev/null || true
  wait "$PID_A" "$PID_B" 2>/dev/null || true
}
trap cleanup EXIT

echo "==> Waiting for both tasks to respond on /livecheck ..."
for port in "$PORT_A" "$PORT_B"; do
  for i in {1..60}; do
    if curl -fsS "http://localhost:$port/livecheck" >/dev/null 2>&1; then
      echo "    :$port ready"
      break
    fi
    [[ "$i" -eq 60 ]] && { echo "Task on :$port never became healthy"; tail -40 /tmp/gatool-a.log /tmp/gatool-b.log; exit 1; }
    sleep 1
  done
done

# Start redis-cli MONITOR in the background so we can count writes on our key.
echo "==> Starting Redis MONITOR"
redis-cli -h localhost -p 6379 MONITOR > /tmp/gatool-monitor.log 2>&1 &
MON_PID=$!
trap 'cleanup; kill "$MON_PID" 2>/dev/null || true' EXIT
sleep 0.5
: > /tmp/gatool-monitor.log

run_test() {
  local title="$1"; shift
  echo
  echo "================================================================"
  echo " $title"
  echo "================================================================"
  redis-cli -h localhost -p 6379 FLUSHDB >/dev/null
  : > /tmp/gatool-monitor.log
  "$@"
  sleep 0.8
  local writes pubs gets
  # FusionCache's IDistributedCache layer stores entries as Redis hashes (HMSET + EXPIRE),
  # not plain SETs. Count writes via HMSET.
  writes=$(grep -ac '"HMSET"' /tmp/gatool-monitor.log || true)
  pubs=$(grep -ac '"PUBLISH"' /tmp/gatool-monitor.log || true)
  gets=$(grep -ac '"HMGET"' /tmp/gatool-monitor.log || true)
  echo "    -> Cache WRITES (HMSET):   $writes   <- factory executions × layers"
  echo "    -> Cache READS  (HMGET):   $gets"
  echo "    -> Backplane PUBLISHes:    $pubs   <- L1 invalidation broadcasts"
  echo "    -> All redis commands seen this test:"
  grep -ao '"[A-Z]\+"' /tmp/gatool-monitor.log 2>/dev/null \
    | sort | uniq -c | sort -rn \
    | sed 's/^/         /'
}

# ---- TEST 1: sequential — prove L2 cross-task hit ------------------
run_test "TEST 1 — Warm A (cold), then read B (expect 1 SET, then 0)" bash -c '
  echo "    A <- first call (cold L1+L2 — should call factory + write L2)"
  curl -fsS -o /dev/null "http://localhost:'"$PORT_A"''"$ENDPOINT_T1"'"
  sleep 0.3
  echo "    B <- second call (B has cold L1, but L2 is warm — should L2-hit)"
  curl -fsS -o /dev/null "http://localhost:'"$PORT_B"''"$ENDPOINT_T1"'"
'

# ---- TEST 2: concurrent burst across both tasks --------------------
# Use a different year so the cache key is fresh in BOTH L1 caches.
run_test "TEST 2 — 50 concurrent requests split A+B on a fresh key" bash -c '
  for i in {1..25}; do
    curl -fsS -o /dev/null "http://localhost:'"$PORT_A"''"$ENDPOINT_T2"'" &
    curl -fsS -o /dev/null "http://localhost:'"$PORT_B"''"$ENDPOINT_T2"'" &
  done
  wait
'

echo
echo "================================================================"
echo " Logs for manual inspection:"
echo "   Task A: /tmp/gatool-a.log"
echo "   Task B: /tmp/gatool-b.log"
echo "   MONITOR stream: /tmp/gatool-monitor.log"
echo "================================================================"
echo
echo "What to look for:"
echo "  * TEST 1: 2 HMSETs (1 attribute layer + 1 service layer for the cold key)."
echo "           B's read should be served from L2 — no additional HMSETs."
echo "  * TEST 2: 2 HMSETs total despite 50 concurrent requests across 2 processes"
echo "           — that's strict cross-process single-flight in action."
echo "  * Both task logs should show GetOrSetAsync<T> returning the SAME T= timestamp"
echo "    across A and B for the same key — that's the proof L2 is shared."
