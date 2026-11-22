#!/usr/bin/env bash
# Publishes the web app (AOT unless -p:Aot=false is passed) to OUT, ready to serve from OUT/wwwroot.
# Needs a .NET SDK with the wasm-tools workload: set DOTNET_ROOT to it (README).
#   rebuild/Web/tools/publish.sh [OUT] [extra dotnet publish args...]
set -euo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"
out="${1:-/tmp/text-survival-web}"; shift || true
dotnet="${DOTNET_ROOT:+$DOTNET_ROOT/}dotnet"
"$dotnet" publish "$here/App/App.csproj" -c Release -o "$out" "$@"
echo "published: $out/wwwroot"
