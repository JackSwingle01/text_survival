using Survival;

namespace Survival.Tests;

internal enum FoodSource { Grazed, Hunted, Scavenged, Remains, Store }

/// One seed's days, by species name and day: what the food chain bench measured.
internal sealed class FoodBenchSeed(int seed, int days)
{
    public int Seed { get; } = seed;
    public int Days { get; } = days;
    /// The killer is a species name, or none (starvation, bleeding) or unknown (gone before it was seen).
    public Dictionary<(string Killer, Species Victim, int Day), int> Kills { get; } = [];
    public Dictionary<(Species Species, int Day), int> Encounters { get; } = [];
    public Dictionary<(Species Species, int Day), double> AnimalDays { get; } = [];
    public Dictionary<(Species Species, FoodSource Source, int Day), double> KcalIn { get; } = [];
    public Dictionary<(Species Species, int Day), double> NetBurn { get; } = [];
}

internal static class FoodBench
{
    const string NoKiller = "none", UnknownKiller = "unknown";

    sealed class Recorder(FoodBenchSeed seed, Dictionary<AgentId, Species> species) : IIntakeSink
    {
        public int Day;
        public readonly Dictionary<AgentId, double> IntakeToday = [];

        void IIntakeSink.Took(Agent animal, IntakeKind kind, double kcal, Cause? killedBy)
        {
            var source = kind switch
            {
                IntakeKind.Grazed => FoodSource.Grazed,
                IntakeKind.Remains => FoodSource.Remains,
                IntakeKind.Store => FoodSource.Store,
                _ => killedBy?.Who is AgentId killer && species.TryGetValue(killer, out var by) && by == animal.Species ? FoodSource.Hunted : FoodSource.Scavenged,
            };
            var key = (animal.Species, source, Day);
            seed.KcalIn[key] = seed.KcalIn.GetValueOrDefault(key) + kcal;
            IntakeToday[animal.Id] = IntakeToday.GetValueOrDefault(animal.Id) + kcal;
        }
    }

    internal static double Held(BodyState b) => b.CaloriesKcal + Body.KcalPerKgFat * b.FatKg + Body.KcalPerKgMuscle * b.MuscleKg;

    static double Held(Agent a) => a.Members.Sum(Held);

    static void Add<K>(Dictionary<K, double> d, K key, double by) where K : notnull => d[key] = d.GetValueOrDefault(key) + by;

    /// Steps the world and counts. Burn is what the body took in less what it kept, so it is net of mending and wasting.
    internal static FoodBenchSeed Run(World w, int seed, int days, Action<WorldEvent>? observe = null)
    {
        var result = new FoodBenchSeed(seed, days);
        var species = new Dictionary<AgentId, Species>();
        var recorder = new Recorder(result, species);
        var heldAtDawn = new Dictionary<AgentId, double>();
        w.Intake = recorder;
        try
        {
            for (int day = 0; day < days; day++)
            {
                recorder.Day = day;
                recorder.IntakeToday.Clear();
                heldAtDawn.Clear();
                foreach (var a in w.Agents.Values)
                {
                    species[a.Id] = a.Species;
                    if (a.Species == Species.Human) continue;
                    heldAtDawn[a.Id] = Held(a);
                    Add(result.AnimalDays, (a.Species, day), a.Members.Count(Body.IsAlive));
                }
                for (int minute = 0; minute < 1440; minute++)
                {
                    foreach (var a in w.Agents.Values) species[a.Id] = a.Species;
                    foreach (var e in Sim.Advance(w, 1))
                    {
                        observe?.Invoke(e);
                        if (e is Killed k)
                        {
                            string killer = k.By is not AgentId by ? NoKiller : species.TryGetValue(by, out var s) ? s.ToString() : UnknownKiller;
                            var key = (killer, k.Species, day);
                            result.Kills[key] = result.Kills.GetValueOrDefault(key) + 1;
                        }
                        else if (e is EncounterEnded ended && species.TryGetValue(ended.Agent, out var who))
                        {
                            var key = (who, day);
                            result.Encounters[key] = result.Encounters.GetValueOrDefault(key) + 1;
                        }
                    }
                }
                foreach (var (id, before) in heldAtDawn)
                    if (w.Agents.TryGetValue(id, out var a))
                        Add(result.NetBurn, (a.Species, day), recorder.IntakeToday.GetValueOrDefault(id) - (Held(a) - before));
            }
        }
        finally { w.Intake = null; }
        return result;
    }
}
