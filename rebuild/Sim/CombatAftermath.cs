namespace Survival;

/// Everything the world keeps after a fight, whoever fought it (old CombatAftermath).
public static partial class Combat
{
    /// The fight leaves the world. Weapons go back to their wielders' packs (or the ground); dead
    /// animals leave their herds as carcasses; a herd left with nobody is gone. A dead person stays
    /// an agent with a dead body: their remains and belongings are where they fell. A wounded animal
    /// the player saw escape splits off, a trail to follow. Every agent that fought hears how it
    /// went from its own side (FightEnded), and a herd's mind takes fear, feeding and flight from it.
    static void Aftermath(World world, Encounter enc, CombatResult result, List<WorldEvent> events)
    {
        world.Encounters.Remove(enc); // first: members may be reordered from here on
        // A released unit's agent settled out of this fight already and may be in another now.
        var units = enc.Units.Where(u => !u.Released).ToList();
        // What each side's dead fell to, read before weapons go home and the dead leave their herds.
        var fellTo = new[] { KilledBy(world, enc, units, 0), KilledBy(world, enc, units, 1) };

        foreach (var u in units.Where(u => u.Weapon != null))
        {
            var pack = Items.PackOf(world.Agent(u.Agent));
            (pack != null && Alive(world, u) ? pack : Items.StashAt(world, enc.Tile, new Cause(u.Agent, $"dropped by {Cause.Describe(world, u.Agent)} in a fight"))).Gear.Add(u.Weapon!);
            u.Weapon = null;
        }

        // Only a wounded animal the player watched get away leaves a trail (old TrackWoundedPrey).
        var escaped = result == CombatResult.AnimalFled && units.Any(IsPlayer)
            ? units.Where(u => u.Team == 1 && StatsOf(world, u).IsAnimal && Alive(world, u) && WoundLevel(BodyOf(world, u)) > 0.05)
                .OrderByDescending(u => WoundLevel(BodyOf(world, u))).FirstOrDefault()
            : null;
        var escapedBody = escaped != null ? BodyOf(world, escaped) : null;

        // Which sides lost someone, before the dead leave their herds.
        var fell = units.Where(u => !Alive(world, u)).Select(u => u.Team).ToHashSet();
        var fought = units.Select(u => u.Agent).Distinct().ToList();
        foreach (var id in fought)
        {
            var agent = world.Agent(id);
            if (!SpeciesTable.Of(agent.Species).IsAnimal) continue;
            foreach (var body in units.Where(u => u.Agent == id).Select(u => BodyOf(world, u)).Where(b => !Body.IsAlive(b)).ToList())
            {
                LeaveCarcass(world, enc.Tile, agent.Species, body, fellTo[units.First(u => u.Agent == id).Team]);
                agent.Members.Remove(body);
            }
            // An agent is one or more bodies; with none left there is nothing to move or decide.
            if (agent.Members.Count == 0) Agents.Remove(world, id);
        }

        if (escaped != null)
        {
            var source = world.Agent(escaped.Agent);
            var newId = Agents.SplitOff(world, escaped.Agent, source.Members.IndexOf(escapedBody!));
            events.Add(new WoundedEscaped(world.Minute, newId, source.Species, 0.3 + WoundLevel(escapedBody!) * 0.5, enc.Tile));
            Fauna.Escaped(world, newId, enc.Tile);
        }

        foreach (var id in fought)
        {
            var side = units.First(u => u.Agent == id).Team;
            var own = side == 0 ? result : Mirror(result);
            events.Add(new FightEnded(world.Minute, id, enc.Tile, own, enc.Rounds));
            Fauna.AfterFight(world, id, own, killedOne: fell.Contains(1 - side), enc.Tile);
        }
    }

    /// A carcass on the tile, ready to butcher (old CombatAftermath.LeaveRemains). Old Animal: over
    /// 500 kg is megafauna; a mammoth leaves its tusks and hide besides.
    internal static void LeaveCarcass(World world, TileId tile, Species species, BodyState body, Cause cause) =>
        Carcasses.Create(world, tile, species.ToString(), body, world.Minute, isMegafauna: body.WeightKg > 500,
            special: species == Species.Mammoth ? new Dictionary<Resource, double> { [Resource.Ivory] = 4, [Resource.MammothHide] = 15 } : null,
            cause: cause);

    /// Who a side's dead fell to: the other side, named by its first fighter still listed (its first
    /// at all, if every one was released), with the weapon in that fighter's hands. Who struck the
    /// last blow is not kept, and a side fights as one.
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

    /// Old herd fear after a fight, by its own result: lost it 0.9, ran 0.7, the other side ran 0.2,
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
