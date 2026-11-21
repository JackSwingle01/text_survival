namespace Survival.Tune;

/// One way a fight is set up and fought: the setup (who meets whom, how it opens), the person's
/// weapon and condition, and the policy that fights the person's side. Named in the band spec.
public sealed record Scenario(string Setup, string Weapon = "none", string Cond = "healthy", string Policy = "ai")
{
    public override string ToString() => $"{Setup}/{Weapon}/{Cond}/{Policy}";
}

/// What one fight came to, from team A's side (the person, or the hunters in an animal-only fight).
public sealed record FightOutcome(string Result, bool ADied, bool BKilled, bool AFled, int Rounds)
{
    /// Each metric a band can name, for one fight. "a" is team A (the person, or the pack in an
    /// animal-only fight), "b" is team B (the animal, or the prey).
    public double Metric(string name) => name switch
    {
        "aDies" => ADied ? 1 : 0,
        "bKilled" => BKilled ? 1 : 0,
        "aFled" => AFled ? 1 : 0,
        "bFled" => Result == nameof(CombatResult.AnimalFled) ? 1 : 0,
        // B is gone and A lived: killed or driven off.
        "aWinsOrDrivesOff" => !ADied && (Result is nameof(CombatResult.Victory) or nameof(CombatResult.AnimalFled)) ? 1 : 0,
        // B left (ran or broke off) alive, and A lived.
        "bLeaves" => !ADied && !BKilled && Result is nameof(CombatResult.AnimalFled) or nameof(CombatResult.AnimalDisengaged) ? 1 : 0,
        "rounds" => Rounds,
        _ => throw new ArgumentException($"No metric '{name}' (aDies, bKilled, aFled, bFled, aWinsOrDrivesOff, bLeaves, rounds)"),
    };

    public static readonly string[] Metrics = ["aDies", "bKilled", "aFled", "bFled", "aWinsOrDrivesOff", "bLeaves", "rounds"];
}

/// Runs single fights, each in a world of its own made from one shared map: nothing carries from one
/// fight to the next, so a fight is a function of its scenario, its seed and the content tables, and
/// fights can run on many threads at once. Scenarios and policies are the combat survey's probe
/// (documentation/combat-survey-2026-11-21.md), less its instrumentation, plus the bear stances.
public sealed class Fights
{
    static readonly AgentId P = AgentId.Player;
    readonly GeneratedMap map;
    readonly TileId tile;

    public Fights(int mapSeed = 1000)
    {
        map = Map.Generate(mapSeed);
        tile = Quiet(map);
    }

    /// A plain tile far from camp with nothing blocking its edges (the survey's fight ground).
    static TileId Quiet(GeneratedMap m) =>
        Enumerable.Range(3, m.Width - 6).SelectMany(x => Enumerable.Range(3, m.Height - 6).Select(y => new TileId(x, y)))
            .First(t => m.TerrainAt(t) == Terrain.Plain && m.CaveAt(t) == Cave.None && MapGen.Manhattan(t, m.Camp) > 30
                && MapGen.Neighbors(t).All(nb => Map.Blocked(m, t, nb) == null));

    public static readonly string[] Setups = ["hunt_caribou_resting", "hunt_caribou_grazing", "hunt_bison_resting",
        "wolf1_ambush", "wolf1_aware", "pack3_aware", "pack3_ambush", "pack5_aware", "pack5_ambush", "bear_close",
        "wolves4_vs_caribou1_noticed", "wolves4_vs_caribou1_unnoticed", "wolves4_vs_caribou3_noticed", "wolves4_vs_bison1_noticed",
        "two_people_vs_wolves4", "one_person_vs_wolves4"];
    public static readonly string[] Policies = ["ai", "aggressive", "cautious", "stand_intimidate", "run",
        "careful_melee", "careful_throw", "quick_melee", "quick_throw"];
    public static readonly string[] Weapons = ["none", "flint_knife", "wooden_spear", "stone_axe"];
    public static readonly string[] Conds = ["healthy", "tired", "hungry", "cold95", "cold92", "leg", "arm", "bleeding", "meat"];

    /// Refuses a scenario this harness cannot run, before any fight does.
    public static void Check(Scenario s)
    {
        if (!Setups.Contains(s.Setup)) throw new ArgumentException($"{s}: no setup '{s.Setup}' ({string.Join(", ", Setups)})");
        if (!Policies.Contains(s.Policy)) throw new ArgumentException($"{s}: no policy '{s.Policy}'");
        if (!Weapons.Contains(s.Weapon)) throw new ArgumentException($"{s}: no weapon '{s.Weapon}'");
        if (!Conds.Contains(s.Cond)) throw new ArgumentException($"{s}: no condition '{s.Cond}'");
        bool hunt = s.Setup.StartsWith("hunt_"), headless = s.Setup.Contains("wolves4");
        if (hunt != s.Policy is "careful_melee" or "careful_throw" or "quick_melee" or "quick_throw")
            throw new ArgumentException($"{s}: hunts take a careful_/quick_ policy, and only hunts do");
        if (headless && s.Policy != "ai") throw new ArgumentException($"{s}: an animal or NPC-only fight is fought by the AI");
    }

    public FightOutcome Run(Scenario s, int seed)
    {
        var w = new World { Seed = seed, GeneratedMap = map };
        w.Agents[P] = new Agent { Id = P, Tile = tile };
        return new OneFight(w, tile, s).Go();
    }

    /// One fight in its world. Drivers and policies follow the probe line for line.
    sealed class OneFight(World w, TileId tile, Scenario c)
    {
        int frozen;

        public FightOutcome Go()
        {
            if (c.Setup.StartsWith("hunt_")) return Hunt();
            if (c.Setup.StartsWith("wolves4_vs_")) return PackVsPrey();
            if (c.Setup.EndsWith("_vs_wolves4")) return PeopleVsWolves();
            return PersonVsAnimal();
        }

        static BodyState Body_(string cond, Inventory pack)
        {
            var b = BodyState.BaselineHuman();
            switch (cond)
            {
                case "healthy": break;
                case "tired": b.EnergyMinutes = 48; Effects.Add(b, EffectKind.Tired, (0.3 - 48.0 / 960) / 0.3); break;
                case "hungry":
                    b.CaloriesKcal = 0;
                    b.FatKg = 0.06 * b.WeightKg;
                    Effects.Add(b, EffectKind.Hungry, 1.0);
                    break;
                case "cold95": b.CoreTempF = 95.0; Effects.Add(b, EffectKind.Shivering, 0.4); break;
                case "cold92": b.CoreTempF = 92.0; Effects.Add(b, EffectKind.Shivering, 1.0); Effects.Add(b, EffectKind.Hypothermia, 0.3); break;
                case "leg":
                    foreach (var r in b.Regions.Where(r => r.Name.Contains("Leg"))) { r.Muscle.Condition = 0.25; r.Bone.Condition = 0.5; }
                    break;
                case "arm":
                    var arm = b.Regions.First(r => r.Name == "Right Arm");
                    arm.Muscle.Condition = 0.2; arm.Bone.Condition = 0.5;
                    break;
                case "bleeding": Effects.Add(b, EffectKind.Bleeding, 0.3); break;
                case "meat": Items.Add(pack, Resource.RawMeat, 30); break;
                default: throw new ArgumentException(cond);
            }
            return b;
        }

        static Gear? Arm(Inventory pack, string weapon)
        {
            if (weapon == "none") return null;
            var g = Items.Make(weapon);
            pack.Gear.Add(g);
            return g;
        }

        Gear? ArmPlayer()
        {
            var pack = Items.Pack(w, P);
            pack.Gear.Clear(); pack.Kg.Clear();
            w.Agent(P).Members[0] = Body_(c.Cond, pack);
            return Arm(pack, c.Weapon);
        }

        AgentId Person(out Gear? g)
        {
            var pack = new Inventory();
            var body = Body_(c.Cond, pack);
            g = Arm(pack, c.Weapon);
            var id = new AgentId(w.NextAgentId++);
            w.Agents[id] = new Agent { Id = id, Tile = tile, Species = Species.Human, Members = [body], Mind = new PlayerMind { Pack = pack } };
            return id;
        }

        // ---- the player's side, round by round through Sim.Step ----

        FightOutcome DrivePlayer(Encounter enc, Func<Encounter, CombatUnit, (CombatAction, CombatUnit?, Cell?)> policy)
        {
            var person = Combat.UnitOf(enc, P);
            var events = new List<WorldEvent>();
            for (int round = 0; round < 300 && w.Encounters.Contains(enc); round++)
            {
                var (act, target, to) = policy(enc, person);
                var r = Sim.Step(w, new Fight(P, act, target == null ? null : enc.Units.IndexOf(target), to), 1);
                events.AddRange(r.Events);
                if (r.InterruptedReason != null && r.InterruptedReason != "fight over" && r.InterruptedReason != "died" && w.Encounters.Contains(enc))
                    events.AddRange(Sim.Step(w, new Fight(P, CombatAction.Wait), 1).Events); // a refused action: wait instead
            }
            if (w.Encounters.Contains(enc)) return new("Unfinished", !Body.IsAlive(w.Agent(P).Body), BKilled(enc, events), false, enc.Rounds);
            var result = events.OfType<FightEnded>().LastOrDefault(f => f.Agent == P)?.Result.ToString() ?? "?";
            bool dead = !Body.IsAlive(w.Agent(P).Body);
            return new(result, dead, BKilled(enc, events), !dead && result == nameof(CombatResult.Fled), enc.Rounds);
        }

        static bool BKilled(Encounter enc, List<WorldEvent> events)
        {
            var b = enc.Units.Where(u => u.Team == 1).Select(u => u.Agent).ToHashSet();
            return events.OfType<Killed>().Any(k => b.Contains(k.Agent));
        }

        const int frozenCap = 3;

        (CombatAction, CombatUnit?, Cell?) HuntPolicy(Encounter e, CombatUnit me)
        {
            var acts = Combat.Actions(w, e, me);
            var target = Combat.NearestEnemy(e, me);
            if (target == null) return (CombatAction.Wait, null, null);
            double gap = me.Position.DistanceTo(target.Position);
            bool throws = c.Policy.EndsWith("throw");
            if (acts.Contains(CombatAction.Attack)) return (CombatAction.Attack, target, null);
            double throwAt = c.Policy == "quick_throw" ? 10 : 8;
            if (throws && me.Weapon != null && gap <= throwAt && acts.Contains(CombatAction.Throw)) return (CombatAction.Throw, target, null);
            if (c.Policy.StartsWith("careful"))
            {
                if (target.Awareness == Awareness.Engaged) return (CombatAction.Advance, target, null); // it bolted: chase
                // freeze while any is alert, at most 3 rounds running (Alert never lapses)
                if (e.Units.Any(u => u.Team == 1 && u.InField && u.Awareness == Awareness.Alert) && frozen < frozenCap) { frozen++; return (CombatAction.Wait, null, null); }
                frozen = 0;
                var step = me.Position.Toward(target.Position, 1);
                if (step == me.Position || step == target.Position) return (CombatAction.Advance, target, null);
                return (CombatAction.Move, null, step);
            }
            return (CombatAction.Advance, target, null);
        }

        (CombatAction, CombatUnit?, Cell?) TowardEdge(Encounter e, CombatUnit me, CombatUnit target)
        {
            var acts = Combat.Actions(w, e, me);
            if (acts.Contains(CombatAction.Flee)) return (CombatAction.Flee, null, null);
            var p = me.Position;
            int left = p.X, right = Combat.MapSize - 1 - p.X, down = p.Y, up = Combat.MapSize - 1 - p.Y;
            int min = Math.Min(Math.Min(left, right), Math.Min(down, up));
            int a = Combat.Allowance(w, me);
            var to = min == left ? new Cell(p.X - a, p.Y) : min == right ? new Cell(p.X + a, p.Y) : min == down ? new Cell(p.X, p.Y - a) : new Cell(p.X, p.Y + a);
            to = new Cell(Math.Clamp(to.X, 0, Combat.MapSize - 1), Math.Clamp(to.Y, 0, Combat.MapSize - 1));
            if (to != p && Combat.Refusal(w, e, me, CombatAction.Move, null, to) == null) return (CombatAction.Move, null, to);
            return (CombatAction.Retreat, target, null);
        }

        (CombatAction, CombatUnit?, Cell?) FightPolicy(Encounter e, CombatUnit me)
        {
            var acts = Combat.Actions(w, e, me);
            var target = Combat.NearestEnemy(e, me);
            if (target == null) return (CombatAction.Wait, null, null);
            switch (c.Policy)
            {
                case "cautious":
                    var body = Combat.BodyOf(w, me);
                    if (Combat.WoundLevel(body) > 0.3 || body.BloodPct < 0.85 || Effects.Severity(body, EffectKind.Bleeding) > 0.4)
                        return TowardEdge(e, me, target);
                    break;
                case "run": return TowardEdge(e, me, target);
                // Stand your ground and shout. Today's rules refuse Intimidate within 3 m, so up close
                // this stance can only guard.
                case "stand_intimidate":
                    if (acts.Contains(CombatAction.Intimidate)) return (CombatAction.Intimidate, null, null);
                    if (acts.Contains(CombatAction.Block)) return (CombatAction.Block, null, null);
                    return (CombatAction.Wait, null, null);
            }
            if (acts.Contains(CombatAction.Attack)) return (CombatAction.Attack, target, null);
            return (CombatAction.Advance, target, null);
        }

        FightOutcome Hunt()
        {
            var g = ArmPlayer();
            var species = c.Setup.Contains("bison") ? Species.Bison : Species.Caribou;
            var herd = Agents.Spawn(w, species, tile, 4);
            ((HerdMind)w.Agent(herd).Mind).State = c.Setup.Contains("grazing") ? HerdState.Grazing : HerdState.Resting;
            var enc = Fauna.BeginHunt(w, P, herd, 0, g);
            return DrivePlayer(enc, HuntPolicy);
        }

        static (int count, Species s, Opening o, int dist, Awareness a) AnimalSetup(string sc) => sc switch
        {
            "wolf1_ambush" => (1, Species.Wolf, Opening.Ambush, 3, Awareness.Unaware),
            "wolf1_aware" => (1, Species.Wolf, Opening.Approach, 0, Awareness.Engaged),
            "pack3_aware" => (3, Species.Wolf, Opening.Approach, 0, Awareness.Engaged),
            "pack3_ambush" => (3, Species.Wolf, Opening.Ambush, 4, Awareness.Unaware),
            "pack5_aware" => (5, Species.Wolf, Opening.Approach, 0, Awareness.Engaged),
            "pack5_ambush" => (5, Species.Wolf, Opening.Ambush, 4, Awareness.Unaware),
            "bear_close" => (1, Species.Bear, Opening.CloseEncounter, 8, Awareness.Engaged),
            _ => throw new ArgumentException(sc),
        };

        FightOutcome PersonVsAnimal()
        {
            var (count, s, o, dist, aw) = AnimalSetup(c.Setup);
            var herd = Agents.Spawn(w, s, tile, count);
            var animals = Enumerable.Range(0, count).Select(i => new Fighter(herd, i)).ToList();
            if (c.Policy != "ai")
            {
                var weapon = ArmPlayer();
                var enc = Combat.Create(w, [new Fighter(P) { Weapon = weapon }], animals, tile, dist, aw, Awareness.Engaged, o);
                return DrivePlayer(enc, FightPolicy);
            }
            var pid = Person(out var g);
            var headless = Combat.Create(w, [new Fighter(pid) { Weapon = g }], animals, tile, dist, aw, Awareness.Engaged, o);
            var events = new List<WorldEvent>();
            var r = Combat.ResolveHeadless(w, headless, events);
            bool dead = !Body.IsAlive(w.Agent(pid).Members[0]);
            return new(r.ToString(), dead, BKilled(headless, events), !dead && r == CombatResult.Fled, headless.Rounds);
        }

        FightOutcome PackVsPrey()
        {
            var pack = Agents.Spawn(w, Species.Wolf, tile, 4);
            var preySpecies = c.Setup.Contains("bison") ? Species.Bison : Species.Caribou;
            int preyCount = c.Setup.Contains("caribou3") ? 3 : 1;
            var prey = Agents.Spawn(w, preySpecies, tile, 4);
            bool noticed = c.Setup.EndsWith("_noticed");
            var enc = Combat.Create(w, [.. Enumerable.Range(0, 4).Select(i => new Fighter(pack, i))],
                [.. Enumerable.Range(0, preyCount).Select(i => new Fighter(prey, i))], tile,
                noticed ? 15 : 5, Awareness.Engaged, noticed ? Awareness.Alert : Awareness.Unaware);
            var events = new List<WorldEvent>();
            var r = Combat.ResolveHeadless(w, enc, events);
            bool wolfDied = events.OfType<Killed>().Any(k => k.Agent == pack);
            return new(r.ToString(), wolfDied, BKilled(enc, events), r == CombatResult.Fled, enc.Rounds);
        }

        FightOutcome PeopleVsWolves()
        {
            int people = c.Setup.StartsWith("two") ? 2 : 1;
            var ids = new List<Fighter>();
            for (int i = 0; i < people; i++) { var pid = Person(out var g); ids.Add(new Fighter(pid) { Weapon = g }); }
            var pack = Agents.Spawn(w, Species.Wolf, tile, 4);
            var enc = Combat.Create(w, ids, [.. Enumerable.Range(0, 4).Select(i => new Fighter(pack, i))], tile, 0,
                Awareness.Engaged, Awareness.Engaged, Opening.Approach);
            var events = new List<WorldEvent>();
            var r = Combat.ResolveHeadless(w, enc, events);
            bool dead = events.OfType<Died>().Any();
            return new(r.ToString(), dead, BKilled(enc, events), r == CombatResult.Fled, enc.Rounds);
        }
    }
}
