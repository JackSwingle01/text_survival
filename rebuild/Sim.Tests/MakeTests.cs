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
        var o = Assert.Single(Outcomes.Parse("succeed:save_material:Sinew:1:extra_product:sharp_rock:1:injury:LeftArm:0.2:break_tool:Cutting:2=.1"));
        Assert.Equal(OutcomeKind.Succeed, o.Kind);
        Assert.Equal(new Effect[]
        {
            new SaveMaterial(Resource.Sinew, 1), new ExtraProduct("sharp_rock", 1), new Injury(BodyTarget.LeftArm, 0.2), new BreakTool(Capability.Cutting, 2),
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
    public void TheStoneTippedSpear_IsReadAsWritten()
    {
        var r = Recipes.Get("stone_tipped_spear");
        Assert.Equal(MakeCategory.HuntingWeapon, r.Category);
        Assert.Equal(["wooden_spear"], r.Items);
        Assert.Equal(new[] { (new[] { Resource.Flint }, 1.0), (new[] { Resource.Sinew, Resource.Rope }, 1.0) },
            r.Materials.Select(m => (m.AnyOf.ToArray(), (double)m.Count)).ToArray(), new MaterialComparer());
        Assert.All(r.Materials, m => Assert.Equal(0, m.Kg)); // pieces, never a weight
        Assert.Equal([Capability.Cutting], r.Tools);
        Assert.Equal([Capability.Striking], r.Faster);
        Assert.Equal([Requirement.Light], r.Requires);
        Assert.Equal(40, r.Minutes);
        Assert.Equal("stone_tipped_spear", r.Result.Gear);
        // Five lines, the injury split in two.
        Assert.Equal(6, r.Outcomes.Count);
        Assert.Equal(0.29, r.Outcomes.Sum(o => o.BasePct), 12);
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
    [InlineData("\"result\": { \"gear\": \"stone_tipped_spear\" }", "\"result\": { \"gear\": \"spear_of_destiny\" }", "no gear kind 'spear_of_destiny'")]
    [InlineData("{ \"item\": \"wooden_spear\" }", "{ \"item\": \"wooden_sword\" }", "no gear kind 'wooden_sword'")]
    [InlineData("\"of\": \"Sinew|Rope\"", "\"of\": \"Sinew|Rop\"", "no Resource 'Rop'")]
    [InlineData("\"tools\": [\"Cutting\"], \"faster\": [\"Striking\"]", "\"tools\": [\"Cuting\"], \"faster\": [\"Striking\"]", "no Capability 'Cuting'")]
    [InlineData("\"requires\": [\"Light\"], \"minutes\": 40", "\"requires\": [\"Moonlight\"], \"minutes\": 40", "no Requirement 'Moonlight'")]
    [InlineData("\"minutes\": 40", "\"minutes\": 0", "stone_tipped_spear: minutes 0")]
    [InlineData("\"fail=.05\", \"fail:waste", "\"fail=\", \"fail:waste", "stone_tipped_spear: outcome 'fail='")]
    // An effect on a material that is not an input, or a tool the recipe does not use.
    [InlineData("\"fail:waste_material:Flint:.5=.08\"", "\"fail:waste_material:Bone:.5=.08\"", "stone_tipped_spear: Bone is not an input")]
    [InlineData("\"fail:break_tool:Cutting=.02\"", "\"fail:break_tool:Digging=.02\"", "stone_tipped_spear: Digging is not a tool it uses")]
    [InlineData("\"fail=.05\", \"fail:waste", "\"fail=.3\", \"fail:waste", "stone_tipped_spear: outcomes' odds add up to")]
    // Camp work placed on the tile.
    [InlineData("\"requires\": [\"Light\", \"NoShelter\"], \"minutes\": 30", "\"requires\": [\"Light\"], \"minutes\": 30", "branch_frame: a shelter needs NoShelter")]
    [InlineData("\"result\": { \"shelter\": \"HideTent\" }", "\"result\": { \"shelter\": \"HideTent\", \"shelterName\": \"Tent\" }", "tent: a shelter is named, unless it is the tent carried")]
    [InlineData("\"result\": { \"bedding\": \"Makeshift\" }, \"outcomes\": []", "\"result\": { \"bedding\": \"Makeshift\" }, \"outcomes\": [\"fail=.1\"]", "makeshift_bedding: a placed result has no outcomes")]
    [InlineData("\"fasterFactor\": 2, \"requires\": []", "\"fasterFactor\": 1, \"requires\": []", "makeshift_bedding: fasterFactor 1")]
    [InlineData("\"requires\": [\"Light\"], \"minutes\": 40", "\"requires\": [\"Light\", \"NoShelter\"], \"minutes\": 40", "stone_tipped_spear: NoShelter and Tent are for a placed result")]
    public void ABrokenRecipe_DoesNotLoad(string from, string to, string why) =>
        Assert.Contains(why, Broken(from, to));

    // ---- the odds sheet ----

    static double Chance(MakeOdds odds, string text) => odds.Outcomes.Single(o => o.Outcome.Text == text).Odds.TotalPct;

    [Fact]
    public void Odds_Untrained_AreTheBaseOdds_AndTheRestIsThePlainResult()
    {
        var odds = Making.Odds(Recipes.Get("stone_tipped_spear"), craftingLevel: 0);
        Assert.Equal(0.05, Chance(odds, "fail=0.05"), 12);
        Assert.Equal(0.02, Chance(odds, "succeed:injury:LeftArm:0.05=0.02"), 12);
        Assert.Equal(0.71, odds.PlainPct, 12);
    }

    /// Each level makes failure-like weights 4% rarer and success bonuses 4% likelier.
    [Fact]
    public void Odds_Skill_ShrinksFailures_AndGrowsBonuses()
    {
        var odds = Making.Odds(Recipes.Get("stone_tipped_spear"), craftingLevel: 10);
        Assert.Equal(0.03, Chance(odds, "fail=0.05"), 12);
        Assert.Equal(0.048, Chance(odds, "fail:waste_material:Flint:0.5=0.08"), 12);
        Assert.Equal(0.14, Chance(odds, "succeed:save_material:Sinew:1=0.1"), 12);
        Assert.Equal(0.012, Chance(odds, "succeed:injury:RightArm:0.05=0.02"), 12); // an injury is failure-like
        Assert.Equal(0.012, Chance(odds, "fail:break_tool:Cutting=0.02"), 12);
        Assert.Equal(1 - (0.03 + 0.048 + 0.14 + 0.024 + 0.012), odds.PlainPct, 12);
    }

    /// Clumsy hands shift the odds the other way: half the dexterity, half again the failures and half the bonus.
    [Fact]
    public void Odds_ClumsyHands_GrowFailures_AndShrinkBonuses()
    {
        var odds = Making.Odds(Recipes.Get("stone_tipped_spear"), craftingLevel: 0, dexterityPct: 0.5);
        Assert.Equal(0.075, Chance(odds, "fail=0.05"), 12);
        Assert.Equal(0.05, Chance(odds, "succeed:save_material:Sinew:1=0.1"), 12);
        // The sheet's terms add up to each total.
        foreach (var o in odds.Outcomes) Assert.Equal(o.Odds.TotalPct, o.Odds.BasePct + o.Odds.Contributions.Sum(c => c.DeltaPct), 12);
    }

    // ---- the activity ----

    /// The bare seed-17 world under a mild grey sky at noon, the player at camp with an empty pack.
    static World Noon(out Inventory pack, double tempC = -2)
    {
        var w = Verification.ConservationWorlds.Bare(tempC);
        w.Minute = 3 * 60; // the clock starts at nine in the morning
        pack = Items.Pack(w, P);
        TestKit.ClearResources(pack);
        pack.Gear.Clear();
        return w;
    }

    static void Burning(World w, double kindlingKg = 1.0, double birchKg = 10)
    {
        var f = new FireState { Tile = Map.Of(w).Camp, LastUpdatedMinute = w.Minute, Pit = PitType.Open };
        Fire.AddFuel(f, Resource.BirchBark, 0.05);
        Fire.AddFuel(f, Resource.Kindling, kindlingKg);
        Fire.IgniteAll(f);
        if (birchKg > 0) Fire.AddFuel(f, Resource.Birch, birchKg);
        w.Fires[new FireId(w.NextFireId++)] = f;
    }

    [Fact]
    public void Make_TakesTheMaterials_DoesTheWork_AndRecordsWhatTheResultIsMadeOf()
    {
        var w = Noon(out var pack);
        pack.Gear.Add(Items.Make("flint_knife"));
        TestKit.Give(pack, Resource.Birch, 1.0);
        TestKit.Give(pack, Resource.Oak, 2.0);
        var step = Sim.Step(w, new Make(P, "crude_spear"), Verbs.UntilDoneMinutes);
        // Twenty minutes for someone whole and warm.
        Assert.Equal(20, step.MinutesElapsed);
        Assert.Equal("crude_spear", Assert.Single(step.Events.OfType<Made>()).Recipe);
        Assert.Null(pack.Projects);

        // The plain result, named: one log, the first alternative held (birch), the very piece in the spear.
        var again = Begun("crude_spear", p => { p.Gear.Add(Items.Make("flint_knife")); TestKit.Give(p, Resource.Birch, 1.0); TestKit.Give(p, Resource.Oak, 2.0); });
        Assert.Equal(Making.MadeIt, Making.Complete(again, again.Agent(P), Recipes.Get("crude_spear"), null, []));
        var spear = Assert.Single(Items.Pack(again, P).Gear, g => g.Kind == "crude_spear");
        Assert.Equal([1.0], spear.MadeOf!.Pieces[Resource.Birch]);
        Assert.False(spear.MadeOf.Pieces.ContainsKey(Resource.Oak));
        Assert.Equal(2.0, Items.Have(Items.Pack(again, P), Resource.Oak), 9);
    }

    /// Any piece of the kind will do: two branches of different weights each satisfy a recipe asking for one, and
    /// the parts record holds the very piece that went in (#142).
    [Fact]
    public void TwoBranchesOfDifferentWeights_EachMakeASpear_AndItsPartsAreTheOneUsed()
    {
        var w = Noon(out var pack);
        pack.Gear.Add(Items.Make("flint_knife"));
        Items.AddPieces(pack, Resource.Pine, [3.1, 1.2]);
        var r = Recipes.Get("crude_spear");
        Assert.Null(Sim.Refusal(w, new Make(P, r.Id)));
        Sim.Step(w, new Make(P, r.Id), 1);
        Assert.Equal(Making.MadeIt, Making.Complete(w, w.Agent(P), r, null, []));
        Assert.Null(Sim.Refusal(w, new Make(P, r.Id))); // the heavier one alone is enough for another
        Sim.Step(w, new Make(P, r.Id), 1);
        Assert.Equal(Making.MadeIt, Making.Complete(w, w.Agent(P), r, null, []));
        var parts = pack.Gear.Where(g => g.Kind == "crude_spear").Select(g => g.MadeOf!.Pieces[Resource.Pine]).ToList();
        Assert.Equal([[1.2], [3.1]], parts);
        Assert.False(pack.Pieces.ContainsKey(Resource.Pine));
        Assert.All(pack.Gear.Where(g => g.Kind == "crude_spear"), g => Assert.Equal(Items.GearKinds["crude_spear"].BaseWeightKg, Items.WeightKg(g)));
    }

    [Fact]
    public void AProject_StaysInThePack_AndGoesOnWhereItWasLeft()
    {
        var w = Noon(out var pack);
        pack.Gear.Add(Items.Make("flint_knife"));
        TestKit.Give(pack, Resource.Pine, 2.0);
        Assert.Null(Sim.Step(w, new Make(P, "crude_spear"), 8).InterruptedReason);
        var p = Assert.Single(pack.Projects!);
        Assert.Equal(8, p.MinutesDone, 9);
        Assert.Equal(2.0, Items.Have(p.Materials, Resource.Pine), 9); // the whole log, never part of it
        Assert.Equal(0, Items.Have(pack, Resource.Pine), 9);
        // The unfinished spear weighs what went into it.
        Assert.Equal(2.0 + Items.WeightKg(pack.Gear[0]), Items.WeightKg(pack), 9);

        Sim.Step(w, new Wait(P), 30); // set down a while: nothing is lost
        Assert.Equal(8, Assert.Single(pack.Projects!).MinutesDone, 9);
        Assert.Equal(12, Sim.Step(w, new Make(P, "crude_spear"), Verbs.UntilDoneMinutes).MinutesElapsed);
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
            pack.Gear.Add(Items.Make("hammerstone"));
            pack.Gear.Add(Items.Make("wooden_spear"));
            TestKit.Give(pack, Resource.Flint, 0.5);
            TestKit.Give(pack, Resource.Sinew, 0.2);
            return w;
        }
        var whole = Start();
        Sim.Step(whole, new Make(P, "stone_tipped_spear"), Verbs.UntilDoneMinutes);

        var split = Start();
        foreach (int n in new[] { 1, 3, 2, 7 }) Sim.Step(split, new Make(P, "stone_tipped_spear"), n);
        split = TestWorlds.Load(Save.ToJson(split));
        while (Items.Pack(split, P).Projects != null) Sim.Step(split, new Make(P, "stone_tipped_spear"), 4);

        Assert.Equal(whole.Minute, split.Minute);
        Assert.Equal(Save.ToJson(whole), Save.ToJson(split));
    }

    [Fact]
    public void Abandoning_GivesBackFourFifthsOfTheMaterials_AndTheItemsWhole()
    {
        var w = Noon(out var pack);
        pack.Gear.Add(Items.Make("flint_knife"));
        pack.Gear.Add(Items.Make("wooden_spear"));
        Items.AddPiece(pack, Resource.Flint, 0.2);
        Items.AddPiece(pack, Resource.Rope, 0.15);
        Sim.Step(w, new Make(P, "stone_tipped_spear"), 10);
        var step = Sim.Step(w, new Abandon(P, "stone_tipped_spear"), 1);
        Assert.Null(pack.Projects);
        // Four fifths of one piece rounds to the piece: nothing is lost, nothing is split.
        Assert.Equal([0.2], pack.Pieces[Resource.Flint]);
        Assert.Equal([0.15], pack.Pieces[Resource.Rope]);
        Assert.Single(pack.Gear, g => g.Kind == "wooden_spear");
        Assert.Empty(Assert.Single(step.Events.OfType<ProjectAbandoned>()).Lost);

        // Three sticks give back two, the lightest lost; the fibre gives back four fifths.
        var w2 = Noon(out var pack2);
        pack2.Gear.Add(Items.Make("flint_knife"));
        Items.AddPieces(pack2, Resource.Kindling, [0.2, 0.3, 0.4]);
        Items.Add(pack2, Resource.PlantFiber, 0.1);
        Sim.Step(w2, new Make(P, "bow_drill"), 10);
        var step2 = Sim.Step(w2, new Abandon(P, "bow_drill"), 1);
        Assert.Equal([0.3, 0.4], pack2.Pieces[Resource.Kindling]);
        Assert.Equal(0.08, Items.Have(pack2, Resource.PlantFiber), 9);
        var gone = Assert.Single(step2.Events.OfType<ProjectAbandoned>());
        Assert.Equal(0.2, gone.Lost[Resource.Kindling], 9);
        Assert.Equal(0.02, gone.Lost[Resource.PlantFiber], 9);
    }

    [Fact]
    public void Refusals_SayWhatIsMissing_AndCostNothing()
    {
        var w = Noon(out var pack);
        Assert.Equal("you need 1 stone", Sim.Refusal(w, new Make(P, "sharp_rock"))?.Reason);
        Assert.Equal("you need 1 pine log or 1 birch log or 1 oak log", Sim.Refusal(w, new Make(P, "crude_spear"))?.Reason);
        TestKit.Give(pack, Resource.Pine, 2.0);
        Assert.Equal("you need something for cutting", Sim.Refusal(w, new Make(P, "crude_spear"))?.Reason);
        Assert.Equal("you need a crude spear", Sim.Refusal(w, new Make(P, "wooden_spear"))?.Reason);
        pack.Gear.Add(Items.Make("crude_spear"));
        Assert.Equal("you need a fire burning here", Sim.Refusal(w, new Make(P, "wooden_spear"))?.Reason);
        Assert.Equal("you have no unfinished crude spear", Sim.Refusal(w, new Abandon(P, "crude_spear"))?.Reason);
        TestKit.Give(pack, Resource.Stone, 0.3);
        Assert.Null(Sim.Refusal(w, new Make(P, "sharp_rock")));

        w.Minute = 14 * 60; // eleven at night, and no fire
        Assert.Equal(Work.TooDark, Sim.Refusal(w, new Make(P, "sharp_rock"))?.Reason);
        Assert.Null(Sim.Refusal(w, new Make(P, "staff"))); // a straight branch, trimmed by feel
        string before = Save.ToJson(w);
        Assert.Equal(0, Sim.Step(w, new Make(P, "sharp_rock"), 10).MinutesElapsed);
        Assert.Equal(before, Save.ToJson(w));
    }

    [Fact]
    public void TheWork_Stops_WhenThePlaceStopsServingIt_AndKeepsItsProgress()
    {
        var w = Noon(out var pack);
        Burning(w, kindlingKg: 0.1, birchKg: 0);
        pack.Gear.Add(Items.Make("crude_spear"));
        var step = Sim.Step(w, new Make(P, "wooden_spear"), Verbs.UntilDoneMinutes);
        Assert.Equal("you need a fire burning here", step.InterruptedReason);
        var p = Assert.Single(pack.Projects!);
        Assert.True(p.MinutesDone > 0 && p.MinutesDone < 30, $"{p.MinutesDone} minutes done");
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
        var w = Begun("stone_tipped_spear", pack =>
        {
            pack.Gear.Add(Items.Make("flint_knife"));
            pack.Gear.Add(Items.Make("wooden_spear"));
            TestKit.Give(pack, Resource.Flint, 0.2);
            TestKit.Give(pack, Resource.Sinew, 0.05);
        });
        var pack = Items.Pack(w, P);
        // Half of one flint rounds to the whole of it: a piece is wasted whole, never split.
        Assert.Equal(Making.Spoiled, Complete(w, "stone_tipped_spear", "fail:waste_material:Flint:0.5=0.08", out var events));
        Assert.Equal(0, Items.Have(pack, Resource.Flint), 9);
        Assert.Equal(0.05, Items.Have(pack, Resource.Sinew), 9);
        Assert.Single(pack.Gear, g => g.Kind == "wooden_spear");
        Assert.DoesNotContain(pack.Gear, g => g.Kind == "stone_tipped_spear");
        var made = Assert.Single(events.OfType<Made>());
        Assert.False(made.Succeeded);
        Assert.Equal(0.2, made.Spent[Resource.Flint], 9);
    }

    [Fact]
    public void SavedMaterial_ComesBack_AndTheResult_IsMadeOfTheRest()
    {
        var w = Begun("stone_tipped_spear", pack =>
        {
            pack.Gear.Add(Items.Make("flint_knife"));
            var spear = Items.Make("wooden_spear");
            spear.MadeOf = new();
            Items.AddPiece(spear.MadeOf, Resource.Pine, 1.5);
            pack.Gear.Add(spear);
            TestKit.Give(pack, Resource.Flint, 0.2);
            TestKit.Give(pack, Resource.Sinew, 0.05);
        });
        var pack = Items.Pack(w, P);
        Assert.Equal(Making.MadeIt, Complete(w, "stone_tipped_spear", "succeed:save_material:Sinew:1=0.1", out _));
        Assert.Equal(0.05, Items.Have(pack, Resource.Sinew), 9);
        var made = Assert.Single(pack.Gear, g => g.Kind == "stone_tipped_spear");
        Assert.Equal(new SortedDictionary<Resource, double> { [Resource.Pine] = 1.5, [Resource.Flint] = 0.2 }, Items.KgByResource(made.MadeOf!));
        Assert.DoesNotContain(pack.Gear, g => g.Kind == "wooden_spear");
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

        var w2 = Begun("crude_spear", pack =>
        {
            pack.Gear.Add(Items.Make("flint_knife"));
            TestKit.Give(pack, Resource.Pine, 1.5);
        });
        var knife = Items.Pack(w2, P).Gear.Single(g => g.Kind == "flint_knife");
        Complete(w2, "crude_spear", "fail:break_tool:Cutting=0.02", out _);
        Assert.False(Items.Works(knife));
    }

    [Fact]
    public void Injury_IsACut_ThroughTheBody()
    {
        var w = Begun("sharp_rock", pack => TestKit.Give(pack, Resource.Stone, 0.3));
        Assert.Equal(Making.MadeIt, Complete(w, "sharp_rock", "succeed:injury:LeftArm:0.05=0.05", out var events));
        var cut = Assert.Single(events.OfType<Injured>());
        Assert.Equal(DamageType.Sharp, cut.Type);
        Assert.Contains("Arm", cut.Region);
        Assert.Single(Items.Pack(w, P).Gear, g => g.Kind == "sharp_rock");
    }

    [Fact]
    public void ExtraProduct_IsMadeBesidesTheResult()
    {
        var w = Begun("flint_knife", pack =>
        {
            pack.Gear.Add(Items.Make("hammerstone"));
            TestKit.Give(pack, Resource.Flint, 0.3);
            TestKit.Give(pack, Resource.Kindling, 0.3);
            TestKit.Give(pack, Resource.PlantFiber, 0.1);
        });
        Complete(w, "flint_knife", "succeed:extra_product:sharp_rock:1=0.05", out var events);
        var pack = Items.Pack(w, P);
        Assert.Single(pack.Gear, g => g.Kind == "flint_knife");
        Assert.Single(pack.Gear, g => g.Kind == "sharp_rock");
        Assert.Equal(["sharp_rock"], Assert.Single(events.OfType<Made>()).ExtraGear);
        Assert.Equal(29, pack.Gear.Single(g => g.Kind == "hammerstone").Durability); // used once
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

    /// Scraping and drying keep a share of the hide's own weight: a big hide gives more than a small one.
    [Theory]
    [InlineData(3.0, 2.4, 2.16)]
    [InlineData(1.0, 0.8, 0.72)]
    public void AHide_ScrapesAndDries_ToAShareOfItsWeight(double hideKg, double scrapedKg, double curedKg)
    {
        var w = Begun("scrape_hide", pack =>
        {
            pack.Gear.Add(Items.Make("flint_knife"));
            Items.AddPieces(pack, Resource.Hide, [hideKg]);
        });
        Assert.Equal(Making.MadeIt, Complete(w, "scrape_hide", "succeed:injury:LeftArm:0.03=0.02", out _));
        var pack = Items.Pack(w, P);
        Assert.Equal(scrapedKg, Assert.Single(pack.Pieces[Resource.ScrapedHide]), 9);

        Burning(w);
        Assert.Null(Sim.Step(w, new Make(P, "dry_hide"), 1).InterruptedReason);
        Assert.Equal(Making.MadeIt, Making.Complete(w, w.Agent(P), Recipes.Get("dry_hide"), null, []));
        Assert.Equal(curedKg, Assert.Single(pack.Pieces[Resource.CuredHide]), 9);
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

    /// A mammoth hide is scraped and dried like any hide; nothing is sewn from it raw.
    [Fact]
    public void AMammothHide_IsCuredBeforeItIsSewn()
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
        Burning(w);
        Assert.Null(Sim.Step(w, new Make(P, "dry_mammoth_hide"), 1).InterruptedReason);
        Assert.Equal(Making.MadeIt, Making.Complete(w, w.Agent(P), Recipes.Get("dry_mammoth_hide"), null, []));
        Assert.Equal(7.2, Assert.Single(pack.Pieces[Resource.CuredMammothHide]), 9);
    }

    // ---- what the player sees ----

    [Fact]
    public void Verbs_ListEveryRecipe_WithItsOddsAndWhyItIsRefused()
    {
        var w = Noon(out var pack);
        TestKit.Give(pack, Resource.Stone, 0.3);
        TestKit.Give(pack, Resource.Kindling, 1.0);
        var make = Verbs.At(w, P).Make.Where(v => v.Activity is Make).ToList(); // then abandoning
        Assert.Equal(Recipes.All.Select(r => r.Id), make.Select(v => ((Make)v.Activity).Recipe));
        var rock = make.Single(v => v.Activity == new Make(P, "sharp_rock"));
        Assert.Equal("Make sharp rock", rock.Label);
        Assert.Null(rock.Refused);
        Assert.Equal(0.75, rock.Outcomes!.PlainPct, 12);
        Assert.Equal("you need something for cutting", make.Single(v => v.Activity == new Make(P, "hand_drill")).Refused?.Reason);

        Sim.Step(w, new Make(P, "sharp_rock"), 2);
        make = [.. Verbs.At(w, P).Make];
        Assert.Equal("Go on making sharp rock", make.Single(v => v.Activity == new Make(P, "sharp_rock")).Label);
        var abandon = Assert.Single(make, v => v.Activity is Abandon);
        Assert.Equal("Abandon the unfinished sharp rock", abandon.Label);
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
        var producers = Recipes.All.Where(r => r.Result.Resource != null).ToDictionary(r => r.Result.Resource!.Value);

        // Makes the recipe once, fetching raw materials from the land and making the rest first.
        // False if it cannot be started with what the land, the kill and earlier recipes give.
        bool MakeOne(Recipe r, int depth)
        {
            Assert.True(depth < 10, $"{r.Id}: its inputs go round in a circle");
            for (int tries = 0; tries < 100; tries++)
            {
                // What the land gives and this recipe does not use goes back to it: the pack stays light.
                foreach (var res in Items.KgByResource(pack).Keys.Where(x => land.Contains(x) && !r.Materials.Any(m => m.AnyOf.Contains(x))).ToList()) TestKit.Remove(pack, res);
                if (Making.ProjectFor(pack, r.Id) == null)
                    foreach (var m in r.Materials)
                        for (int fetched = 0; !TestKit.Enough(pack, m); fetched++)
                        {
                            Assert.True(fetched < 20, $"{r.Id}: {string.Join("|", m.AnyOf)} never reaches the pack ({Items.Describe(pack)})");
                            if (m.AnyOf.Where(land.Contains).Select(x => (Resource?)x).FirstOrDefault() is Resource raw) TestKit.GiveFor(pack, raw, m);
                            else if (producers.TryGetValue(m.AnyOf[0], out var from)) { if (!MakeOne(from, depth + 1)) return false; }
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
                        && !MakeOne(maker, depth + 1)) return false;
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
                // Keep the pack light: what no recipe uses as a tool or input goes on the camp pile,
                // and what the land gives goes back to it.
                foreach (var g in pack.Gear.Where(g => !Recipes.All.Any(x => x.Items.Contains(g.Kind) || x.Tools.Concat(x.Faster).Any(cap => Items.Def(g).Capabilities.Contains(cap)))).ToList())
                {
                    pack.Gear.Remove(g);
                    Items.PileAt(w, camp).Gear.Add(g);
                }
                foreach (var res in Items.KgByResource(pack).Keys.Where(land.Contains).ToList()) TestKit.Remove(pack, res);
            }
        }
        Assert.Equal(Carried.Select(r => r.Id).Order(), made.Order());
    }

    /// Camp work needs a shovel and a tent no recipe makes yet; ShelterTests builds each.
    static readonly IReadOnlyList<Recipe> Carried = [.. Recipes.All.Where(r => !r.Result.Placed)];

    static void KeepTheFireUp(World w, Inventory pack)
    {
        var pit = w.Fires[Fire.PitAt(w, Map.Of(w).Camp)!.Value];
        if (Fire.IsActive(pit) && pit.UnburnedKg.Values.Sum() + pit.BurningKg.Values.Sum() > 4) return;
        TestKit.Give(pack, Resource.Birch, 4);
        Assert.Null(Sim.Step(w, new AddFuel(P, Resource.Birch, 4), 1).InterruptedReason);
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
        var w = Begun("crude_spear", pack =>
        {
            pack.Gear.Add(Items.Make("sharp_rock", durability: 1));
            pack.Gear.Add(Items.Make("flint_knife"));
            TestKit.Give(pack, Resource.Pine, 1.5);
        });
        Complete(w, "crude_spear", "fail:break_tool:Cutting=0.02", out _);
        var pack = Items.Pack(w, P);
        Assert.False(Items.Works(pack.Gear.Single(g => g.Kind == "sharp_rock")));
        Assert.True(Items.Works(pack.Gear.Single(g => g.Kind == "flint_knife")));
    }

    [Fact]
    public void Odds_NeverAddUpToMoreThanACertainty()
    {
        // As risky as the loader allows, with no dexterity and a fogged head: 0.5 x 2 x 1.2 = 1.2.
        var risky = Recipes.Get("hammerstone") with { Outcomes = [.. Outcomes.Parse("fail=.3"), .. Outcomes.Parse("fail:break_tool:Striking=.2")] };
        var odds = Making.Odds(risky, craftingLevel: 0, dexterityPct: 0, dazed: true);
        Assert.Equal(1.0, odds.Outcomes.Sum(o => o.Odds.TotalPct), 12);
        Assert.Equal(0.3 / 0.5, odds.Outcomes[0].Odds.TotalPct, 12); // each keeps its share
        Assert.All(odds.Outcomes, o => Assert.Contains(o.Odds.Terms, t => t.Factor == "Capped"));
        Assert.Equal(0, odds.PlainPct, 12);
    }

    [Fact]
    public void Odds_ReadTheMakersOwnCraftingLevel()
    {
        var w = Noon(out _);
        var me = w.Agent(P);
        ((PersonMind)me.Mind).Skills[Skill.Crafting] = new SkillProgress { Level = 10 };
        var odds = Making.Odds(w, me, Recipes.Get("hammerstone"));
        Assert.Equal(0.05 * 0.6, Assert.Single(odds.Outcomes).Odds.TotalPct, 9);
    }
}
