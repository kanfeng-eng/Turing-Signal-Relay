#!/usr/bin/env bash
set -euo pipefail
[[ $EUID -eq 0 ]] || { echo 'Run this project recovery check with sudo.' >&2; exit 1; }
submission="${1:?Pass a ready submission ID}"
output="${2:?Pass a new JSON report path}"
[[ ! -e "$output" ]] || { echo 'Use a new report path.' >&2; exit 1; }
python3 - <<'PY'
import sqlite3
with sqlite3.connect('/var/lib/tsr/data/contest.db') as database:
    assert database.execute("SELECT COUNT(*) FROM Submissions WHERE Status IN ('queued','compiling')").fetchone()[0] == 0, 'Wait for the compilation queue to become idle'
    assert database.execute("SELECT COUNT(*) FROM Matches WHERE Status IN ('queued','running')").fetchone()[0] == 0, 'Wait for the match queue to become idle'
PY
token="$(python3 -c 'import uuid; print(uuid.uuid4().hex)')"
helper=/usr/local/libexec/tsr-sandbox
trap '"$helper" release "$token"' EXIT
"$helper" prepare "$token" "$submission"
systemctl restart tsr-worker
for attempt in {1..30}; do
    if systemctl is-active --quiet tsr-worker && [[ ! -e "/var/lib/tsr-sandbox/runs/$token" && ! -e "/sys/fs/cgroup/tsr/$token" ]]; then
        python3 - "$output" <<'PY'
import json
from pathlib import Path
import sys
with Path(sys.argv[1]).open('x') as report:
    json.dump({'status': 'passed', 'preparedBeforeLauncher': True, 'runDirectoryRemoved': True, 'cgroupRemoved': True}, report, indent=2)
    report.write('\n')
PY
        echo PREPARED_SANDBOX_RECOVERY_CHECKS_PASSED
        exit 0
    fi
    sleep 1
done
echo 'Worker did not reclaim the prepared sandbox.' >&2
exit 1
