using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CsCheck;
using Survival;

namespace Survival.Tests.Verification;

/// What the player means to do, before it meets the world: Resolve turns it into a real activity
/// chosen from what is on the tile, in the pack and in the fire at that moment.
public enum Act { Wait, Rest, Sleep, Travel, Gather, StartFire, AddFuel, LightTorch, Eat, Drink, Wash, MeltSnow, Cook, PutDown, PickUp, Butcher, Build, Improve, Fight, Make, Abandon, WalkAbout, PutOn, TakeFinished, TakeBack }

/// One scripted activity. Pick chooses among the valid options (a pool, a direction, a fuel); it is
/// a plain int so that shrinking it walks toward the first option.
public sealed record Intent(Act Act, int Pick, int Minutes)
{
    public override string ToString() => $"new({nameof(Act)}.{Act}, {Pick}, {Minutes})";
}

/// The world a script starts in, built the same way for every run of it. Kit is a full pack (fire
/// gear, knife, axe, bag, food, water, hide); Npc puts a bench NPC at camp; Carcass a caribou at camp;
/// Fight starts the player in a fight with a lone wolf at camp.
public sealed record Setup(int Seed, bool Kit, bool Npc, bool Carcass, bool Fight, bool Mild)
{
    public override string ToString() => $"new({Seed}, Kit: {B(Kit)}, Npc: {B(Npc)}, Carcass: {B(Carcass)}, Fight: {B(Fight)}, Mild: {B(Mild)})";
    static string B(bool b) => b ? "true" : "false";
}

/// A script and the two ways of cutting it: Chunks (cycled) splits each splittable activity's
/// minutes; Saves are the step indexes after which run (c) saves and loads.
public sealed record Script(Setup Setup, Intent[] Intents, int[] Chunks, int[] Saves)
{
    public override string ToString() =>
        $"new Script({Setup}, [{string.Join(", ", Intents.Select(i => i.ToString()))}], Chunks: [{string.Join(", ", Chunks)}], Saves: [{string.Join(", ", Saves)}])";
}

/// One way of running a script: its final save, its events, and what each step did.
public sealed record Outcome(string Save, List<string> Events, List<string> Trace, List<Activity> Resolved);

/// Generators and runners for the random-action-sequence determinism law (ActionSequenceTests).
static class ActionGen
{
    static readonly AgentId P = AgentId.Player;

    /// A few seeds whose maps are known to generate; a generated seed would make every case a new
    /// map, costing a map generation for nothing the law is about.
    public static readonly int[] Seeds = [1, 5, 17, 42];

    public static readonly WeatherSample MildSky = new(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);

    public static Gen<Setup> SetupGen => Gen.Select(Gen.Int[0, Seeds.Length - 1].Select(k => Seeds[k]), Gen.Bool, Gen.Bool, Gen.Bool, Gen.Frequency((4, Gen.Const(false)), (1, Gen.Const(true))), Gen.Bool,
        (seed, kit, npc, carcass, fight, mild) => new Setup(seed, kit, npc, carcass, fight, mild));

    /// Minutes by kind of activity, scaled by maxScale (1 for the fast suite's short scripts).
    static Gen<int> MinutesFor(Act act, double scale) => act switch
    {
        Act.Wait or Act.Rest => Gen.Int[1, Math.Max(1, (int)(180 * scale))],
        Act.Sleep => Gen.Int[1, Math.Max(1, (int)(480 * scale))],
        Act.Travel => Gen.Int[1, Math.Max(1, (int)(60 * scale))],
        Act.Gather or Act.Butcher or Act.Build or Act.Improve or Act.Make => Gen.Int[1, Math.Max(1, (int)(150 * scale))],
        Act.Fight => Gen.Const(1),
        _ => Gen.Int[1, 20], // fire acts and instant work: the act, then waiting it out
    };

    public static Gen<Intent> IntentGen(double scale) =>
        Gen.Frequency(
            (3, Gen.Const(Act.Wait)), (1, Gen.Const(Act.Rest)), (1, Gen.Const(Act.Sleep)), (3, Gen.Const(Act.Travel)),
            (4, Gen.Const(Act.Gather)), (2, Gen.Const(Act.StartFire)), (2, Gen.Const(Act.AddFuel)), (1, Gen.Const(Act.LightTorch)), (1, Gen.Const(Act.WalkAbout)),
            (1, Gen.Const(Act.Eat)), (1, Gen.Const(Act.Drink)), (1, Gen.Const(Act.Wash)), (1, Gen.Const(Act.MeltSnow)), (1, Gen.Const(Act.Cook)),
            (1, Gen.Const(Act.PutDown)), (1, Gen.Const(Act.PickUp)), (2, Gen.Const(Act.Butcher)), (1, Gen.Const(Act.Build)),
            (1, Gen.Const(Act.Improve)), (1, Gen.Const(Act.Fight)), (2, Gen.Const(Act.Make)), (1, Gen.Const(Act.Abandon)),
            (1, Gen.Const(Act.PutOn)), (1, Gen.Const(Act.TakeFinished)), (1, Gen.Const(Act.TakeBack)))
        .SelectMany(act => Gen.Select(Gen.Int[0, 63], MinutesFor(act, scale), (pick, minutes) => new Intent(act, pick, minutes)));

    public static Gen<Script> ScriptGen(int minIntents, int maxIntents, double scale) =>
        Gen.Select(SetupGen, IntentGen(scale).Array[minIntents, maxIntents], Gen.Int[1, 60].Array[1, 6], Gen.Int[0, maxIntents].Array[0, 6],
            (setup, intents, chunks, saves) => new Script(setup, intents, chunks, saves));

    // ---- the world ----

    public static World MakeWorld(Setup s)
    {
        var w = TestWorlds.New(s.Seed);
        if (s.Mild) w.FixedWeather = MildSky;
        var camp = Map.Of(w).Camp;
        if (s.Npc) NpcBench.Spawn(w, camp); // before anything else is born, so its id is fixed
        if (s.Kit)
        {
            TestKit.Supply(w);
            var pack = Items.Pack(w, P);
            foreach (var kind in new[] { "flint_knife", "stone_axe", "large_bag", "hide_tent", "wooden_spear" }) pack.Gear.Add(Items.Make(kind));
            foreach (var (r, kg) in new[] { (Resource.RawMeat, 1.5), (Resource.Berries, 0.5), (Resource.Water, 1.0), (Resource.Hide, 2.0), (Resource.Stone, 1.0) })
                TestKit.Give(pack, r, kg);
        }
        if (s.Carcass)
            Combat.LeaveCarcass(w, camp, Species.Caribou, BodyState.Create(SpeciesTable.Of(Species.Caribou).Body, 100, 0.1, 0.4), new Cause(null, "a test"));
        if (s.Fight)
        {
            var wolf = Agents.Spawn(w, Species.Wolf, camp);
            Combat.Create(w, [new Fighter(P)], [new Fighter(wolf)], camp, 8, Awareness.Engaged, Awareness.Engaged);
        }
        return w;
    }

    // ---- intent to activity ----

    static T PickOf<T>(IReadOnlyList<T> options, int pick, T fallback) => options.Count == 0 ? fallback : options[pick % options.Count];

    /// The activity this intent means here and now. Always an activity: one that will be refused
    /// is part of the behaviour under test.
    public static Activity Resolve(World w, Intent i)
    {
        var me = w.Agent(P);
        var pack = Items.Pack(w, P);
        var tile = me.Tile;
        int pick = i.Pick;
        // In a fight, everything but a round is refused: most intents become a round, so fights end.
        if (Combat.EncounterOf(w, P) is Encounter enc && (i.Act == Act.Fight || pick % 4 != 0))
            return FightRound(w, enc, pick);
        List<Resource> Held(Func<Resource, bool> which) => Items.KgByResource(pack).Where(kv => kv.Value > 0 && which(kv.Key)).Select(kv => kv.Key).ToList();
        switch (i.Act)
        {
            case Act.Wait: return new Wait(P);
            case Act.Rest: return new Rest(P);
            case Act.Sleep: return new Sleep(P);
            case Act.Travel: return new Travel(P, (Direction)(pick % 4), pick / 4 % 3 == 0 ? Pace.Careful : Pace.Quick);
            case Act.Gather:
                {
                    var pools = Pools.At(w, tile);
                    var known = Enumerable.Range(0, pools.Count).Where(k => Pools.Knows(w, P, new PoolId(tile, k))).ToList();
                    return new Gather(P, PickOf(known, pick, 0), (ForageFocus)(pick / 8 % 5));
                }
            case Act.StartFire:
                {
                    // The best tool and tinder held, mostly, so fires get lit and what needs one gets reached.
                    var tools = pack.Gear.Where(g => Items.Def(g).Tool is ToolType.FireStriker or ToolType.HandDrill or ToolType.BowDrill)
                        .Select(g => Items.Def(g).Tool!.Value).Distinct().OrderBy(t => t == ToolType.FireStriker ? 0 : 1).ToList();
                    var tinders = Held(r => Fire.IsFuel(r) && Fire.Props[r].MinFireTempF == 0 && r != Resource.Stick && r != Resource.Charcoal)
                        .OrderByDescending(r => Fire.Props[r].IgnitionBonusPct).ToList();
                    if (pick % 3 != 0) (tools, tinders) = (tools.Take(1).ToList(), tinders.Take(1).ToList());
                    var pit = Fire.PitAt(w, tile) is FireId id ? w.Fires[id].Pit : (PitType)(pick % 3 == 2 ? 1 : 0);
                    return new StartFire(P, PickOf(tools, pick, ToolType.HandDrill), PickOf(tinders, pick / 2, Resource.Tinder),
                        0.03 + pick % 4 * 0.02, pick % 5 * 0.1, pit);
                }
            case Act.AddFuel:
                return new AddFuel(P, PickOf(Held(Fire.IsFuel), pick, Resource.Stick), 0.2 + pick % 8 * 0.4);
            case Act.LightTorch: return new LightTorch(P);
            case Act.Eat:
                return new Eat(P, PickOf(Held(r => Items.Resources[r].Category == ResourceCategory.Food), pick, Resource.Berries), 0.1 + pick % 5 * 0.1);
            case Act.Drink: return new Drink(P, 0.25 + pick % 4 * 0.25);
            case Act.MeltSnow: return new MeltSnow(P);
            case Act.Wash: return new Wash(P);
            case Act.WalkAbout: return new WalkAbout(P);
            case Act.Cook:
                return new Cook(P, PickOf(Held(r => r is Resource.RawMeat or Resource.RawFish), pick, Resource.RawMeat), 0.2 + pick % 5 * 0.2);
            case Act.PutDown:
                return new PutDown(P, PickOf(Held(_ => true), pick, Resource.Stick), 0.1 + pick % 5 * 0.2);
            case Act.PickUp:
                {
                    var pile = w.Tiles.GetValueOrDefault(tile)?.Pile;
                    List<Resource> there = pile == null ? [] : Items.KgByResource(pile).Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
                    return new PickUp(P, PickOf(there, pick, Resource.Stick), 0.1 + pick % 5 * 0.3);
                }
            case Act.Butcher:
                {
                    var here = w.Tiles.GetValueOrDefault(tile)?.Carcasses?.Select(c => c.Id).ToList() ?? [];
                    ButcherMode? mode = pick % 4 == 3 ? null : (ButcherMode)(pick % 3);
                    return new Butcher(P, PickOf(here, pick, 0), mode);
                }
            case Act.Build:
                {
                    // A project under way here is the one to resume; otherwise any camp recipe.
                    var camp = Recipes.All.Where(r => r.Result.Placed).ToList();
                    return new Make(P, w.Projects.TryGetValue(tile, out var p) && pick % 3 != 0 ? p.Recipe : camp[pick % camp.Count].Id);
                }
            case Act.Improve:
                {
                    var materials = Held(r => Shelters.Materials.ContainsKey(r));
                    return new ImproveShelter(P, (ShelterStat)(pick % 3), PickOf(materials, pick / 3, Resource.Hide));
                }
            case Act.Fight: return new Fight(P, CombatAction.Wait); // not in a fight: refused
            case Act.Make:
                {
                    // A project in the pack is the one to go on with; otherwise any recipe.
                    var projects = Items.Pack(w, P).Projects?.Select(p => p.Recipe).ToList() ?? [];
                    return new Make(P, projects.Count > 0 && pick % 3 != 0 ? PickOf(projects, pick, "") : Recipes.ByHand[pick % Recipes.ByHand.Count].Id);
                }
            case Act.Abandon:
                return new Abandon(P, PickOf(Items.Pack(w, P).Projects?.Select(p => p.Recipe).ToList() ?? [], pick, Recipes.ByHand[pick % Recipes.ByHand.Count].Id));
            case Act.PutOn: return new PutOn(P, PickOf([.. Stations.FireRecipes.Select(r => r.Id)], pick, "cook_meat"));
            case Act.TakeFinished: return new TakeFinished(P);
            case Act.TakeBack:
                {
                    var working = Fire.PitAt(w, tile) is FireId pit ? Stations.At(w, pit).Working : [];
                    var x = PickOf(working, pick, null);
                    return new TakeBack(P, x?.Input ?? Resource.RawMeat, x?.InputKg ?? 0.4);
                }
            default: throw new NotSupportedException(i.Act.ToString());
        }
    }

    static readonly CombatAction[] Rounds =
    [
        CombatAction.Attack, CombatAction.Attack, CombatAction.Attack, CombatAction.Advance, CombatAction.Retreat, CombatAction.PickUp,
        CombatAction.Turn, CombatAction.Intimidate, CombatAction.Throw, CombatAction.ThrowStone, CombatAction.Shove, CombatAction.Flee,
        CombatAction.Wait, CombatAction.Move, CombatAction.CallHelp, CombatAction.CallRetreat,
    ];

    static Fight FightRound(World w, Encounter enc, int pick)
    {
        var action = Rounds[pick % Rounds.Length];
        var me = Combat.UnitOf(enc, P);
        var enemies = Enumerable.Range(0, enc.Units.Count).Where(k => enc.Units[k].Team != me.Team && enc.Units[k].InField).ToList();
        int? target = enemies.Count == 0 ? null : enemies[pick / 16 % enemies.Count];
        Cell? to = action == CombatAction.Move ? new Cell(me.Position.X + pick % 5 - 2, me.Position.Y + pick / 5 % 5 - 2) : null;
        return new Fight(P, action, target, to);
    }

    // ---- running ----

    /// Activities whose minutes may be split across steps: repeating them carries on where the last
    /// step left off. The others act once when they start (light, eat, put down, a round), so two steps
    /// of them are two acts, not one act cut in half; they always run as one step.
    /// A fight is splittable too: every minute of a Fight step is one round of the same action.
    public static bool Splittable(Activity a) => a is Wait or Rest or Sleep or Travel or Gather or WalkAbout or Butcher or ImproveShelter or Fight or Make;

    /// An intent turned into a round keeps its minutes, up to a few rounds.
    static int MinutesOf(Activity a, Intent i) => a is Fight ? Math.Min(i.Minutes, 5) : i.Minutes;

    /// Straight is (a), Chunked (b), Saved (c). ChunkedSaved is (b) with a save and load after the
    /// chunk steps Saves names, counting chunks across the script: a save mid-activity, as a player
    /// quitting part-way through a walk or a day's work makes one.
    public enum Mode { Straight, Chunked, Saved, ChunkedSaved }

    /// Runs the script one way. Resolved, when given, is the activity list another run resolved:
    /// every way of running must do the same activities, so only the first run resolves.
    public static Outcome Run(Script s, Mode mode, List<Activity>? resolved = null)
    {
        var w = MakeWorld(s.Setup);
        var events = new List<string>();
        var trace = new List<string>();
        var acts = new List<Activity>();
        int chunkIndex = 0;
        for (int step = 0; step < s.Intents.Length; step++)
        {
            var me = w.Agent(P);
            if (!Sim.IsAlive(me)) break; // stepping the dead throws by design
            if (resolved != null && step >= resolved.Count) break; // the first run ended here; the saves will differ
            var intent = s.Intents[step];
            var activity = resolved != null ? resolved[step] : Resolve(w, intent);
            acts.Add(activity);
            int minutes = MinutesOf(activity, intent), done = 0;
            string? reason = null;
            int steps = 1;
            if (mode is Mode.Chunked or Mode.ChunkedSaved && Splittable(activity))
            {
                for (steps = 0; done < minutes;)
                {
                    if (mode == Mode.ChunkedSaved && steps > 0 && s.Saves.Contains(chunkIndex)) (w, me) = Reload(w);
                    int chunk = Math.Min(s.Chunks[chunkIndex++ % s.Chunks.Length], minutes - done);
                    steps++;
                    var r = Sim.Step(w, activity, chunk, more: done + chunk < minutes); // one job, its work held between chunks
                    events.AddRange(r.Events.Select(Json));
                    done += r.MinutesElapsed;
                    reason = r.InterruptedReason;
                    if (reason != null || r.MinutesElapsed < chunk || !Sim.IsAlive(me)) break;
                }
            }
            else
            {
                var r = Sim.Step(w, activity, minutes);
                events.AddRange(r.Events.Select(Json));
                (done, reason) = (r.MinutesElapsed, r.InterruptedReason);
            }
            trace.Add($"{step}: {activity} for {minutes} -> {done} min in {steps} step(s), {reason ?? "ran out"}");
            Valid(w, step);
            if (mode is Mode.Saved or Mode.ChunkedSaved && s.Saves.Contains(step)) w = TestWorlds.Load(Save.ToJson(w));
        }
        return new Outcome(Save.ToJson(w), events, trace, acts);
    }

    static (World, Agent) Reload(World w)
    {
        var loaded = TestWorlds.Load(Save.ToJson(w));
        return (loaded, loaded.Agent(P));
    }

    static readonly JsonSerializerOptions EventJson = new() { WriteIndented = false };
    static string Json(WorldEvent e) => JsonSerializer.Serialize(e, EventJson);

    /// Step tallies: what one step of work brought in. They depend on how the work was cut into steps
    /// by design (Journal.Files leaves them out of the journals for the same reason).
    public static bool IsTally(string eventJson) =>
        eventJson.StartsWith("{\"kind\":\"gathered\"") || eventJson.StartsWith("{\"kind\":\"dropped\"") || eventJson.StartsWith("{\"kind\":\"butchered\"");

    // ---- validity ----

    /// The world between steps: every herd has members and all of them alive (the dead leave as they
    /// fall), every person has one body, and no body holds a NaN or an infinity.
    static void Valid(World w, int step)
    {
        foreach (var (id, a) in w.Agents)
        {
            // Every agent is one body (decision record "Animals are individuals"), and a dead animal leaves the world.
            if (a.Members.Count != 1) throw new InvalidOperationException($"after step {step}: {id} has {a.Members.Count} bodies");
            if (a.Mind is HerdMind && !Body.IsAlive(a.Body)) throw new InvalidOperationException($"after step {step}: animal {id} is dead and still here");
            foreach (var b in a.Members) Finite(b, $"after step {step}: {id}");
        }
    }

    static void Finite(object o, string where)
    {
        foreach (var prop in o.GetType().GetProperties())
        {
            if (prop.GetIndexParameters().Length > 0) continue;
            var v = prop.GetValue(o);
            switch (v)
            {
                case double d when !double.IsFinite(d):
                    throw new InvalidOperationException($"{where}: {o.GetType().Name}.{prop.Name} is {d}");
                case Region or Tissue or BodyEffect:
                    Finite(v, where);
                    break;
                case System.Collections.IEnumerable list when v is not string:
                    foreach (var item in list) if (item is Region or Tissue or BodyEffect) Finite(item, where);
                    break;
            }
        }
    }

    // ---- minimising ----

    /// Greedy minimisation: each pass tries removing one activity, halving one activity's minutes,
    /// dropping or growing one chunk, and dropping one save, keeping any change that still fails.
    /// Stops at a fixed point or after maxTries runs of the property.
    public static (Script Script, int Tries) Minimize(Script s, Func<Script, bool> fails, int maxTries)
    {
        int tries = 0;
        bool Try(Script t)
        {
            if (tries >= maxTries) return false;
            tries++;
            return fails(t);
        }
        for (bool changed = true; changed && tries < maxTries;)
        {
            changed = false;
            // Blocks first, halves down to pairs, so a long script loses most of itself in a few runs.
            for (int size = s.Intents.Length / 2; size >= 2; size /= 2)
                for (int from = s.Intents.Length - size; from >= 0 && s.Intents.Length > size; from -= size)
                {
                    int f = from, n = size;
                    var t = s with
                    {
                        Intents = [.. s.Intents.Where((_, j) => j < f || j >= f + n)],
                        Saves = [.. s.Saves.Where(x => x < f || x >= f + n).Select(x => x >= f + n ? x - n : x)],
                    };
                    if (Try(t)) { s = t; changed = true; }
                }
            for (int k = s.Intents.Length - 1; k >= 0 && s.Intents.Length > 1; k--)
            {
                var t = s with { Intents = [.. s.Intents.Where((_, j) => j != k)], Saves = [.. s.Saves.Where(x => x != k).Select(x => x > k ? x - 1 : x)] };
                if (Try(t)) { s = t; changed = true; }
            }
            for (int k = 0; k < s.Intents.Length; k++)
                while (s.Intents[k].Minutes > 1)
                {
                    var intents = (Intent[])s.Intents.Clone();
                    intents[k] = intents[k] with { Minutes = intents[k].Minutes / 2 };
                    var t = s with { Intents = intents };
                    if (!Try(t)) break;
                    s = t;
                    changed = true;
                }
            for (int k = s.Chunks.Length - 1; k >= 0 && s.Chunks.Length > 1; k--)
            {
                var t = s with { Chunks = [.. s.Chunks.Where((_, j) => j != k)] };
                if (Try(t)) { s = t; changed = true; }
            }
            for (int k = 0; k < s.Chunks.Length; k++)
                if (s.Chunks[k] < 1000)
                {
                    var chunks = (int[])s.Chunks.Clone();
                    chunks[k] = Math.Min(1000, chunks[k] * 2);
                    var t = s with { Chunks = chunks };
                    if (Try(t)) { s = t; changed = true; }
                }
            for (int k = s.Saves.Length - 1; k >= 0; k--)
            {
                var t = s with { Saves = [.. s.Saves.Where((_, j) => j != k)] };
                if (Try(t)) { s = t; changed = true; }
            }
            foreach (var t in new[] { s with { Setup = s.Setup with { Npc = false } }, s with { Setup = s.Setup with { Carcass = false } },
                s with { Setup = s.Setup with { Kit = false } }, s with { Setup = s.Setup with { Mild = true } } })
                if (t.Setup != s.Setup && Try(t)) { s = t; changed = true; }
        }
        return (s, tries);
    }

    // ---- reporting ----

    /// The path of the first place two saves differ, and the two values there.
    public static string FirstDifference(string a, string b)
    {
        var path = new StringBuilder("$");
        return Diff(JsonNode.Parse(a), JsonNode.Parse(b), path) ?? "(no difference)";
    }

    static string? Diff(JsonNode? a, JsonNode? b, StringBuilder path)
    {
        string Here(JsonNode? x, JsonNode? y) => $"{path}: {Short(x)} vs {Short(y)}";
        switch (a, b)
        {
            case (JsonObject oa, JsonObject ob):
                foreach (var (k, va) in oa)
                {
                    if (!ob.ContainsKey(k)) return $"{path}.{k}: present vs missing";
                    int len = path.Length;
                    path.Append('.').Append(k);
                    if (Diff(va, ob[k], path) is string d) return d;
                    path.Length = len;
                }
                foreach (var (k, _) in ob) if (!oa.ContainsKey(k)) return $"{path}.{k}: missing vs present";
                return null;
            case (JsonArray xa, JsonArray xb):
                for (int k = 0; k < Math.Min(xa.Count, xb.Count); k++)
                {
                    int len = path.Length;
                    path.Append('[').Append(k).Append(']');
                    if (Diff(xa[k], xb[k], path) is string d) return d;
                    path.Length = len;
                }
                return xa.Count == xb.Count ? null : $"{path}: {xa.Count} items vs {xb.Count}";
            default:
                return JsonNode.DeepEquals(a, b) ? null : Here(a, b);
        }
    }

    static string Short(JsonNode? n)
    {
        var s = n?.ToJsonString() ?? "null";
        return s.Length > 200 ? s[..200] + "..." : s;
    }

    /// The first index where two event streams differ, with both events.
    public static string? FirstEventDifference(List<string> a, List<string> b)
    {
        for (int k = 0; k < Math.Min(a.Count, b.Count); k++)
            if (a[k] != b[k]) return $"event {k}: {a[k]} vs {b[k]}";
        return a.Count == b.Count ? null : $"{a.Count} events vs {b.Count}; first extra: {(a.Count > b.Count ? a[b.Count] : b[a.Count])}";
    }
}
