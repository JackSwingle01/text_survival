using System.Numerics;

namespace Survival;

/// Unaware: wanders or stays put; being struck doubles damage. Alert: suspicious, harder to approach.
/// Engaged: fighting.
public enum Awareness { Unaware, Alert, Engaged }

/// Distance bands that decide what can be done: Close 0-1 m, Near to 3, Mid to 15, Far beyond.
public enum Zone { Close, Near, Mid, Far }

/// How a fight ended, from team A's side.
public enum CombatResult { Victory, Defeat, Fled, AnimalFled, AnimalDisengaged }

/// Move is a step to a chosen cell (the AI's steering, or a cell the player picked); Advance and
/// Retreat step a full allowance toward or away from the target.
public enum CombatAction { Wait, Move, Advance, Retreat, Attack, Throw, ThrowStone, Dodge, Block, Shove, Intimidate, Flee, CallHelp, CallRetreat }

/// How an animal-initiated fight opens. Only an Approach spreads a pack into a formation.
public enum Opening { Approach, CloseEncounter, Ambush }
public enum Formation { Cluster, BroadFront, Pincer, Surround }

/// A cell on the tactical grid: one metre square.
public readonly record struct Cell(int X, int Y)
{
    internal Vector2 Vector => new(X, Y);
    public double DistanceTo(Cell other) => Vector2.Distance(Vector, other.Vector);
    /// Unnormalised, as the old GridPosition.DirectionTo: morale reads its raw dot products.
    internal Vector2 DirectionTo(Cell other) => other.Vector - Vector;
    internal Cell Move(Vector2 direction, float metres) =>
        direction == Vector2.Zero ? this : Of(Vector + Vector2.Normalize(direction) * metres);
    internal Cell Toward(Cell target, float metres) => Move(target.Vector - Vector, metres);
    internal Cell Away(Cell target, float metres) => Move(Vector - target.Vector, metres);
    static Cell Of(Vector2 v) => new((int)Math.Round(v.X), (int)Math.Round(v.Y));
}

/// Who goes into a fight: one member of an agent. A herd sends some members and keeps the rest.
public sealed record Fighter(AgentId Agent, int Member = 0)
{
    /// Wielded for the fight: taken out of the agent's pack when the fight starts, given back after.
    public Gear? Weapon { get; init; }
    /// How readily this one notices movement (1 = baseline); a grazing animal is below 1.
    public double DetectionFactor { get; init; } = 1.0;
    /// Added to opening morale, e.g. Combat.Boldness(...) - 0.5 for a predator that chose to come.
    public double BoldnessDelta { get; init; }
}

/// One fighter on the grid. Plain data: the body is world.Agent(Agent).Members[Member].
public sealed class CombatUnit
{
    public required AgentId Agent { get; init; }
    public required int Member { get; init; }
    /// 0 is team A, 1 is team B.
    public required int Team { get; init; }
    public Cell Position { get; set; }
    /// False once dead or fled (out of the field and alive is fled). Team rosters never shrink, so the aftermath can see who fell.
    public bool InField { get; set; } = true;
    public Awareness Awareness { get; set; }
    public double DetectionFactor { get; set; } = 1.0;
    public double BoldnessModifier { get; set; }
    public bool JustDamaged { get; set; }
    public bool InitialOptimism { get; set; } = true;
    public bool DodgeSet { get; set; }
    public bool BlockSet { get; set; }
    public Gear? Weapon { get; set; }
    /// A companion's call for help or retreat: a morale nudge that lapses.
    public double SignalModifier { get; set; }
    public long SignalExpiresMinute { get; set; }
    public long? LastSignalMinute { get; set; }
}

/// A fight under way on one tile. Lives in World.Encounters from Combat.Create to its aftermath,
/// so a save taken mid-fight resumes it.
public sealed class Encounter
{
    public required TileId Tile { get; init; }
    public List<CombatUnit> Units { get; init; } = [];
    public int Rounds { get; set; }
    public Formation Formation { get; set; }
}

/// What one deliberate action did. Refused actions changed nothing and drew no random numbers.
public sealed record ActionResult(string? Refused = null, bool Hit = false, bool Dodged = false, bool Blocked = false, DamageResult? Damage = null);

/// The tactical grid: every fight in the game, the player's turn by turn and everyone else's headless.
/// Port of old Combat/: CombatScenario, CombatActionRules, CombatFormulas, EncounterPlacement and
/// the mechanical half of CompanionCombat. The AI is in CombatAI.cs, the aftermath in CombatAftermath.cs.
///
/// Time: one round is one minute. The player's rounds are Fight activities, so Sim.Step lives the
/// minute (every body in the fight: the others on the scheduler, then the player's) between the
/// player's action and the others' turns.
/// A headless fight resolves within one instant of world time; Encounter.Rounds says how many
/// minutes it took, for the caller (a herd's or NPC's own catch-up) to charge.
public static partial class Combat
{
    public const int MapSize = 50; // old MAP_SIZE; the overview's "25x25" is stale
    public const int HeadlessRoundCap = 20;
    const int FleeEdgeM = 3, FleeEnergyMinutes = 20, DodgeEnergyMinutes = 10, SignalCooldownMinutes = 5, SignalMinutes = 5;
    const double StoneRangeM = 15, StoneAccuracy = 0.90;
    /// ponytail: old inventories counted stones as items; resources are kilograms now, so one throw is a typical stone.
    public const double StoneKg = 0.3;

    // ---- setup ----

    /// Builds a fight on a tile and registers it in the world. Team A clusters near the bottom of the
    /// grid and team B distanceM up from them, unless an opening places them as an animal encounter.
    /// A hunt is an Engaged player against Unaware prey; an ambush is the reverse.
    public static Encounter Create(World world, IReadOnlyList<Fighter> teamA, IReadOnlyList<Fighter> teamB, TileId tile,
        int distanceM, Awareness awarenessA, Awareness awarenessB, Opening? opening = null)
    {
        if (teamA.Count == 0 || teamB.Count == 0) throw new ArgumentException("Combat needs at least one fighter on each side");
        if (teamB.Any(f => f.Agent == AgentId.Player)) throw new ArgumentException("The player fights on team A: results are from team A's side");
        var all = teamA.Concat(teamB).ToList();
        if (all.Select(f => (f.Agent, f.Member)).Distinct().Count() != all.Count) throw new ArgumentException("A member cannot fight twice");
        var weapons = all.Where(f => f.Weapon != null).Select(f => f.Weapon!).ToList();
        if (weapons.Distinct().Count() != weapons.Count) throw new ArgumentException("One weapon cannot be wielded twice");
        foreach (var f in all) Admit(world, f, tile, into: null);

        int aY = MapSize / 6, bY = Math.Clamp(aY + distanceM, aY + 1, MapSize - 2);
        var enc = new Encounter { Tile = tile };
        void Add(IReadOnlyList<Fighter> team, int side, int y, Awareness awareness)
        {
            for (int i = 0; i < team.Count; i++)
            {
                var f = team[i];
                if (f.Weapon != null) Items.PackOf(world.Agent(f.Agent))!.Gear.Remove(f.Weapon);
                enc.Units.Add(new CombatUnit
                {
                    Agent = f.Agent, Member = f.Member, Team = side, Awareness = awareness,
                    Position = new Cell(MapSize / 2 + (i % 3) * 2 - 2, y + i / 3),
                    Weapon = f.Weapon, DetectionFactor = f.DetectionFactor, BoldnessModifier = f.BoldnessDelta,
                });
            }
        }
        Add(teamA, 0, aY, awarenessA);
        Add(teamB, 1, bY, awarenessB);
        if (opening is Opening o) Place(world, enc, o, distanceM, world.Rng("combat"));
        world.Encounters.Add(enc);
        return enc;
    }

    /// A late arrival joins its side from the free edge cell nearest that side (old AddParticipant).
    /// Phase 3d's NPCs call this when they decide to help.
    public static CombatUnit Join(World world, Encounter enc, Fighter f, int team)
    {
        if (IsOver(enc)) throw new InvalidOperationException("The fight is over");
        if (team == 1 && f.Agent == AgentId.Player) throw new ArgumentException("The player fights on team A");
        var agent = world.Agent(f.Agent);
        Admit(world, f, enc.Tile, into: enc);
        var anchor = Field(enc).FirstOrDefault(u => u.Team == team) ?? throw new InvalidOperationException($"Team {team} has nobody left to join");
        var occupied = Field(enc).Select(u => u.Position).ToHashSet();
        var entry = Enumerable.Range(1, MapSize - 2)
            .SelectMany(i => new[] { new Cell(i, 1), new Cell(i, MapSize - 2), new Cell(1, i), new Cell(MapSize - 2, i) })
            .Where(p => !occupied.Contains(p)).OrderBy(p => p.DistanceTo(anchor.Position))
            .Cast<Cell?>().FirstOrDefault() ?? throw new InvalidOperationException("No free edge to enter from");
        if (f.Weapon != null) Items.PackOf(agent)!.Gear.Remove(f.Weapon);
        var unit = new CombatUnit
        {
            Agent = f.Agent, Member = f.Member, Team = team, Position = entry, Awareness = Awareness.Engaged,
            Weapon = f.Weapon, DetectionFactor = f.DetectionFactor, BoldnessModifier = f.BoldnessDelta,
        };
        enc.Units.Add(unit);
        return unit;
    }

    /// Who may go into a fight: a living member, on the tile, of an agent in no other fight (units
    /// name members by index, and one fight's aftermath reorders them), wielding a weapon from its own pack.
    static void Admit(World world, Fighter f, TileId tile, Encounter? into)
    {
        var agent = world.Agent(f.Agent);
        if (f.Member < 0 || f.Member >= agent.Members.Count)
            throw new ArgumentOutOfRangeException(nameof(f), f.Member, $"{f.Agent} has {agent.Members.Count} members");
        if (!Body.IsAlive(agent.Members[f.Member])) throw new ArgumentException($"{f.Agent} member {f.Member} is dead");
        if (agent.Tile != tile) throw new ArgumentException($"{f.Agent} is on {agent.Tile}, not {tile}");
        if (world.Encounters.Any(e => e != into && e.Units.Any(u => u.Agent == f.Agent)))
            throw new InvalidOperationException($"{f.Agent} is already in another fight");
        if (into != null && into.Units.Any(u => u.Agent == f.Agent && u.Member == f.Member))
            throw new InvalidOperationException($"{f.Agent} member {f.Member} is already fighting");
        if (f.Weapon is not Gear weapon) return;
        if (Items.Def(weapon).Weapon == null) throw new ArgumentException($"{Items.Def(weapon).Name} is not a weapon");
        if (Items.PackOf(agent)?.Gear.Contains(weapon) != true)
            throw new ArgumentException($"{Items.Def(weapon).Name} is not in {f.Agent}'s pack");
    }

    /// The lead plus a random handful (0 to maxExtra) of its living herd mates (old AnimalSide).
    public static List<Fighter> SendMembers(World world, AgentId herd, int lead, int maxExtra = 3)
    {
        var agent = world.Agent(herd);
        var rng = world.Rng("combat");
        var others = Enumerable.Range(0, agent.Members.Count).Where(i => i != lead && Body.IsAlive(agent.Members[i])).ToList();
        int extra = Math.Min(others.Count, maxExtra) is int max and > 0 ? rng.NextInt(max + 1) : 0;
        for (int i = others.Count - 1; i > 0; i--) { int j = rng.NextInt(i + 1); (others[i], others[j]) = (others[j], others[i]); }
        return [new Fighter(herd, lead), .. others.Take(extra).Select(i => new Fighter(herd, i))];
    }

    /// How readily a herd comes at a target (old Herd.BoldnessToward): species temperament, pack size,
    /// the target's wounds, the night. It decides whether a predator engages and seeds its morale
    /// (Fighter.BoldnessDelta = this - 0.5). Hunger and learned fear live on HerdMind (Fauna);
    /// callers pass them in (neutral by default).
    public static double Boldness(World world, AgentId herd, BodyState target, bool defending = false,
        double hunger = 0, double fear = 0, bool targetCarriesMeat = false)
    {
        var agent = world.Agent(herd);
        var t = SpeciesTable.Of(agent.Species).Temperament;
        if (t.Cap <= 0) return 0;
        double bold = t.Base + agent.Members.Count * t.PerMember;
        if (hunger > t.HungryAt) bold += t.HungerBonus;
        if (hunger > t.StarvingAt) bold += t.HungerBonus;
        if (defending) bold += t.DefendBonus;
        if (Effects.Has(target, EffectKind.Bleeding) || Effects.Severity(target, EffectKind.Bloody) > 0.3) bold += 0.15;
        if (targetCarriesMeat) bold += 0.1;
        if (Body.GetCapacities(target).Moving < 0.5) bold += 0.2;
        if (Body.Vitality(target) < 0.7) bold += 0.1;
        int hour = Weather.MinuteOfDay(world.Minute) / 60;
        if (hour < 5 || hour >= 21) bold += 0.1; // old TimeOfDay.Night
        if (fear > 0) bold *= 1.0 - fear;
        return Math.Clamp(bold, 0, t.Cap);
    }

    // ---- placement (old EncounterPlacement) ----

    public static Formation SelectFormation(Species species, int count, Rng rng)
    {
        int roll = rng.NextInt(100);
        if (count < 2) return Formation.Cluster;
        if (species == Species.Hyena) return roll < 70 ? Formation.Cluster : Formation.BroadFront;
        if (species != Species.Wolf) return Formation.Cluster;
        if (count == 2) return roll < 60 ? Formation.Cluster : roll < 90 ? Formation.BroadFront : Formation.Pincer;
        return roll < 35 ? Formation.Cluster : roll < 70 ? Formation.BroadFront : roll < 90 ? Formation.Pincer : Formation.Surround;
    }

    /// The player (or team A's first) at the centre, allies within 4 m, the animals inset from the
    /// edges on a random bearing: at the edge for an approach, at the given distance otherwise.
    public static void Place(World world, Encounter enc, Opening opening, int distanceM, Rng rng, Formation? formation = null)
    {
        var center = new Cell(MapSize / 2 + rng.NextInt(5) - 2, MapSize / 2 + rng.NextInt(5) - 2);
        var occupied = new HashSet<Cell>();
        var teamA = enc.Units.Where(u => u.Team == 0).ToList();
        var teamB = enc.Units.Where(u => u.Team == 1).ToList();
        var anchor = teamA.FirstOrDefault(IsPlayer) ?? teamA[0];
        anchor.Position = center;
        occupied.Add(center);
        foreach (var ally in teamA.Where(u => u != anchor)) ally.Position = TakeFree(center, occupied, rng, 4);

        var lead = StatsOf(world, teamB[0]);
        var selected = opening == Opening.Approach && lead.IsAnimal
            ? formation ?? SelectFormation(world.Agent(teamB[0].Agent).Species, teamB.Count, rng)
            : Formation.Cluster;
        enc.Formation = selected;
        double bearing = rng.NextDouble() * Math.Tau;
        for (int i = 0; i < teamB.Count; i++)
        {
            double angle = bearing + selected switch
            {
                Formation.BroadFront => Spread(i, teamB.Count, Math.PI / 2),
                Formation.Pincer => (i % 2) * Math.PI * 0.8 + (i / 2) * 0.12,
                Formation.Surround => i * Math.Tau / teamB.Count + (rng.NextDouble() - 0.5) * 0.25,
                _ => Spread(i, teamB.Count, 0.2),
            };
            double dx = Math.Cos(angle), dy = Math.Sin(angle);
            int inset = 2 + rng.NextInt(4);
            double edgeRadius = Math.Min(
                Math.Abs(dx) < 0.0001 ? double.PositiveInfinity : (dx > 0 ? MapSize - 1 - inset - center.X : center.X - inset) / Math.Abs(dx),
                Math.Abs(dy) < 0.0001 ? double.PositiveInfinity : (dy > 0 ? MapSize - 1 - inset - center.Y : center.Y - inset) / Math.Abs(dy));
            double radius = opening == Opening.Approach ? edgeRadius : Math.Min(Math.Max(2, distanceM), edgeRadius);
            var desired = new Cell((int)Math.Round(center.X + dx * radius), (int)Math.Round(center.Y + dy * radius));
            teamB[i].Position = TakeFree(desired, occupied, rng);
        }
    }

    static double Spread(int index, int count, double width) => count <= 1 ? 0 : width * (index / (double)(count - 1) - 0.5);

    static Cell TakeFree(Cell desired, HashSet<Cell> occupied, Rng rng, int clusterRadius = 0)
    {
        var free = Enumerable.Range(1, MapSize - 2)
            .SelectMany(x => Enumerable.Range(1, MapSize - 2).Select(y => new Cell(x, y)))
            .Where(p => !occupied.Contains(p)).ToList();
        var nearby = free.Where(p => p.DistanceTo(desired) <= clusterRadius).ToList();
        var chosen = nearby.Count > 0 ? nearby[rng.NextInt(nearby.Count)] : free.OrderBy(p => p.DistanceTo(desired)).First();
        occupied.Add(chosen);
        return chosen;
    }

    // ---- queries ----

    public static bool IsPlayer(CombatUnit u) => u.Agent == AgentId.Player;
    public static IEnumerable<CombatUnit> Field(Encounter enc) => enc.Units.Where(u => u.InField);
    static IEnumerable<CombatUnit> Enemies(Encounter enc, CombatUnit u) => Field(enc).Where(o => o.Team != u.Team);
    /// Units on the same side still in the field, never the unit itself.
    static IEnumerable<CombatUnit> Allies(Encounter enc, CombatUnit u) => Field(enc).Where(o => o.Team == u.Team && o != u);
    public static CombatUnit? NearestEnemy(Encounter enc, CombatUnit u) =>
        Enemies(enc, u).OrderBy(e => u.Position.DistanceTo(e.Position)).FirstOrDefault();
    /// Over when one side has nobody left in the field.
    public static bool IsOver(Encounter enc) => !Field(enc).Any(u => u.Team == 0) || !Field(enc).Any(u => u.Team == 1);
    public static Encounter? EncounterOf(World world, AgentId who) => world.Encounters.FirstOrDefault(e => e.Units.Any(u => u.Agent == who));
    public static CombatUnit UnitOf(Encounter enc, AgentId who) =>
        enc.Units.FirstOrDefault(u => u.Agent == who) ?? throw new KeyNotFoundException($"{who} is not in this fight");

    public static BodyState BodyOf(World world, CombatUnit u)
    {
        var members = world.Agent(u.Agent).Members;
        if (u.Member >= members.Count) throw new InvalidOperationException($"{u.Agent} lost member {u.Member} mid-fight");
        return members[u.Member];
    }
    static SpeciesStats StatsOf(World world, CombatUnit u) => SpeciesTable.Of(world.Agent(u.Agent).Species);
    static bool Alive(World world, CombatUnit u) => Body.IsAlive(BodyOf(world, u));

    public static Zone ZoneOf(double distanceM) => distanceM switch
    {
        <= 1 => Zone.Close,
        <= 3 => Zone.Near,
        <= 15 => Zone.Mid,
        _ => Zone.Far,
    };

    static bool InBounds(Cell p) => p.X >= 0 && p.X < MapSize && p.Y >= 0 && p.Y < MapSize;
    public static int DistanceFromEdge(Cell p) => Math.Min(Math.Min(p.X, MapSize - 1 - p.X), Math.Min(p.Y, MapSize - 1 - p.Y));
    public static bool CanFlee(Cell p) => DistanceFromEdge(p) <= FleeEdgeM;

    /// Who won, from team A's side. The player, when fighting, is team A's point of view.
    public static CombatResult Result(World world, Encounter enc)
    {
        var player = enc.Units.FirstOrDefault(IsPlayer);
        if (player != null && !Alive(world, player)) return CombatResult.Defeat;
        if (player != null && !player.InField) return CombatResult.Fled;
        if (!enc.Units.Any(u => u.Team == 1 && Alive(world, u))) return CombatResult.Victory;
        if (!enc.Units.Any(u => u.Team == 0 && Alive(world, u))) return CombatResult.Defeat;
        if (!Field(enc).Any(u => u.Team == 0)) return CombatResult.Fled;
        if (!Field(enc).Any(u => u.Team == 1)) return CombatResult.AnimalFled;
        return CombatResult.AnimalDisengaged;
    }

    // ---- the player's choices (old CombatActionRules and the orchestrator's Execute*) ----

    /// What this unit could do now against its nearest enemy. Move is not listed: it needs a cell.
    public static IReadOnlyList<CombatAction> Actions(World world, Encounter enc, CombatUnit unit) =>
        Enum.GetValues<CombatAction>().Where(a => a != CombatAction.Move && Refusal(world, enc, unit, a) == null).ToList();

    /// Why this action cannot be taken, or null. Target defaults to the nearest enemy.
    public static string? Refusal(World world, Encounter enc, CombatUnit unit, CombatAction action, CombatUnit? target = null, Cell? to = null)
    {
        if (!unit.InField) return "You are out of the fight.";
        switch (action)
        {
            case CombatAction.Flee: return CanFlee(unit.Position) ? null : "Move nearer an edge to flee.";
            case CombatAction.Wait: return null;
            case CombatAction.CallHelp or CombatAction.CallRetreat:
                return unit.LastSignalMinute is long last && world.Minute - last < SignalCooldownMinutes ? "You called out a moment ago." : null;
            case CombatAction.Move:
                if (to is not Cell cell) return "Choose where to move.";
                double metres = unit.Position.DistanceTo(cell);
                if (metres == 0) return "You are already there.";
                return metres > Allowance(world, unit) ? "That's too far to move in one action." : null;
        }
        target ??= NearestEnemy(enc, unit);
        if (target == null || !target.InField || target.Team == unit.Team) return "No available target.";
        double distance = unit.Position.DistanceTo(target.Position);
        var zone = ZoneOf(distance);
        var nearestZone = ZoneOf(unit.Position.DistanceTo((NearestEnemy(enc, unit) ?? target).Position));
        return action switch
        {
            CombatAction.Attack when zone is not (Zone.Close or Zone.Near) => "Requires close or near range.",
            CombatAction.Block when nearestZone is not (Zone.Close or Zone.Near) => "Requires a threat at close or near range.",
            CombatAction.Dodge when nearestZone != Zone.Near => "Requires near range.",
            CombatAction.Shove when zone != Zone.Close => "Requires close range.",
            CombatAction.Intimidate when nearestZone is not (Zone.Mid or Zone.Far) => "Requires mid or far range.",
            CombatAction.Throw when unit.Weapon == null => "No weapon to throw.",
            CombatAction.Throw or CombatAction.ThrowStone when zone is not (Zone.Mid or Zone.Far) => "Requires mid or far range.",
            CombatAction.ThrowStone when zone != Zone.Mid => "Stones require mid range.",
            CombatAction.ThrowStone when Items.PackOf(world.Agent(unit.Agent)) is not Inventory pack || Items.Have(pack, Resource.Stone) < StoneKg => "No stones to throw.",
            CombatAction.Throw when distance > Throwing(unit.Weapon!).RangeM => "Beyond weapon range.",
            _ => null,
        };
    }

    /// One deliberate action by any unit: the player, or an NPC that chose it. AI turns use CombatAI.
    public static ActionResult Apply(World world, Encounter enc, CombatUnit unit, CombatAction action, List<WorldEvent> events,
        CombatUnit? target = null, Cell? to = null)
    {
        if (Refusal(world, enc, unit, action, target, to) is string refused) return new ActionResult(refused);
        // Stances last until the unit's next turn. Old game: the player's were never cleared, so one
        // block guarded every blow for the rest of the fight (bug, fixed).
        unit.DodgeSet = unit.BlockSet = false;
        target ??= NearestEnemy(enc, unit);
        var result = new ActionResult();
        switch (action)
        {
            case CombatAction.Wait: break;
            case CombatAction.Move: Move(world, enc, unit, to!.Value); break;
            case CombatAction.Advance: Move(world, enc, unit, unit.Position.Toward(target!.Position, Allowance(world, unit))); break;
            case CombatAction.Retreat: Move(world, enc, unit, unit.Position.Away(target!.Position, Allowance(world, unit))); break;
            case CombatAction.Attack: result = Attack(world, enc, unit, target!, events); break;
            case CombatAction.Throw: result = Throw(world, enc, unit, target!, events); break;
            case CombatAction.ThrowStone:
                Items.Take(Items.PackOf(world.Agent(unit.Agent))!, Resource.Stone, StoneKg);
                result = ThrowStone(world, enc, unit, target!, events);
                break;
            case CombatAction.Dodge: unit.DodgeSet = true; break;
            case CombatAction.Block: unit.BlockSet = true; break;
            case CombatAction.Shove: result = Shove(world, enc, unit, target!); break;
            case CombatAction.Intimidate: Intimidate(world, enc, unit); break;
            case CombatAction.Flee:
                var body = BodyOf(world, unit);
                body.EnergyMinutes = Math.Max(0, body.EnergyMinutes - FleeEnergyMinutes);
                Flee(world, enc, unit);
                break;
            case CombatAction.CallHelp or CombatAction.CallRetreat: Signal(world, enc, unit, action == CombatAction.CallRetreat); break;
            default: throw new NotSupportedException($"No rule for {action}");
        }
        Sweep(world, enc, events, unit.Agent);
        unit.JustDamaged = false;
        return result;
    }

    // ---- the player's round, driven by Sim.Step(Fight) ----

    internal static string? Refusal(World world, Fight fight)
    {
        // ponytail: only the player picks rounds; phase 3d's NPCs fight through the AI (or Combat.Apply) until they step themselves.
        if (fight.Who != AgentId.Player) return "Only the player fights round by round; others fight through the AI.";
        if (EncounterOf(world, fight.Who) is not Encounter enc) return "You are not in a fight.";
        CombatUnit? target = null;
        if (fight.Target is int i)
        {
            if (i < 0 || i >= enc.Units.Count) return "No such fighter.";
            target = enc.Units[i];
        }
        return Refusal(world, enc, UnitOf(enc, fight.Who), fight.Action, target, fight.To);
    }

    /// The player's action, at the start of the round's minute. Movement may be noticed.
    internal static string? Act(World world, Fight fight, List<WorldEvent> events)
    {
        if (Refusal(world, fight) is string refused) return refused;
        var enc = EncounterOf(world, fight.Who)!;
        var unit = UnitOf(enc, fight.Who);
        var start = unit.Position;
        Apply(world, enc, unit, fight.Action, events, fight.Target is int i ? enc.Units[i] : null, fight.To);
        if (unit.InField)
            RunDetection(world, enc, unit, huntingSkill: 0, fight.Action == CombatAction.Wait ? 0 : ExposureMeters(start, unit.Position));
        return null;
    }

    /// The rest of the round, after the minute has been lived: everyone else's wounds work, then
    /// everyone else acts. Ends the fight (aftermath and all) when it is over or the player is out.
    internal static string? EndRound(World world, Fight fight, List<WorldEvent> events)
    {
        var enc = EncounterOf(world, fight.Who) ?? throw new InvalidOperationException($"{fight.Who} has no fight to finish");
        var me = UnitOf(enc, fight.Who);
        // Everyone else in the field lived this minute already, on the scheduler (Sim.LiveBodies), before the player's.
        // Old game: a fighter that bled out between blows stayed on the field, so the fight never ended (bug, fixed).
        Sweep(world, enc, events, by: null);
        foreach (var u in enc.Units.Where(u => u.SignalModifier != 0 && world.Minute >= u.SignalExpiresMinute))
        {
            u.BoldnessModifier -= u.SignalModifier;
            u.SignalModifier = 0;
        }
        // Snapshot: old game indexed into a list that shrank as units fell, so one could lose its turn (bug, fixed).
        if (me.InField)
            foreach (var u in Field(enc).Where(u => !IsPlayer(u)).ToList())
            {
                if (IsOver(enc)) break;
                AiTurn(world, enc, u, events);
            }
        enc.Rounds++;
        if (me.InField && !IsOver(enc)) return null;

        // The player is out (fled or fell): whoever is left fights it out unwatched.
        // ponytail: old game ran the remainder as a background fight, one round a minute; here it resolves at once.
        RunRounds(world, enc, events, HeadlessRoundCap);
        var result = Result(world, enc);
        Aftermath(world, enc, result, events);
        return Alive(world, me) ? "fight over" : "died";
    }

    /// A fight with no player in it, to the end or the round cap. Same grid, same AI, same aftermath.
    /// Takes no world time: the fight's minutes are Encounter.Rounds, for the caller to charge.
    public static CombatResult ResolveHeadless(World world, Encounter enc, List<WorldEvent> events)
    {
        if (!world.Encounters.Contains(enc)) throw new InvalidOperationException("That fight is not under way");
        if (Field(enc).Any(IsPlayer)) throw new InvalidOperationException("Headless combat cannot include the player; the player fights through Fight activities");
        RunRounds(world, enc, events, HeadlessRoundCap);
        var result = Result(world, enc);
        Aftermath(world, enc, result, events);
        return result;
    }

    /// AI rounds for everyone in the field. After each unit acts, enemies still Unaware or Alert get
    /// a chance to notice it, so an ambushed herd wakes up as the pack closes in. A round is a
    /// minute: wounds work in it as they do in the player's rounds, so the unwatched bleed too.
    static void RunRounds(World world, Encounter enc, List<WorldEvent> events, int maxRounds)
    {
        for (int round = 0; round < maxRounds && !IsOver(enc); round++)
        {
            foreach (var unit in Field(enc).ToList())
            {
                if (IsOver(enc)) break;
                var before = unit.Position;
                AiTurn(world, enc, unit, events);
                if (unit.InField) RunDetection(world, enc, unit, huntingSkill: 0, ExposureMeters(before, unit.Position));
            }
            // Each round's wounds tick without world.Minute moving: a headless fight spends no world time.
            // A round is each body's next minute not yet lived: the minute under way for a fighter the
            // scheduler has not reached yet, the one after for a fighter that has lived it. Whoever
            // lives those minutes later skips their wounds (WoundsLivedThroughMinute), so every body
            // lives every minute once, whatever order the fighters were scheduled in.
            foreach (var u in Field(enc).ToList())
            {
                var body = BodyOf(world, u);
                Body.TickWounds(body, world.Rng("body"));
                body.WoundsLivedThroughMinute = Math.Max(body.WoundsLivedThroughMinute, world.Minute - 1) + 1;
            }
            Sweep(world, enc, events, by: null);
            enc.Rounds++;
        }
    }

    // ---- primitives ----

    /// Steps to a cell (or a free one beside it), tells the enemies whether that was an advance or
    /// a retreat, and leaves the field past its edge.
    internal static void Move(World world, Encounter enc, CombatUnit unit, Cell destination)
    {
        destination = ResolveDestination(world, enc, destination, unit);
        var moveDirection = unit.Position.DirectionTo(destination);
        unit.Position = destination;
        foreach (var enemy in Enemies(enc, unit).ToList())
        {
            // Raw, unnormalised dot product against a 0.5 threshold, as the old game had it.
            float dot = Vector2.Dot(moveDirection, unit.Position.DirectionTo(enemy.Position));
            if (dot > 0.5f) Morale(world, enc, enemy, MoraleEvent.EnemyAdvanced, unit);
            else if (dot < -0.5f) Morale(world, enc, enemy, MoraleEvent.EnemyRetreated, unit);
        }
        if (!InBounds(unit.Position)) Flee(world, enc, unit);
    }

    /// An occupied cell gives way to a random free neighbour, or to staying put.
    static Cell ResolveDestination(World world, Encounter enc, Cell destination, CombatUnit mover)
    {
        bool Occupied(Cell p) => Field(enc).Any(u => u != mover && u.Position == p && Alive(world, u));
        if (!Occupied(destination)) return destination;
        var free = new List<Cell>();
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var p = new Cell(destination.X + dx, destination.Y + dy);
                if ((dx != 0 || dy != 0) && InBounds(p) && !Occupied(p)) free.Add(p);
            }
        return free.Count == 0 ? mover.Position : free[world.Rng("combat").NextInt(free.Count)];
    }

    static ActionResult Attack(World world, Encounter enc, CombatUnit attacker, CombatUnit defender, List<WorldEvent> events) =>
        world.Rng("combat").NextDouble() < 0.9 ? Strike(world, enc, attacker, defender, events) : new ActionResult();

    /// A thrown weapon: closer is surer, small targets are harder, and the weapon lands on the ground.
    static ActionResult Throw(World world, Encounter enc, CombatUnit attacker, CombatUnit target, List<WorldEvent> events)
    {
        var weapon = attacker.Weapon ?? throw new InvalidOperationException("No weapon to throw");
        var (rangeM, accuracy) = Throwing(weapon);
        double chance = ThrownAccuracy(attacker.Position.DistanceTo(target.Position), rangeM, accuracy, StatsOf(world, target).IsSmall);
        var result = world.Rng("combat").NextDouble() < chance ? Strike(world, enc, attacker, target, events) : new ActionResult();
        attacker.Weapon = null; // after the strike: its damage came from the weapon
        Items.StashAt(world, enc.Tile).Gear.Add(weapon);
        return result;
    }

    /// Old rule by name: stone-tipped weapons carry further and truer.
    static (double RangeM, double Accuracy) Throwing(Gear weapon) =>
        Items.Def(weapon).Name.Contains("Stone") ? (25.0, 0.75) : (20.0, 0.70);

    /// A quarter of a blow, no dodging or blocking it. Needs no weapon.
    static ActionResult ThrowStone(World world, Encounter enc, CombatUnit attacker, CombatUnit target, List<WorldEvent> events)
    {
        var rng = world.Rng("combat");
        double chance = ThrownAccuracy(attacker.Position.DistanceTo(target.Position), StoneRangeM, StoneAccuracy, StatsOf(world, target).IsSmall);
        if (rng.NextDouble() >= chance) return new ActionResult();
        var (amount, type) = AttackDamage(world, attacker, rng);
        amount *= 0.25 * AwarenessDamageFactor(target.Awareness);
        target.Awareness = Awareness.Engaged;
        var damage = Hurt(world, enc, target, amount, type, events);
        AfterHit(world, enc, attacker, target, events);
        return new ActionResult(Hit: true, Damage: damage);
    }

    /// A blow that connected with something: the defender may still dodge or block it.
    static ActionResult Strike(World world, Encounter enc, CombatUnit attacker, CombatUnit defender, List<WorldEvent> events)
    {
        if (defender.Awareness == Awareness.Engaged && defender.DodgeSet && ResolveDodge(world, enc, defender, attacker))
            return new ActionResult(Dodged: true);
        var rng = world.Rng("combat");
        var (amount, type) = AttackDamage(world, attacker, rng);
        amount *= AwarenessDamageFactor(defender.Awareness);
        bool blocked = defender.Awareness == Awareness.Engaged && defender.BlockSet;
        if (blocked) amount = ResolveBlock(defender, amount, rng);
        var damage = Hurt(world, enc, defender, amount, type, events);
        defender.Awareness = Awareness.Engaged;
        AfterHit(world, enc, attacker, defender, events);
        return new ActionResult(Hit: true, Blocked: blocked, Damage: damage);
    }

    static void AfterHit(World world, Encounter enc, CombatUnit attacker, CombatUnit defender, List<WorldEvent> events)
    {
        Morale(world, enc, attacker, MoraleEvent.DealtDamage, defender);
        Morale(world, enc, defender, MoraleEvent.TookDamage, attacker);
        // Old game listed every unit among its own allies, so a wolf took heart from its own wound
        // (+0.6 on top of -0.1) and from its own bite (bug, fixed).
        foreach (var ally in Allies(enc, defender).ToList()) Morale(world, enc, ally, MoraleEvent.AllyDamaged, defender);
        foreach (var ally in Allies(enc, attacker).ToList()) Morale(world, enc, ally, MoraleEvent.AllyDealtDamage, attacker);
        Sweep(world, enc, events, attacker.Agent);
    }

    /// Base attack (weapon or species) scaled by strength 50-100%, vitality 70-100% and luck 50-150%.
    // ponytail: old Player added Fighting skill level to every blow; skills arrive with progression.
    static (double Amount, DamageType Type) AttackDamage(World world, CombatUnit attacker, Rng rng)
    {
        var (amount, type) = AverageBlow(world, attacker);
        return (amount * (0.5 + rng.NextDouble()), type);
    }

    /// The blow before luck: what it does on average.
    internal static (double Amount, DamageType Type) AverageBlow(World world, CombatUnit attacker)
    {
        var body = BodyOf(world, attacker);
        var stats = StatsOf(world, attacker);
        var weapon = attacker.Weapon is Gear g ? Items.Def(g).Weapon! : null;
        var type = weapon == null ? stats.AttackType : weapon.Class switch
        {
            WeaponClass.Blade => DamageType.Sharp,
            WeaponClass.Pierce => DamageType.Pierce,
            _ => DamageType.Blunt,
        };
        return ((weapon?.Damage ?? stats.AttackDamage) * (Body.Strength(body) / 2 + 0.5) * (0.7 + 0.3 * Body.Vitality(body)), type);
    }

    /// Through what the member wears (old Inventory.TotalCushioning/TotalToughness: summed, not worn down).
    static DamageResult Hurt(World world, Encounter enc, CombatUnit target, double amount, DamageType type, List<WorldEvent> events)
    {
        var body = BodyOf(world, target);
        double cushioning = body.Worn.Sum(g => Items.Def(g).Cushioning), toughness = body.Worn.Sum(g => Items.Def(g).Toughness);
        var result = Body.Damage(body, new DamageInfo(amount, type, BodyTarget.Random, cushioning, toughness), world.Rng("body"));
        events.Add(new Injured(world.Minute, target.Agent, enc.Tile, result.HitRegion, type, amount));
        return result;
    }

    /// Unaware targets take double: the ambush.
    public static double AwarenessDamageFactor(Awareness a) => a == Awareness.Unaware ? 2.0 : 1.0;

    /// 60% at full speed. A dodge costs breath and gives a metre.
    static bool ResolveDodge(World world, Encounter enc, CombatUnit defender, CombatUnit attacker)
    {
        var body = BodyOf(world, defender);
        if (world.Rng("combat").NextDouble() >= DodgeChance(body)) return false;
        body.EnergyMinutes = Math.Max(0, body.EnergyMinutes - DodgeEnergyMinutes);
        Move(world, enc, defender, defender.Position.Move(attacker.Position.DirectionTo(defender.Position), 1.0f));
        return true;
    }

    public static double DodgeChance(BodyState body) => 0.6 * Body.Speed(body);

    /// A weapon turns more of the blow (to 0-70%) than an arm does (50-95%), and wears for it.
    internal static double ResolveBlock(CombatUnit defender, double amount, Rng rng)
    {
        if (defender.Weapon is not Gear weapon) return amount * (0.5 + rng.NextDouble() * 0.45);
        Items.Use(weapon);
        return amount * (rng.NextDouble() * 0.7);
    }

    /// Strength and weight against strength and weight; a guarded target is half as easy to move.
    static ActionResult Shove(World world, Encounter enc, CombatUnit attacker, CombatUnit target)
    {
        var rng = world.Rng("combat");
        var (chance, pushM) = ShoveOdds(BodyOf(world, attacker), BodyOf(world, target));
        if (target.BlockSet) chance /= 2;
        if (rng.NextDouble() >= chance) return new ActionResult();
        if (target.DodgeSet && ResolveDodge(world, enc, target, attacker)) return new ActionResult(Dodged: true);
        Move(world, enc, target, target.Position.Move(attacker.Position.DirectionTo(target.Position), (float)pushM));
        return new ActionResult(Hit: true);
    }

    public static (double Chance, double PushM) ShoveOdds(BodyState attacker, BodyState target)
    {
        double strengthRatio = Body.Strength(attacker) / Body.Strength(target);
        double weightRatio = attacker.WeightKg / target.WeightKg;
        return (0.5 * strengthRatio * weightRatio, 1.0 + 2 * strengthRatio + weightRatio);
    }

    static void Intimidate(World world, Encounter enc, CombatUnit unit)
    {
        foreach (var enemy in Enemies(enc, unit).ToList()) Morale(world, enc, enemy, MoraleEvent.Intimidated, unit);
    }

    /// Leaves the field. Allies feel it in proportion to how much they trusted the one who ran.
    static void Flee(World world, Encounter enc, CombatUnit unit)
    {
        unit.InField = false;
        foreach (var ally in Allies(enc, unit).ToList()) Morale(world, enc, ally, MoraleEvent.AllyFled, unit);
    }

    /// A call to the human companions on this side: a morale nudge that replaces the last one and
    /// lapses after five minutes (old CompanionCombat.Signal).
    // ponytail: old game skipped companions whose opinion of the caller was negative; relationships are phase 3d.
    static void Signal(World world, Encounter enc, CombatUnit caller, bool retreat)
    {
        caller.LastSignalMinute = world.Minute;
        foreach (var ally in Allies(enc, caller).Where(u => !IsPlayer(u) && !StatsOf(world, u).IsAnimal))
        {
            ally.BoldnessModifier -= ally.SignalModifier;
            ally.SignalModifier = retreat ? -0.75 : 0.25;
            ally.BoldnessModifier += ally.SignalModifier;
            ally.SignalExpiresMinute = world.Minute + SignalMinutes;
        }
    }

    /// Takes the newly dead off the field: allies lose heart, and the world hears of it. An animal
    /// is Killed (a carcass to be); a person Died.
    static void Sweep(World world, Encounter enc, List<WorldEvent> events, AgentId? by)
    {
        foreach (var unit in Field(enc).Where(u => !Alive(world, u)).ToList())
        {
            foreach (var ally in Allies(enc, unit).ToList()) Morale(world, enc, ally, MoraleEvent.AllyKilled, unit);
            unit.InField = false;
            var agent = world.Agent(unit.Agent);
            if (StatsOf(world, unit).IsAnimal)
                events.Add(new Killed(world.Minute, unit.Agent, agent.Species, BodyOf(world, unit).WeightKg, enc.Tile, by));
            else if (!events.Any(e => e is Died d && d.Who == unit.Agent))
                events.Add(new Died(world.Minute, unit.Agent));
        }
    }

    // ---- detection ----

    /// Ground covered this turn; any other action is as noticeable as a stalking step.
    public static double ExposureMeters(Cell before, Cell after) => before == after ? StalkStepMeters : before.DistanceTo(after);

    public const double StalkStepMeters = 3.0;
    /// Metres of movement over which DetectionChance applies in full. Tuned: ~70% of stalks on a
    /// feeding animal reach 8 m, ~10% on an alert one.
    public const double DetectionReferenceMeters = 20.0;

    /// Chance an animal notices movement at this distance: 100 m ~0%, 50 m ~20%, 30 m ~50%, 10 m ~90%
    /// before awareness and skill (old CombatFormulas.CalculateDetectionChance).
    public static double DetectionChance(double distanceM, Awareness awareness, int huntingSkill = 0, int failedAttempts = 0)
    {
        double chance = Math.Pow(1.0 - distanceM / 100.0, 0.7);
        chance *= awareness switch { Awareness.Alert => 1.5, Awareness.Engaged => 2.0, _ => 1.0 };
        chance -= huntingSkill * 0.05;
        chance += failedAttempts * 0.10;
        return Math.Clamp(chance, 0.05, 0.95);
    }

    /// The chance of being noticed while covering this many metres: a hazard spread over the approach,
    /// not a roll per step. Standing still is free.
    public static double StepDetectionChance(double detectionChance, double metres) =>
        metres <= 0 ? 0 : 1 - Math.Pow(1 - Math.Clamp(detectionChance, 0, 0.99), metres / DetectionReferenceMeters);

    /// Hit chance for a thrown spear or stone: linear falloff to the weapon's range; small game x0.66.
    public static double ThrownAccuracy(double distanceM, double maxRangeM, double baseAccuracy, bool targetIsSmall, double manipulationPenalty = 0)
    {
        if (distanceM > maxRangeM) return 0;
        double accuracy = baseAccuracy * (1.0 - distanceM / maxRangeM);
        if (targetIsSmall) accuracy *= 0.66;
        accuracy -= manipulationPenalty;
        return Math.Clamp(accuracy, 0.05, 0.95);
    }

    /// Risk of one normal stalking step being noticed, as shown to the player (old Assess).
    public static double DetectionRisk(World world, Encounter enc, CombatUnit mover, CombatUnit detector, int huntingSkill = 0) =>
        ExposedDetectionChance(world, enc, mover, detector, huntingSkill, StalkStepMeters);

    static double ExposedDetectionChance(World world, Encounter enc, CombatUnit mover, CombatUnit detector, int huntingSkill, double exposureMeters)
    {
        double chance = DetectionChance(mover.Position.DistanceTo(detector.Position), detector.Awareness, huntingSkill) * detector.DetectionFactor;
        // Cover: up to 30% less on ground with nowhere to see far (visibility 0) than on open ground (2).
        chance *= 1.0 - (1.0 - CoverVisibility(world, enc.Tile) / 2.0) * 0.3;
        return StepDetectionChance(Math.Clamp(chance, 0.05, 0.95), exposureMeters);
    }

    /// Old terrain BaseVisibility (0-2), and the camp's 0.8.
    // ponytail: named places (caves, the pass) had their own visibility; terrain stands in until phase 4's locations.
    static double CoverVisibility(World world, TileId tile)
    {
        var map = Map.Of(world);
        if (tile == map.Camp) return 0.8;
        return map.TerrainAt(tile) switch
        {
            Terrain.Forest or Terrain.Mountain => 0.5,
            Terrain.Marsh => 0.7,
            Terrain.Plain => 1.2,
            Terrain.Rock => 0.8,
            Terrain.Water => 1.3,
            Terrain.Hills => 1.5,
            _ => 1.0,
        };
    }

    /// Enemies not yet engaged may notice the mover, escalating one step: Unaware to Alert to Engaged.
    // Old game also had a "near miss makes it alert" branch, but it tested roll < chance inside the
    // roll >= chance branch, so it never fired. Kept dead (stalking odds are tuned without it).
    internal static void RunDetection(World world, Encounter enc, CombatUnit mover, int huntingSkill, double exposureMeters)
    {
        var rng = world.Rng("combat");
        foreach (var enemy in Enemies(enc, mover).Where(e => e.Awareness != Awareness.Engaged).ToList())
            if (rng.NextDouble() < ExposedDetectionChance(world, enc, mover, enemy, huntingSkill, exposureMeters))
                enemy.Awareness = enemy.Awareness == Awareness.Unaware ? Awareness.Alert : Awareness.Engaged;
    }
}
