#!/bin/sh
set -eu

PGDATA="${PGDATA:-/var/lib/postgresql/data}"
PRIMARY_HOST="${PRIMARY_HOST:-db_primary}"
REPLICATION_USER="${REPLICATION_USER:-replicator}"
REPLICATION_PASSWORD="${REPLICATION_PASSWORD:-replicator}"
APP_USER="${POSTGRES_USER:-test_project_db}"
APP_DB="${POSTGRES_DB:-test_project_db}"

echo "Waiting for Primary ${PRIMARY_HOST}..."
until pg_isready -h "$PRIMARY_HOST" -U "$APP_USER" -d "$APP_DB" >/dev/null 2>&1; do
  sleep 2
done
sleep 5

if [ ! -s "$PGDATA/PG_VERSION" ]; then
  echo "Taking base backup from Primary..."
  # очищаем каталог (может содержать пустые mount-файлы)
  find "$PGDATA" -mindepth 1 -delete 2>/dev/null || rm -rf "$PGDATA"/*
  export PGPASSWORD="$REPLICATION_PASSWORD"
  pg_basebackup \
    -h "$PRIMARY_HOST" \
    -D "$PGDATA" \
    -U "$REPLICATION_USER" \
    -Fp -Xs -P -R
  unset PGPASSWORD
  echo "Base backup complete."
fi

exec docker-entrypoint.sh postgres \
  -c hot_standby=on \
  -c hot_standby_feedback=on
