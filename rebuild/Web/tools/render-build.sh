#!/usr/bin/env bash
# The build command of the Render static site text-survival-rebuild (publish path web-out/wwwroot),
# run from the repo root on every push to main. Render's build image has no .NET, so this installs the
# SDK global.json names and the wasm-tools workload into the checkout, then publishes the AOT build.
set -euo pipefail
export DOTNET_ROOT="$PWD/.dotnet" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
sdk="$(sed -n 's/.*"version": *"\([^"]*\)".*/\1/p' global.json)"
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --version "$sdk" --install-dir "$DOTNET_ROOT"
"$DOTNET_ROOT/dotnet" workload install wasm-tools
bash rebuild/Web/tools/publish.sh "$PWD/web-out"
