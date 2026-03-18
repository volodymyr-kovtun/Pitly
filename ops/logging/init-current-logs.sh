#!/bin/sh
set -eu

LOG_DIR="${LOG_DIR:-/var/log/pitly}"

mkdir -p "$LOG_DIR"
chmod 1777 "$LOG_DIR"

for file in \
  backend.current.txt \
  frontend-access.current.txt \
  frontend-error.current.txt
do
  touch "$LOG_DIR/$file"
  chmod 0666 "$LOG_DIR/$file"
done
