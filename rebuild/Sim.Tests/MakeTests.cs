namespace Survival.Tests;

/// Making things (emergent-world-design §3.7): recipes as data, one outcome rolled per finished piece
/// of work, skill on the odds, projects that keep their progress in the pack.
public class MakeTests
{
    static readonly AgentId P = AgentId.Player;

    // ---- the outcome syntax ----

    [Fact]
    public void Parse_ReadsTheKind_TheEffects_AndTheBaseOdds()
    {
        var o = Assert.Single(Outcomes.Parse("fail:waste_material:Flint:0.5=.08"));
        Assert.Equal(OutcomeKind.Fail, o.Kind);
        Assert.Equal(0.08, o.BasePct);
        Assert.Equal(new Effect[] { new WasteMaterial(Resource.Flint, 0.5) }, o.Effects);

        var plain = Assert.Single(Outcomes.Parse("fail=.05"));
        Assert.Equal(OutcomeKind.Fail, plain.Kind);
        Assert.Empty(plain.Effects);
    }

    [Fact]
    public void Parse_ReadsEveryEffectKind_AndStacksThem()
    {
        var o = Assert.Single(Outcomes.Parse("succeed:save_material:Sinew:1:extra_product:crude_edge:1:injury:LeftArm:0.2:break_tool:Cutting:2=.1"));
        Assert.Equal(OutcomeKind.Succeed, o.Kind);
        Assert.Equal(new Effect[]
        {
            new SaveMaterial(Resource.Sinew, 1), new ExtraProduct("crude_edge", 1), new Injury(BodyTarget.LeftArm, 0.2), new BreakTool(Capability.Cutting, 2),
        }, o.Effects);
        // No amount: the tool breaks outright.
        Assert.Equal(new Effect[] { new BreakTool(Capability.Striking, null) }, Assert.Single(Outcomes.Parse("fail:break_tool:Striking=.02")).Effects);
    }

    /// `|` lists alternatives, the outcome's odds split evenly between them.
    [Fact]
    public void Parse_Alternatives_SplitTheOddsEvenly()
    {
        var split = Outcomes.Parse("succeed:injury:LeftArm|RightArm:0.05=.04");
        Assert.Equal(2, split.Count);
        Assert.Equal(new Effect[] { new Injury(BodyTarget.LeftArm, 0.05) }, split[0].Effects);
        Assert.Equal(new Effect[] { new Injury(BodyTarget.RightArm, 0.05) }, split[1].Effects);
        Assert.All(split, o => Assert.Equal(0.02, o.BasePct, 12));

        // Two lists of alternatives: every pairing, the odds split four ways.
        var both = Outcomes.Parse("fail:waste_material:Flint|Stone:0.5:injury:LeftArm|RightArm:0.1=.08");
        Assert.Equal(4, both.Count);
        Assert.All(both, o => Assert.Equal(0.02, o.BasePct, 12));
        Assert.Equal(4, both.Select(o => string.Join(",", o.Effects)).Distinct().Count());
    }

    [Theory]
    [InlineData("fail")] // no odds
    [InlineData("maybe=.1")] // no such kind
    [InlineData("fail:melt:Flint:1=.1")] // no such effect
    [InlineData("fail:waste_material:Flint=.1")] // a missing parameter
    [InlineData("fail:waste_material:Stoned:1=.1")] // no such resource
    [InlineData("fail:waste_material:Flint:1.5=.1")] // a share above 1
    [InlineData("fail:injury:Tail:0.1=.1")] // no such body part
    [InlineData("fail:break_tool:Cutting:0=.1")] // no uses lost is not an amount
    [InlineData("fail=1.5")] // odds above 1
    [InlineData("fail=0")] // odds of nothing
    public void Parse_RefusesWhatIsNotAnOutcome(string line) =>
        Assert.Throws<FormatException>(() => Outcomes.Parse(line));

    // ---- recipes are data ----

    [Fact]
    public void TheFlintTippedSpear_IsReadAsWritten()
    {
        var r = Recipes.Get("flint_tipped_spear");
        Assert.Equal(MakeCategory.HuntingWeapon, r.Category);
        Assert.Empty(r.Items);
        Assert.Equal(new[] { (new[] { Resource.PineBranch, Resource.BirchBranch, Resource.OakBranch }, 1.0), (new[] { Resource.Flint }, 1.0),
                (new[] { Resource.PlantFiber, Resource.Sinew }, 1.0) },
            r.Materials.Select(m => (m.AnyOf.ToArray(), (double)m.Count)).ToArray(), new MaterialComparer());
        Assert.Equal([0, 0, 0.05], r.Materials.Select(m => m.Kg)); // pieces, and the binding's fibre by weight
        Assert.Equal([Capability.Cutting, Capability.Striking], r.Tools);
        Assert.Empty(r.Faster);
        Assert.Equal([Requirement.Light], r.Requires);
        Assert.Equal(40, r.Minutes);
        Assert.Equal("flint_tipped_spear", r.Result.Gear);
        // Four lines, the injury split in two.
        Assert.Equal(5, r.Outcomes.Count);
        Assert.Equal(0.19, r.Outcomes.Sum(o => o.BasePct), 12);
    }

    sealed class MaterialComparer : IEqualityComparer<(Resource[], double)>
    {
        public bool Equals((Resource[], double) a, (Resource[], double) b) => a.Item1.SequenceEqual(b.Item1) && a.Item2 == b.Item2;
        public int GetHashCode((Resource[], double) x) => x.Item2.GetHashCode();
    }

    static string Broken(string from, string to)
    {
        var files = Content.EmbeddedFiles();
        int at = files["recipes.json"].IndexOf(from, StringComparison.Ordinal);
        Assert.True(at >= 0, $"no '{from}' to edit");
        files["recipes.json"] = files["recipes.json"][..at] + to + files["recipes.json"][(at + from.Length)..];
        return Assert.Throws<InvalidDataException>(() => Content.Parse(files)).Message;
    }

    [Theory]
    [InlineData("\"result\": { \"gear\": \"flint_tipped_spear\" }", "\"result\": { \"gear\": \"spear_of_destiny\" }", "no gear kind 'spear_of_destiny'")]
    [InlineData("\"inputs\": [ { \"of\": \"PineBranch|BirchBranch|OakBranch\", \"count\": 1 }, { \"of\": \"Flint\"",
        "\"inputs\": [ { \"item\": \"wooden_sword\" }, { \"of\": \"PineBranch|BirchBranch|OakBranch\", \"count\": 1 }, { \"of\": \"Flint\"", "no gear kind 'wooden_sword'")]
    [InlineData("\"of\": \"PlantFiber|Sinew\"", "\"of\": \"PlantFiber|Sinw\"", "no Resource 'Sinw'")]
    [InlineData("\"tools\": [\"Cutting\", \"Striking\"], \"requires\": [\"Light\"], \"minutes\": 40", "\"tools\": [\"Cuting\", \"Striking\"], \"requires\": [\"Light\"], \"minutes\": 40", "no Capability 'Cuting'")]
    [InlineData("\"requires\": [\"Light\"], \"minutes\": 40", "\"requires\": [\"Moonlight\"], \"minutes\": 40", "no Requirement 'Moonlight'")]
    [InlineData("\"Striking\"], \"requires\": [\"Light\"], \"minutes\": 40", "\"Striking\"], \"requires\": [\"Light\"], \"minutes\": 0", "flint_tipped_spear: minutes 0")]
    [InlineData("\"fail=.05\", \"fail:waste_material:Flint", "\"fail=\", \"fail:waste_material:Flint", "flint_tipped_spear: outcome 'fail='")]
    // An effect on a material that is not an input, or a tool the recipe does not use.
    [InlineData("\"fail:waste_material:Flint:1=.08\"", "\"fail:waste_material:Bone:1=.08\"", "flint_tipped_spear: Bone is not an input")]
    [InlineData("\"fail:waste_material:Flint:1=.08\", \"succeed:injury:LeftArm|RightArm:0.05=.04\", \"fail:break_tool:Striking=.02\"",
        "\"fail:waste_material:Flint:1=.08\", \"succeed:injury:LeftArm|RightArm:0.05=.04\", \"fail:break_tool:Digging=.02\"", "flint_tipped_spear: Digging is not a tool it uses")]
    [InlineData("\"fail=.05\", \"fail:waste_material:Flint", "\"fail=.4\", \"fail:waste_material:Flint", "flint_tipped_spear: outcomes' odds add up to")]
    // Camp work placed on the tile.
    [InlineData("\"requires\": [\"Light\", \"NoShelter\"], \"minutes\": 30", "\"requires\": [\"Light\"], \"minutes\": 30", "lean_to: a shelter needs NoShelter")]
    [InlineData("\"result\": { \"shelter\": \"HideTent\" }", "\"result\": { \"shelter\": \"HideTent\", \"shelterName\": \"Tent\" }", "tent: a shelter is named, unless it is the tent carried")]
    [InlineData("\"result\": { \"bedding\": \"Makeshift\" }, \"outcomes\": []", "\"result\": { \"bedding\": \"Makeshift\" }, \"outcomes\": [\"fail=.1\"]", "makeshift_bedding: a placed result has no outcomes")]
    [InlineData("\"fasterFactor\": 2, \"requires\": []", "\"fasterFactor\": 1, \"requires\": []", "makeshift_bedding: fasterFactor 1")]
    [InlineData("\"Striking\"], \"requires\": [\"Light\"], \"minutes\": 40", "\"Striking\"], \"requires\": [\"Light\", \"NoShelter\"], \"minutes\": 40", "flint_tipped_spear: NoShelter, Tent and MoundPit are for a placed result")]
    [InlineData("\"replaces\": \"LeanTo\"", "\"replaces\": \"BranchHut\"", "branch_hut: only a built shelter replaces another kind")]
    [InlineData("\"result\": { \"pit\": \"Mound\" }", "\"result\": { \"pit\": \"Open\" }", "mound_fire_pit: a pit result is Mound or Stone")]
    [InlineData("\"requires\": [\"MoundPit\"]", "\"requires\": []", "stone_ring: a stone ring needs a mound pit")]
    public void ABrokenRecipe_DoesNotLoad(string from, string to, string why) =>
        Assert.Contains(why, Broken(from, to));

    // ---- the odds sheet ----

    static double Chance(MakeOdds odds, string text) => odds.Outcomes.Single(o => o.Outcome.Text == text).Odds.TotalPct;

    [Fact]
    public void Odds_Untrained_AreTheBaseOdds_AndTheRestIsThePlainResult()
    {
        var odds = Making.Odds(Recipes.Get("ivory_tipped_spear"), craftingLevel: 0);
        Assert.Equal(0.05, Chance(odds, "fail=0.05"), 12);
        Assert.Equal(0.02, Chance(odds, "succeed:injury:LeftArm:0.05=0.02"), 12);
        Assert.Equal(0.81, odds.PlainPct, 12);
    }

    /// Each level makes failure-like weights 4% rarer and success bonuses 4% likelier.
    [Fact]
    public void Odds_Skill_ShrinksFailures_AndGrowsBonuses()
    {
        var odds = Making.Odds(Recipes.Get("ivory_tipped_spear"), craftingLevel: 10);
        Assert.Equal(0.03, Chance(odds, "fail=0.05"), 12);
        Assert.Equal(0.14, Chance(odds, "succeed:save_material:Sinew:0.5=0.1"), 12);
        Assert.Equal(0.012, Chance(odds, "succeed:injury:RightArm:0.05=0.02"), 12); // an injury is failure-like
        Assert.Equal(1 - (0.03 + 0.14 + 0.024), odds.PlainPct, 12);
        var flint = Making.Odds(Recipes.Get("flint_tipped_spear"), craftingLevel: 10);
        Assert.Equal(0.048, Chance(flint, "fail:waste_material:Flint:1=0.08"), 12);
        Assert.Equal(0.012, Chance(flint, "fail:break_tool:Striking=0.02"), 12);
    }

    /// Clumsy hands shift the odds the other way: half the dexterity, half again the failures and half the bonus.
    [Fact]
    public void Odds_ClumsyHands_GrowFailures_AndShrinkBonuses()
    {
        var odds = Making.Odds(Recipes.Get("ivory_tipped_spear"), craftingLevel: 0, dexterityPct: 0.5);
        Assert.Equal(0.075, Chance(odds, "fail=0.05"), 12);
        Assert.Equal(0.05, Chance(odds, "succeed:save_material:Sinew:0.5=0.1"), 12);
        // The sheet's terms add up to each total.
        foreach (var o in odds.Outcomes) Assert.Equal(o.Odds.TotalPct, o.Odds.BasePct + o.Odds.Contributions.Sum(c => c.DeltaPct), 12);
    }

    // ---- the activity ----

    /// The bare seed-17 world under a mild grey sky at noon, the player at camp with an empty pack.
    internal static World Noon(out Inventory pack, double tempC = -2)
    {
        var w = Verification.ConservationWorlds.Bare(tempC);
        w.Minute = 3 * 60; // the clock starts at nine in the morning
        pack = Items.Pack(w, P);
        TestKit.ClearResources(pack);
        pack.Gear.Clear();
        return w;
    }

    internal static void Burning(World w, double kindlingKg = 1.0, double birchKg = 10)
    {
        var f = new FireState { Tile = Map.Of(w).Camp, LastUpdatedMinute = w.Minute, Pit = PitType.Open };
        Fire.AddFuel(f, Resource.BirchBark, 0.05);
        Fire.AddFuel(f, Resource.Stick, kindlingKg);
        Fire.IgniteAll(f);
        if (birchKg > 0) Fire.AddFuel(f, Resource.BirchBranch, birchKg);
        w.Fires[new FireId(w.NextFireId++)] = f;
    }

    [Fact]
    public void Make_TakesTheMaterials_DoesTheWork_AndRecordsWhatTheResultIsMadeOf()
    {
        var w = Noon(out var pack);
        pack.Gear.Add(Items.Make("flint_knife"));
        ClubKit(pack);
        var step = Sim.Step(w, new Make(P, "stone_club"), Verbs.UntilDoneMinutes);
        // Twenty minutes for someone whole and warm.
        Assert.Equal(20, step.MinutesElapsed);
        Assert.Equal("stone_club", Assert.Single(step.Events.OfType<Made>()).Recipe);
        Assert.Null(pack.Projects);

        // The plain result, named: one branch, the first alternative held (birch), the very piece in the club.
        var again = Begun("stone_club", p => { p.Gear.Add(Items.Make("flint_knife")); ClubKit(p); });
        Assert.Equal(Making.MadeIt, Making.Complete(again, again.Agent(P), Recipes.Get("stone_club"), null, []));
        var club = Assert.Single(Items.Pack(again, P).Gear, g => g.Kind == "stone_club");
        Assert.Equal([1.0], club.MadeOf!.Pieces[Resource.BirchBranch]);
        Assert.False(club.MadeOf.Pieces.ContainsKey(Resource.OakBranch));
        Assert.Equal(2.0, Items.Have(Items.Pack(again, P), Resource.OakBranch), 9);
    }

    /// A birch and an oak branch, a stone and a binding of fibre: what the club tests make one from.
    static void ClubKit(Inventory pack)
    {
        TestKit.Give(pack, Resource.BirchBranch, 1.0);
        TestKit.Give(pack, Resource.OakBranch, 2.0);
        Items.AddPiece(pack, Resource.Stone, 0.4);
        Items.Add(pack, Resource.PlantFiber, 0.05);
    }

    /// Any piece of the kind will do: two branches of different weights each satisfy a recipe asking for one, and
    /// the parts record holds the very piece that went in (#142).
    [Fact]
    public void TwoBranchesOfDifferentWeights_EachMakeAClub_AndItsPartsAreTheOneUsed()
    {
        var w = Noon(out var pack);
        pack.Gear.Add(Items.Make("flint_knife"));
        Items.AddPieces(pack, Resource.PineBranch, [3.1, 1.2]);
        Items.AddPieces(pack, Resource.Stone, [0.4, 0.4]);
        Items.Add(pack, Resource.PlantFiber, 0.1);
        var r = Recipes.Get("stone_club");
        Assert.Null(Sim.Refusal(w, new Make(P, r.Id)));
        Sim.Step(w, new Make(P, r.Id), 1);
        Assert.Equal(Making.MadeIt, Making.Complete(w, w.Agent(P), r, null, []));
        Assert.Null(Sim.Refusal(w, new Make(P, r.Id))); // the heavier one alone is enough for another
        Sim.Step(w, new Make(P, r.Id), 1);
        Assert.Equal(Making.MadeIt, Making.Complete(w, w.Agent(P), r, null, []));
        var parts = pack.Gear.Where(g => g.Kind == "stone_club").Select(g => g.MadeOf!.Pieces[Resource.PineBranch]).ToList();
        Assert.Equal([[1.2], [3.1]], parts);
        Assert.False(pack.Pieces.ContainsKey(Resource.PineBranch));
        Assert.All(pack.Gear.Where(g => g.Kind == "stone_club"), g => Assert.Equal(Items.GearKinds["stone_club"].BaseWeightKg, Items.WeightKg(g)));
    }

    [Fact]
    public void AProject_StaysInThePack_AndGoesOnWhereItWasLeft()
    {
        var w = Noon(out var pack);
        pack.Gear.Add(Items.Make("flint_knife"));
        TestKit.Give(pack, Resource.PineBranch, 2.0);
        Items.AddPiece(pack, Resource.Stone, 0.4);
        Items.Add(pack, Resource.PlantFiber, 0.05);
        Assert.Null(Sim.Step(w, new Make(P, "stone_club"), 8).InterruptedReason);
        var p = Assert.Single(pack.Projects!);
        Assert.Equal(8, p.MinutesDone, 9);
        Assert.Equal(2.0, Items.Have(p.Materials, Resource.PineBranch), 9); // the whole branch, never part of it
        Assert.Equal(0, Items.Have(pack, Resource.PineBranch), 9);
        // The unfinished club weighs what went into it.
        Assert.Equal(2.0 + 0.4 + 0.05 + Items.WeightKg(pack.Gear[0]), Items.WeightKg(pack), 9);

        Sim.Step(w, new Wait(P), 30); // set down a while: nothing is lost
        Assert.Equal(8, Assert.Single(pack.Projects!).MinutesDone, 9);
        Assert.Equal(12, Sim.Step(w, new Make(P, "stone_club"), Verbs.UntilDoneMinutes).MinutesElapsed);
        Assert.Null(pack.Projects);
    }

    /// One long step, many short ones, and a save and load between them make the same world.
    [Fact]
    public void AProject_IsTheSame_HoweverItsWorkIsSplit_AndThroughASave()
    {
        static World Start()
        {
            var w = Noon(out var pack);
            pack.Gear.Add(Items.Make("flint_knife"));
            pack.Gear.Add(Items.Make("stone"));
            TestKit.Give(pack, Resource.PineBranch, 1.5);
            TestKit.Give(pack, Resource.Flint, 0.5);
            TestKit.Give(pack, Resource.Sinew, 0.2);
            return w;
        }
        var whole = Start();
        Sim.Step(whole, new Make(P, "flint_tipped_spear"), Verbs.UntilDoneMinutes);

        var split = Start();
        foreach (int n in new[] { 1, 3, 2, 7 }) Sim.Step(split, new Make(P, "flint_tipped_spear"), n);
        split = TestWorlds.Load(Save.ToJson(split));
        while (Items.Pack(split, P).Projects != null) Sim.Step(split, new Make(P, "flint_tipped_spear"), 4);

        Assert.Equal(whole.Minute, split.Minute);
        Assert.Equal(Save.ToJson(whole), Save.ToJson(split));
    }

    [Fact]
    public void Abandoning_GivesBackFourFifthsOfTheMaterials_AndSinglePiecesWhole()
    {
        var w = Noon(out var pack);
        pack.Gear.Add(Items.Make("flint_knife"));
        pack.Gear.Add(Items.Make("stone"));
        Items.AddPiece(pack, Resource.PineBranch, 1.5);
        Items.AddPiece(pack, Resource.Flint, 0.2);
        Items.AddPiece(pack, Resource.Sinew, 0.15);
        Sim.Step(w, new Make(P, "flint_tipped_spear"), 10);
        var step = Sim.Step(w, new Abandon(P, "flint_tipped_spear"), 1);
        Assert.Null(pack.Projects);
        // Four fifths of one piece rounds to the piece: nothing is lost, nothing is split.
        Assert.Equal([1.5], pack.Pieces[Resource.PineBranch]);
        Assert.Equal([0.2], pack.Pieces[Resource.Flint]);
        Assert.Equal([0.15], pack.Pieces[Resource.Sinew]);
        Assert.Empty(Assert.Single(step.Events.OfType<ProjectAbandoned>()).Lost);

        // Three sticks give back two, the lightest lost; the fibre gives back four fifths.
        var w2 = Noon(out var pack2);
        pack2.Gear.Add(Items.Make("flint_knife"));
        Items.AddPieces(pack2, Resource.Stick, [0.2, 0.3, 0.4]);
        Items.Add(pack2, Resource.PlantFiber, 0.1);
        Sim.Step(w2, new Make(P, "bow_drill"), 10);
        var step2 = Sim.Step(w2, new Abandon(P, "bow_drill"), 1);
        Assert.Equal([0.3, 0.4], pack2.Pieces[Resource.Stick]);
        Assert.Equal(0.08, Items.Have(pack2, Resource.PlantFiber), 9);
        var gone = Assert.Single(step2.Events.OfType<ProjectAbandoned>());
        Assert.Equal(0.2, gone.Lost[Resource.Stick], 9);
        Assert.Equal(0.02, gone.Lost[Resource.PlantFiber], 9);
    }

    [Fact]
    public void Refusals_SayWhatIsMissing_AndCostNothing()
    {
        var w = Noon(out var pack);
        Assert.Equal("you need 1 stone", Sim.Refusal(w, new Make(P, "crude_edge"))?.Reason);
        Assert.Equal("you need 1 pine branch or 1 birch branch or 1 oak branch", Sim.Refusal(w, new Make(P, "wooden_spear"))?.Reason);
        TestKit.Give(pack, Resource.PineBranch, 2.0);
        Assert.Equal("you need something for cutting", Sim.Refusal(w, new Make(P, "wooden_spear"))?.Reason);
        pack.Gear.Add(Items.Make("crude_edge"));
        Assert.Equal("you need a fire burning here", Sim.Refusal(w, new Make(P, "wooden_spear"))?.Reason);
        Assert.Equal("you have no unfinished wooden spear", Sim.Refusal(w, new Abandon(P, "wooden_spear"))?.Reason);
        TestKit.Give(pack, Resource.Stone, 0.3);
        Assert.Null(Sim.Refusal(w, new Make(P, "crude_edge")));

        w.Minute = 14 * 60; // eleven at night, and no fire
        Assert.Equal(Work.TooDark, Sim.Refusal(w, new Make(P, "crude_edge"))?.Reason);
        Assert.Null(Sim.Refusal(w, new Make(P, "staff"))); // a straight branch, trimmed by feel
        string before = Save.ToJson(w);
        Assert.Equal(0, Sim.Step(w, new Make(P, "crude_edge"), 10).MinutesElapsed);
        Assert.Equal(before, Save.ToJson(w));
    }

    [Fact]
    public void TheWork_Stops_WhenThePlaceStopsServingIt_AndKeepsItsProgress()
    {
        var w = Noon(out var pack);
        Burning(w, kindlingKg: 0.1, birchKg: 0);
        pack.Gear.Add(Items.Make("flint_knife"));
        TestKit.Give(pack, Resource.PineBranch, 1.5);
        var step = Sim.Step(w, new Make(P, "wooden_spear"), Verbs.UntilDoneMinutes);
        Assert.Equal("you need a fire burning here", step.InterruptedReason);
        var p = Assert.Single(pack.Projects!);
        Assert.True(p.MinutesDone > 0 && p.MinutesDone < 50, $"{p.MinutesDone} minutes done");
    }

    // ---- each effect, with the outcome named ----

    /// A project for this recipe one minute in, ready to be completed as a named outcome.
    static World Begun(string recipe, Action<Inventory> kit)
    {
        var w = Noon(out var pack);
        kit(pack);
        Assert.Null(Sim.Step(w, new Make(P, recipe), 1).InterruptedReason);
        return w;
    }

    static string Complete(World w, string recipe, string outcome, out List<WorldEvent> events)
    {
        events = [];
        return Making.Complete(w, w.Agent(P), Recipes.Get(recipe), Outcomes.Parse(outcome).Single(), events);
    }

    [Fact]
    public void Fail_LosesTheTime_AndGivesTheMaterialsBack_LessWhatWasWasted()
    {
        var w = Begun("flint_tipped_spear", pack =>
        {
            pack.Gear.Add(Items.Make("flint_knife"));
            pack.Gear.Add(Items.Make("stone"));
            TestKit.Give(pack, Resource.PineBranch, 1.5);
            TestKit.Give(pack, Resource.Flint, 0.2);
            TestKit.Give(pack, Resource.Sinew, 0.05);
        });
        var pack = Items.Pack(w, P);
        // Half of one flint rounds to the whole of it: a piece is wasted whole, never split.
        Assert.Equal(Making.Spoiled, Complete(w, "flint_tipped_spear", "fail:waste_material:Flint:0.5=0.08", out var events));
        Assert.Equal(0, Items.Have(pack, Resource.Flint), 9);
        Assert.Equal(0.05, Items.Have(pack, Resource.Sinew), 9);
        Assert.Equal(1.5, Items.Have(pack, Resource.PineBranch), 9);
        Assert.DoesNotContain(pack.Gear, g => g.Kind == "flint_tipped_spear");
        var made = Assert.Single(events.OfType<Made>());
        Assert.False(made.Succeeded);
        Assert.Equal(0.2, made.Spent[Resource.Flint], 9);
    }

    [Fact]
    public void SavedMaterial_ComesBack_AndTheResult_IsMadeOfTheRest()
    {
        var w = Begun("ivory_tipped_spear", pack =>
        {
            pack.Gear.Add(Items.Make("flint_knife"));
            pack.Gear.Add(Items.Make("stone"));
            TestKit.Give(pack, Resource.PineBranch, 1.5);
            Items.AddPiece(pack, Resource.Ivory, 0.8);
            Items.AddPieces(pack, Resource.Sinew, [0.05, 0.05]);
        });
        var pack = Items.Pack(w, P);
        // Half of the two sinews comes back: one whole piece.
        Assert.Equal(Making.MadeIt, Complete(w, "ivory_tipped_spear", "succeed:save_material:Sinew:0.5=0.1", out _));
        Assert.Equal(0.05, Items.Have(pack, Resource.Sinew), 9);
        var made = Assert.Single(pack.Gear, g => g.Kind == "ivory_tipped_spear");
        Assert.Equal(new SortedDictionary<Resource, double> { [Resource.PineBranch] = 1.5, [Resource.Ivory] = 0.8, [Resource.Sinew] = 0.05 },
            Items.KgByResource(made.MadeOf!));
    }

    /// Breaking a tool costs whole uses; with no amount it breaks outright, even one that never wears.
    [Fact]
    public void BreakTool_CostsWholeUses_OrBreaksItOutright()
    {
        var w = Begun("hide_cap", pack =>
        {
            pack.Gear.Add(Items.Make("bone_needle"));
            TestKit.GivePieces(pack, Resource.HideStrip, 2);
            TestKit.Give(pack, Resource.Sinew, 0.1);
        });
        var needle = Items.Pack(w, P).Gear.Single(g => g.Kind == "bone_needle");
        Complete(w, "hide_cap", "fail:break_tool:Sewing:3=0.02", out _);
        Assert.Equal(20 - 1 - 3, needle.Durability); // one use for the work, three for the slip

        var w2 = Begun("stone_club", pack =>
        {
            pack.Gear.Add(Items.Make("flint_knife"));
            ClubKit(pack);
        });
        var knife = Items.Pack(w2, P).Gear.Single(g => g.Kind == "flint_knife");
        Complete(w2, "stone_club", "fail:break_tool:Cutting=0.02", out _);
        Assert.False(Items.Works(knife));
    }

    [Fact]
    public void Injury_IsACut_ThroughTheBody()
    {
        var w = Begun("crude_edge", pack => TestKit.Give(pack, Resource.Stone, 0.3));
        Assert.Equal(Making.MadeIt, Complete(w, "crude_edge", "succeed:injury:LeftArm:0.05=0.05", out var events));
        var cut = Assert.Single(events.OfType<Injured>());
        Assert.Equal(DamageType.Sharp, cut.Type);
        Assert.Contains("Arm", cut.Region);
        Assert.Single(Items.Pack(w, P).Gear, g => g.Kind == "crude_edge");
    }

    [Fact]
    public void ExtraProduct_IsMadeBesidesTheResult()
    {
        var w = Begun("flint_knife", pack =>
        {
            pack.Gear.Add(Items.Make("stone"));
            TestKit.Give(pack, Resource.Flint, 0.3);
            TestKit.Give(pack, Resource.Stick, 0.3);
            TestKit.Give(pack, Resource.PlantFiber, 0.1);
        });
        Complete(w, "flint_knife", "succeed:extra_product:crude_edge:1=0.05", out var events);
        var pack = Items.Pack(w, P);
        Assert.Single(pack.Gear, g => g.Kind == "flint_knife");
        Assert.Single(pack.Gear, g => g.Kind == "crude_edge");
        Assert.Equal(["crude_edge"], Assert.Single(events.OfType<Made>()).ExtraGear);
        Assert.Equal(-1, pack.Gear.Single(g => g.Kind == "stone").Durability); // used once, and a stone never wears
    }

    [Fact]
    public void AResourceResult_IsWorkedFromTheInputs()
    {
        var w = Begun("scrape_hide", pack =>
        {
            pack.Gear.Add(Items.Make("flint_knife"));
            Items.AddPieces(pack, Resource.Hide, [1.5, 1.0]);
        });
        Assert.Equal(Making.MadeIt, Complete(w, "scrape_hide", "succeed:injury:LeftArm:0.03=0.02", out var events));
        var pack = Items.Pack(w, P);
        // Any hide will do; the lightest goes, and its scraped hide is a share of its own weight.
        Assert.Equal([0.8], pack.Pieces[Resource.ScrapedHide]);
        Assert.Equal([1.5], pack.Pieces[Resource.Hide]);
        var made = Assert.Single(events.OfType<Made>());
        Assert.Equal(1.0, made.Spent[Resource.Hide], 9);
        Assert.Equal(0.8, made.Produced[Resource.ScrapedHide], 9);
    }

    // ---- hides, strips and clothing (#146) ----

    /// Scraping keeps a share of the hide's own weight: a big hide gives more than a small one. Curing is the
    /// rack's (RackTests).
    [Theory]
    [InlineData(3.0, 2.4)]
    [InlineData(1.0, 0.8)]
    public void AHide_Scrapes_ToAShareOfItsWeight(double hideKg, double scrapedKg)
    {
        var w = Begun("scrape_hide", pack =>
        {
            pack.Gear.Add(Items.Make("flint_knife"));
            Items.AddPieces(pack, Resource.Hide, [hideKg]);
        });
        Assert.Equal(Making.MadeIt, Complete(w, "scrape_hide", "succeed:injury:LeftArm:0.03=0.02", out _));
        var pack = Items.Pack(w, P);
        Assert.Equal(scrapedKg, Assert.Single(pack.Pieces[Resource.ScrapedHide]), 9);
    }

    [Theory]
    [InlineData("scrape_hide", Resource.Hide)]
    [InlineData("scrape_mammoth_hide", Resource.MammothHide)]
    [InlineData("hide_strips", Resource.CuredHide)]
    public void ScrapingAndCuttingStrips_NeedSomethingForCutting(string recipe, Resource hide)
    {
        var w = Noon(out var pack);
        TestKit.GivePieces(pack, hide, 1);
        Assert.Equal("you need something for cutting", Sim.Refusal(w, new Make(P, recipe))?.Reason);
        pack.Gear.Add(Items.Make("flint_knife"));
        Assert.Null(Sim.Refusal(w, new Make(P, recipe)));
    }

    /// A cured hide cuts into two strips, or three on the outcome that gives one more, all of its weight between them.
    [Theory]
    [InlineData(null, 2)]
    [InlineData("succeed:extra_product:HideStrip:1=0.5", 3)]
    public void ACuredHide_CutsIntoTwoOrThreeStrips(string? outcome, int strips)
    {
        var w = Begun("hide_strips", pack =>
        {
            pack.Gear.Add(Items.Make("flint_knife"));
            Items.AddPieces(pack, Resource.CuredHide, [0.6]);
        });
        var events = new List<WorldEvent>();
        Assert.Equal(Making.MadeIt, Making.Complete(w, w.Agent(P), Recipes.Get("hide_strips"),
            outcome == null ? null : Outcomes.Parse(outcome).Single(), events));
        var pack = Items.Pack(w, P);
        Assert.Equal(strips, pack.Pieces[Resource.HideStrip].Count);
        Assert.All(pack.Pieces[Resource.HideStrip], kg => Assert.Equal(0.6 / strips, kg, 9));
        Assert.Equal(0, Items.Count(pack, Resource.CuredHide));
        var made = Assert.Single(events.OfType<Made>());
        Assert.Equal(0.6, made.Produced[Resource.HideStrip], 9);
        Assert.Empty(made.ExtraGear);
    }

    /// Rolled as it is in play, by a cutter of no skill with warm hands, by a fire through the nights: the third
    /// strip about half the time.
    [Fact]
    public void TheThirdStrip_ComesAboutHalfTheTime()
    {
        var w = Noon(out var pack, tempC: 15);
        var agent = w.Agent(P);
        Burning(w);
        pack.Gear.Add(Items.Make("flint_knife"));
        int runs = 300, thirds = 0;
        for (int i = 0; i < runs; i++)
        {
            KeepTheFireUp(w, pack);
            (agent.Body.EnergyMinutes, agent.Body.HydrationMl) = (Body.MaxEnergyMinutes, Body.MaxHydrationMl);
            ((PersonMind)agent.Mind).Skills.Clear();
            agent.Body.Effects.Clear();
            pack.Gear[0].Durability = Items.Def(pack.Gear[0]).MaxDurability;
            pack.Pieces.Remove(Resource.HideStrip);
            Items.AddPieces(pack, Resource.CuredHide, [0.6]);
            Assert.Equal(0.5, Making.Odds(w, agent, Recipes.Get("hide_strips")).Outcomes.Single().Odds.TotalPct, 9);
            string? why = null;
            for (int m = 0; m < 30 && why == null; m++) why = Sim.Step(w, new Make(P, "hide_strips"), 1).InterruptedReason;
            Assert.Equal(Making.MadeIt, why);
            int n = pack.Pieces[Resource.HideStrip].Count;
            Assert.InRange(n, 2, 3);
            if (n == 3) thirds++;
        }
        Assert.InRange(thirds / (double)runs, 0.4, 0.6);
    }

    /// Each garment from what the spec lists, with a needle in the light; refused without one.
    [Theory]
    [InlineData("hide_gloves", 2, 0, 0, 1)]
    [InlineData("hide_cap", 2, 0, 0, 1)]
    [InlineData("hide_boots", 0, 1, 0, 1)]
    [InlineData("hide_wrap", 0, 2, 0, 2)]
    [InlineData("hide_leggings", 0, 2, 0, 1)]
    [InlineData("mammoth_hide_coat", 0, 1, 1, 4)]
    [InlineData("mammoth_hood", 0, 0, 1, 2)]
    public void EachGarment_IsSewnFromWhatItLists_AndNeedsANeedle(string garment, int strips, int curedHides, int curedMammothHides, int sinew)
    {
        var w = Noon(out var pack);
        TestKit.GivePieces(pack, Resource.HideStrip, strips);
        TestKit.GivePieces(pack, Resource.CuredHide, curedHides);
        TestKit.GivePieces(pack, Resource.CuredMammothHide, curedMammothHides);
        TestKit.GivePieces(pack, Resource.Sinew, sinew);
        Assert.Equal("you need something for sewing", Sim.Refusal(w, new Make(P, garment))?.Reason);

        pack.Gear.Add(Items.Make("bone_needle"));
        Assert.Null(Sim.Refusal(w, new Make(P, garment)));
        Assert.Null(Sim.Step(w, new Make(P, garment), 1).InterruptedReason);
        Assert.Equal(Making.MadeIt, Making.Complete(w, w.Agent(P), Recipes.Get(garment), null, []));
        Assert.Single(pack.Gear, g => g.Kind == garment);
        foreach (var r in new[] { Resource.HideStrip, Resource.CuredHide, Resource.CuredMammothHide, Resource.Sinew })
            Assert.Equal(0, Items.Count(pack, r));
    }

    /// A mammoth hide is scraped like any hide, and cured on the rack (RackTests); nothing is sewn from it raw.
    [Fact]
    public void AMammothHide_IsNotSewnRaw_AndScrapesToEightTenths()
    {
        Assert.DoesNotContain(Recipes.All, r => r.Tools.Contains(Capability.Sewing) && r.Materials.Any(m => m.AnyOf.Contains(Resource.MammothHide)));
        var w = Begun("scrape_mammoth_hide", pack =>
        {
            pack.Gear.Add(Items.Make("flint_knife"));
            Items.AddPieces(pack, Resource.MammothHide, [10.0]);
        });
        Assert.Equal(Making.MadeIt, Making.Complete(w, w.Agent(P), Recipes.Get("scrape_mammoth_hide"), null, []));
        var pack = Items.Pack(w, P);
        Assert.Equal(8.0, Assert.Single(pack.Pieces[Resource.ScrapedMammothHide]), 9);
    }

    // ---- processing, lighting and carrying (#150) ----

    /// Each recipe from the spec's inputs, written "Resource=pieces" or "Resource=grams g": made with its tool, refused
    /// without it, its inputs all used. Render fat is the fire's (StationTests).
    [Theory]
    [InlineData("willow_fibre", "WillowBark=100g", null, "PlantFiber", 10)]
    [InlineData("cordage", "PlantFiber=300g", null, "Rope", 15)]
    [InlineData("torch", "Stick=1 BirchBark=50g", null, "torch", 5)]
    [InlineData("torch", "Stick=1 Tinder=50g", null, "torch", 5)]
    [InlineData("resin_torch", "Stick=1 PineResin=30g Tinder=20g", null, "resin_torch", 10)]
    [InlineData("small_pouch", "BirchBark=100g PlantFiber=50g", "flint_knife", "small_pouch", 15)]
    [InlineData("rope_belt", "Rope=2", null, "rope_belt", 20)]
    [InlineData("hide_belt", "HideStrip=2", "flint_knife", "hide_belt", 30)]
    [InlineData("large_bag", "CuredHide=3 Sinew=2 Rope=2", "bone_needle", "large_bag", 60)]
    [InlineData("mammoth_hide_pack", "CuredMammothHide=1 Sinew=2 Rope=1", "bone_needle", "mammoth_hide_pack", 120)]
    public void EachRecipe_IsMadeFromWhatItLists_AndRefusedWithoutItsTool(string recipe, string inputs, string? tool, string result, int minutes)
    {
        var w = Noon(out var pack);
        w.Minute = 14 * 60; // eleven at night: none of these needs light
        var given = inputs.Split(' ').Select(s => s.Split('=')).Select(p => (R: Enum.Parse<Resource>(p[0]), Amount: p[1])).ToList();
        foreach (var (r, amount) in given)
            if (amount.EndsWith('g')) Items.Add(pack, r, double.Parse(amount[..^1]) / 1000);
            else TestKit.GivePieces(pack, r, int.Parse(amount));
        Assert.Equal(minutes, Recipes.Get(recipe).Minutes);

        if (tool != null)
        {
            Assert.Equal(tool == "bone_needle" ? "you need something for sewing" : "you need something for cutting", Sim.Refusal(w, new Make(P, recipe))?.Reason);
            pack.Gear.Add(Items.Make(tool));
        }
        Assert.Null(Sim.Refusal(w, new Make(P, recipe)));
        Assert.Null(Sim.Step(w, new Make(P, recipe), 1).InterruptedReason);
        Assert.Equal(Making.MadeIt, Making.Complete(w, w.Agent(P), Recipes.Get(recipe), null, []));

        if (Enum.TryParse<Resource>(result, out var made)) Assert.True(Items.Have(pack, made) > 0, $"no {made}");
        else Assert.Single(pack.Gear, g => g.Kind == result);
        foreach (var (r, _) in given.Where(x => x.R.ToString() != result)) Assert.Equal(0, Items.Have(pack, r), 9);
    }

    [Fact]
    public void WillowBark_TwistsToEightTenthsItsWeightInFibre()
    {
        var w = Begun("willow_fibre", pack => Items.Add(pack, Resource.WillowBark, 0.25));
        Assert.Equal(Making.MadeIt, Making.Complete(w, w.Agent(P), Recipes.Get("willow_fibre"), null, []));
        // A hundred grams go in; the rest of the bark stays.
        Assert.Equal(0.08, Items.Have(Items.Pack(w, P), Resource.PlantFiber), 9);
        Assert.Equal(0.15, Items.Have(Items.Pack(w, P), Resource.WillowBark), 9);
    }

    /// The three torch recipes that made one torch are two, making two kinds.
    [Fact]
    public void TwoTorchRecipes_MakeTwoKindsOfTorch()
    {
        Assert.Equal(["torch", "resin_torch"], Recipes.All.Where(r => r.Category == MakeCategory.Lighting).Select(r => r.Id));
        Assert.Equal(["torch", "resin_torch"], Recipes.All.Where(r => r.Category == MakeCategory.Lighting).Select(r => r.Result.Gear));
    }

    // ---- what the player sees ----

    [Fact]
    public void Verbs_ListEveryRecipe_WithItsOddsAndWhyItIsRefused()
    {
        var w = Noon(out var pack);
        TestKit.Give(pack, Resource.Stone, 0.3);
        TestKit.Give(pack, Resource.Stick, 1.0);
        var make = Verbs.At(w, P).Make.Where(v => v.Activity is Make).ToList(); // then abandoning
        Assert.Equal(Recipes.ByHand.Where(r => r.Result.Repair == null).Select(r => r.Id), make.Select(v => ((Make)v.Activity).Recipe)); // repairs: per piece held (RepairTests)
        var rock = make.Single(v => v.Activity == new Make(P, "crude_edge"));
        Assert.Equal("Make crude edge", rock.Label);
        Assert.Null(rock.Refused);
        Assert.Equal(0.75, rock.Outcomes!.PlainPct, 12);
        Assert.Equal("you need something for cutting", make.Single(v => v.Activity == new Make(P, "hand_drill")).Refused?.Reason);

        Sim.Step(w, new Make(P, "crude_edge"), 2);
        make = [.. Verbs.At(w, P).Make];
        Assert.Equal("Go on making crude edge", make.Single(v => v.Activity == new Make(P, "crude_edge")).Label);
        var abandon = Assert.Single(make, v => v.Activity is Abandon);
        Assert.Equal("Abandon the unfinished crude edge", abandon.Label);
        Assert.Null(abandon.Refused);
    }

    // ---- a run where you prepare ----

    /// Every recipe can be made from what foraging, salvage and one butchered caribou give, starting with no
    /// tools at all, each tool made by an earlier recipe, at a camp fire kept burning.
    [Fact]
    public void EveryRecipe_CanBeMade_FromWhatTheLandAndAKillGive()
    {
        var w = Noon(out var pack);
        var camp = Map.Of(w).Camp;

        // What the land gives: everything any forage table or named place's forage and harvest finds.
        // What a kill gives: butcher a caribou.
        var land = Content.Tables.Forage.Values.SelectMany(t => t).Select(i => i.Resource)
            .Concat(Content.Locations.SelectMany(l => l.All).OfType<SiteForage>().SelectMany(f => f.Items).Select(i => i.Resource))
            .Concat(Content.Locations.SelectMany(l => l.All).OfType<SiteStock>().SelectMany(f => f.Items ?? []).Select(i => i.Resource))
            .Concat(Content.Loot.Values.SelectMany(Rolled))
            .ToHashSet();
        static IEnumerable<Resource> Rolled(IEnumerable<LootRoll> rolls) =>
            rolls.SelectMany(l => (l.Resource is Resource r ? [r] : Enumerable.Empty<Resource>()).Concat(Rolled(l.Of ?? [])));
        Burning(w);
        pack.Gear.Add(Items.Make("flint_knife")); // borrowed for the butchering, then put back
        Carcasses.Create(w, camp, "Caribou", SpeciesTable.NewBody(Species.Caribou, Survival.Rng.ForSystem(1, "carcass")), w.Minute);
        var c = w.Tiles[camp].Carcasses!.Single();
        for (int i = 0; i < 40 && Carcasses.Find(w, camp, c.Id) != null; i++)
        {
            KeepTheFireUp(w, pack);
            Sim.Step(w, new Butcher(P, c.Id, ButcherMode.Careful), 60);
            foreach (var r in Items.KgByResource(pack).Keys.ToList()) { land.Add(r); TestKit.Remove(pack, r); } // carried off to camp
        }
        Assert.Null(Carcasses.Find(w, camp, c.Id));
        pack.Gear.Clear();
        TestKit.ClearResources(pack);
        w.Agent(P).Body.Effects.Clear();

        var made = new HashSet<string>();
        var producers = Recipes.ByHand.Where(r => r.Result.Resource != null).ToDictionary(r => r.Result.Resource!.Value);
        // A hide is cured only on a rack (#191): its scraped hide is made by hand, put on a rack at camp, and taken
        // two days on. The rack's own recipe is RackTests'.
        var onRack = Stations.RecipesOf(StationKind.Rack).ToDictionary(r => r.Result.Resource!.Value);
        RackId? rack = null;

        // Fetches or makes the rack recipe's one input, puts it on, and waits out the drying with the camp kept up.
        bool Cure(Recipe r, int depth, IReadOnlySet<Resource> uses)
        {
            var (input, of) = (r.Materials[0], Stations.InputOf(r)); // one input, the loader's rule
            var keep = uses.Append(of).ToHashSet();
            for (int fetched = 0; !TestKit.Enough(pack, input); fetched++)
            {
                Assert.True(fetched < 20, $"{r.Id}: {of} never reaches the pack ({Items.Describe(pack)})");
                if (land.Contains(of)) TestKit.GiveFor(pack, of, input);
                else if (!producers.TryGetValue(of, out var from) || !MakeOne(from, depth + 1, keep)) return false;
            }
            rack ??= Stations.Build(w, camp);
            if (Sim.Step(w, new PutOn(P, r.Id, rack), 1).InterruptedReason is string refused) Assert.Fail($"{r.Id}: {refused}");
            bool done = false;
            for (int hours = 0; !done; hours++)
            {
                Assert.True(hours < 2 * r.Minutes / 60, $"{r.Id}: not done on the rack");
                KeepTheFireUp(w, pack);
                var body = w.Agent(P).Body;
                (body.EnergyMinutes, body.HydrationMl) = (Body.MaxEnergyMinutes, Body.MaxHydrationMl);
                body.Effects.Clear();
                done = Sim.Advance(w, 60).OfType<TransformationFinished>().Any(f => f.Recipe == r.Id);
            }
            Sim.Step(w, new TakeFinished(P, rack), 1);
            return true;
        }

        // Makes the recipe once, fetching raw materials from the land and making the rest first.
        // False if it cannot be started with what the land, the kill and earlier recipes give.
        bool MakeOne(Recipe r, int depth, IReadOnlySet<Resource>? keep = null)
        {
            var uses = r.Materials.SelectMany(m => m.AnyOf).Concat(keep ?? new HashSet<Resource>()).ToHashSet();
            Assert.True(depth < 10, $"{r.Id}: its inputs go round in a circle");
            for (int tries = 0; tries < 100; tries++)
            {
                // What the land gives and neither this recipe nor the ones it is made for use goes back to it: the pack stays light.
                foreach (var res in Items.KgByResource(pack).Keys.Where(x => land.Contains(x) && !uses.Contains(x)).ToList()) TestKit.Remove(pack, res);
                if (Making.ProjectFor(pack, r.Id) == null)
                    foreach (var m in r.Materials)
                        for (int fetched = 0; !TestKit.Enough(pack, m); fetched++)
                        {
                            Assert.True(fetched < 20, $"{r.Id}: {string.Join("|", m.AnyOf)} never reaches the pack ({Items.Describe(pack)})");
                            if (m.AnyOf.Where(land.Contains).Select(x => (Resource?)x).FirstOrDefault() is Resource raw) TestKit.GiveFor(pack, raw, m);
                            else if (producers.TryGetValue(m.AnyOf[0], out var from)) { if (!MakeOne(from, depth + 1, uses)) return false; }
                            else if (onRack.TryGetValue(m.AnyOf[0], out var cure)) { if (!Cure(cure, depth + 1, uses)) return false; }
                            else return false;
                        }
                KeepTheFireUp(w, pack);
                // The sleep, food and water between days of work, which this test is not about.
                var body = w.Agent(P).Body;
                (body.EnergyMinutes, body.HydrationMl) = (Body.MaxEnergyMinutes, Body.MaxHydrationMl);
                body.Effects.Clear();
                // A tool worn out on earlier work is made again, as a player would.
                foreach (var cap in r.Tools.Where(c => !pack.Gear.Any(g => Items.Works(g) && Items.Def(g).Capabilities.Contains(c))).ToList())
                    if (Recipes.All.FirstOrDefault(x => made.Contains(x.Id) && x.Result.Gear is string k && Items.GearKinds[k].Capabilities.Contains(cap)) is Recipe maker
                        && !MakeOne(maker, depth + 1, uses)) return false;
                if (Sim.Refusal(w, new Make(P, r.Id)) != null) return false;
                var step = Sim.Step(w, new Make(P, r.Id), 30);
                Assert.True(step.InterruptedReason is Making.MadeIt or Making.Spoiled or null, $"{r.Id}: {step.InterruptedReason}");
                if (step.Events.OfType<Made>().Any(e => e.Succeeded)) return true;
            }
            return false;
        }

        for (bool progress = true; progress;)
        {
            progress = false;
            foreach (var r in Carried.Where(r => !made.Contains(r.Id)).ToList())
            {
                if (!MakeOne(r, 0)) continue;
                made.Add(r.Id);
                progress = true;
                // Keep the pack light: what no recipe uses as a tool or input goes on the camp pile, but a bag stays on
                // (the mammoth hide tent's hides and poles are more than the bare pack holds), and what the land gives goes
                // back to it, with what no carried recipe takes (a scraped hide, which only a rack does).
                foreach (var g in pack.Gear.Where(g => Items.Def(g).CapacityBonusKg == 0
                    && !Recipes.All.Any(x => x.Items.Contains(g.Kind) || x.Tools.Concat(x.Faster).Any(cap => Items.Def(g).Capabilities.Contains(cap)))).ToList())
                {
                    pack.Gear.Remove(g);
                    Items.PileAt(w, camp).Gear.Add(g);
                }
                foreach (var res in Items.KgByResource(pack).Keys.Where(x => land.Contains(x) || !Carried.Any(c => c.Materials.Any(m => m.AnyOf.Contains(x)))).ToList())
                    TestKit.Remove(pack, res);
            }
        }
        Assert.Equal(Carried.Select(r => r.Id).Order(), made.Order());
        Assert.True(rack != null, "a cured mammoth hide comes only off a rack, and the walk never used one");
    }

    /// Camp work needs a shovel and a tent no recipe makes yet; ShelterTests builds each. A repair makes nothing: RepairTests.
    static readonly IReadOnlyList<Recipe> Carried = [.. Recipes.ByHand.Where(r => !r.Result.Placed && r.Result.Repair == null)];

    static void KeepTheFireUp(World w, Inventory pack)
    {
        var pit = w.Fires[Fire.PitAt(w, Map.Of(w).Camp)!.Value];
        if (Fire.IsActive(pit) && pit.UnburnedKg.Values.Sum() + pit.BurningKg.Values.Sum() > 4) return;
        TestKit.Give(pack, Resource.BirchBranch, 4);
        Assert.Null(Sim.Step(w, new AddFuel(P, Resource.BirchBranch, 4), 1).InterruptedReason);
    }

    // ---- review: saving on a resource result, worn-out tools, the cap, the real skill ----

    [Fact]
    public void SavingOnAResourceResult_IsABonus_AndWasteShrinksIt()
    {
        var w = Begun("cordage", pack => TestKit.Give(pack, Resource.PlantFiber, 0.3));
        Assert.Equal(Making.MadeIt, Complete(w, "cordage", "succeed:save_material:PlantFiber:0.33=0.1", out _));
        Assert.Equal(0.3 * Recipes.Get("cordage").Result.SharePct, Items.Have(Items.Pack(w, P), Resource.Rope), 9);
        Assert.Equal(0.099, Items.Have(Items.Pack(w, P), Resource.PlantFiber), 9);

        var w2 = Begun("scrape_hide", pack => { pack.Gear.Add(Items.Make("flint_knife")); TestKit.Give(pack, Resource.Hide, 1.0); });
        Assert.Equal(Making.MadeIt, Complete(w2, "scrape_hide", "succeed:waste_material:Hide:0.25=0.05", out _));
        Assert.Equal(0.8, Items.Have(Items.Pack(w2, P), Resource.ScrapedHide), 9); // a quarter of one hide is none of it
    }

    /// A tool worn out by the finishing work is the one a broken-tool outcome breaks, not the next.
    [Fact]
    public void BreakTool_BreaksTheToolThatDidTheWork()
    {
        var w = Begun("stone_club", pack =>
        {
            pack.Gear.Add(Items.Make("crude_edge", durability: 1));
            pack.Gear.Add(Items.Make("flint_knife"));
            ClubKit(pack);
        });
        Complete(w, "stone_club", "fail:break_tool:Cutting=0.02", out _);
        var pack = Items.Pack(w, P);
        Assert.False(Items.Works(pack.Gear.Single(g => g.Kind == "crude_edge")));
        Assert.True(Items.Works(pack.Gear.Single(g => g.Kind == "flint_knife")));
    }

    [Fact]
    public void Odds_NeverAddUpToMoreThanACertainty()
    {
        // As risky as the loader allows, with no dexterity (a foggy head is in it): 0.5 x 2 = 1.
        var risky = Recipes.Get("hand_drill") with { Outcomes = [.. Outcomes.Parse("fail=.3"), .. Outcomes.Parse("fail:break_tool:Striking=.2")] };
        var odds = Making.Odds(risky, craftingLevel: 0, dexterityPct: 0);
        Assert.Equal(1.0, odds.Outcomes.Sum(o => o.Odds.TotalPct), 12);
        Assert.Equal(0.3 / 0.5, odds.Outcomes[0].Odds.TotalPct, 12); // each keeps its share
        Assert.Equal(0, odds.PlainPct, 12);
    }

    [Fact]
    public void Odds_ReadTheMakersOwnCraftingLevel()
    {
        var w = Noon(out _);
        var me = w.Agent(P);
        ((PersonMind)me.Mind).Skills[Skill.Crafting] = new SkillProgress { Level = 10 };
        var odds = Making.Odds(w, me, Recipes.Get("hand_drill"));
        Assert.Equal(0.05 * 0.6, Assert.Single(odds.Outcomes).Odds.TotalPct, 9);
    }
}
