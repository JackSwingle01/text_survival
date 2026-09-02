namespace text_survival.Tests.Support;

/// <summary>
/// An xunit fact that only runs when the NPC_SIM environment variable is set. These runs
/// simulate thousands of in-game minutes and are not deterministic (several unseeded
/// <see cref="Random"/> instances feed world generation and NPC decisions), so they stay
/// out of the default `dotnet test` / CI run and are invoked explicitly:
///
///   NPC_SIM=1 dotnet test --filter "FullyQualifiedName~NPCSimulation"
/// </summary>
public sealed class SimulationFactAttribute : FactAttribute
{
    public SimulationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("NPC_SIM") != "1")
            Skip = "Set NPC_SIM=1 to run NPC simulation tests (slow, non-deterministic; see documentation/npc-simulation-plan.md).";
    }
}
