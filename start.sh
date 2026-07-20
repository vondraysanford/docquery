#!/usr/bin/env bash
# ============================================================================
# start.sh — run the full DocQuery dev stack with one command
#
# Starts the .NET backend API and the React (Vite) frontend together in a
# single terminal, then waits. Press Ctrl+C once to stop both.
#
# Prerequisites (see README "Getting Started"):
#   - .NET 10 SDK and Node.js 22+ on your PATH
#   - UI dependencies installed:  cd src/docquery-ui && npm install
#   - For working uploads/queries: ChromaDB (Docker) and Ollama running
# ============================================================================

# Fail fast: -e exits on any error, -u errors on undefined variables,
# -o pipefail makes a pipeline fail if any command in it fails.
set -euo pipefail

# Resolve the repo root from this script's own location, so the script works
# no matter which directory it is invoked from (./start.sh, ../start.sh, etc.).
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# ----------------------------------------------------------------------------
# 1. Preflight: required tooling
#    Hard failures — the stack cannot start without these, so exit with a
#    message that says exactly what to install.
# ----------------------------------------------------------------------------
command -v dotnet >/dev/null 2>&1 || { echo "error: dotnet not found — install the .NET 10 SDK"; exit 1; }
command -v npm >/dev/null 2>&1 || { echo "error: npm not found — install Node.js 22 LTS or newer"; exit 1; }

# The UI can't start without its node_modules. Deliberately NOT auto-running
# npm install here: downloading dependencies should be an explicit user action.
if [ ! -d "$ROOT/src/docquery-ui/node_modules" ]; then
  echo "error: UI dependencies are not installed yet. Run:"
  echo "  cd src/docquery-ui && npm install"
  exit 1
fi

# ----------------------------------------------------------------------------
# 2. Preflight: inference services
#    Soft failures — the API and UI boot fine without Ollama/ChromaDB, but
#    uploads and queries will error. Warn now so the cause is obvious later.
# ----------------------------------------------------------------------------
curl -s --max-time 2 http://localhost:11434/api/version >/dev/null 2>&1 \
  || echo "warning: Ollama not reachable on :11434 — start Ollama first"
curl -s --max-time 2 http://localhost:8000/api/v2/heartbeat >/dev/null 2>&1 \
  || echo "warning: ChromaDB not reachable on :8000 — run: docker run -d -p 8000:8000 chromadb/chroma"

# ----------------------------------------------------------------------------
# 3. Shutdown handling
#    Both apps run as background jobs of this script. On Ctrl+C (INT) or
#    termination (TERM), 'kill 0' signals every process in this script's
#    process group — API, UI, and the script itself — so nothing is orphaned.
# ----------------------------------------------------------------------------
trap 'echo; echo "Stopping DocQuery..."; kill 0' INT TERM

# ----------------------------------------------------------------------------
# 4. Start both apps as background jobs ('&')
#    The UI command runs in a subshell '(...)' so its 'cd' does not affect
#    the rest of the script. Vite serves on :3000 and proxies /api calls to
#    the backend on :5000 (configured in src/docquery-ui/vite.config.js).
# ----------------------------------------------------------------------------
echo "Starting API (http://localhost:5000)..."
dotnet run --project "$ROOT/src/DocQuery.Api" &

echo "Starting UI (http://localhost:3000)..."
(cd "$ROOT/src/docquery-ui" && npm start) &

# ----------------------------------------------------------------------------
# 5. Readiness banner
#    Poll the API's health endpoint (up to 30s) and print the URLs once it
#    responds, so the user knows the stack is actually ready — not just
#    starting. If it never comes up, the API's own error output will show why.
#    -f makes curl fail on HTTP errors: macOS AirPlay also answers on :5000
#    (with a 403), and only a real 200 from /health should count as ready.
# ----------------------------------------------------------------------------
for _ in $(seq 1 30); do
  if curl -sf --max-time 2 http://localhost:5000/health >/dev/null 2>&1; then
    echo
    echo "DocQuery is up:"
    echo "  UI:  http://localhost:3000"
    echo "  API: http://localhost:5000 (check /health)"
    echo "Press Ctrl+C to stop both."
    break
  fi
  sleep 1
done

# ----------------------------------------------------------------------------
# 6. Keep the script alive
#    'wait' blocks until the background jobs exit, keeping both apps in the
#    foreground of this terminal so their logs stream here and Ctrl+C works.
# ----------------------------------------------------------------------------
wait