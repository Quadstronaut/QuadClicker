#!/bin/bash
# QCLK-8 F31: the CLI must exit on Ctrl+C within 2 s with the documented code 130.
# --idle-wait is huge so the run waits and sends no clicks while we signal it.
set -u
BIN=$(find macos/build -path '*Release*' -path '*MacOS/QuadClicker' -type f | head -1)
[ -n "$BIN" ] || { echo "FAIL: CLI binary not found"; exit 1; }
echo "binary: $BIN"

"$BIN" --rate 1/s --idle-wait 1000000000 > /tmp/qclk-cli.log 2>&1 &
pid=$!
sleep 4
kill -0 "$pid" 2>/dev/null || { echo "FAIL: CLI exited before SIGINT"; cat /tmp/qclk-cli.log; exit 1; }

kill -INT "$pid"
for i in $(seq 1 20); do                      # up to 2.0 s
  kill -0 "$pid" 2>/dev/null || break
  sleep 0.1
done
if kill -0 "$pid" 2>/dev/null; then
  echo "FAIL: CLI still running 2 s after SIGINT (deadlock?)"; kill -9 "$pid"; cat /tmp/qclk-cli.log; exit 1
fi
wait "$pid"; code=$?
cat /tmp/qclk-cli.log
[ "$code" -eq 130 ] || { echo "FAIL: exit code $code, expected 130"; exit 1; }
echo "PASS F31: exited $code within 2 s of SIGINT"
