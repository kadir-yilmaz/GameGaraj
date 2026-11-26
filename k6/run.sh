#!/usr/bin/env bash
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
mkdir -p "$SCRIPT_DIR/results"

# k6 esik (threshold) asilirsa exit code 99 doner; yine de raporu toplamak icin set -e kullanmiyoruz
K6_WEB_DASHBOARD="${K6_WEB_DASHBOARD:-true}" \
K6_WEB_DASHBOARD_PERIOD="${K6_WEB_DASHBOARD_PERIOD:-1s}" \
K6_WEB_DASHBOARD_EXPORT="${K6_WEB_DASHBOARD_EXPORT:-$SCRIPT_DIR/report.html}" \
k6 run "$@" "$SCRIPT_DIR/loadtest.js"
K6_EXIT=$?

PYTHON_BIN="$REPO_ROOT/locust/.venv/bin/python"
if [ ! -x "$PYTHON_BIN" ]; then
  PYTHON_BIN="python3"
fi

"$PYTHON_BIN" "$SCRIPT_DIR/collect_report.py"
exit $K6_EXIT
