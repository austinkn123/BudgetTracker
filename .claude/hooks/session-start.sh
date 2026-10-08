#!/bin/bash
# SessionStart hook: provisions Claude Code cloud sessions with the full local toolchain.
#   .NET SDK (9, or 10 as fallback) + dotnet-ef, PowerShell (for .claude/scripts/*.ps1 hooks), sqlcmd,
#   client npm deps, and a SQL Server 2022 container with EF migrations applied.
# No-op outside cloud sessions, so local Windows development is unaffected.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "$0")/../.." && pwd)}"
DOTNET_DIR="/usr/share/dotnet"
SQL_CONTAINER="budgettracker-sql"
SQL_IMAGE="mcr.microsoft.com/mssql/server:2022-latest"
# Throwaway dev-only password for an ephemeral container bound to localhost.
SQL_SA_PASSWORD="BudgetTracker!Dev1"
CONNECTION_STRING="Server=localhost,1433;Database=BudgetTracker;User Id=sa;Password=${SQL_SA_PASSWORD};TrustServerCertificate=True;"

log() { echo "[session-start] $*" >&2; }

export DEBIAN_FRONTEND=noninteractive
export DOTNET_ROOT="$DOTNET_DIR"
export PATH="$DOTNET_DIR:$HOME/.dotnet/tools:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# --- Microsoft apt repo (PowerShell, sqlcmd) ---------------------------------
if [ ! -f /etc/apt/sources.list.d/microsoft-prod.list ]; then
  log "Adding Microsoft apt repository"
  . /etc/os-release
  curl -fsSL "https://packages.microsoft.com/config/ubuntu/${VERSION_ID}/packages-microsoft-prod.deb" -o /tmp/packages-microsoft-prod.deb
  dpkg -i /tmp/packages-microsoft-prod.deb >/dev/null
  rm -f /tmp/packages-microsoft-prod.deb
  apt-get update -qq
fi

if ! command -v pwsh >/dev/null 2>&1; then
  log "Installing PowerShell"
  apt-get install -y -qq powershell >/dev/null
fi
# Hooks in .claude/settings.json invoke `powershell` (Windows name).
ln -sf "$(command -v pwsh)" /usr/local/bin/powershell

# sqlcmd: the mssql-tools apt package is blocked by the network policy, so wrap the
# copy bundled in the SQL Server container (pre-authenticated as sa, cert trusted).
cat > /usr/local/bin/sqlcmd <<EOF
#!/bin/bash
exec docker exec -i -e SQLCMDSERVER=localhost -e SQLCMDUSER=sa -e 'SQLCMDPASSWORD=${SQL_SA_PASSWORD}' \\
  ${SQL_CONTAINER} /opt/mssql-tools18/bin/sqlcmd -C "\$@"
EOF
chmod +x /usr/local/bin/sqlcmd

# --- .NET SDK + dotnet-ef ----------------------------------------------------
# Prefer the .NET 9 SDK. Its binaries come from builds.dotnet.microsoft.com, which the
# environment's network policy may block; if so, fall back to Ubuntu's .NET 10 SDK,
# which builds net9.0 targets, and roll the 9.0 apps/tests forward onto the 10 runtime.
ROLL_FORWARD=""
if ! "$DOTNET_DIR/dotnet" --list-sdks 2>/dev/null | grep -q '^9\.'; then
  log "Installing .NET 9 SDK"
  if ! { curl -fsSL https://raw.githubusercontent.com/dotnet/install-scripts/main/src/dotnet-install.sh -o /tmp/dotnet-install.sh \
         && bash /tmp/dotnet-install.sh --channel 9.0 --install-dir "$DOTNET_DIR" >/dev/null 2>&1; }; then
    log ".NET 9 download blocked; using Ubuntu's .NET 10 SDK with DOTNET_ROLL_FORWARD=Major"
    DOTNET_DIR="/usr/lib/dotnet"
    ROLL_FORWARD="Major"
    [ -x "$DOTNET_DIR/dotnet" ] || apt-get install -y -qq dotnet-sdk-10.0 >/dev/null
  fi
  rm -f /tmp/dotnet-install.sh
fi
export DOTNET_ROOT="$DOTNET_DIR"
export PATH="$DOTNET_DIR:$PATH"
[ -n "$ROLL_FORWARD" ] && export DOTNET_ROLL_FORWARD="$ROLL_FORWARD"
ln -sf "$DOTNET_DIR/dotnet" /usr/local/bin/dotnet

if ! dotnet tool list -g | grep -q '^dotnet-ef '; then
  log "Installing dotnet-ef"
  dotnet tool install -g dotnet-ef --version '9.*' >/dev/null
fi

# --- Dependencies -------------------------------------------------------------
log "Restoring .NET packages"
dotnet restore "$PROJECT_DIR/BudgetTracker.sln" >/dev/null

log "Installing client npm packages"
(cd "$PROJECT_DIR/budgettracker.client" && npm install --no-audit --no-fund >/dev/null)

# --- SQL Server in Docker -----------------------------------------------------
if ! docker info >/dev/null 2>&1; then
  log "Starting Docker daemon"
  nohup dockerd >/tmp/dockerd.log 2>&1 &
  for _ in $(seq 1 30); do docker info >/dev/null 2>&1 && break; sleep 1; done
  docker info >/dev/null 2>&1 || { log "Docker daemon failed to start (see /tmp/dockerd.log)"; exit 1; }
fi

if [ -z "$(docker ps -aq -f name="^${SQL_CONTAINER}$")" ]; then
  log "Creating SQL Server container (first run pulls ~1.5GB)"
  docker run -d --name "$SQL_CONTAINER" \
    -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=${SQL_SA_PASSWORD}" \
    -p 127.0.0.1:1433:1433 "$SQL_IMAGE" >/dev/null
else
  docker start "$SQL_CONTAINER" >/dev/null
fi

log "Waiting for SQL Server"
for _ in $(seq 1 60); do
  sqlcmd -Q "SELECT 1" >/dev/null 2>&1 && break
  sleep 2
done
sqlcmd -Q "SELECT 1" >/dev/null 2>&1 \
  || { log "SQL Server did not become ready"; docker logs --tail 20 "$SQL_CONTAINER" >&2; exit 1; }

log "Applying EF migrations"
ConnectionStrings__BudgetTrackerConnection="$CONNECTION_STRING" \
  dotnet ef database update \
    --project "$PROJECT_DIR/BudgetTracker.Domain" \
    --startup-project "$PROJECT_DIR/BudgetTracker.Server" >/dev/null

# --- Persist environment for the session --------------------------------------
if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  {
    echo "export DOTNET_ROOT=\"$DOTNET_DIR\""
    echo "export PATH=\"$DOTNET_DIR:\$HOME/.dotnet/tools:\$PATH\""
    echo "export DOTNET_CLI_TELEMETRY_OPTOUT=1"
    echo "export DOTNET_NOLOGO=1"
    [ -n "$ROLL_FORWARD" ] && echo "export DOTNET_ROLL_FORWARD=$ROLL_FORWARD"
    # Overrides the LocalDB connection string in appsettings*.json.
    echo "export ConnectionStrings__BudgetTrackerConnection=\"$CONNECTION_STRING\""
  } >> "$CLAUDE_ENV_FILE"
fi

log "Done"
