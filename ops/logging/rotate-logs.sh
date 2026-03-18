#!/bin/sh
set -eu

LOG_DIR="${LOG_DIR:-/var/log/pitly}"
LOG_RETENTION_DAYS="${LOG_RETENTION_DAYS:-14}"
LOG_ROTATION_CHECK_INTERVAL_SECONDS="${LOG_ROTATION_CHECK_INTERVAL_SECONDS:-60}"

mkdir -p "$LOG_DIR"

current_day="$(date -u +%Y-%m-%d)"

rotate_logs() {
  timestamp="$(date -u +%Y%m%dT%H%M%SZ)"

  for file in "$LOG_DIR"/*.current.txt; do
    [ -f "$file" ] || continue

    if [ -s "$file" ]; then
      base_name="$(basename "$file" .current.txt)"
      cp "$file" "$LOG_DIR/${base_name}-${timestamp}.txt"
      : > "$file"
    fi
  done
}

prune_old_logs() {
  find "$LOG_DIR" -type f -name '*.txt' ! -name '*.current.txt' -mtime +"$LOG_RETENTION_DAYS" -delete
}

prune_old_logs

while true; do
  day_now="$(date -u +%Y-%m-%d)"

  if [ "$day_now" != "$current_day" ]; then
    rotate_logs
    prune_old_logs
    current_day="$day_now"
  fi

  sleep "$LOG_ROTATION_CHECK_INTERVAL_SECONDS"
done
