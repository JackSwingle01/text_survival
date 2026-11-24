using Survival;

/// Headless pack hunts on a bare strip, each by the real hunt (Fauna.PackHunt): hungry hunters, all of whose food is due,
/// beside a herd of the quarry. What the kill-chance table in species.json is measured from, and what ForecastTests checks it by.
static class KillOdds
{
    public static double Measure(Species predator, int hunters, Species quarry, int herd, int trials, int firstSeed)
    {
        int kills = 0;
        var w = PlannerTests.Strip();
        var tile = new TileId(20, 2);
        // Spawned ids differ every trial, so each trial draws its own streams: firstSeed skips that many trials' ids.
        for (int skip = 1; skip < firstSeed; skip++) Agents.Remove(w, Agents.Spawn(w, quarry, tile));
        for (int i = 0; i < trials; i++)
        {
            var prey = Agents.SpawnGroup(w, quarry, tile, herd);
            var pack = Agents.SpawnGroup(w, predator, tile, hunters);
            foreach (var id in pack) TestKit.SetHunger(w.Agent(id), 0.8);
            var events = new List<WorldEvent>();
            Fauna.PackHunt(w, w.Agent(pack[0]), w.Agent(prey[0]), events);
            if (events.OfType<Killed>().Any(k => prey.Contains(k.Agent))) kills++;
            foreach (var a in w.Agents.Values.Where(a => a.Mind is HerdMind).ToList()) Agents.Remove(w, a.Id);
            foreach (var t in w.Tiles.Values) t.Carcasses?.Clear();
        }
        return (double)kills / trials;
    }
}
