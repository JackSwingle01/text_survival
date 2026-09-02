# npcsim

Batch bench for the autonomous NPC survival AI. Runs the real game headlessly — same
`GameContext.CreateNewGame`, same world, same simulation tick — with the player inert, and
reports what the NPCs did.

This was previously a set of xunit tests gated behind `NPC_SIM=1`. That was the wrong
shape: they asserted nothing, took ~2.7 minutes, and had to run serially because the
harness redirected `Console.Out`. It is an experiment bench, so it lives here next to
`PixelArtCli` and emits CSV you can diff.

## Usage

```bash
dotnet run --project tools/NpcSim -c Release -- run --seeds 1-10 --days 7 --out before.csv
# ... change the AI ...
dotnet run --project tools/NpcSim -c Release -- run --seeds 1-10 --days 7 --out after.csv
diff before.csv after.csv
```

Same world layout and RNG sequence both times, so any difference is the change, not luck.

`npcsim verify` runs the same seeds three times and exits non-zero if they diverge. Run it
after adding anything random: a single unseeded draw makes every comparison meaningless.
Everything random must go through `Utils.Rng`.

## Speed

A run is dominated by the simulation tick (~80%), not world generation (~20%), and the tick
is allocation-bound — a fresh `SurvivalContext` per actor per minute. Release and Debug come
out within ~7% of each other for that reason.

**In-process `--parallel` is not reproducible yet and defaults to 1.** `Utils.Rng`, the
event-cooldown table, `NPC.FuelStockpileTargetKg` and `EnvironmentalDetail`'s id counter are
all `[ThreadStatic]`, but something else is still shared: at `--parallel 14` the same seed
produces different outcomes run to run. Until that is found, do not use it for comparisons.

To go faster today, **shard across processes** — separate processes get separate statics,
and this is verified byte-identical to a serial run:

```bash
for s in 1-3 4-6 7-9 10-12; do
  dotnet run --project tools/NpcSim -c Release --no-build -- \
    run --seeds $s --days 7 --out shard_$s.csv &
done
wait
```

12 seeds × 7 days: **7.1s serial → 4.4s across 4 processes** (including ~0.6s of dotnet
startup per process). Measured on 14 cores.
