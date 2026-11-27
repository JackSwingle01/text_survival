namespace Survival;

public static partial class Combat
{
    /// A dead person stays an agent with a dead body, so their remains and belongings lie where they fell.
    static void Aftermath(World world, Encounter enc, CombatResult result, List<WorldEvent> events)
    {
        world.Encounters.Remove(enc); // first: members may be reordered from here on
        // A released unit's agent settled out of this fight already and may be in another now.
        var units = enc.Units.Where(u => !u.Released).ToList();
        // For dead no blow here names (they bled out of an earlier fight): read before weapons go home.
        var fellTo = new[] { KilledBy(world, enc, units, 0), KilledBy(world, enc, units, 1) };

        foreach (var d in enc.Dropped ?? [])
            Items.PileAt(world, enc.Tile, new Cause(d.ThrownBy, $"thrown by {Cause.Describe(world, d.ThrownBy)}")).Gear.Add(d.Weapon);
        foreach (var u in units.Where(u => u.Weapon != null))
        {
            var pack = Items.PackOf(world.Agent(u.Agent));
            (pack != null && Alive(world, u) ? pack : Items.PileAt(world, enc.Tile, new Cause(u.Agent, $"dropped by {Cause.Describe(world, u.Agent)} in a fight"))).Gear.Add(u.Weapon!);
            u.Weapon = null;
        }

        // Only a wounded animal the player watched get away leaves a trail.
        var escaped = result == CombatResult.AnimalFled && units.Any(IsPlayer)
            ? units.Where(u => u.Team == 1 && StatsOf(world, u).IsAnimal && Alive(world, u) && WoundLevel(BodyOf(world, u)) > 0.05)
                .OrderByDescending(u => WoundLevel(BodyOf(world, u))).FirstOrDefault()
            : null;
        var escapedBody = escaped != null ? BodyOf(world, escaped) : null;

        // Read before the dead leave the world.
        var drifts = units.Where(u => StatsOf(world, u).IsAnimal).Select(u => u.Agent).Distinct()
            .Select(id => (Id: id, Living: units.Where(u => u.Agent == id && Alive(world, u)).ToList()))
            .Where(a => a.Living.Count > 0 && a.Living.Count == world.Agent(a.Id).Members.Count(Body.IsAlive) && a.Living.All(u => u.FledHeading != null))
            .Select(a => (a.Id, Far: a.Living.OrderByDescending(u => u.FledTiles).First()))
            .ToList();

        Fauna.Fought(world, units.Where(u => u.Team == 0).Select(u => u.Agent), units.Where(u => u.Team == 1).Select(u => u.Agent));
        var fell = units.Where(u => !Alive(world, u)).Select(u => u.Team).ToHashSet();
        var fought = units.Select(u => u.Agent).Distinct().ToList();
        // Kin not drawn in saw the fight and take its side's outcome: read while the dead still stand among them.
        var inFight = enc.Units.Select(u => u.Agent).ToHashSet();
        var watched = new Dictionary<AgentId, int>();
        foreach (var id in fought)
            foreach (var kin in Side(world, world.Agent(id)).Where(k => !inFight.Contains(k.Id)))
                watched.TryAdd(kin.Id, units.First(u => u.Agent == id).Team);
        foreach (var id in fought)
        {
            var agent = world.Agent(id);
            if (!SpeciesTable.Of(agent.Species).IsAnimal) continue;
            foreach (var body in units.Where(u => u.Agent == id).Select(u => BodyOf(world, u)).Where(b => !Body.IsAlive(b)).ToList())
            {
                LeaveCarcass(world, enc.Tile, agent.Species, body, body.WoundedBy ?? fellTo[units.First(u => u.Agent == id).Team]);
                agent.Members.Remove(body);
            }
            if (agent.Members.Count == 0) Agents.Remove(world, id);
        }

        if (escaped != null)
        {
            var id = escaped.Agent;
            if (escaped.FledHeading is Direction heading) Drift(world, id, heading, escaped.FledTiles);
            events.Add(new WoundedEscaped(world.Minute, id, world.Agent(id).Species, 0.3 + WoundLevel(escapedBody!) * 0.5, enc.Tile, escaped.FledHeading));
            Fauna.Escaped(world, id, world.Agent(id).Tile);
        }

        foreach (var id in fought)
        {
            var side = units.First(u => u.Agent == id).Team;
            var own = side == 0 ? result : Mirror(result);
            events.Add(new EncounterEnded(world.Minute, id, enc.Tile, own, enc.Rounds));
            Fauna.AfterEncounter(world, id, own, killedOne: fell.Contains(1 - side), enc.Tile);
            // Whatever a person was at when the fight found them is over.
            if (world.Agents.TryGetValue(id, out var person) && person.Mind is PersonMind) Sim.EndDoing(world, person, events);
        }
        foreach (var (id, side) in watched)
            Fauna.AfterEncounter(world, id, side == 0 ? result : Mirror(result), killedOne: fell.Contains(1 - side), enc.Tile);

        // The wounded one that got away has drifted already.
        foreach (var (id, far) in drifts.Where(d => world.Agents.ContainsKey(d.Id) && d.Id != escaped?.Agent))
            Drift(world, id, far.FledHeading!.Value, far.FledTiles);
    }

    internal static Carcass LeaveCarcass(World world, TileId tile, Species species, BodyState body, Cause cause, double harvestedPct = 0, long? diedMinute = null) =>
        Carcasses.Create(world, tile, species.ToString(), body, diedMinute ?? world.Minute, isMegafauna: body.WeightKg > 500, harvestedPct: harvestedPct,
            special: species == Species.Mammoth ? new Dictionary<Resource, double> { [Resource.Ivory] = 4, [Resource.MammothHide] = 15 } : null,
            cause: cause);

    /// Who a side's dead fell to when no blow names anyone (BodyState.WoundedBy).
    static Cause KilledBy(World world, Encounter enc, List<CombatUnit> units, int team)
    {
        var killer = units.FirstOrDefault(u => u.Team != team) ?? enc.Units.FirstOrDefault(u => u.Team != team);
        if (killer == null) return new Cause(null, "a fight");
        string who = Cause.Describe(world, killer.Agent);
        return new Cause(killer.Agent, killer.Weapon is { } w ? $"{who}'s {Items.Def(w).Name.ToLowerInvariant()}" : who);
    }

    public static double WoundLevel(BodyState body) => Math.Max(1 - body.Regions.Min(r => r.Condition), 1 - body.BloodPct);

    public static CombatResult Mirror(CombatResult result) => result switch
    {
        CombatResult.Victory => CombatResult.Defeat,
        CombatResult.Defeat => CombatResult.Victory,
        CombatResult.Fled => CombatResult.AnimalFled,
        CombatResult.AnimalFled => CombatResult.Fled,
        _ => CombatResult.AnimalDisengaged,
    };

    public static double FearAfter(CombatResult ownResult) => ownResult switch
    {
        CombatResult.Defeat => 0.9,
        CombatResult.Fled => 0.7,
        CombatResult.AnimalFled => 0.2,
        CombatResult.Victory => 0.0,
        _ => 0.4,
    };
}
