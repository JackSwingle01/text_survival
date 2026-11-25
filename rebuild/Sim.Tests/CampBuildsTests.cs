namespace Survival.Tests;

/// The camp builds of #151 (spec 2026-11-24 recipes-that-make-sense, the recipe table): each made through Make
/// with what it lists, and refused without its requirement.
public class CampBuildsTests
{
    static readonly AgentId P = AgentId.Player;
    static readonly WeatherSample Mild = new(0, WeatherCondition.Cloudy, 5, 0.1, 0, 0.8, WindDirection.North);

    /// Noon on an ordinary open tile, an empty pack.
    static World Builder(out TileId tile, out Inventory pack)
    {
        var w = TestWorlds.Bare(17);
        w.FixedWeather = Mild;
        var m = Map.Of(w);
        tile = (from x in Enumerable.Range(0, m.Width)
                from y in Enumerable.Range(0, m.Height)
                let t = new TileId(x, y)
                where m.TerrainAt(t) == Terrain.Plain && t != m.Camp && m.CaveAt(t) == Cave.None && m.StageAt(t) == null && !m.BesideRiver(t)
                select t).First();
        w.Agent(P).Tile = tile;
        pack = Items.Pack(w, P);
        TestKit.ClearResources(pack);
        return w;
    }

    static int Branches(Inventory pack) => Items.Count(pack, Resource.PineBranch) + Items.Count(pack, Resource.BirchBranch);

    [Fact]
    public void LeanTo_TakesSixBranches_AndCoversUpToHalf()
    {
        var w = Builder(out var tile, out var pack);
        TestKit.GivePieces(pack, Resource.PineBranch, 5);
        Assert.StartsWith("you need 6 pine branches", Sim.Step(w, new Make(P, "lean_to"), 60).InterruptedReason);
        TestKit.GivePieces(pack, Resource.PineBranch, 1);
        var r = Sim.Step(w, new Make(P, "lean_to"), 60);
        Assert.Equal(("built", 30), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Equal(0, Branches(pack));
        var s = Shelters.At(w, tile, [])!;
        Assert.Equal((ShelterKind.LeanTo, 0.5, 0.5, 0.5), (s.Kind, s.InsulationCap, s.OverheadCap, s.WindCap));
    }

    [Fact]
    public void BranchHut_TakesTwelveBranchesAndTwoRope_AndCoversUpToFourFifths()
    {
        var w = Builder(out var tile, out var pack);
        TestKit.GivePieces(pack, Resource.BirchBranch, 12);
        TestKit.GivePieces(pack, Resource.Rope, 1);
        Assert.StartsWith("you need 2 ", Sim.Step(w, new Make(P, "branch_hut"), 60).InterruptedReason);
        TestKit.GivePieces(pack, Resource.Rope, 1);
        var r = Sim.Step(w, new Make(P, "branch_hut"), 60);
        Assert.Equal(("built", 45), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Equal((0, 0), (Branches(pack), Items.Count(pack, Resource.Rope)));
        var s = Shelters.At(w, tile, [])!;
        Assert.Equal((ShelterKind.BranchHut, 0.8, 0.8, 0.8), (s.Kind, s.InsulationCap, s.OverheadCap, s.WindCap));
    }

    [Fact]
    public void BothShelters_NeedLight()
    {
        var w = Builder(out _, out var pack);
        w.Minute = 15 * 60; // midnight (the clock starts at nine), no fire
        TestKit.GivePieces(pack, Resource.PineBranch, 12);
        TestKit.GivePieces(pack, Resource.Rope, 2);
        Assert.Equal(Work.Dark.Reason, Sim.Refusal(w, new Make(P, "lean_to"))?.Reason);
        Assert.Equal(Work.Dark.Reason, Sim.Refusal(w, new Make(P, "branch_hut"))?.Reason);
    }

    /// The lean-to's own branches come back whole, and the hut takes its own twelve (#151).
    [Fact]
    public void Hut_OverALeanTo_ReturnsItsSixBranches_AndLeavesOneHut()
    {
        var w = Builder(out var tile, out var pack);
        for (int i = 0; i < 6; i++) Items.AddPiece(pack, Resource.OakBranch, 1.5 + i * 0.1);
        Assert.Equal("built", Sim.Step(w, new Make(P, "lean_to"), 60).InterruptedReason);
        var leanTo = Shelters.At(w, tile, [])!;
        TestKit.GivePieces(pack, Resource.PineBranch, 12);
        TestKit.GivePieces(pack, Resource.Rope, 2);
        var r = Sim.Step(w, new Make(P, "branch_hut"), 60);
        Assert.Equal(("built", 45), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Equal([1.5, 1.6, 1.7, 1.8, 1.9, 2.0], pack.Pieces[Resource.OakBranch].Select(kg => Math.Round(kg, 6)).Order());
        Assert.Equal(0, Items.Count(pack, Resource.PineBranch));
        var hut = Shelters.At(w, tile, [])!;
        Assert.Equal(ShelterKind.BranchHut, hut.Kind);
        Assert.NotEqual(leanTo.Id, hut.Id);
        Assert.Single(w.Shelters, kv => kv.Key == tile);
        Assert.Contains(r.Events, e => e is ShelterTakenDown { Name: "Lean-to" });
        // A hut stands: neither shelter goes up over it.
        Assert.Contains("already a shelter", Sim.Step(w, new Make(P, "lean_to"), 10).InterruptedReason);
        Assert.Contains("already a shelter", Sim.Step(w, new Make(P, "branch_hut"), 10).InterruptedReason);
    }

    /// While the hut goes up the lean-to still stands; a save in the middle changes nothing.
    [Fact]
    public void Hut_OverALeanTo_HalfBuilt_KeepsTheLeanTo_ThroughASave()
    {
        var w = Builder(out var tile, out var pack);
        TestKit.GivePieces(pack, Resource.PineBranch, 18);
        TestKit.GivePieces(pack, Resource.Rope, 2);
        Sim.Step(w, new Make(P, "lean_to"), 60);
        Assert.Null(Sim.Step(w, new Make(P, "branch_hut"), 20).InterruptedReason);
        Assert.Equal(ShelterKind.LeanTo, Shelters.At(w, tile, [])!.Kind);
        w = TestWorlds.Load(Save.ToJson(w));
        var r = Sim.Step(w, new Make(P, "branch_hut"), 60);
        Assert.Equal(("built", 25), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Equal(ShelterKind.BranchHut, Shelters.At(w, tile, [])!.Kind);
        // The six come back to the pack, or what it has no room for to the pile.
        Assert.Equal(6, Items.Count(Items.Pack(w, P), Resource.PineBranch) + Items.Count(Items.PileAt(w, tile), Resource.PineBranch));
    }

    /// A collapsed lean-to found in the world was never built from anyone's branches: no hut replaces it.
    [Fact]
    public void Hut_OverAFoundShelter_IsRefused()
    {
        var w = Builder(out var tile, out var pack);
        Shelters.Place(w, tile, new ShelterDef("Collapsed Lean-to", ShelterKind.LeanTo));
        TestKit.GivePieces(pack, Resource.PineBranch, 12);
        TestKit.GivePieces(pack, Resource.Rope, 2);
        Assert.Contains("already a shelter", Sim.Step(w, new Make(P, "branch_hut"), 10).InterruptedReason);
    }

    // ---- fire pits ----

    static FireState? PitHere(World w, TileId tile) => Fire.PitAt(w, tile) is FireId id ? w.Fires[id] : null;

    [Fact]
    public void MoundPit_NeedsDigging_TakesThreeHours_AndDigsAColdPit()
    {
        var w = Builder(out var tile, out var pack);
        Assert.Equal("you need something for digging", Sim.Step(w, new Make(P, "mound_fire_pit"), 60).InterruptedReason);
        pack.Gear.Add(Items.Make("bone_shovel"));
        var r = Sim.Step(w, new Make(P, "mound_fire_pit"), 240);
        Assert.Equal(("built", 180), (r.InterruptedReason, r.MinutesElapsed));
        var pit = PitHere(w, tile)!;
        Assert.Equal(PitType.Mound, pit.Pit);
        Assert.False(Fire.IsActive(pit));
        Assert.Equal(new Cause(P, "dug by the player"), pit.Cause);
        Assert.Contains("already as good", Sim.Step(w, new Make(P, "mound_fire_pit"), 10).InterruptedReason);
    }

    /// The pit someone lit a fire in becomes a mound, once its fire is out.
    [Fact]
    public void MoundPit_RaisesTheOpenPitHere_OnlyWithItsFireOut()
    {
        var w = Builder(out var tile, out var pack);
        pack.Gear.Add(Items.Make("bone_shovel"));
        var id = new FireId(w.NextFireId++);
        w.Fires[id] = new FireState { Tile = tile, LastUpdatedMinute = w.Minute, Pit = PitType.Open, BurningKg = { [Resource.BirchFirewood] = 3 } };
        Assert.Equal("put the fire out first", Sim.Step(w, new Make(P, "mound_fire_pit"), 10).InterruptedReason);
        w.Fires[id].BurningKg.Clear();
        Assert.Equal("built", Sim.Step(w, new Make(P, "mound_fire_pit"), 240).InterruptedReason);
        Assert.Equal((id, PitType.Mound), (Fire.PitAt(w, tile), w.Fires[id].Pit));
        Assert.Single(w.Fires);
    }

    [Fact]
    public void StoneRing_TakesTenStones_OnAMoundPit_FasterWithDigging()
    {
        var w = Builder(out var tile, out var pack);
        TestKit.GivePieces(pack, Resource.Stone, 10);
        Assert.Equal("you need a mound fire pit here", Sim.Step(w, new Make(P, "stone_ring"), 60).InterruptedReason);
        w.Fires[new FireId(w.NextFireId++)] = new FireState { Tile = tile, LastUpdatedMinute = w.Minute, Pit = PitType.Open };
        Assert.Equal("you need a mound fire pit here", Sim.Step(w, new Make(P, "stone_ring"), 60).InterruptedReason);
        PitHere(w, tile)!.Pit = PitType.Mound;
        TestKit.ClearResources(pack);
        TestKit.GivePieces(pack, Resource.Stone, 9);
        Assert.StartsWith("you need 10 stones", Sim.Step(w, new Make(P, "stone_ring"), 60).InterruptedReason);
        TestKit.GivePieces(pack, Resource.Stone, 1);
        var r = Sim.Step(w, new Make(P, "stone_ring"), 240);
        Assert.Equal(("built", 100), (r.InterruptedReason, r.MinutesElapsed));
        Assert.Equal((PitType.Stone, 0), (PitHere(w, tile)!.Pit, Items.Count(pack, Resource.Stone)));
        Assert.Contains("already as good", Sim.Step(w, new Make(P, "stone_ring"), 10).InterruptedReason);
        Assert.Contains("already as good", Sim.Step(w, new Make(P, "mound_fire_pit"), 10).InterruptedReason);

        // With a shovel the ring goes faster.
        var w2 = Builder(out var tile2, out var pack2);
        w2.Fires[new FireId(w2.NextFireId++)] = new FireState { Tile = tile2, LastUpdatedMinute = w2.Minute, Pit = PitType.Mound };
        TestKit.GivePieces(pack2, Resource.Stone, 10);
        pack2.Gear.Add(Items.Make("bone_shovel"));
        Assert.True(Sim.Step(w2, new Make(P, "stone_ring"), 240).MinutesElapsed < 100);
    }

    /// A pit's type is what the fire data says of it: a stone ring holds the most and keeps the most wind off.
    [Fact]
    public void EachPitType_BehavesAsTheFireDataSays()
    {
        foreach (var (type, room, fuelKg, wind) in new[] { (PitType.Open, 2, 12.0, 0.0), (PitType.Mound, 3, 15.0, 0.4), (PitType.Stone, 4, 18.0, 0.7) })
        {
            var w = Builder(out var tile, out var pack);
            pack.Gear.Add(Items.Make("bone_shovel"));
            TestKit.GivePieces(pack, Resource.Stone, 10);
            if (type != PitType.Open) Assert.Equal("built", Sim.Step(w, new Make(P, "mound_fire_pit"), 240).InterruptedReason);
            if (type == PitType.Stone) Assert.Equal("built", Sim.Step(w, new Make(P, "stone_ring"), 240).InterruptedReason);
            if (type == PitType.Open) w.Fires[new FireId(w.NextFireId++)] = new FireState { Tile = tile, LastUpdatedMinute = w.Minute, Pit = PitType.Open };
            var pit = PitHere(w, tile)!;
            Assert.Equal((type, room, fuelKg, wind), (pit.Pit, Fire.Room(pit.Pit), Fire.MaxFuelKg(pit.Pit), Fire.WindProtectionLevel(pit.Pit)));
        }
    }

    /// The struck fire takes the dug pit's type, whoever strikes it (an NPC's strike names no pit).
    [Fact]
    public void AFireStruck_InAStoneRing_BurnsInIt()
    {
        var w = Builder(out var tile, out var pack);
        w.Fires[new FireId(w.NextFireId++)] = new FireState { Tile = tile, LastUpdatedMinute = w.Minute, Pit = PitType.Stone };
        pack.Gear.Add(Items.Make("fire_striker"));
        Items.Add(pack, Resource.BirchBark, 1);
        TestKit.Give(pack, Resource.Stick, 3);
        var strike = new StartFire(P, ToolType.FireStriker, Resource.BirchBark, 0.05, 0.3);
        Assert.Null(Sim.Refusal(w, strike));
        for (int i = 0; i < 20 && !Fire.IsActive(PitHere(w, tile)!); i++) Sim.Step(w, strike, 1);
        Assert.True(Fire.IsActive(PitHere(w, tile)!));
        Assert.Equal((PitType.Stone, 1), (PitHere(w, tile)!.Pit, w.Fires.Count));
    }

    // ---- tents and bedding ----

    static void Sewing(Inventory pack) => pack.Gear.Add(Items.Make("bone_needle"));

    static void Branches4(Inventory pack) => TestKit.GivePieces(pack, Resource.BirchBranch, 4);

    [Fact]
    public void HideTent_IsSewnFromCuredHidesOnBranchPoles_AndPitched()
    {
        var w = Builder(out var tile, out var pack);
        TestKit.GivePieces(pack, Resource.CuredHide, 4);
        TestKit.GivePieces(pack, Resource.Rope, 2);
        TestKit.GivePieces(pack, Resource.Sinew, 4);
        Branches4(pack);
        Assert.Equal("you need something for sewing", Sim.Step(w, new Make(P, "hide_tent"), 60).InterruptedReason);
        Sewing(pack);
        var made = MakeUntilMade(w, "hide_tent");
        Assert.Equal(180, made);
        Assert.Contains(pack.Gear, g => g.Kind == "hide_tent");
        Assert.Equal("built", Sim.Step(w, new Make(P, "tent"), 30).InterruptedReason);
        Assert.Equal(ShelterKind.HideTent, Shelters.At(w, tile, [])!.Kind);
    }

    [Fact]
    public void MammothHideTent_IsSewnFromTwoCuredMammothHides()
    {
        var w = Builder(out _, out var pack);
        pack.Gear.Add(Items.Make("large_bag")); // two mammoth hides and the poles are more than the bare pack holds
        TestKit.GivePieces(pack, Resource.CuredMammothHide, 2);
        TestKit.GivePieces(pack, Resource.Sinew, 4);
        Branches4(pack);
        Assert.Equal("you need something for sewing", Sim.Step(w, new Make(P, "mammoth_hide_tent"), 60).InterruptedReason);
        Sewing(pack);
        Assert.Equal(240, MakeUntilMade(w, "mammoth_hide_tent"));
        Assert.Contains(pack.Gear, g => g.Kind == "mammoth_hide_tent");
    }

    /// Minutes of work to a made piece, the sewing rested between steps; fails if the work stops for anything else.
    static int MakeUntilMade(World w, string recipe)
    {
        int minutes = 0;
        for (int i = 0; i < 20; i++)
        {
            var r = Sim.Step(w, new Make(P, recipe), 60);
            minutes += r.MinutesElapsed;
            if (r.Events.OfType<Made>().Any(e => e.Succeeded)) return minutes;
            Assert.True(r.InterruptedReason is null or Making.Spoiled, $"{recipe}: {r.InterruptedReason}");
            var body = w.Agent(P).Body;
            (body.EnergyMinutes, body.HydrationMl) = (Body.MaxEnergyMinutes, Body.MaxHydrationMl);
        }
        throw new Xunit.Sdk.XunitException($"{recipe} was never made");
    }

    [Fact]
    public void SleepingBag_IsSewnFromFourCuredHides_InTheLight()
    {
        var w = Builder(out var tile, out var pack);
        TestKit.GivePieces(pack, Resource.CuredHide, 4);
        TestKit.GivePieces(pack, Resource.Sinew, 6);
        Assert.Equal("you need something for sewing", Sim.Step(w, new Make(P, "sleeping_bag"), 60).InterruptedReason);
        Sewing(pack);
        w.Minute = 15 * 60;
        Assert.Equal(Work.Dark.Reason, Sim.Refusal(w, new Make(P, "sleeping_bag"))?.Reason);
        w.Minute = 3 * 60;
        int minutes = 0;
        for (int i = 0; i < 10 && Shelters.BeddingAt(w, tile)?.Name != "Sleeping bag"; i++) minutes += Sim.Step(w, new Make(P, "sleeping_bag"), 60).MinutesElapsed;
        Assert.Equal(("Sleeping bag", 195), (Shelters.BeddingAt(w, tile)?.Name, minutes));
        Assert.Equal((0, 0), (Items.Count(pack, Resource.CuredHide), Items.Count(pack, Resource.Sinew)));
    }

    [Theory]
    [InlineData("padded_bedding", "Padded bedding")]
    [InlineData("moss_bedding", "Moss-padded bedding")]
    public void Bedding_TakesACuredHide_NotARawOne(string recipe, string bed)
    {
        var w = Builder(out var tile, out var pack);
        Items.Add(pack, Resource.PlantFiber, 1);
        Items.Add(pack, Resource.SphagnumMoss, 1.2);
        Items.Add(pack, Resource.PineNeedles, 0.24);
        TestKit.GivePieces(pack, Resource.Hide, 1);
        Assert.StartsWith("you need 1 cured hide", Sim.Step(w, new Make(P, recipe), 60).InterruptedReason);
        TestKit.GivePieces(pack, Resource.CuredHide, 1);
        w.Minute = 15 * 60;
        Assert.Equal(Work.Dark.Reason, Sim.Refusal(w, new Make(P, recipe))?.Reason);
        w.Minute = 3 * 60;
        Assert.Equal("built", Sim.Step(w, new Make(P, recipe), 60).InterruptedReason);
        Assert.Equal(bed, Shelters.BeddingAt(w, tile)?.Name);
        Assert.Equal((1, 0), (Items.Count(pack, Resource.Hide), Items.Count(pack, Resource.CuredHide)));
    }

    // ---- what camp builds take ----

    /// No camp build takes a raw hide, sticks for poles or frames, or logs (there are none: #143).
    [Fact]
    public void NoCampBuild_TakesARawHide_OrSticksForItsFrame()
    {
        var camp = Recipes.All.Where(r => r.Category == MakeCategory.Camp).ToList();
        Assert.All(camp, r => Assert.DoesNotContain(r.Materials.SelectMany(m => m.AnyOf), res => res is Resource.Hide or Resource.MammothHide));
        string[] framed = ["lean_to", "branch_hut", "hide_tent", "mammoth_hide_tent"];
        Assert.All(framed, id => Assert.DoesNotContain(Resource.Stick, Recipes.Get(id).Materials.SelectMany(m => m.AnyOf)));
        Assert.All(framed, id => Assert.Contains(Recipes.Get(id).Materials, m => m.AnyOf.SequenceEqual([Resource.PineBranch, Resource.BirchBranch, Resource.OakBranch])));
        Assert.DoesNotContain(Enum.GetNames<Resource>(), n => n.Contains("Log"));
    }
}
