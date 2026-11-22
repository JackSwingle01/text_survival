# Web.Spike: THROWAWAY

A spike, not product code. Delete it once the hosting decision is made. Nothing in `rebuild/Sim` or
`rebuild/Sim.Tests` depends on it; it is not in any solution and nothing builds it in CI.

The question it answers: can `rebuild/Sim` run in the browser through .NET WebAssembly fast enough for
the web UI, and what does a UI/game boundary that could later move to a server look like? See
[REPORT.md](REPORT.md) for the answer.

- `Host/`: `GameHost`, the boundary (NewGame, Act, View, Save, Load; JSON views, bytes for saves), and
  `Bench`, the measurements. A plain `net10.0` class library, so the native bench and the wasm app
  run the same code.
- `Web/`: the `wasmbrowser` app. `Program.cs` is a JSExport shim over `GameHost`; `wwwroot/` is a
  minimal page (canvas map with `assets/icons`, clock, vitals, a few actions, save/load).
- `Bench/`: native console that runs `Bench.RunAll` for comparison.
- `tools/serve.mjs`: static server that serves the precompressed `.br` files.
  `tools/measure.mjs`: drives headless Chrome over CDP, screenshots, clicks through the page, runs
  `?bench`.

## Running it

The wasm build needs the `wasm-tools` workload. The system SDK here could not install it without
root, so the spike used a private SDK:

```bash
curl -sSL -o /tmp/dotnet-install.sh https://dot.net/v1/dotnet-install.sh
bash /tmp/dotnet-install.sh --version 10.0.400 --install-dir /tmp/wasmspike/dotnet
export DOTNET_ROOT=/tmp/wasmspike/dotnet
$DOTNET_ROOT/dotnet workload install wasm-tools

cd rebuild/Web.Spike/Web
$DOTNET_ROOT/dotnet publish -c Release -o /tmp/wasmspike/pub-interp              # interpreter
$DOTNET_ROOT/dotnet publish -c Release -p:Aot=true -o /tmp/wasmspike/pub-aot     # AOT

node ../tools/serve.mjs /tmp/wasmspike/pub-aot/wwwroot 8182 &
open http://localhost:8182/            # play; add ?bench to run the measurements
node ../tools/measure.mjs "http://localhost:8182/?bench" /tmp/wasmspike/aot bench

dotnet run --project ../Bench -c Release -- 1 30                                 # native
```
