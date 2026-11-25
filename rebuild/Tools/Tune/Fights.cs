namespace Survival.Tune;

/// One way a fight is set up and fought: the setup (who meets whom, how it opens), the person's
/// weapon and condition, the policy that fights the person's side, and any fire: a strong campfire on
/// the tile (at Encounter.FireCell, where the person starts) or a lit torch in the person's
/// hand. Named in the band spec.
public sealed record Scenario(string Setup, string Weapon = "none", string Cond = "healthy", string Policy = "ai", string Fire = "none")
{
    public override string ToString() => $"{Setup}/{Weapon}/{Cond}/{Policy}" + (Fire == "none" ? "" : $"/{Fire}");
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

    /// The ending as one letter, for the run log: V Victory, D Defeat, F Fled, A AnimalFled,
    /// X AnimalDisengaged, U Unfinished, ? none recorded.
    public static readonly IReadOnlyDictionary<string, char> EndingCodes = new Dictionary<string, char>
    {
        [nameof(CombatResult.Victory)] = 'V',
        [nameof(CombatResult.Defeat)] = 'D',
        [nameof(CombatResult.Fled)] = 'F',
        [nameof(CombatResult.AnimalFled)] = 'A',
        [nameof(CombatResult.AnimalDisengaged)] = 'X',
        ["Unfinished"] = 'U',
        ["?"] = '?',
    };

    /// One fight in a few characters: ending letter, a digit of flags (1 A died, 2 B killed, 4 A fled),
    /// a dot and the rounds. "D1.12" is a defeat in which A died, after 12 rounds.
    public string Code() => $"{EndingCodes[Result]}{(ADied ? 1 : 0) + (BKilled ? 2 : 0) + (AFled ? 4 : 0)}.{Rounds}";

    public static FightOutcome Parse(string code)
    {
        string result = EndingCodes.Single(kv => kv.Value == code[0]).Key;
        int flags = code[1] - '0', dot = code.IndexOf('.');
        if (dot != 2 || flags is < 0 or > 7) throw new FormatException($"Not a fight code: '{code}'");
        return new(result, (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, int.Parse(code[(dot + 1)..], System.Globalization.CultureInfo.InvariantCulture));
    }
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

    /// The map every fight is fought on, unless a caller asks for another.
    public const int DefaultMapSeed = 1000;

    public Fights(int mapSeed = DefaultMapSeed)
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
        "wolf1_ambush", "wolf1_aware", "pack3_aware", "hyena3_aware", "pack3_ambush", "pack5_aware", "pack5_ambush", "bear_close", "bear_den",
        "wolves4_vs_caribou1_noticed", "wolves4_vs_caribou1_unnoticed", "wolves4_vs_caribou3_noticed", "wolves4_vs_bison1_noticed",
        "two_people_vs_wolves4", "one_person_vs_wolves4"];
    public static readonly string[] Policies = ["ai", "aggressive", "cautious", "stand_intimidate", "run",
        "careful_melee", "careful_throw", "quick_melee", "quick_throw"];
    public static readonly string[] Weapons = ["none", "flint_knife", "wooden_spear", "ground_stone_axe", "crude_spear", "staff", "stone",
        "flint_axe", "obsidian_knife", "bone_knife"];
    public static readonly string[] Conds = ["healthy", "tired", "hungry", "cold95", "cold92", "leg", "arm", "bleeding", "meat"];
    public static readonly string[] Fires = ["none", "campfire", "torch"];

    /// Refuses a scenario this harness cannot run, before any fight does.
    public static void Check(Scenario s)
    {
        if (!Setups.Contains(s.Setup)) throw new ArgumentException($"{s}: no setup '{s.Setup}' ({string.Join(", ", Setups)})");
        if (!Policies.Contains(s.Policy)) throw new ArgumentException($"{s}: no policy '{s.Policy}'");
        if (!Weapons.Contains(s.Weapon)) throw new ArgumentException($"{s}: no weapon '{s.Weapon}'");
        if (!Conds.Contains(s.Cond)) throw new ArgumentException($"{s}: no condition '{s.Cond}'");
        if (!Fires.Contains(s.Fire)) throw new ArgumentException($"{s}: no fire '{s.Fire}' ({string.Join(", ", Fires)})");
        bool hunt = s.Setup.StartsWith("hunt_"), headless = s.Setup.Contains("wolves4");
        if (hunt != s.Policy is "careful_melee" or "careful_throw" or "quick_melee" or "quick_throw")
            throw new ArgumentException($"{s}: hunts take a careful_/quick_ policy, and only hunts do");
        if (headless && s.Policy != "ai") throw new ArgumentException($"{s}: an animal or NPC-only fight is fought by the AI");
    }

    /// One fight. A watcher, if given, sees the grid at the opening and after every round, through Sim's
    /// round observer (Combat.Watch), headless fights too; it only reads, so a watched fight is the same fight.
    public FightOutcome Run(Scenario s, int seed, Action<TraceFrame>? watch = null)
    {
        var w = new World { Seed = seed, GeneratedMap = map };
        w.Agents[P] = new Agent { Id = P, Tile = tile };
        return new OneFight(w, tile, s, watch).Go();
    }

    /// Whether Run can show this scenario's rounds: every fight, now that Sim reports its rounds.
    public static bool Watchable(Scenario s) => true;

    /// One fight in its world. Drivers and policies follow the probe line for line.
    sealed class OneFight(World w, TileId tile, Scenario c, Action<TraceFrame>? watch)
    {
        int frozen;

        public FightOutcome Go()
        {
            if (c.Fire == "campfire") Campfire();
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
                case "meat": Items.AddPieces(pack, Resource.RawMeat, Enumerable.Repeat(1.0, 30)); break;
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

        /// A strong fire on the fight's tile: sticks and pine firewood, burned for half an hour so the pine has caught.
        void Campfire()
        {
            const int LitMinutesBefore = 30;
            var f = new FireState { Tile = tile, LastUpdatedMinute = w.Minute - LitMinutesBefore, Pit = PitType.Open };
            if (Fire.AddFuel(f, Resource.Stick, 1.0) < 1.0) throw new InvalidOperationException("the campfire's sticks were refused");
            Fire.IgniteAll(f);
            if (Fire.AddFuel(f, Resource.PineFirewood, 5.0) < 5.0) throw new InvalidOperationException("the campfire's pine firewood was refused");
            var id = new FireId(w.NextFireId++);
            while (f.LastUpdatedMinute < w.Minute) Fire.Tick(f, id, ++f.LastUpdatedMinute, []);
            w.Fires[id] = f;
        }

        /// A freshly lit torch in this person's hand, when the scenario carries one.
        void Torch(AgentId person)
        {
            if (c.Fire != "torch") return;
            w.Fires[new FireId(w.NextFireId++)] = new FireState
            {
                Tile = tile,
                LastUpdatedMinute = w.Minute,
                CarriedBy = person,
                TorchMinutesLeft = Items.GearKinds["torch"].BurnMinutes,
            };
        }

        Gear? ArmPlayer()
        {
            var pack = Items.Pack(w, P);
            pack.Gear.Clear(); pack.Amounts.Clear(); pack.Pieces.Clear();
            w.Agent(P).Members[0] = Body_(c.Cond, pack);
            Torch(P);
            return Arm(pack, c.Weapon);
        }

        AgentId Person(out Gear? g)
        {
            var pack = new Inventory();
            var body = Body_(c.Cond, pack);
            g = Arm(pack, c.Weapon);
            var id = new AgentId(w.NextAgentId++);
            w.Agents[id] = new Agent { Id = id, Tile = tile, Species = Species.Human, Members = [body], Mind = new PlayerMind { Pack = pack } };
            Torch(id);
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
            var result = events.OfType<EncounterEnded>().LastOrDefault(f => f.Agent == P)?.Result.ToString() ?? "?";
            bool dead = !Body.IsAlive(w.Agent(P).Body);
            return new(result, dead, BKilled(enc, events), !dead && result == nameof(CombatResult.Fled), enc.Rounds);
        }

        /// Shows the fight to the watcher, round by round, through Sim's observer.
        void Observe(Encounter enc)
        {
            if (watch != null) Combat.Watch(w, enc, new Watcher(watch, this));
        }

        /// Sim's rounds as the trace's frames: plain lines for the events, names for the species.
        sealed class Watcher(Action<TraceFrame> watch, OneFight fight) : ICombatObserver
        {
            public void Round(CombatRoundView round)
            {
                foreach (var f in round.Fighters) fight.species[f.Agent] = f.Species.ToString();
                var units = round.Fighters.Select(f => new TraceUnit(f.Index, f.Team, f.Species.ToString(), f.Member, f.Position.X, f.Position.Y,
                    f.State.ToString(), f.Action?.ToString(), f.Target, f.Resolve, f.Boldness, f.Vitality, f.BloodPct, f.SprintReservePct, f.WoundLevel)).ToList();
                var lines = round.Events.Select(e => e switch
                {
                    Injured i => $"{fight.Name(i.Who)} hurt: {i.Region}, {i.Type} {i.Amount:0.00}",
                    Killed k => $"{k.Species} killed",
                    Died d => $"{fight.Name(d.Who)} died",
                    EncounterEnded f when f.Agent == P => $"fight over: {f.Result}",
                    _ => null,
                }).OfType<string>().ToList();
                watch(new TraceFrame(round.Round, units, round.Fire is Cell c ? new TraceCell(c.X, c.Y) : null, lines));
            }
        }

        readonly Dictionary<AgentId, string> species = [];

        string Name(AgentId id) => w.Agents.TryGetValue(id, out var a) ? a.Species.ToString() : species.GetValueOrDefault(id, id.ToString());

        static bool BKilled(Encounter enc, List<WorldEvent> events)
        {
            var b = enc.Units.Where(u => u.Team == 1).Select(u => u.Agent).ToHashSet();
            return events.OfType<Killed>().Any(k => b.Contains(k.Agent));
        }

        const int frozenCap = 3;

        (CombatAction, CombatUnit?, Cell?) HuntPolicy(Encounter e, CombatUnit me)
        {
            var acts = Combat.Actions(w, e, me);
            var target = Combat.TargetOf(e, me); // the one faced: naming another would spend the round turning
            if (target == null) return (CombatAction.Wait, null, null);
            double gap = me.Position.DistanceTo(target.Position);
            bool throws = c.Policy.EndsWith("throw");
            if (acts.Contains(CombatAction.Attack)) return (CombatAction.Attack, target, null);
            double throwAt = c.Policy == "quick_throw" ? 10 : 8;
            if (throws && me.Weapon != null && gap <= throwAt && acts.Contains(CombatAction.Throw)) return (CombatAction.Throw, target, null);
            if (c.Policy.StartsWith("careful"))
            {
                if (target.Awareness == Awareness.Engaged) return (CombatAction.Advance, target, null); // it bolted: chase
                // freeze while any is wary, at most 3 rounds running
                if (e.Units.Any(u => u.Team == 1 && u.InField && u.Awareness == Awareness.Alert) && frozen < frozenCap) { frozen++; return (CombatAction.Wait, null, null); }
                frozen = 0;
                var step = me.Position.Toward(target.Position, 1);
                if (step == me.Position || step == target.Position) return (CombatAction.Advance, target, null);
                return (CombatAction.Move, null, step);
            }
            return (CombatAction.Advance, target, null);
        }

        (CombatAction, CombatUnit?, Cell?) FightPolicy(Encounter e, CombatUnit me)
        {
            var acts = Combat.Actions(w, e, me);
            var target = Combat.TargetOf(e, me);
            if (target == null) return (CombatAction.Wait, null, null);
            switch (c.Policy)
            {
                case "cautious":
                    var body = Combat.BodyOf(w, me);
                    if (Combat.WoundLevel(body) > 0.3 || body.BloodPct < 0.85 || Effects.Severity(body, EffectKind.Bleeding) > 0.4)
                        return (CombatAction.Flee, null, null);
                    break;
                // Running for it: Flee works at any distance (combat round 1).
                case "run": return (CombatAction.Flee, null, null);
                // Stand your ground and shout (Standoff): Intimidate from Near out, keep still up close.
                // Every aware fighter defends itself when struck, so standing is a stance of its own.
                case "stand_intimidate":
                    if (acts.Contains(CombatAction.Intimidate)) return (CombatAction.Intimidate, null, null);
                    return (CombatAction.Wait, null, null);
            }
            if (acts.Contains(CombatAction.Attack)) return (CombatAction.Attack, target, null);
            return (CombatAction.Advance, target, null);
        }

        FightOutcome Hunt()
        {
            var g = ArmPlayer();
            var species = c.Setup.Contains("bison") ? Species.Bison : Species.Caribou;
            var herd = Agents.SpawnGroup(w, species, tile, 4);
            foreach (var id in herd) ((HerdMind)w.Agent(id).Mind).Choice = c.Setup.Contains("grazing") ? new Graze(id) : new Rest(id);
            var enc = Fauna.BeginHunt(w, P, herd[0], g);
            Observe(enc);
            return DrivePlayer(enc, HuntPolicy);
        }

        static (int count, Species s, Opening o, int dist, Awareness a) AnimalSetup(string sc) => sc switch
        {
            "wolf1_ambush" => (1, Species.Wolf, Opening.Ambush, 3, Awareness.Unaware),
            "wolf1_aware" => (1, Species.Wolf, Opening.Approach, 0, Awareness.Engaged),
            "pack3_aware" => (3, Species.Wolf, Opening.Approach, 0, Awareness.Engaged),
            "pack3_ambush" => (3, Species.Wolf, Opening.Ambush, 4, Awareness.Unaware),
            "hyena3_aware" => (3, Species.Hyena, Opening.Approach, 0, Awareness.Engaged),
            "pack5_aware" => (5, Species.Wolf, Opening.Approach, 0, Awareness.Engaged),
            "pack5_ambush" => (5, Species.Wolf, Opening.Ambush, 4, Awareness.Unaware),
            "bear_close" => (1, Species.Bear, Opening.CloseEncounter, 8, Awareness.Engaged),
            "bear_den" => (1, Species.Bear, Opening.CloseEncounter, 8, Awareness.Engaged),
            _ => throw new ArgumentException(sc),
        };

        /// A bear defending a kill or its den (bear_den) comes with its temperament's defending boldness.
        double OpeningBoldness(string sc, Species s) => sc == "bear_den" ? SpeciesTable.Of(s).Temperament.DefendBonus : 0;

        FightOutcome PersonVsAnimal()
        {
            var (count, s, o, dist, aw) = AnimalSetup(c.Setup);
            var herd = Agents.SpawnGroup(w, s, tile, count);
            var animals = herd.Select(id => new Fighter(id) { BoldnessDelta = OpeningBoldness(c.Setup, s) }).ToList();
            if (c.Policy != "ai")
            {
                var weapon = ArmPlayer();
                var enc = Combat.Create(w, [new Fighter(P) { Weapon = weapon }], animals, tile, dist, aw, Awareness.Engaged, o);
                Observe(enc);
                return DrivePlayer(enc, FightPolicy);
            }
            var pid = Person(out var g);
            var headless = Combat.Create(w, [new Fighter(pid) { Weapon = g }], animals, tile, dist, aw, Awareness.Engaged, o);
            Observe(headless);
            var events = new List<WorldEvent>();
            var r = Combat.ResolveHeadless(w, headless, events);
            bool dead = !Body.IsAlive(w.Agent(pid).Members[0]);
            return new(r.ToString(), dead, BKilled(headless, events), !dead && r == CombatResult.Fled, headless.Rounds);
        }

        FightOutcome PackVsPrey()
        {
            var pack = Agents.SpawnGroup(w, Species.Wolf, tile, 4);
            var preySpecies = c.Setup.Contains("bison") ? Species.Bison : Species.Caribou;
            int preyCount = c.Setup.Contains("caribou3") ? 3 : 1;
            var prey = Agents.SpawnGroup(w, preySpecies, tile, 4);
            bool noticed = c.Setup.EndsWith("_noticed");
            var enc = Combat.Create(w, [.. pack.Select(id => new Fighter(id))],
                [.. prey.Take(preyCount).Select(id => new Fighter(id))], tile,
                noticed ? 15 : 5, Awareness.Engaged, noticed ? Awareness.Alert : Awareness.Unaware);
            var events = new List<WorldEvent>();
            Observe(enc);
            var r = Combat.ResolveHeadless(w, enc, events);
            bool wolfDied = events.OfType<Killed>().Any(k => pack.Contains(k.Agent));
            return new(r.ToString(), wolfDied, BKilled(enc, events), r == CombatResult.Fled, enc.Rounds);
        }

        FightOutcome PeopleVsWolves()
        {
            int people = c.Setup.StartsWith("two") ? 2 : 1;
            var ids = new List<Fighter>();
            for (int i = 0; i < people; i++) { var pid = Person(out var g); ids.Add(new Fighter(pid) { Weapon = g }); }
            var pack = Agents.SpawnGroup(w, Species.Wolf, tile, 4);
            var enc = Combat.Create(w, ids, [.. pack.Select(id => new Fighter(id))], tile, 0,
                Awareness.Engaged, Awareness.Engaged, Opening.Approach);
            var events = new List<WorldEvent>();
            Observe(enc);
            var r = Combat.ResolveHeadless(w, enc, events);
            bool dead = events.OfType<Died>().Any();
            return new(r.ToString(), dead, BKilled(enc, events), r == CombatResult.Fled, enc.Rounds);
        }
    }
}
