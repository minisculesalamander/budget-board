#!/usr/bin/env bash
# Idempotent Cloud Agent bootstrap for this fork. Safe to run more than once.
# Installs toolchains and project dependencies. Does not start long-running services.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [ ! -f "${ROOT}/server/BudgetBoard.sln" ]; then
  ROOT="/workspace"
fi
cd "$ROOT"

export DEBIAN_FRONTEND=noninteractive
export DOTNET_ROOT=/usr/local/share/dotnet
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export NUGET_XMLDOC_MODE=skip
export PATH="${DOTNET_ROOT}:${PATH}"

if ! command -v node >/dev/null 2>&1; then
  echo "node is required on PATH (Node.js 22+)." >&2
  exit 1
fi

if ! command -v corepack >/dev/null 2>&1; then
  echo "corepack is required so Yarn can be pinned to the version in client/package.json." >&2
  exit 1
fi

echo "Activating Yarn 4.18.0 via corepack"
corepack enable
corepack prepare yarn@4.18.0 --activate

# A preinstalled Yarn 1 can sit earlier on PATH than /usr/local/bin. Keep the
# pinned Yarn on a directory that profile.d places first.
YARN_BIN="$(command -v yarn)"
sudo mkdir -p /usr/local/lib/budget-board/bin
sudo ln -sfn "$YARN_BIN" /usr/local/lib/budget-board/bin/yarn
export PATH="/usr/local/lib/budget-board/bin:${PATH}"

if ! "${DOTNET_ROOT}/dotnet" --list-sdks 2>/dev/null | grep -q '^10\.'; then
  echo "Installing .NET 10 SDK into ${DOTNET_ROOT}"
  sudo mkdir -p "${DOTNET_ROOT}"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  sudo bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "${DOTNET_ROOT}"
  rm -f /tmp/dotnet-install.sh
fi

sudo tee /usr/local/bin/dotnet >/dev/null <<'EOF'
#!/bin/sh
export DOTNET_ROOT=/usr/local/share/dotnet
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export PATH="${DOTNET_ROOT}:${PATH}"
exec /usr/local/share/dotnet/dotnet "$@"
EOF
sudo chmod 755 /usr/local/bin/dotnet

if ! command -v psql >/dev/null 2>&1; then
  echo "Installing PostgreSQL"
  sudo apt-get update
  sudo apt-get install -y postgresql postgresql-contrib
fi

# Local-only defaults for the agent VM. Postgres listens on localhost.
# This is not a production password. Do not reuse it on a shared host.
sudo tee /etc/profile.d/budget-board-dev.sh >/dev/null <<'EOF'
export DOTNET_ROOT=/usr/local/share/dotnet
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export PATH="/usr/local/lib/budget-board/bin:${DOTNET_ROOT}:${PATH}"
export CLIENT_ADDRESS="${CLIENT_ADDRESS:-http://localhost:5173}"
export POSTGRES_HOST="${POSTGRES_HOST:-localhost}"
export POSTGRES_PORT="${POSTGRES_PORT:-5432}"
export POSTGRES_DATABASE="${POSTGRES_DATABASE:-budgetboard}"
export POSTGRES_USER="${POSTGRES_USER:-budgetboard}"
export POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-budgetboard}"
export AUTO_UPDATE_DB="${AUTO_UPDATE_DB:-true}"
EOF
sudo chmod 644 /etc/profile.d/budget-board-dev.sh

if [ ! -f "${ROOT}/client/.env.development" ]; then
  cat > "${ROOT}/client/.env.development" <<'EOF'
VITE_BUDGET_BOARD_DOMAIN=localhost:5293
VITE_OIDC_ENABLED=false
VITE_DISABLE_LOCAL_AUTH=false
VITE_DISABLE_NEW_USERS=false
VITE_DEMO_MODE=false
EOF
fi

echo "Restoring .NET solution"
(cd "${ROOT}/server" && dotnet restore BudgetBoard.sln)

echo "Installing client dependencies"
(cd "${ROOT}/client" && yarn install --immutable)

echo "Install complete"
dotnet --version
yarn --version
psql --version
