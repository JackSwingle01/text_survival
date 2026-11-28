# The rebuild on Render, and the start-up crash in the browser

## Hypothesis

The rebuild is static (Sim in a wasm worker, no server), so a Render static site can serve
`publish.sh`'s `wwwroot` as it is. Render's build image has no .NET: a build script installs the SDK
from `global.json` and the `wasm-tools` workload into the checkout, then publishes AOT. A clean
checkout here builds in about 3.5 min on 4 cores.

## Log

- Revised: the published page never started on main: `TypeInitialization_Type, Survival.Shelters`. A
  build with `UseSystemResourceKeys=false` and the exception's full text showed the cycle: `Map` ->
  `Content` -> `Content.ItemProblems` calls `Items.CanBeWornTo` (#143) -> `Items`' initializer reads
  `Content.Tables`, still null. CoreCLR runs a beforefieldinit initializer only on a field read, so native
  tests pass; the wasm runtime runs it on the first static method call. Fix: `CanBeWornTo` moves onto
  `GearDef`, so content validation never touches `Items`.
- Revised: Render's API refused a build command that downloads with `curl` inline (403 from a firewall);
  the command is a script in the repo instead (`rebuild/Web/tools/render-build.sh`).
- Fast suite: `NpcNeedTests.SticksOnly_WaterCritical_ItDoesNotPace` fails on main without this change.
- Follow-up: nothing checks that the published page starts. A nightly job that publishes (interpreter is
  enough, about 15 s plus the workload) and loads the page headless would have caught this the day #143 merged.
