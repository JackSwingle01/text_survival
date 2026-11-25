using Survival;
using Xunit;
using Xunit.Abstractions;

/// The valley's predators over 60 days on one seed: hunts (an encounter a predator's side ends, once a minute and tile), kills
/// of quarry, and their own deaths, in a fight or not. It reports and never gates; it runs only with HUNT_VALLEY set, its seed
/// from HUNT_VALLEY_SEED (17 by default):
/// HUNT_VALLEY=1 HUNT_VALLEY_SEED=42 dotnet test rebuild/Sim.Tests -c Release --filter HuntValleyReport
public class HuntValleyReport(ITestOutputHelper output)
{
    const int Days = 60;

    [Trait("Category", TestCategories.Slow)]
    [ReportFact("HUNT_VALLEY")]
    public void SixtyDays()
    {
        int seed = int.TryParse(Environment.GetEnvironmentVariable("HUNT_VALLEY_SEED"), out var s) ? s : 17;
        var w = TestWorlds.New(seed);
        var hunts = new HashSet<(Species Hunter, long Minute, TileId Tile)>();
        var kills = new Dictionary<Species, int>();
        var fought = new Dictionary<Species, int>();
        var died = new Dictionary<Species, int>();
        var hunters = new[] { Species.SaberTooth, Species.Wolf, Species.Hyena };
        for (int minute = 0; minute < Days * 1440; minute += 60)
        {
            var species = w.Agents.Values.Where(a => a.Mind is HerdMind).ToDictionary(a => a.Id, a => a.Species);
            var events = Sim.Advance(w, 60);
            var body = w.Agent(AgentId.Player).Body;
            (body.HydrationMl, body.CaloriesKcal, body.EnergyMinutes) = (3000, 2000, 900); // the player stays out of it, alive
            foreach (var a in w.Agents.Values.Where(a => a.Mind is HerdMind)) species[a.Id] = a.Species;
            foreach (var e in events)
            {
                if (e is EncounterEnded end && species.TryGetValue(end.Agent, out var side) && hunters.Contains(side)) hunts.Add((side, end.Minute, end.Tile));
                if (e is Killed { By: AgentId by } kill && species.TryGetValue(by, out var killer) && hunters.Contains(killer) && Fauna.IsQuarry(kill.Species))
                    kills[killer] = kills.GetValueOrDefault(killer) + 1;
                if (e is Killed dead && hunters.Contains(dead.Species))
                {
                    died[dead.Species] = died.GetValueOrDefault(dead.Species) + 1;
                    if (dead.By != null) fought[dead.Species] = fought.GetValueOrDefault(dead.Species) + 1;
                }
            }
        }
        foreach (var h in hunters)
            output.WriteLine($"seed {seed} {h}: hunts {hunts.Count(x => x.Hunter == h)}, kills {kills.GetValueOrDefault(h)}, deaths {died.GetValueOrDefault(h)} ({fought.GetValueOrDefault(h)} in a fight)");
    }
}
