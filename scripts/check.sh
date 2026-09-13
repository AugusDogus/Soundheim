#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet test tests/Soundheim.Tests/Soundheim.Tests.csproj -c Release \
  -p:ManagedDir="${MANAGED_DIR:-$HOME/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed}"
