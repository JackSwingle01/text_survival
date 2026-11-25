using CsCheck;
using Survival;
using static Survival.Tests.Verification.ConservationWorlds;

namespace Survival.Tests.Verification;

/// Law 2: butchering. For every resource on a carcass, at every moment of the work:
///
///   carried + pending + remaining + waste (+ rotted) = the carcass's ledger when it died
///
/// carried is measured off the inventories (packs and piles), pending and remaining off the
/// carcass, rotted is soft tissue Remains.Hour took off a carcass gone to bones (declared sink).
/// waste is what is left over, and must be non-negative: that is the cap, yield never above what
/// the carcass held, at any skill, session pattern, tool, mode, room or age. The carcass's own
/// counters must agree with the inventories (YieldKg = carried + pending, CutKg + Remaining +
/// rotted = ledger). Waste never grows with skill or a blade. One long session and many short
/// ones, with saves between, give the same result. No rng reaches butchering (Carcass.cs, Work.CutMinute):
/// luck is not an input.
public class ConservationButcheringTests
{
    static readonly Species[] Kinds =
        [Species.Hare, Species.Fox, Species.Wolf, Species.Caribou, Species.Megaloceros, Species.Bison, Species.Bear, Species.Mammoth];
    static readonly string?[] Tools = [null, "flint_knife", "stone_axe", "worn_knife"];

    public sealed record Chunk(int Minutes, bool SaveLoad, int GapMinutes, bool Unload);

    public sealed record Scene(int Kind, int BodySeed, int AgeMinutes, double TempC, int Mode, int Skill, int Tool,
        double FillKg, double WetPct, Chunk[] Chunks);

    static Gen<Chunk> ChunkGen(bool gaps) => Gen.Select(
        Gen.Frequency((3, Gen.Int[1, 30]), (2, Gen.Int[30, 240]), (1, Gen.Const(2000))),
        Gen.Frequency((3, Gen.Const(false)), (1, Gen.Const(true))),
        gaps ? Gen.Frequency((3, Gen.Const(0)), (1, Gen.Int[1, 600])) : Gen.Const(0),
        gaps ? Gen.Bool : Gen.Const(false),
        (m, s, g, u) => new Chunk(m, s, g, u));

    static Gen<Scene> Scenes(bool gaps) => Gen.Select(
        Gen.Int[0, Kinds.Length - 1], Gen.Int[0, 1000],
        Gen.Frequency((1, Gen.Const(0)), (3, Gen.Int[0, 5 * 1440])),
        Gen.Double[-30, 20], Gen.Int[0, 2], Gen.Int[0, Skills.MaxLevel], Gen.Int[0, Tools.Length - 1],
        Gen.Frequency((1, Gen.Const(0.0)), (2, Gen.Double[0, 11.5])),
        Gen.Frequency((3, Gen.Const(0.0)), (1, Gen.Double[0, 1])),
        ChunkGen(gaps).Array[1, 12],
        (k, seed, age, t, mode, skill, tool, fill, wet, chunks) => new Scene(k, seed, age, t, mode, skill, tool, fill, wet, chunks));

    static readonly Resource[] CarcassResources =
        [Resource.RawMeat, Resource.RawFat, Resource.Bone, Resource.Hide, Resource.Sinew, Resource.Feather, Resource.Ivory, Resource.MammothHide];

    sealed class Run
    {
        public required World W;
        public required int CarcassId;
        public required Carcass C; // the live object; refetched after a load
        public required Dictionary<Resource, double> Ledger;
        public required SortedDictionary<Resource, double> ItemsAtStart;
        public required double BodyKg;
        public bool Rotted;
        public int Minutes;
    }

    static Run Build(Scene s)
    {
        var w = Bare(s.TempC);
        var camp = Camp(w);
        w.Minute = s.AgeMinutes;
        var species = Kinds[s.Kind];
        var body = SpeciesTable.NewBody(species, Survival.Rng.ForSystem(s.BodySeed, "carcass"));
        // As Combat.LeaveCarcass makes it, dead since minute 0.
        var c = Carcasses.Create(w, camp, species.ToString(), body, 0, isMegafauna: body.WeightKg > 500,
            special: species == Species.Mammoth ? new Dictionary<Resource, double> { [Resource.Ivory] = 4, [Resource.MammothHide] = 15 } : null,
            cause: new Cause(null, "test"));
        var pack = Items.Pack(w, P);
        if (Tools[s.Tool] is string tool) pack.Gear.Add(Items.Make(tool));
        if (s.FillKg > 0) TestKit.Give(pack, Resource.Stone, s.FillKg);
        ((PersonMind)w.Agent(P).Mind).Skills[Skill.Butchering] = new SkillProgress { Level = s.Skill };
        if (s.WetPct > 0) Effects.Set(w.Agent(P).Body, EffectKind.Wet, s.WetPct);
        // Light to work by however long it takes: a stone pit of oak, topped up between chunks.
        w.Fires[new FireId(w.NextFireId++)] = new FireState { Tile = camp, LastUpdatedMinute = w.Minute, Pit = PitType.Stone, BurningKg = { [Resource.OakBranch] = 10 } };
        return new Run
        {
            W = w,
            CarcassId = c.Id,
            C = c,
            Ledger = new Dictionary<Resource, double>(c.Remaining),
            ItemsAtStart = Carried(w),
            BodyKg = body.WeightKg,
        };
    }

    /// Packs and piles only: the items a butcher has carried off (no cut piece still waiting).
    static SortedDictionary<Resource, double> Carried(World w)
    {
        var t = new SortedDictionary<Resource, double>();
        foreach (var a in w.Agents.Values) if (Items.PackOf(a) is { } pack) foreach (var (r, kg) in Items.Totals(pack)) AddTo(t, r, kg);
        foreach (var ts in w.Tiles.Values) if (ts.Pile is { } st) foreach (var (r, kg) in Items.Totals(st)) AddTo(t, r, kg);
        return t;
    }

    static void KeepLit(World w)
    {
        var pit = w.Fires.Values.Single(f => f.CarriedBy == null);
        Fire.CatchUpAround(w, w.Agent(P), []);
        if (pit.BurningKg.Count == 0) pit.HasEmbers = false;
        pit.BurningKg[Resource.OakBranch] = 10; // light, not fuel: no item moves
        pit.UnburnedKg.Clear();
    }

    const double Eps = 1e-9;
    /// Items.Take and Items.Receive keep a sliver under Items.SliverKg with the rest of its stack, so
    /// nothing is lost to rounding: what is left is floating-point summation, relative to the pile
    /// (a mammoth's 339 kg of sinew sums to within 1.2e-11 kg).
    static double SliverKg(double ledgerKg) => 1e-12 * Math.Max(1, ledgerKg);

    /// Every clause of the law, now.
    static void CheckLedger(Run run, string when)
    {
        var c = run.C;
        var carriedNow = Carried(run.W);
        if (Remains.StageOf(run.W, Camp(run.W), c) == RemainsStage.Bones || run.Rotted) run.Rotted = true;
        double totalYield = 0;
        foreach (var r in CarcassResources)
        {
            double ledger = run.Ledger.GetValueOrDefault(r);
            double carried = carriedNow.GetValueOrDefault(r) - run.ItemsAtStart.GetValueOrDefault(r);
            double pending = c.Pending.GetValueOrDefault(r), remaining = c.Remaining.GetValueOrDefault(r), cut = c.CutKg.GetValueOrDefault(r);
            double rotted = ledger - remaining - cut;
            double waste = ledger - carried - pending - remaining - rotted;
            string at = $"{when}, {r}: ledger {ledger:R}, carried {carried:R}, pending {pending:R}, remaining {remaining:R}, cut {cut:R}, yield {c.YieldKg.GetValueOrDefault(r):R}";
            Assert.True(carried >= -Eps, $"{at}: carried went negative");
            Assert.True(remaining >= -Eps && pending >= -Eps, $"{at}: a negative pile");
            Assert.True(carried + pending <= ledger + Eps, $"{at}: yield {carried + pending:R} above what the carcass held");
            Assert.True(waste >= -Eps, $"{at}: waste {waste:R} negative");
            Assert.True(Math.Abs(c.YieldKg.GetValueOrDefault(r) - carried - pending) <= SliverKg(ledger), $"{at}: YieldKg disagrees with the items by {c.YieldKg.GetValueOrDefault(r) - carried - pending:R}");
            if (!run.Rotted) Assert.True(Math.Abs(rotted) <= Eps, $"{at}: {rotted:R} kg left the ledger without being cut, on a carcass that never went to bones");
            else Assert.True(rotted >= -Eps && (rotted <= Eps || r is not (Resource.Bone or Resource.Ivory)), $"{at}: rotted {rotted:R}");
            totalYield += carried + pending;
        }
        Assert.True(totalYield <= run.BodyKg, $"{when}: {totalYield:R} kg yielded from a {run.BodyKg:R} kg animal");
    }

    /// Sessions in the chunk plan, cycling, until the carcass is butchered or the plan's budget runs
    /// out. A full pack is emptied onto the pile only when the plan says so (or the carcass waits).
    static Run Butcher(Scene s, bool topUp, Action<Run, string>? check, List<string>? log = null, int budgetMinutes = 4000, int maxSessions = 60)
    {
        var run = Build(s);
        var mode = (ButcherMode)s.Mode;
        int sessions = 0;
        for (int i = 0; sessions < maxSessions && run.Minutes < budgetMinutes; i = (i + 1) % s.Chunks.Length, sessions++)
        {
            var chunk = s.Chunks[i];
            var w = run.W;
            if (!Sim.IsAlive(w.Agent(P))) break;
            if (topUp) { TopUp(w.Agent(P).Body); KeepLit(w); }
            var step = Sim.Step(w, new Butcher(P, run.CarcassId, mode), Math.Min(chunk.Minutes, budgetMinutes - run.Minutes));
            run.Minutes += step.MinutesElapsed;
            log?.Add($"{w.Minute}: {step.MinutesElapsed} {step.InterruptedReason}");
            check?.Invoke(run, $"session {sessions} ({step.MinutesElapsed} min, {step.InterruptedReason})");
            if (Carcasses.Find(w, Camp(w), run.CarcassId) == null) break; // butchered (or weathered away)
            if (step.InterruptedReason == Work.PackFull || chunk.Unload)
                foreach (var r in CarcassResources)
                    if (Items.Have(Items.Pack(w, P), r) > 0 && Sim.IsAlive(w.Agent(P))) Sim.Step(w, new PutDown(P, r, Items.Have(Items.Pack(w, P), r)), 1);
            if (step.InterruptedReason == Work.PackFull && !chunk.Unload) break; // nowhere to put it: the rest waits on the carcass
            if (step.InterruptedReason == Work.TooDark && step.MinutesElapsed == 0 && Sim.IsAlive(w.Agent(P)))
            {
                int toDawn = ((6 * 60 - Weather.MinuteOfDay(w.Minute)) % 1440 + 1440) % 1440;
                Sim.Step(w, new Wait(P), Math.Max(1, toDawn));
            }
            else if (step.InterruptedReason is string why && why != Work.PackFull && step.MinutesElapsed == 0) break;
            if (chunk.GapMinutes > 0 && Sim.IsAlive(w.Agent(P))) Sim.Step(w, new Wait(P), chunk.GapMinutes);
            if (chunk.SaveLoad)
            {
                run.W = TestWorlds.Load(Save.ToJson(w));
                run.C = Carcasses.Find(run.W, Camp(run.W), run.CarcassId)!;
            }
            check?.Invoke(run, $"after session {sessions}'s break");
        }
        return run;
    }

    static void Ledger(Scene s) => Butcher(s, topUp: true, CheckLedger);

    [Fact]
    public void Butchering_Ledger_And_Cap() => Scenes(gaps: true).Check(Ledger, iter: 15);

    [Fact]
    [Trait("Category", TestCategories.Slow)]
    public void Butchering_Ledger_And_Cap_Many() => Scenes(gaps: true).Check(Ledger, iter: 600);

    // ---- one long session or many short ones, saved and loaded between: the same carcass, the same pack ----

    static string Outcome(Run run)
    {
        var c = run.C;
        string Kgs(IEnumerable<KeyValuePair<Resource, double>> d) => string.Join(",", d.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value:0.#########}"));
        return $"min {run.W.Minute} | carried {Kgs(Carried(run.W))} | pending {Kgs(c.Pending)} | remaining {Kgs(c.Remaining)} | yield {Kgs(c.YieldKg)}";
    }

    static void Chunking(Scene s)
    {
        var (logChunked, logWhole) = (new List<string>(), new List<string>());
        var chunked = Butcher(s, topUp: false, check: null, logChunked, budgetMinutes: 900, maxSessions: 10_000);
        var whole = Butcher(s with { Chunks = [new Chunk(5000, false, 0, false)] }, topUp: false, check: null, logWhole, budgetMinutes: 900, maxSessions: 10_000);
        Assert.True(Outcome(whole) == Outcome(chunked),
            $"whole: {Outcome(whole)}\n  chunked: {Outcome(chunked)}\n  whole sessions: {string.Join("; ", logWhole)}\n  chunked sessions: {string.Join("; ", logChunked.TakeLast(12))}");
    }

    [Fact]
    public void Butchering_SplitIntoSessions_GivesTheSameYield() => Scenes(gaps: false).Check(Chunking, iter: 8);

    [Fact]
    [Trait("Category", TestCategories.Slow)]
    public void Butchering_SplitIntoSessions_GivesTheSameYield_Many() => Scenes(gaps: false).Check(Chunking, iter: 600);

    // ---- waste never grows with skill or a blade (the cutting itself, Carcasses.Prepare and Cut) ----

    public sealed record CutScene(int Kind, int BodySeed, double EffectiveHours, int Mode, bool HandsImpaired, int SkillLo, int SkillHi, int[] Chunks);

    static readonly Gen<CutScene> CutScenes = Gen.Select(
        Gen.Int[0, Kinds.Length - 1], Gen.Int[0, 1000], Gen.Frequency((1, Gen.Const(0.0)), (3, Gen.Double[0, 60])),
        Gen.Int[0, 2], Gen.Bool, Gen.Int[0, Skills.MaxLevel], Gen.Int[0, Skills.MaxLevel], Gen.Int[1, 400].Array[1, 20],
        (k, seed, h, mode, hands, a, b, chunks) => new CutScene(k, seed, h, mode, hands, Math.Min(a, b), Math.Max(a, b), chunks));

    static readonly Lazy<World> CarcassMaker = new(() => Bare(5));

    static Carcass Fresh(CutScene s)
    {
        var species = Kinds[s.Kind];
        var body = SpeciesTable.NewBody(species, Survival.Rng.ForSystem(s.BodySeed, "carcass"));
        Dictionary<Resource, double> ledger;
        lock (CarcassMaker)
        {
            var w = CarcassMaker.Value;
            var made = Carcasses.Create(w, Camp(w), species.ToString(), body, 0, isMegafauna: body.WeightKg > 500,
                special: species == Species.Mammoth ? new Dictionary<Resource, double> { [Resource.Ivory] = 4, [Resource.MammothHide] = 15 } : null);
            Carcasses.Remove(w, Camp(w), made);
            ledger = made.Remaining;
        }
        return new Carcass
        {
            Id = 0,
            Animal = species.ToString(),
            BodyWeightKg = body.WeightKg,
            DiedMinute = 0,
            LastUpdatedMinute = 0,
            EffectiveHours = s.EffectiveHours,
            Remaining = new(ledger),
            FleshAtDeathKg = ledger[Resource.RawMeat] + ledger[Resource.RawFat],
        };
    }

    /// Cuts in the given sessions; returns the carcass. Prepare pays the setup, Cut does the rest.
    static Carcass CutAll(CutScene s, bool blade, int skill, IEnumerable<int>? chunks = null)
    {
        var c = Fresh(s);
        var mode = (ButcherMode)s.Mode;
        c.Mode = mode;
        foreach (int m in chunks ?? s.Chunks)
            Carcasses.Cut(c, Carcasses.Prepare(c, m, mode), blade, s.HandsImpaired, mode, Skills.ButcherYieldFactor(skill));
        return c;
    }

    static double Waste(Carcass c, Resource r) => c.CutKg.GetValueOrDefault(r) - c.YieldKg.GetValueOrDefault(r);

    /// Skill: the same cuts, better hands. Waste is judged per resource.
    static void MonotoneInSkill(CutScene s)
    {
        var lo = CutAll(s, blade: true, s.SkillLo);
        var hi = CutAll(s, blade: true, s.SkillHi);
        foreach (var r in CarcassResources)
        {
            Assert.Equal(lo.CutKg.GetValueOrDefault(r), hi.CutKg.GetValueOrDefault(r), 9);
            Assert.True(Waste(hi, r) <= Waste(lo, r) + Eps,
                $"{r}: skill {s.SkillHi} wastes {Waste(hi, r):R}, skill {s.SkillLo} wastes {Waste(lo, r):R} ({(ButcherMode)s.Mode})");
            Assert.True(Waste(hi, r) >= -Eps && Waste(lo, r) >= -Eps, $"{r}: negative waste");
        }
    }

    /// A blade, per kilogram cut: bare hands cannot cut tusks or a mammoth's hide, so the two cut
    /// different totals, but of what both cut the blade must waste no larger share.
    static void MonotoneInTool(CutScene s)
    {
        var blade = CutAll(s, blade: true, s.SkillLo);
        var bare = CutAll(s, blade: false, s.SkillLo);
        foreach (var r in CarcassResources)
        {
            double cutBlade = blade.CutKg.GetValueOrDefault(r), cutBare = bare.CutKg.GetValueOrDefault(r);
            if (cutBlade < 1e-6 || cutBare < 1e-6) continue;
            double pctBlade = Waste(blade, r) / cutBlade, pctBare = Waste(bare, r) / cutBare;
            Assert.True(pctBlade <= pctBare + 1e-9,
                $"{r}: with a blade {pctBlade:P1} of what is cut is wasted, bare-handed {pctBare:P1} ({(ButcherMode)s.Mode}, skill {s.SkillLo}, hands impaired {s.HandsImpaired})");
        }
    }

    [Fact]
    public void Butchering_Waste_NeverGrowsWithSkill() => CutScenes.Check(MonotoneInSkill, iter: 300);

    [Fact]
    [Trait("Category", TestCategories.Slow)]
    public void Butchering_Waste_NeverGrowsWithSkill_Many() => CutScenes.Check(MonotoneInSkill, iter: 20000);

    /// Carcass.Cut once kept 67% of bare-handed bone whatever the mode, a QuickStrip with a blade 50%
    /// (fixed: the tear applies on top of the mode).
    [Fact]
    public void Butchering_Waste_NeverGrowsWithABlade() => CutScenes.Check(MonotoneInTool, iter: 300);

    /// The blade law in the modes the bug does not reach.
    [Fact]
    public void Butchering_Waste_NeverGrowsWithABlade_CarefulAndFull() =>
        CutScenes.Where(s => (ButcherMode)s.Mode != ButcherMode.QuickStrip).Check(MonotoneInTool, iter: 300);

    [Fact]
    [Trait("Category", TestCategories.Slow)]
    public void Butchering_Waste_NeverGrowsWithABlade_CarefulAndFull_Many() =>
        CutScenes.Where(s => (ButcherMode)s.Mode != ButcherMode.QuickStrip).Check(MonotoneInTool, iter: 20000);

    /// Minutes of cutting split any way take what one cut of them does. Exact while anything is left
    /// to cut; once Carcasses.AllCut (under 0.01 kg left) stops the cutting, a split run may leave
    /// up to that hundredth of a kilo on the bone that one long cut would have taken (declared: the
    /// finishing threshold).
    static void SplitCut(CutScene s)
    {
        int total = s.Chunks.Sum();
        var split = CutAll(s, blade: true, s.SkillLo);
        var whole = CutAll(s, blade: true, s.SkillLo, [total]);
        bool finished = Carcasses.AllCut(split) || Carcasses.AllCut(whole);
        double cutApart = CarcassResources.Sum(r => Math.Abs(split.CutKg.GetValueOrDefault(r) - whole.CutKg.GetValueOrDefault(r)));
        Assert.True(cutApart <= (finished ? 0.01 : 1e-9), $"cut split {Show(split.CutKg)}, whole {Show(whole.CutKg)} (finished {finished})");
        if (!finished)
            foreach (var r in CarcassResources)
                Assert.True(Math.Abs(split.YieldKg.GetValueOrDefault(r) - whole.YieldKg.GetValueOrDefault(r)) <= 1e-9,
                    $"{r} yield: split {split.YieldKg.GetValueOrDefault(r):R}, whole {whole.YieldKg.GetValueOrDefault(r):R}");
    }

    [Fact]
    public void Butchering_CutMinutes_SplitExactly() => CutScenes.Check(SplitCut, iter: 300);

    [Fact]
    [Trait("Category", TestCategories.Slow)]
    public void Butchering_CutMinutes_SplitExactly_Many() => CutScenes.Check(SplitCut, iter: 20000);
}
