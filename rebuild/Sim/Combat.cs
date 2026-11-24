using System.Numerics;
using System.Text.Json.Serialization;

namespace Survival;

/// How aware a fighter is as a fight opens, and how aware it is now (CombatUnit.Awareness, read off its
/// FighterState): Unaware wanders or stays put, and being struck does half again; Alert is Wary,
/// suspicious and harder to approach; Engaged is in the fight.
public enum Awareness { Unaware, Alert, Engaged }

/// Distance bands that decide what can be done: Close 0-1 m, Near to 3, Mid to 15, Far beyond.
public enum Zone { Close, Near, Mid, Far }

/// How a fight ended, from team A's side.
public enum CombatResult { Victory, Defeat, Fled, AnimalFled, AnimalDisengaged }

/// Move is a step to a chosen cell (the AI's steering, or a cell the player picked); Advance and
/// Retreat step a full allowance toward or away from the target. Turn faces another target, and costs
/// the round. PickUp takes up a weapon lying within reach. There is no Block or Dodge: every aware
/// fighter defends itself when struck (Combat.DefenceChance).
/// Flee runs for it, resolved at once by the catch-up (CombatFlight); Chase gives chase to a fighter that
/// has just run from the player.
public enum CombatAction { Wait, Move, Advance, Retreat, Attack, Throw, ThrowStone, Shove, Intimidate, Flee, CallHelp, CallRetreat, PickUp, Turn, Chase }

/// How an animal-initiated fight opens. Only an Approach spreads a pack into a formation.
public enum Opening { Approach, CloseEncounter, Ambush }
public enum Formation { Cluster, BroadFront, Pincer, Surround }

/// A cell on the tactical grid: one metre square.
public readonly record struct Cell(int X, int Y)
{
    internal Vector2 Vector => new(X, Y);
    public double DistanceTo(Cell other) => Vector2.Distance(Vector, other.Vector);
    /// Unnormalised: morale reads its raw dot products.
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
    /// Where it stands toward its target (CombatAI's state machine). Read it through Combat.StateOf,
    /// which adds Down and Gone.
    public FighterState State { get; set; }
    /// 0-1: how near an unaware fighter is to knowing it is in a fight. Rises when it notices movement,
    /// fades when it does not; once aware it stays at 1.
    public double AlertnessLevel { get; set; }
    /// The index in Encounter.Units of the fighter it faces.
    public int? Target { get; set; }
    /// Gone by running or breaking off: which way, and how many tiles on it ended up (100 m each, five at most).
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Direction? FledHeading { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int FledTiles { get; set; }
    /// Gone after a runner, not away from the fight.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool LeftChasing { get; set; }
    /// Run down this round: no defence, and no second flight, until the round ends.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool RunDown { get; set; }
    /// Whether it has turned to run in this fight: the chase instinct fires on the first time only.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool HasRun { get; set; }
    [JsonIgnore]
    public Awareness Awareness => State switch
    {
        FighterState.Unaware => Awareness.Unaware,
        FighterState.Wary => Awareness.Alert,
        _ => Awareness.Engaged,
    };
    public double DetectionFactor { get; set; } = 1.0;
    public double BoldnessModifier { get; set; }
    public Gear? Weapon { get; set; }
    /// A companion's call for help or retreat: a morale nudge that lapses.
    public double SignalModifier { get; set; }
    public long SignalExpiresMinute { get; set; }
    public long? LastSignalMinute { get; set; }
    /// Fled, and its agent has since gone into another fight (Combat.Admit): its part here is settled,
    /// its weapon back in its pack, and nothing here reads its body again (the other fight may reorder
    /// or remove it). It stays listed so Fight.Target indexes keep pointing at the same units.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Released { get; set; }
    /// What it did this round and to whom, kept only while a watcher is attached (Combat.Record).
    [JsonIgnore] internal CombatAction? RoundAction { get; set; }
    [JsonIgnore] internal int? RoundTarget { get; set; }
}

/// A fight under way on one tile. Lives in World.Encounters from Combat.Create to its aftermath,
/// so a save taken mid-fight resumes it.
public sealed class Encounter
{
    public required TileId Tile { get; init; }
    public List<CombatUnit> Units { get; init; } = [];
    public int Rounds { get; set; }
    public Formation Formation { get; set; }
    /// Where the tile's fire pit stands on the grid: where team A's lead started (the person, at the
    /// middle in an opening), since a fire is where the people are. Nothing burns there unless a pit burns on the tile.
    public Cell FireCell { get; set; }
    /// The unit that has just run from the player, waiting on whether the player gives chase.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PendingFlight { get; set; }
    /// Weapons thrown and lying on the grid, to be picked up; the aftermath piles whatever is left.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DroppedWeapon>? Dropped { get; set; }
    /// Who watches the fight round by round (Combat.Watch); never saved.
    [JsonIgnore] public ICombatObserver? Observer { get; set; }
    /// What each agent in it was when the watching began: a herd gone with its last member is still named.
    [JsonIgnore] internal Dictionary<AgentId, Species>? ObservedSpecies { get; set; }
    /// Where in its events list the round under way began, for the watcher.
    [JsonIgnore] internal int RoundEventsFrom { get; set; }
}

/// An enemy a fighter could face: its index in Encounter.Units (as Fight.Target names it), how far it
/// is, whether it is faced now, and why turning to it would be refused.
public sealed record CombatTarget(int Index, AgentId Agent, int Member, double DistanceM, bool Facing, Refusal? TurnRefusal);

/// A weapon lying where it fell on the grid, and who threw it.
public sealed record DroppedWeapon(Cell At, Gear Weapon, AgentId ThrownBy);

/// What one deliberate action did. Refused actions changed nothing and drew no random numbers.
public sealed record ActionResult(Refusal? Refused = null, bool Hit = false, bool Dodged = false, bool Blocked = false, DamageResult? Damage = null);

/// The tactical grid: every fight in the game, the player's turn by turn and everyone else's headless.
/// The AI is in CombatAI.cs, the aftermath in CombatAftermath.cs.
///
/// Time: one round is one minute. The player's rounds are Fight activities, so Sim.Step lives the
/// minute (every body in the fight: the others on the scheduler, then the player's) between the
/// player's action and the others' turns.
/// A headless fight resolves within one instant of world time; Encounter.Rounds says how many
/// minutes it took, for the caller (a herd's or NPC's own catch-up) to charge.
public static partial class Combat
{
    public const int MapSize = 50;
    public const int HeadlessRoundCap = 20;
    const int SignalCooldownMinutes = 5, SignalMinutes = 5;
    /// How close a dropped weapon must be to pick up.
    const double PickUpReachM = 1.5;
    const string StoneKind = "stone";
    /// A stone from the pack weighs and flies as the stone gear does (items.json "stone").
    public static double StoneKg => Items.GearKinds[StoneKind].BaseWeightKg;
    /// Two fighters in neighbouring cells, diagonals too, are in contact: a reach shorter than that still lands.
    public const double TouchingM = 1.5;

    // ---- setup ----

    /// Builds a fight on a tile and registers it in the world. Team A clusters near the bottom of the
    /// grid and team B distanceM up from them, unless an opening places them as an animal encounter.
    /// A hunt is an Engaged player against Unaware prey; an ambush is the reverse.
    public static Encounter Create(World world, IReadOnlyList<Fighter> teamA, IReadOnlyList<Fighter> teamB, TileId tile,
        int distanceM, Awareness awarenessA, Awareness awarenessB, Opening? opening = null, List<WorldEvent>? events = null)
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
                    Agent = f.Agent,
                    Member = f.Member,
                    Team = side,
                    State = OpeningState(awareness),
                    AlertnessLevel = OpeningAlertnessLevel(awareness),
                    Position = new Cell(MapSize / 2 + (i % 3) * 2 - 2, y + i / 3),
                    Weapon = f.Weapon,
                    DetectionFactor = f.DetectionFactor,
                    BoldnessModifier = f.BoldnessDelta,
                });
            }
        }
        Add(teamA, 0, aY, awarenessA);
        Add(teamB, 1, bY, awarenessB);
        if (opening is Opening o) Place(world, enc, o, distanceM, world.Rng("combat", teamA[0].Agent)); // whoever opened it
        foreach (var u in enc.Units) u.Target = enc.Units.IndexOf(NearestEnemy(enc, u)!);
        enc.FireCell = (enc.Units.FirstOrDefault(IsPlayer) ?? enc.Units[0]).Position;
        world.Encounters.Add(enc);
        // Anyone awake beside a fight stops what they are at, and their mind chooses whether to help.
        foreach (var bystander in Nearby.At(world, tile).Where(a => all.All(f => f.Agent != a.Id)))
            if (bystander is { Mind: NpcMind, Doing: Doing d } && Sim.Looks(bystander, d.Activity)) Sim.EndDoing(world, bystander, events ?? []);
        return enc;
    }

    /// Where a fighter starts: one that knows of the fight stands and weighs it up (the bands take it from
    /// there on its first turn); a wary one has noticed something once.
    static FighterState OpeningState(Awareness a) => a switch
    {
        Awareness.Unaware => FighterState.Unaware,
        Awareness.Alert => FighterState.Wary,
        _ => FighterState.Standoff,
    };

    static double OpeningAlertnessLevel(Awareness a) => a switch { Awareness.Unaware => 0, Awareness.Alert => NoticeAlertnessDelta, _ => 1 };

    /// A late arrival joins its side from the free edge cell nearest that side.
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
            Agent = f.Agent,
            Member = f.Member,
            Team = team,
            Position = entry,
            State = FighterState.Standoff,
            AlertnessLevel = 1,
            Weapon = f.Weapon,
            DetectionFactor = f.DetectionFactor,
            BoldnessModifier = f.BoldnessDelta,
        };
        enc.Units.Add(unit);
        unit.Target = enc.Units.IndexOf(NearestEnemy(enc, unit)!);
        return unit;
    }

    /// Who may go into a fight: a living member, on the tile, of an agent no other fight holds
    /// (Sim.Held: nobody of it in that field), wielding a weapon from its own pack. Units name members
    /// by index and one fight's aftermath reorders them, so an agent that fled another fight still
    /// under way is released from it first (CombatUnit.Released); one that also lost a member there
    /// stays that fight's until its aftermath has taken the dead.
    static void Admit(World world, Fighter f, TileId tile, Encounter? into)
    {
        var agent = world.Agent(f.Agent);
        if (f.Member < 0 || f.Member >= agent.Members.Count)
            throw new ArgumentOutOfRangeException(nameof(f), f.Member, $"{f.Agent} has {agent.Members.Count} members");
        if (!Body.IsAlive(agent.Members[f.Member])) throw new ArgumentException($"{f.Agent} member {f.Member} is dead");
        if (agent.Tile != tile) throw new ArgumentException($"{f.Agent} is on {agent.Tile}, not {tile}");
        var left = world.Encounters.Where(e => e != into).SelectMany(e => e.Units).Where(u => u.Agent == f.Agent && !u.Released).ToList();
        if (left.Any(u => u.InField || !Alive(world, u)))
            throw new InvalidOperationException($"{f.Agent} is already in another fight");
        if (into != null && into.Units.Any(u => u.Agent == f.Agent && u.Member == f.Member))
            throw new InvalidOperationException($"{f.Agent} member {f.Member} is already fighting");
        foreach (var u in left) // fled and alive: settled out of that fight, with the weapon it ran off with
        {
            if (u.Weapon != null) Items.PackOf(agent)!.Gear.Add(u.Weapon);
            u.Weapon = null;
            u.Released = true;
        }
        if (f.Weapon is not Gear weapon) return;
        if (Items.Def(weapon).Weapon == null) throw new ArgumentException($"{Items.Def(weapon).Name} is not a weapon");
        if (Items.PackOf(agent)?.Gear.Contains(weapon) != true)
            throw new ArgumentException($"{Items.Def(weapon).Name} is not in {f.Agent}'s pack");
    }

    /// Who is on this agent's side where it stands: itself first, then its kin on its tile in id order
    /// (Fauna.IsKin: any of its species for an open group, the ones it knows for a closed one) that no
    /// fight holds. A person stands alone. The one read for boldness from numbers, noticing, and who
    /// joins a fight; rival packs meeting on a tile are two sides.
    public static List<Agent> Side(World world, Agent agent)
    {
        var side = new List<Agent> { agent };
        if (agent.Mind is not HerdMind) return side;
        var here = Nearby.At(world, agent.Tile);
        for (int i = 0; i < here.Count; i++)
            if (OnSide(world, agent, here[i])) side.Add(here[i]);
        return side;
    }

    /// Whether the other, on the agent's tile, is on its side (Side): the rule, for reads that only count.
    public static bool OnSide(World world, Agent agent, Agent other) =>
        Fauna.IsKin(agent, other) && other.Tile == agent.Tile && !Sim.Held(world, other.Id);

    /// The lead plus a random handful (0 to maxExtra) of its side.
    public static List<Fighter> SendSide(World world, AgentId lead, int maxExtra = 3)
    {
        var others = Side(world, world.Agent(lead)).Skip(1).Select(a => a.Id).ToList();
        var rng = world.Rng("combat", lead);
        int extra = Math.Min(others.Count, maxExtra) is int max and > 0 ? rng.NextInt(max + 1) : 0;
        for (int i = others.Count - 1; i > 0; i--) { int j = rng.NextInt(i + 1); (others[i], others[j]) = (others[j], others[i]); }
        return [new Fighter(lead), .. others.Take(extra).Select(id => new Fighter(id))];
    }

    /// The herd sizes the kill-chance table tells apart: a herd of more fights as one of this many (SendSide's lead and three).
    public const int KillChanceHerds = 4;

    /// A forecast: the chance a pack hunt (Fauna.PackHunt) of this many hunters kills, against a herd of the quarry this big,
    /// measured from headless hunts (species.json killChances); 0 for a quarry the predator has no row for.
    public static double KillChance(Species predator, int hunters, Species quarry, int herd)
    {
        if (hunters < 1 || herd < 1 || !Content.Tables.KillChances.TryGetValue(predator, out var quarries)
            || !quarries.TryGetValue(quarry, out var rows)) return 0;
        return rows[Math.Min(hunters, rows.Count) - 1][Math.Min(herd, KillChanceHerds) - 1];
    }

    /// Whether the predator has a kill chance for anything: what makes a meat-eater hunt.
    public static bool Hunts(Species predator) => Content.Tables.KillChances.ContainsKey(predator);

    const double ActiveHoursBoldnessDelta = 0.1;

    /// How readily an animal comes at a target: species temperament, its side's numbers (Side)
    /// against the people standing with the target, the target's wounds, its active hours. It decides whether a
    /// predator engages and seeds its morale (Fighter.BoldnessDelta = this - 0.5). Hunger and learned fear
    /// live on HerdMind (Fauna); callers pass them in (neutral by default).
    public static double Boldness(World world, AgentId herd, BodyState target, bool defending = false,
        double hunger = 0, double fear = 0, int othersWithTarget = 0)
    {
        var agent = world.Agent(herd);
        var t = SpeciesTable.Of(agent.Species).Temperament;
        if (t.Cap <= 0) return 0;
        double bold = t.Base + (Side(world, agent).Count - othersWithTarget) * t.PerMember;
        if (hunger > t.HungryAt) bold += t.HungerBonus;
        if (hunger > t.StarvingAt) bold += t.HungerBonus;
        if (defending) bold += t.DefendBonus;
        if (Effects.Has(target, EffectKind.Bleeding) || Effects.Severity(target, EffectKind.Bloody) > 0.3) bold += 0.15;
        if (Body.GetCapacities(target).Moving < 0.5) bold += 0.2;
        if (Body.Vitality(target) < 0.7) bold += 0.1;
        if (Fauna.ActiveNow(world, agent.Species)) bold += ActiveHoursBoldnessDelta;
        if (fear > 0) bold *= 1.0 - fear;
        return Math.Clamp(bold, 0, t.Cap);
    }

    // ---- placement ----

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
    /// The fight that lists this agent (in the field, fallen or fled), unless it has been released from it.
    public static Encounter? EncounterOf(World world, AgentId who) => world.Encounters.FirstOrDefault(e => e.Units.Any(u => u.Agent == who && !u.Released));
    public static CombatUnit UnitOf(Encounter enc, AgentId who) =>
        enc.Units.FirstOrDefault(u => u.Agent == who && !u.Released) ?? throw new KeyNotFoundException($"{who} is not in this fight");

    public static BodyState BodyOf(World world, CombatUnit u)
    {
        var members = world.Agent(u.Agent).Members;
        if (u.Member >= members.Count) throw new InvalidOperationException($"{u.Agent} lost member {u.Member} mid-fight");
        return members[u.Member];
    }
    static SpeciesStats StatsOf(World world, CombatUnit u) => SpeciesTable.Of(world.Agent(u.Agent).Species);

    /// How this fighter runs now: its kind's running, cut by its wounds, tiredness and what it carries.
    public static Running RunningOf(World world, CombatUnit u)
    {
        var agent = world.Agent(u.Agent);
        return SpeciesTable.Running(agent.Species, BodyOf(world, u), new AbilityContext(EncumbrancePct: Items.EncumbrancePct(agent)), Items.CarriedKg(agent));
    }
    static bool Alive(World world, CombatUnit u) => Body.IsAlive(BodyOf(world, u));

    public static Zone ZoneOf(double distanceM) => distanceM switch
    {
        <= 1 => Zone.Close,
        <= 3 => Zone.Near,
        <= 15 => Zone.Mid,
        _ => Zone.Far,
    };

    /// A cell a fighter was knocked or stepped to without choosing it, kept on the grid: only a chosen Move or
    /// Flee leaves the field.
    static Cell OnTheGrid(Cell p) => new(Math.Clamp(p.X, 0, MapSize - 1), Math.Clamp(p.Y, 0, MapSize - 1));

    static bool InBounds(Cell p) => p.X >= 0 && p.X < MapSize && p.Y >= 0 && p.Y < MapSize;
    public static int DistanceFromEdge(Cell p) => Math.Min(Math.Min(p.X, MapSize - 1 - p.X), Math.Min(p.Y, MapSize - 1 - p.Y));

    /// Who won, from team A's side. The player, when fighting, is team A's point of view.
    public static CombatResult Result(World world, Encounter enc)
    {
        var player = enc.Units.FirstOrDefault(IsPlayer);
        if (player != null && !Alive(world, player)) return CombatResult.Defeat;
        // A player who went after a runner that got away drove it off; one who left any other way fled.
        if (player != null && !player.InField) return player.LeftChasing && !Field(enc).Any(u => u.Team == 1) ? CombatResult.AnimalFled : CombatResult.Fled;
        var listed = enc.Units.Where(u => !u.Released).ToList();
        if (!listed.Any(u => u.Team == 1 && Alive(world, u))) return CombatResult.Victory;
        if (!listed.Any(u => u.Team == 0 && Alive(world, u))) return CombatResult.Defeat;
        if (!Field(enc).Any(u => u.Team == 0)) return CombatResult.Fled;
        if (!Field(enc).Any(u => u.Team == 1)) return CombatResult.AnimalFled;
        return CombatResult.AnimalDisengaged;
    }

    // ---- the player's choices ----

    /// What this unit could do now against its target. Move and Turn are not listed: they need a cell or a target.
    public static IReadOnlyList<CombatAction> Actions(World world, Encounter enc, CombatUnit unit) =>
        Enum.GetValues<CombatAction>().Where(a => a is not (CombatAction.Move or CombatAction.Turn) && Refusal(world, enc, unit, a) == null).ToList();

    /// Why this action cannot be taken, or null: what the distance allows, and for a fighter the AI
    /// drives, what its state allows too (the player's state follows from the action instead). The
    /// target is the unit's own; naming another is Turn, which faces it and costs the round.
    public static Refusal? Refusal(World world, Encounter enc, CombatUnit unit, CombatAction action, CombatUnit? target = null, Cell? to = null)
    {
        if (!unit.InField) return new("You are out of the fight.");
        if (IsDown(world, unit)) return action == CombatAction.Wait ? null : new("You are down.");
        if (!IsPlayer(unit) && !StateAllows(unit.State, action)) return new($"Not while {unit.State}.");
        var current = TargetOf(enc, unit);
        if (action == CombatAction.Turn)
            return target == null ? new("Choose whom to face.") : !target.InField || target.Team == unit.Team ? new("No available target.")
                : target == current ? new("You are already facing them.") : null;
        if (target != null && target != current) return new("Turn to face them first.");
        switch (action)
        {
            case CombatAction.Flee: return unit.RunDown ? new("You were run down.") : null;
            case CombatAction.Chase: return ChaseOffer(world, enc, unit) == null ? new("Nobody is running from you.") : null;
            case CombatAction.Wait: return null;
            case CombatAction.PickUp:
                return unit.Weapon != null ? new("Your hands are full.") : DroppedNear(enc, unit) == null ? new("Nothing to pick up within reach.") : null;
            case CombatAction.CallHelp or CombatAction.CallRetreat:
                return !Hearers(world, enc, unit).Any() ? new("Nobody is with you to hear it.")
                    : unit.LastSignalMinute is long last && world.Minute - last < SignalCooldownMinutes ? new("You called out a moment ago.") : null;
            case CombatAction.Move:
                if (to is not Cell cell) return new("Choose where to move.");
                double metres = unit.Position.DistanceTo(cell);
                if (metres == 0) return new("You are already there.");
                return metres > Allowance(world, unit) ? new("That's too far to move in one action.") : null;
        }
        target = current;
        if (target == null) return new("No available target.");
        // The fight is the confrontation: an occupant of the target does not bar striking at it.
        if (Access.Refusal(world, unit.Agent, LocationFeatures.OfAgent(world, target.Agent), confronting: true) is { } barred) return barred;
        double distance = unit.Position.DistanceTo(target.Position);
        var zone = ZoneOf(distance);
        var nearestZone = ZoneOf(unit.Position.DistanceTo((NearestEnemy(enc, unit) ?? target).Position));
        return action switch
        {
            CombatAction.Attack when distance > StrikeRangeM(world, unit) => new("Out of reach."),
            CombatAction.Shove when zone != Zone.Close => new("Requires close range."),
            // Near too: a bear's bluff is met at a few metres.
            CombatAction.Intimidate when nearestZone == Zone.Close => new("Requires near range or further."),
            CombatAction.Throw when unit.Weapon == null => new("No weapon to throw."),
            CombatAction.Throw when Items.Def(unit.Weapon!).Weapon!.Thrown == null => new("That is not made to be thrown."),
            CombatAction.Throw or CombatAction.ThrowStone when zone is not (Zone.Mid or Zone.Far) => new("Requires mid or far range."),
            CombatAction.ThrowStone when zone != Zone.Mid => new("Stones require mid range."),
            CombatAction.ThrowStone when StonesHeldKg(world, unit) < StoneKg => new("No stones to throw.", new MissingResource(Resource.Stone, StoneKg - StonesHeldKg(world, unit))),
            CombatAction.Throw when distance > Items.Def(unit.Weapon!).Weapon!.Thrown!.RangeM => new("Beyond weapon range."),
            CombatAction.ThrowStone when distance > Stone.Thrown!.RangeM => new("Beyond weapon range."),
            _ => null,
        };
    }

    static double StonesHeldKg(World world, CombatUnit unit) => Items.PackOf(world.Agent(unit.Agent)) is { } pack ? Items.Have(pack, Resource.Stone) : 0;

    static DroppedWeapon? DroppedNear(Encounter enc, CombatUnit unit) =>
        enc.Dropped?.Where(d => d.At.DistanceTo(unit.Position) <= PickUpReachM).OrderBy(d => d.At.DistanceTo(unit.Position)).FirstOrDefault();

    /// One deliberate action by any unit: the player, or an NPC that chose it. AI turns use CombatAI.
    public static ActionResult Apply(World world, Encounter enc, CombatUnit unit, CombatAction action, List<WorldEvent> events,
        CombatUnit? target = null, Cell? to = null)
    {
        if (Refusal(world, enc, unit, action, target, to) is { } refused) return new ActionResult(refused);
        if (action == CombatAction.Turn)
        {
            unit.Target = enc.Units.IndexOf(target!);
            Record(enc, unit, action, target);
            return new ActionResult();
        }
        target = TargetOf(enc, unit);
        Record(enc, unit, action, target);
        var result = new ActionResult();
        switch (action)
        {
            case CombatAction.Wait: break;
            case CombatAction.Move: Walk(world, enc, unit, to!.Value, events); break;
            case CombatAction.Advance: Walk(world, enc, unit, unit.Position.Toward(target!.Position, Allowance(world, unit)), events); break;
            case CombatAction.Retreat: Walk(world, enc, unit, unit.Position.Away(target!.Position, Allowance(world, unit)), events); break;
            case CombatAction.Attack: result = Attack(world, enc, unit, target!, events); break;
            case CombatAction.Throw: result = Throw(world, enc, unit, target!, events); break;
            case CombatAction.ThrowStone:
                Items.Take(Items.PackOf(world.Agent(unit.Agent))!, Resource.Stone, StoneKg);
                result = ThrowStone(world, enc, unit, target!, events);
                break;
            case CombatAction.PickUp:
                var lying = DroppedNear(enc, unit)!;
                enc.Dropped!.Remove(lying);
                unit.Weapon = lying.Weapon;
                break;
            case CombatAction.Shove: result = Shove(world, enc, unit, target!); break;
            case CombatAction.Intimidate: Intimidate(world, enc, unit); break;
            case CombatAction.Flee: Flight(world, enc, unit, events); break;
            case CombatAction.Chase: PlayerChase(world, enc, unit); break;
            case CombatAction.CallHelp or CombatAction.CallRetreat: Signal(world, enc, unit, action == CombatAction.CallRetreat); break;
            default: throw new NotSupportedException($"No rule for {action}");
        }
        Sweep(world, enc, events, unit.Agent);
        return result;
    }

    // ---- the player's round, driven by Sim.Step(Fight) ----

    internal static Refusal? Refusal(World world, Fight fight)
    {
        // Only the player picks rounds; NPCs fight through the AI (or Combat.Apply) until they step themselves.
        if (fight.Who != AgentId.Player) return new("Only the player fights round by round; others fight through the AI.");
        if (EncounterOf(world, fight.Who) is not Encounter enc) return new("You are not in a fight.");
        var me = UnitOf(enc, fight.Who);
        if (fight.Target is int i)
        {
            if (i < 0 || i >= enc.Units.Count) return new("No such fighter.");
            // Naming someone else spends the round turning to face them, whatever the action.
            if (enc.Units[i] != TargetOf(enc, me) || fight.Action == CombatAction.Turn)
                return Refusal(world, enc, me, CombatAction.Turn, enc.Units[i]);
        }
        return Refusal(world, enc, me, fight.Action, null, fight.To);
    }

    /// The action a player's round really is: turning, when it names someone other than the one faced.
    static CombatAction RoundAction(Encounter enc, CombatUnit me, Fight fight) =>
        fight.Target is int i && enc.Units[i] != TargetOf(enc, me) ? CombatAction.Turn : fight.Action;

    /// The player's action, at the start of the round's minute. Movement may be noticed.
    internal static string? Act(World world, Fight fight, List<WorldEvent> events)
    {
        if (Refusal(world, fight) is { } refused) return refused.Reason;
        var enc = EncounterOf(world, fight.Who)!;
        var unit = UnitOf(enc, fight.Who);
        var start = unit.Position;
        enc.RoundEventsFrom = events.Count;
        CatchUpFlames(world, enc, events); // a flight resolves in the action, and its pursuers weigh the fire
        var action = RoundAction(enc, unit, fight);
        if (action != CombatAction.Chase) LetGo(world, enc); // doing anything else, the player lets the runner go
        Apply(world, enc, unit, action, events, action == CombatAction.Turn ? enc.Units[fight.Target!.Value] : null, fight.To);
        PlayerState(world, enc, unit, action, start);
        if (unit.InField) RunDetection(world, enc, unit, huntingSkill: 0, ExposureMeters(action, start, unit.Position));
        return null;
    }

    /// The rest of the round, after the minute has been lived: everyone else's wounds work, then
    /// everyone else acts. Ends the fight (aftermath and all) when it is over or the player is out.
    internal static string? EndRound(World world, Fight fight, List<WorldEvent> events)
    {
        var enc = EncounterOf(world, fight.Who) ?? throw new InvalidOperationException($"{fight.Who} has no fight to finish");
        var me = UnitOf(enc, fight.Who);
        // Everyone else in the field lived this minute already, on the scheduler (Sim.LiveBodies), before the player's.
        Sweep(world, enc, events, by: null);
        foreach (var u in enc.Units.Where(u => u.SignalModifier != 0 && world.Minute >= u.SignalExpiresMinute))
        {
            u.BoldnessModifier -= u.SignalModifier;
            u.SignalModifier = 0;
        }
        CatchUpFlames(world, enc, events);
        // A snapshot, so a unit falling mid-round costs no one else a turn.
        if (me.InField)
            foreach (var u in Field(enc).Where(u => !IsPlayer(u)).ToList())
            {
                if (IsOver(enc)) break;
                AiTurn(world, enc, u, events);
            }
        BreakOffs(world, enc);
        foreach (var u in enc.Units) u.RunDown = false;
        enc.Rounds++;
        ReTarget(world, enc, me); // its target gone, the player faces the best one left, free
        if (enc.Observer != null) Report(world, enc, events.Skip(enc.RoundEventsFrom).ToList());
        if (me.InField && !IsOver(enc)) return null;
        Finish(world, enc, events);
        return Alive(world, me) ? "fight over" : "died";
    }

    /// The player is out (fled or fell, in a round or not): whoever is left fights it out unwatched,
    /// and the fight leaves the world. No fight outlives the player's part in it.
    internal static void Finish(World world, Encounter enc, List<WorldEvent> events)
    {
        LetGo(world, enc); // nobody left to give chase to a runner the player was weighing up
        Sweep(world, enc, events, by: null);
        RunRounds(world, enc, events, HeadlessRoundCap);
        Aftermath(world, enc, Result(world, enc), events);
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
            int from = events.Count;
            CatchUpFlames(world, enc, events);
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
                Body.TickWounds(body, world.Rng("body", u.Agent));
                // An animal burns and mends in the minute it bleeds (Fauna.Tick).
                if (world.Agent(u.Agent) is { Mind: HerdMind } animal && Body.IsAlive(body)) Fauna.Live(world, animal, body, sleeping: false);
                body.WoundsLivedThroughMinute = Math.Max(body.WoundsLivedThroughMinute, world.Minute - 1) + 1;
            }
            Sweep(world, enc, events, by: null);
            BreakOffs(world, enc);
            foreach (var u in enc.Units) u.RunDown = false;
            enc.Rounds++;
            if (enc.Observer != null) Report(world, enc, events.Skip(from).ToList());
        }
    }

    // ---- primitives ----

    /// A step the fighter takes on its own legs: Move, paid for as metabolic work. A shove or a dodge's
    /// metre is not walked (the dodge's burst pays for it). Stepping into a longer reach meets it braced.
    internal static void Walk(World world, Encounter enc, CombatUnit unit, Cell destination, List<WorldEvent> events)
    {
        var from = unit.Position;
        Move(world, enc, unit, destination);
        Body.Cover(BodyOf(world, unit), RunningOf(world, unit), from.DistanceTo(unit.Position));
        MeetBraced(world, enc, unit, from, events);
    }

    /// How far a fighter strikes from: its weapon's reach, or its body's own, never short of contact.
    public static double StrikeRangeM(World world, CombatUnit u) =>
        Math.Max(TouchingM, u.Weapon is Gear g ? Items.Def(g).Weapon!.ReachM : StatsOf(world, u).ReachM);

    /// What a weapon is worth to whoever picks one: its damage, times how much further than contact it
    /// strikes, since the longer reach strikes first.
    public static double WeaponWorth(Weapon w) => w.Damage * Math.Max(TouchingM, w.ReachM) / TouchingM;

    /// A fighter standing off or giving ground holds its weapon braced toward the one it faces: when that
    /// one steps from outside its reach to inside it, and its own reach is the shorter, the braced one
    /// strikes first. Nothing more than reach decides it, so a spear braces against a wolf and a tusk
    /// against a person.
    static void MeetBraced(World world, Encounter enc, CombatUnit mover, Cell from, List<WorldEvent> events)
    {
        double moverRangeM = StrikeRangeM(world, mover);
        foreach (var braced in Enemies(enc, mover).ToList())
        {
            if (!mover.InField || IsDown(world, mover)) return;
            if (TargetOf(enc, braced) != mover || IsDown(world, braced) || Body.IsAsleep(BodyOf(world, braced))
                || StateOf(world, braced) is not (FighterState.Standoff or FighterState.GivingGround)) continue;
            double rangeM = StrikeRangeM(world, braced);
            if (rangeM > moverRangeM && from.DistanceTo(braced.Position) > rangeM && Refusal(world, enc, braced, CombatAction.Attack) == null)
                Attack(world, enc, braced, mover, events);
        }
    }

    /// Steps to a cell (or a free one beside it), on the grid. How the enemies
    /// take it is read off the mover's state once a round (RoundMorale), not off each step's direction.
    internal static void Move(World world, Encounter enc, CombatUnit unit, Cell destination)
    {
        // The grid's edge holds: a fighter leaves only by running (Flight) or breaking off.
        unit.Position = ResolveDestination(world, enc, OnTheGrid(destination), unit);
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
        return free.Count == 0 ? mover.Position : free[world.Rng("combat", mover.Agent).NextInt(free.Count)];
    }

    static ActionResult Attack(World world, Encounter enc, CombatUnit attacker, CombatUnit defender, List<WorldEvent> events)
    {
        Exert(world, attacker);
        return world.Rng("combat", attacker.Agent).NextDouble() < 0.9 ? Strike(world, enc, attacker, defender, AverageBlow(world, attacker), events) : new ActionResult();
    }

    /// A strike, throw, shove or dodge: its slice of the sprint reserve and its seconds of hard work.
    static void Exert(World world, CombatUnit u) => Body.Burst(BodyOf(world, u), RunningOf(world, u));

    /// A thrown weapon: closer is surer, small targets are harder, and the weapon lands where the target
    /// stands, to be picked up again (PickUp); the aftermath piles it if nobody does.
    static ActionResult Throw(World world, Encounter enc, CombatUnit attacker, CombatUnit target, List<WorldEvent> events)
    {
        var weapon = attacker.Weapon ?? throw new InvalidOperationException("No weapon to throw");
        Exert(world, attacker);
        double chance = ThrowChance(world, attacker, target, weapon);
        var at = target.Position;
        var result = world.Rng("combat", attacker.Agent).NextDouble() < chance
            ? Strike(world, enc, attacker, target, AverageThrow(world, attacker, Items.Def(weapon).Weapon!), events) : new ActionResult();
        attacker.Weapon = null; // after the strike: it is named by the weapon
        (enc.Dropped ??= []).Add(new DroppedWeapon(at, weapon, attacker.Agent));
        return result;
    }

    /// The chance a throw of this weapon at this target lands, as the thrower would judge it.
    public static double ThrowChance(World world, CombatUnit attacker, CombatUnit target, Gear weapon) =>
        Items.Def(weapon).Weapon!.Thrown is ThrownWeapon t ? ThrownChance(world, attacker, target, t) : 0;

    /// The chance a stone thrown at this target lands.
    public static double StoneChance(World world, CombatUnit attacker, CombatUnit target) => ThrownChance(world, attacker, target, Stone.Thrown!);

    static double ThrownChance(World world, CombatUnit attacker, CombatUnit target, ThrownWeapon t) =>
        ThrownAccuracy(attacker.Position.DistanceTo(target.Position), t.RangeM, t.Accuracy, StatsOf(world, target).IsSmall);

    static Weapon Stone => Items.GearKinds[StoneKind].Weapon!;

    /// Too quick to dodge or block. Needs no weapon in hand.
    static ActionResult ThrowStone(World world, Encounter enc, CombatUnit attacker, CombatUnit target, List<WorldEvent> events)
    {
        Exert(world, attacker);
        var rng = world.Rng("combat", attacker.Agent);
        if (rng.NextDouble() >= StoneChance(world, attacker, target)) return new ActionResult();
        var (amount, type) = Luck(AverageThrow(world, attacker, Stone), rng);
        amount *= AwarenessDamageFactor(target.Awareness);
        Engage(world, enc, target);
        var damage = Hurt(world, enc, target, amount, type, StrikerOf(world, attacker, thrown: "thrown stone"), events);
        AfterHit(world, enc, attacker, target, events);
        return new ActionResult(Hit: true, Damage: damage);
    }

    /// The defender may still turn the blow aside (DefenceChance): a block on its weapon, which wears it, or a
    /// dodge. Either does no harm and gives a metre, so a weapon never makes the defence worse than none.
    static ActionResult Strike(World world, Encounter enc, CombatUnit attacker, CombatUnit defender, (double Amount, DamageType Type) blow,
        List<WorldEvent> events)
    {
        if (DefenceChance(world, enc, defender, attacker) is double defence and > 0 && world.Rng("combat", defender.Agent).NextDouble() < defence)
        {
            Body.Burst(BodyOf(world, defender), RunningOf(world, defender));
            if (defender.Weapon is Gear weapon) Items.Use(weapon);
            Move(world, enc, defender, OnTheGrid(defender.Position.Move(attacker.Position.DirectionTo(defender.Position), 1.0f)));
            return new ActionResult(Dodged: defender.Weapon == null, Blocked: defender.Weapon != null);
        }
        var (amount, type) = Luck(blow, world.Rng("combat", attacker.Agent));
        amount *= AwarenessDamageFactor(defender.Awareness);
        var damage = Hurt(world, enc, defender, amount, type, StrikerOf(world, attacker), events);
        Engage(world, enc, defender);
        AfterHit(world, enc, attacker, defender, events);
        return new ActionResult(Hit: true, Damage: damage);
    }

    /// Landing a blow heartens the striker a little; being hurt costs morale through the wound
    /// itself, for as long as it lasts (HealthPenalty), not as an event.
    static void AfterHit(World world, Encounter enc, CombatUnit attacker, CombatUnit defender, List<WorldEvent> events)
    {
        Morale(world, enc, attacker, MoraleEvent.DealtDamage, defender);
        Sweep(world, enc, events, attacker.Agent);
    }

    /// Luck takes a blow from half to one and a half of its average.
    static (double Amount, DamageType Type) Luck((double Amount, DamageType Type) blow, Rng rng) => (blow.Amount * (0.5 + rng.NextDouble()), blow.Type);

    /// The blow before luck: the weapon's damage, or the species' own unarmed, as an average fresh member of
    /// its kind strikes it, scaled by its strength against theirs (half weight) and its vitality 70-100%.
    internal static (double Amount, DamageType Type) AverageBlow(World world, CombatUnit attacker) =>
        attacker.Weapon is Gear g && Items.Def(g).Weapon is Weapon w ? Behind(world, attacker, w.Damage, TypeOf(w))
            : Behind(world, attacker, StatsOf(world, attacker).AttackDamage, StatsOf(world, attacker).AttackType);

    /// A throw before luck: the thrown row's damage, with the same strength and vitality behind it.
    internal static (double Amount, DamageType Type) AverageThrow(World world, CombatUnit thrower, Weapon weapon) =>
        Behind(world, thrower, weapon.Thrown!.Damage, TypeOf(weapon));

    static (double Amount, DamageType Type) Behind(World world, CombatUnit attacker, double damage, DamageType type)
    {
        var body = BodyOf(world, attacker);
        double strengthPct = SpeciesTable.StrengthPct(world.Agent(attacker.Agent).Species, body);
        return (damage * (strengthPct / 2 + 0.5) * (0.7 + 0.3 * Body.Vitality(body)), type);
    }

    static DamageType TypeOf(Weapon weapon) => weapon.Class switch
    {
        WeaponClass.Blade => DamageType.Sharp,
        WeaponClass.Pierce => DamageType.Pierce,
        _ => DamageType.Blunt,
    };

    /// Who struck a blow and with what, as the body it lands on names it: "the player's wooden spear",
    /// "a wolf pack". Read as the blow lands, so a thrown weapon is named before it leaves the hand.
    static Cause StrikerOf(World world, CombatUnit attacker, string? thrown = null)
    {
        string who = Cause.Describe(world, attacker.Agent);
        string? with = thrown ?? (attacker.Weapon is { } w ? Items.Def(w).Name.ToLowerInvariant() : null);
        return new Cause(attacker.Agent, with == null ? who : $"{who}'s {with}");
    }

    /// Through what the member wears over the region hit (Body.Damage). The body keeps who hurt it
    /// (BodyState.WoundedBy): whatever it dies of later names them.
    static DamageResult Hurt(World world, Encounter enc, CombatUnit target, double amount, DamageType type, Cause by, List<WorldEvent> events)
    {
        var body = BodyOf(world, target);
        body.WoundedBy = by;
        var result = Body.Damage(body, new DamageInfo(amount, type, BodyTarget.Random, by), world.Rng("body", target.Agent));
        events.Add(new Injured(world.Minute, target.Agent, enc.Tile, result.HitRegion, type, amount));
        return result;
    }

    /// Unaware targets take half again: the ambush.
    public static double AwarenessDamageFactor(Awareness a) => a == Awareness.Unaware ? 1.5 : 1.0;

    /// The chance an aware fighter turns a blow aside:
    /// 60% fresh and unladen, cut as its speed is (Running.SpeedPct: wounds, tiredness, load), higher
    /// giving ground or standing off, lower pressing in. None without breath for the burst, none for a
    /// fighter that does not know the fight is on or is running, and none against anyone but its own
    /// target: a blow from the side lands unguarded.
    public static double DefenceChance(World world, Encounter enc, CombatUnit defender, CombatUnit attacker)
    {
        if (TargetOf(enc, defender) != attacker || defender.RunDown) return 0; // caught running: no defence this round
        double stance = StateOf(world, defender) switch
        {
            FighterState.GivingGround => GivingGroundDefenceFactor,
            FighterState.Standoff => 1.0,
            FighterState.Pressing => PressingDefenceFactor,
            _ => 0,
        };
        var running = RunningOf(world, defender);
        if (stance <= 0 || !Body.CanBurst(BodyOf(world, defender), running)) return 0;
        return Math.Min(MaxDefencePct, DefenceBasePct * stance * running.SpeedPct);
    }

    const double DefenceBasePct = 0.6, GivingGroundDefenceFactor = 1.3, PressingDefenceFactor = 0.5, MaxDefencePct = 0.9;

    /// Weight behind it against weight braced against it; a target standing off or giving ground has
    /// its feet set and is half as easy to move.
    static ActionResult Shove(World world, Encounter enc, CombatUnit attacker, CombatUnit target)
    {
        Exert(world, attacker);
        var rng = world.Rng("combat", attacker.Agent);
        var (chance, pushM) = ShoveOdds(BodyOf(world, attacker), RunningOf(world, attacker), BodyOf(world, target), RunningOf(world, target));
        if (StateOf(world, target) is FighterState.Standoff or FighterState.GivingGround) chance /= 2;
        if (rng.NextDouble() >= chance) return new ActionResult();
        Move(world, enc, target, OnTheGrid(target.Position.Move(attacker.Position.DirectionTo(target.Position), (float)pushM)));
        return new ActionResult(Hit: true);
    }

    /// Mass times how well each moves now (Running.SpeedPct: wounds, tiredness, load): a heavier, fitter
    /// body moves a lighter one.
    public static (double Chance, double PushM) ShoveOdds(BodyState attacker, Running attackerRunning, BodyState target, Running targetRunning)
    {
        double ratio = attacker.WeightKg * attackerRunning.SpeedPct / (target.WeightKg * targetRunning.SpeedPct);
        return (Math.Min(MaxShovePct, 0.5 * ratio), Math.Min(MaxShoveM, 1.0 + 3 * ratio));
    }

    const double MaxShovePct = 0.95, MaxShoveM = 10;

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

    /// The allies a call reaches: people beside the caller, not animals.
    static IEnumerable<CombatUnit> Hearers(World world, Encounter enc, CombatUnit caller) =>
        Allies(enc, caller).Where(u => !IsPlayer(u) && !StatsOf(world, u).IsAnimal);

    /// A call to the human companions on this side: a morale nudge that replaces the last one and
    /// lapses after five minutes.
    static void Signal(World world, Encounter enc, CombatUnit caller, bool retreat)
    {
        caller.LastSignalMinute = world.Minute;
        foreach (var ally in Hearers(world, enc, caller))
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
            var body = BodyOf(world, unit);
            if (StatsOf(world, unit).IsAnimal)
                events.Add(new Killed(world.Minute, unit.Agent, agent.Species, body.WeightKg, enc.Tile, body.WoundedBy?.Who ?? by));
            else
            {
                Body.MarkDead(body, world.Minute, Body.CauseOfDeath(body));
                if (!events.Any(e => e is Died d && d.Who == unit.Agent)) events.Add(new Died(world.Minute, unit.Agent, enc.Tile));
            }
        }
    }

    // ---- detection ----

    /// Ground covered this turn; any other action is as noticeable as a stalking step.
    public static double ExposureMeters(Cell before, Cell after) => before == after ? StalkStepMeters : before.DistanceTo(after);

    /// As above for the player's round, where keeping still is a choice: freezing shows a little, never
    /// nothing.
    static double ExposureMeters(CombatAction action, Cell before, Cell after) => action == CombatAction.Wait ? FrozenExposureMeters : ExposureMeters(before, after);

    public const double StalkStepMeters = 3.0;
    /// As noticeable as this many metres of stalking: a hunter keeping still, breathing and watching.
    public const double FrozenExposureMeters = 0.5;
    /// Metres of movement over which DetectionChance applies in full. Tuned: ~70% of stalks on a
    /// feeding animal reach 8 m, ~10% on an alert one.
    public const double DetectionReferenceMeters = 20.0;

    /// Chance an animal notices movement at this distance: 100 m ~0%, 50 m ~20%, 30 m ~50%, 10 m ~90%
    /// before awareness and skill.
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
    public static double ThrownAccuracy(double distanceM, double maxRangeM, double baseAccuracy, bool targetIsSmall, double manipulationPenalty = 0) =>
        ThrownOdds(distanceM, maxRangeM, baseAccuracy, targetIsSmall, manipulationPenalty).TotalPct;

    /// The throw's odds as their terms: the weapon's accuracy, scaled down with
    /// distance and for a small target, less any penalty for the hands; 5-95%. Out of range is 0.
    public static Odds ThrownOdds(double distanceM, double maxRangeM, double baseAccuracy, bool targetIsSmall, double manipulationPenalty = 0)
    {
        if (distanceM > maxRangeM) return new Odds("Out of range", 0, [], MinPct: 0, MaxPct: 0);
        return new Odds("Weapon", baseAccuracy, Odds.Moving(
            OddsTerm.Scale("Distance", 1.0 - distanceM / maxRangeM),
            OddsTerm.Scale("Small target", targetIsSmall ? 0.66 : 1.0),
            OddsTerm.Add("Hands", -manipulationPenalty)), MinPct: 0.05, MaxPct: 0.95);
    }

    /// Risk of one normal stalking step being noticed, as shown to the player.
    public static double DetectionRisk(World world, Encounter enc, CombatUnit mover, CombatUnit detector, int huntingSkill = 0) =>
        ExposedDetectionChance(world, enc, mover, detector, huntingSkill, StalkStepMeters);

    static double ExposedDetectionChance(World world, Encounter enc, CombatUnit mover, CombatUnit detector, int huntingSkill, double exposureMeters)
    {
        double chance = DetectionChance(mover.Position.DistanceTo(detector.Position), detector.Awareness, huntingSkill) * detector.DetectionFactor;
        // Cover: up to 30% less on ground with nowhere to see far (visibility 0) than on open ground (2).
        chance *= 1.0 - (1.0 - CoverVisibility(world, enc.Tile) / 2.0) * 0.3;
        return StepDetectionChance(Math.Clamp(chance, 0.05, 0.95), exposureMeters);
    }

    /// Tile visibility (0-2): terrain, camp, cave or named site (Map.Visibility).
    static double CoverVisibility(World world, TileId tile) => Map.Visibility(Map.Of(world), tile);

    /// Enemies not yet in the fight look at the mover: noticing it raises their alertness, seeing
    /// nothing lets it fade, and the awareness bands move them (Combat.Alert).
    internal static void RunDetection(World world, Encounter enc, CombatUnit mover, int huntingSkill, double exposureMeters)
    {
        // Seeing nothing calms a watcher less while the mover covers more ground than a stalking step.
        double fade = FadeAlertnessDelta * Math.Min(1, StalkStepMeters / Math.Max(exposureMeters, 1e-9));
        foreach (var enemy in Enemies(enc, mover).Where(e => !IsAware(e.State)).ToList())
            Alert(world, enc, enemy, world.Rng("combat", enemy.Agent).NextDouble() < ExposedDetectionChance(world, enc, mover, enemy, huntingSkill, exposureMeters)
                ? NoticeAlertnessDelta : -fade);
    }
}
