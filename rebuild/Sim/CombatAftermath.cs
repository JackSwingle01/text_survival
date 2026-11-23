namespace Survival;

/// Everything the world keeps after a fight, whoever fought it.
public static partial class Combat
{
    /// The fight leaves the world. Weapons go back to their wielders' packs (or the ground); dead
    /// animals leave their herds as carcasses; a herd left with nobody is gone. A dead person stays
    /// an agent with a dead body: their remains and belongings are where they fell. A wounded animal
    /// the player saw escape splits off, a trail to follow. Every agent that fought hears how it
    /// went from its own side (EncounterEnded), and a herd's mind takes fear, feeding and flight from it.
    static void Aftermath(World world, Encounter enc, CombatResult result, List<WorldEvent> events)
    {
        world.Encounters.Remove(enc); // first: members may be reordered from here on
        // A released unit's agent settled out of this fight already and may be in another now.
        var units = enc.Units.Where(u => !u.Released).ToList();
        // What each side's dead fell to when no blow of this fight is on them (they bled out of an
        // earlier one): read before weapons go home and the dead leave their herds.
        var fellTo = new[] { KilledBy(world, enc, units, 0), KilledBy(world, enc, units, 1) };

        // What was thrown and never picked up lies where the fight was.
        foreach (var d in enc.Dropped ?? [])
            Items.StashAt(world, enc.Tile, new Cause(d.ThrownBy, $"thrown by {Cause.Describe(world, d.ThrownBy)}")).Gear.Add(d.Weapon);
        foreach (var u in units.Where(u => u.Weapon != null))
        {
            var pack = Items.PackOf(world.Agent(u.Agent));
            (pack != null && Alive(world, u) ? pack : Items.StashAt(world, enc.Tile, new Cause(u.Agent, $"dropped by {Cause.Describe(world, u.Agent)} in a fight"))).Gear.Add(u.Weapon!);
            u.Weapon = null;
        }

        // Only a wounded animal the player watched get away leaves a trail.
        var escaped = result == CombatResult.AnimalFled && units.Any(IsPlayer)
            ? units.Where(u => u.Team == 1 && StatsOf(world, u).IsAnimal && Alive(world, u) && WoundLevel(BodyOf(world, u)) > 0.05)
                .OrderByDescending(u => WoundLevel(BodyOf(world, u))).FirstOrDefault()
            : null;
        var escapedBody = escaped != null ? BodyOf(world, escaped) : null;

        // Which animals ran off: alive, and left the fight on a heading (or chasing). Read before the dead leave the world.
        var drifts = units.Where(u => StatsOf(world, u).IsAnimal).Select(u => u.Agent).Distinct()
            .Select(id => (Id: id, Living: units.Where(u => u.Agent == id && Alive(world, u)).ToList()))
            .Where(a => a.Living.Count > 0 && a.Living.Count == world.Agent(a.Id).Members.Count(Body.IsAlive) && a.Living.All(u => u.FledHeading != null))
            .Select(a => (a.Id, Far: a.Living.OrderByDescending(u => u.FledTiles).First()))
            .ToList();

        // Which sides lost someone, before the dead leave the world.
        var fell = units.Where(u => !Alive(world, u)).Select(u => u.Team).ToHashSet();
        var fought = units.Select(u => u.Agent).Distinct().ToList();
        foreach (var id in fought)
        {
            var agent = world.Agent(id);
            if (!SpeciesTable.Of(agent.Species).IsAnimal) continue;
            foreach (var body in units.Where(u => u.Agent == id).Select(u => BodyOf(world, u)).Where(b => !Body.IsAlive(b)).ToList())
            {
                // Whoever struck it last, with what was in their hands then (a throw included).
                LeaveCarcass(world, enc.Tile, agent.Species, body, body.WoundedBy ?? fellTo[units.First(u => u.Agent == id).Team]);
                agent.Members.Remove(body);
            }
            // Its body is dead: there is nothing left to move or decide.
            if (agent.Members.Count == 0) Agents.Remove(world, id);
        }

        if (escaped != null)
        {
            var id = escaped.Agent;
            // It ran on the way it fled, leaving its trail that way.
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
            // Whatever a person was at when the fight found them is over: they decide afresh.
            if (world.Agents.TryGetValue(id, out var person) && person.Mind is PersonMind) Sim.EndDoing(world, person, events);
        }

        // Those carry on along their heading as far as they ran; the wounded one that got away already has.
        foreach (var (id, far) in drifts.Where(d => world.Agents.ContainsKey(d.Id) && d.Id != escaped?.Agent))
            Drift(world, id, far.FledHeading!.Value, far.FledTiles);
    }

    /// A carcass on the tile, ready to butcher. Over 500 kg is megafauna; a mammoth leaves its tusks
    /// and hide besides.
    internal static Carcass LeaveCarcass(World world, TileId tile, Species species, BodyState body, Cause cause, double harvestedPct = 0, long? diedMinute = null) =>
        Carcasses.Create(world, tile, species.ToString(), body, diedMinute ?? world.Minute, isMegafauna: body.WeightKg > 500, harvestedPct: harvestedPct,
            special: species == Species.Mammoth ? new Dictionary<Resource, double> { [Resource.Ivory] = 4, [Resource.MammothHide] = 15 } : null,
            cause: cause);

    /// Who a side's dead fell to when no blow names anyone (BodyState.WoundedBy): the other side, named
    /// by its first fighter still listed (its first at all, if every one was released), with the
    /// weapon in that fighter's hands.
    static Cause KilledBy(World world, Encounter enc, List<CombatUnit> units, int team)
    {
        var killer = units.FirstOrDefault(u => u.Team != team) ?? enc.Units.FirstOrDefault(u => u.Team != team);
        if (killer == null) return new Cause(null, "a fight");
        string who = Cause.Describe(world, killer.Agent);
        return new Cause(killer.Agent, killer.Weapon is { } w ? $"{who}'s {Items.Def(w).Name.ToLowerInvariant()}" : who);
    }

    /// How badly hurt (0-1): the worst tissue or the blood, whichever is worse.
    public static double WoundLevel(BodyState body) => Math.Max(1 - body.Regions.Min(r => r.Condition), 1 - body.BloodPct);

    /// The same outcome seen from the other side of the field.
    public static CombatResult Mirror(CombatResult result) => result switch
    {
        CombatResult.Victory => CombatResult.Defeat,
        CombatResult.Defeat => CombatResult.Victory,
        CombatResult.Fled => CombatResult.AnimalFled,
        CombatResult.AnimalFled => CombatResult.Fled,
        _ => CombatResult.AnimalDisengaged,
    };

    /// Herd fear after a fight, by its own result: lost it 0.9, ran 0.7, the other side ran 0.2,
    /// won 0, broke off 0.4.
    public static double FearAfter(CombatResult ownResult) => ownResult switch
    {
        CombatResult.Defeat => 0.9,
        CombatResult.Fled => 0.7,
        CombatResult.AnimalFled => 0.2,
        CombatResult.Victory => 0.0,
        _ => 0.4,
    };
}
