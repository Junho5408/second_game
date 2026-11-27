#!/usr/bin/env bash
# Run engine-free Core tests with dotnet.
# Usage: verify_core.sh [GameName]   (no argument = every Games/*/Core.Tests)
set -u
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
if ! command -v dotnet >/dev/null 2>&1 && [ -x "$HOME/.dotnet/dotnet" ]; then
  export PATH="$HOME/.dotnet:$PATH"
fi
command -v dotnet >/dev/null 2>&1 || { echo "dotnet not found (install .NET 8 SDK)"; exit 2; }

if [ $# -ge 1 ]; then
  projects=("$ROOT/Games/$1/Core.Tests")
else
  projects=("$ROOT"/Games/*/Core.Tests)
fi

status=0
for p in "${projects[@]}"; do
  [ -d "$p" ] || { echo "missing: $p"; status=1; continue; }
  game="$(basename "$(dirname "$p")")"
  echo "== $game =="
  if dotnet test "$p" --nologo -v quiet; then
    echo "== $game: PASS =="
  else
    echo "== $game: FAIL =="
    status=1
  fi
done
exit $status
