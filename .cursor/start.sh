#!/usr/bin/env bash
# Per-boot services for Cloud Agents. Idempotent. Does not install packages.
set -euo pipefail

if ! command -v pg_isready >/dev/null 2>&1; then
  echo "PostgreSQL is not installed. Run bash .cursor/install.sh first." >&2
  exit 1
fi

if ! pg_isready -q; then
  sudo service postgresql start
fi

ready=0
for _ in $(seq 1 30); do
  if pg_isready -q; then
    ready=1
    break
  fi
  sleep 1
done

if [ "$ready" -ne 1 ]; then
  echo "PostgreSQL did not become ready." >&2
  exit 1
fi

# Local-only role and database. Password matches /etc/profile.d/budget-board-dev.sh.
sudo -u postgres psql -v ON_ERROR_STOP=1 <<'SQL'
DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'budgetboard') THEN
    CREATE ROLE budgetboard LOGIN PASSWORD 'budgetboard';
  ELSE
    ALTER ROLE budgetboard WITH LOGIN PASSWORD 'budgetboard';
  END IF;
END
$$;
SQL

if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='budgetboard'" | grep -q 1; then
  sudo -u postgres psql -v ON_ERROR_STOP=1 -c "CREATE DATABASE budgetboard OWNER budgetboard;"
fi

sudo -u postgres psql -v ON_ERROR_STOP=1 -d budgetboard -c "GRANT ALL ON SCHEMA public TO budgetboard;"

echo "PostgreSQL is ready (database budgetboard on localhost:5432)."
