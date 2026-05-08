#!/usr/bin/env sh
set -u

PORT=38111
BASE_URL=
CHECK_DOCKER=0
SERVICE=dacollector
FAILED=0

usage() {
  cat <<'EOF'
Usage: ./scripts/verify-install.sh [options]

Options:
  --port PORT          Local host port to verify. Defaults to 38111.
  --base-url URL       Full base URL to verify. Overrides --port.
  --docker            Also check the Docker Compose service status.
  --service NAME      Docker Compose service name. Defaults to dacollector.
  -h, --help          Show this help.
EOF
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --port)
      PORT="${2:-}"
      shift 2
      ;;
    --base-url)
      BASE_URL="${2:-}"
      shift 2
      ;;
    --docker)
      CHECK_DOCKER=1
      shift
      ;;
    --service)
      SERVICE="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage
      exit 2
      ;;
  esac
done

if [ -z "$BASE_URL" ]; then
  BASE_URL="http://127.0.0.1:$PORT"
fi
BASE_URL="${BASE_URL%/}"

fail() {
  FAILED=$((FAILED + 1))
  printf '[FAIL] %s\n' "$1" >&2
}

pass() {
  printf '[ OK ] %s\n' "$1"
}

warn() {
  printf '[WARN] %s\n' "$1"
}

check_endpoint() {
  NAME="$1"
  PATH_PART="$2"
  EXPECTED="$3"
  TMP_BODY="/tmp/dacollector-verify-body.$$"
  TMP_ERR="/tmp/dacollector-verify-err.$$"
  URL="$BASE_URL$PATH_PART"

  CODE="$(curl -sS -o "$TMP_BODY" -w '%{http_code}' "$URL" 2>"$TMP_ERR")"
  CURL_STATUS=$?

  if [ "$CURL_STATUS" -ne 0 ]; then
    fail "$NAME could not connect to $URL"
    sed 's/^/       /' "$TMP_ERR" >&2
    rm -f "$TMP_BODY" "$TMP_ERR"
    return
  fi

  if [ "$CODE" != "$EXPECTED" ]; then
    fail "$NAME returned HTTP $CODE, expected $EXPECTED"
    head -c 500 "$TMP_BODY" | sed 's/^/       /' >&2
    printf '\n' >&2
    rm -f "$TMP_BODY" "$TMP_ERR"
    return
  fi

  pass "$NAME returned HTTP $CODE"
  rm -f "$TMP_BODY" "$TMP_ERR"
}

check_docker() {
  if ! command -v docker >/dev/null 2>&1; then
    fail "docker command was not found"
    return
  fi

  CID="$(docker compose ps -q "$SERVICE" 2>/dev/null || true)"
  if [ -z "$CID" ]; then
    fail "Docker Compose service '$SERVICE' was not found"
    return
  fi

  STATUS="$(docker inspect -f '{{.State.Status}}' "$CID" 2>/dev/null || true)"
  HEALTH="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "$CID" 2>/dev/null || true)"

  if [ "$STATUS" != "running" ]; then
    fail "Docker container status is '$STATUS'"
    docker compose logs --tail 120 "$SERVICE" 2>/dev/null || true
    return
  fi

  pass "Docker container is running"

  case "$HEALTH" in
    healthy)
      pass "Docker healthcheck is healthy"
      ;;
    starting)
      warn "Docker healthcheck is still starting"
      ;;
    none)
      warn "Docker healthcheck is not configured"
      ;;
    *)
      fail "Docker healthcheck is '$HEALTH'"
      docker compose logs --tail 120 "$SERVICE" 2>/dev/null || true
      ;;
  esac
}

printf 'Verifying DaCollector at %s\n' "$BASE_URL"

check_endpoint "Startup API" "/api/v3/Init/Status" "200"
check_endpoint "Web UI" "/webui" "200"

if [ "$CHECK_DOCKER" -eq 1 ]; then
  check_docker
fi

if [ "$FAILED" -gt 0 ]; then
  printf '%s check(s) failed. See docs/getting-started/verify-install.md for troubleshooting.\n' "$FAILED" >&2
  exit 1
fi

printf 'DaCollector install verification passed.\n'
