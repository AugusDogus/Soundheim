#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

: "${MANAGED_DIR:?Set MANAGED_DIR to Valheim's Managed directory.}"
: "${BEPINEX_DIR:?Set BEPINEX_DIR to your profile's BepInEx directory.}"
export MANAGED_DIR
dotnet test tests/Soundheim.Tests/Soundheim.Tests.csproj -c Release \
  -p:ManagedDir="$MANAGED_DIR" -p:BepInExDir="$BEPINEX_DIR"
