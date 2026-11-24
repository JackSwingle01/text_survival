# Baselines

The nightly run's measurements of `main` (`.github/workflows/nightly.yml`), committed by
`.github/scripts/publish_baselines.sh`. Compare a branch with these instead of measuring `main` again.

- `npc-bench.txt`: the NPC bench (`NpcBenchReport`), one line a seed.
- `cohesion-gate.txt`: what the cohesion gate printed (`CohesionGateSeed*Tests`), by seed.

Each file's first line names the commit and day it was measured. The branch's history is the trend.
