namespace Survival.Tests;

/// Verbs.At: the one answer to "what can I do here" (emergent-world-design §3.5), read by the CLI,
/// the UI host and these tests alike.
public class VerbsTests
{
    static readonly AgentId P = AgentId.Player;

    static World Camp(out TileId camp)
    {
        var w = TestWorlds.Bare(17);
        camp = Map.Of(w).Camp;
        return w;
    }

    [Fact]
    public void AtCamp_TheTileVerbs_AreWaitRestSleep_AndAWalkEachWay()
    {
        var w = Camp(out _);
        var here = Verbs.At(w, P);
        Assert.Equal(new Activity[] { new Wait(P), new Rest(P), new Sleep(P) },
            here.Tile.Where(v => v.Activity is Wait or Rest or Sleep).Select(v => v.Activity));
        Assert.Equal(new[] { Direction.North, Direction.East, Direction.South, Direction.West },
            here.Walk.Where(v => ((Travel)v.Activity).Pace == Pace.Quick).Select(v => ((Travel)v.Activity).Dir));
    }

    /// Every verb listed, run on a copy of the world, is refused exactly when it says it is, with
    /// the same words; and listing them changes nothing. A walk that can start runs its whole trip,
    /// to check the minutes it lists; every other verb runs one minute. That is all the check reads:
    /// a refusal comes before any minute passes, and Step does not read the minutes it is given
    /// until a job has lived its first.
    static void AssertEveryVerbIsTrue(World w, AgentId who)
    {
        string before = Save.ToJson(w);
        var verbs = All(Verbs.At(w, who)).ToList();
        Assert.Equal(before, Save.ToJson(w));
        Assert.NotEmpty(verbs);
        foreach (var v in verbs)
        {
            var copy = TestWorlds.Load(before);
            copy.AccessGates = w.AccessGates; // never saved
            copy.GeneratedMap = Map.Of(w); // never saved: the seed's own terrain, which a load would generate again for every verb
            copy.GroundCache = w.GroundCache == null ? null : new(w.GroundCache); // never saved: a scene's ground (TestKit.EditGround)
            int minutes = v.Activity is Travel && v.Allowed ? v.Minutes : 1; // a walk to its end; anything else its first minute
            var result = Sim.Step(copy, v.Activity, minutes);
            if (v.Refused?.Reason != null) Assert.True(result.MinutesElapsed == 0 && result.InterruptedReason == v.Refused?.Reason,
                $"{v.Label}: listed refused '{v.Refused}', Step said '{result.InterruptedReason}' after {result.MinutesElapsed} min");
            else Assert.True(result.MinutesElapsed > 0, $"{v.Label}: listed allowed, Step refused '{result.InterruptedReason}'");
            // A walk that can start says how long it takes: exactly the crossing Step makes.
            if (v.Activity is Travel && v.Allowed)
                Assert.True(result.InterruptedReason is not ("arrived" or "crossed the pass") || result.MinutesElapsed == v.Trip!.TotalMinutes,
                    $"{v.Label}: listed {v.Trip!.TotalMinutes} min, took {result.MinutesElapsed}");
        }
    }

    static IEnumerable<Verb> All(Here here) => here.All;

    [Fact]
    public void EveryVerb_AtTheStart_IsRefusedExactlyWhenStepRefusesIt()
    {
        var w = Camp(out _);
        Sim.Step(w, new Wait(P), 1);
        AssertEveryVerbIsTrue(w, P);
    }

    /// Against the map's edge and the mountain wall a walk is listed, refused with the map's reason.
    [Fact]
    public void AWalkIntoTheMountains_IsListed_RefusedWithTheMapsReason()
    {
        var w = Camp(out _);
        var m = Map.Of(w);
        var wall = Enumerable.Range(1, m.Height - 1).SelectMany(y => Enumerable.Range(0, m.Width).Select(x => new TileId(x, y)))
            .First(t => m.TerrainAt(t).IsPassable() && !m.TerrainAt(Map.Step(t, Direction.North)).IsPassable());
        w.Agent(P).Tile = wall;
        var north = Verbs.At(w, P).Walk.Single(v => v.Activity == new Travel(P, Direction.North));
        Assert.Equal(Map.Blocked(m, wall, Map.Step(wall, Direction.North)), north.Refused?.Reason);
        Assert.NotNull(north.Refused);
        AssertEveryVerbIsTrue(w, P);
    }

    /// Pace is a choice only where a quick crossing is risky enough to weigh, so only such a crossing
    /// is offered carefully too.
    [Fact]
    public void ACarefulWalk_IsOffered_OnlyWhereAQuickOneCarriesMeaningfulRisk()
    {
        var w = Camp(out _);
        var m = Map.Of(w);
        bool Matters(TileId t, Direction d) { w.Agent(P).Tile = t; return Trip.PaceMatters(w, P, d); }
        var start = Enumerable.Range(1, m.Height - 2).SelectMany(y => Enumerable.Range(1, m.Width - 2).Select(x => new TileId(x, y)))
            .First(t => m.TerrainAt(t).IsPassable() && Directions.All(d => m.TerrainAt(Map.Step(t, d)).IsPassable())
                && Directions.Any(d => Matters(t, d)) && Directions.Any(d => !Matters(t, d)));
        w.Agent(P).Tile = start;
        var careful = Verbs.At(w, P).Walk.Where(v => ((Travel)v.Activity).Pace == Pace.Careful).Select(v => ((Travel)v.Activity).Dir);
        var sky = Weather.At(w, w.Minute);
        Assert.Equal(Directions.Where(d => Traversal.InjuryRiskPct(w, Map.Step(start, d), w.Agent(P).Body, sky) > Content.Tables.Footing.PaceMattersAbovePct), careful);
        AssertEveryVerbIsTrue(w, P);
    }

    static readonly Direction[] Directions = [Direction.North, Direction.East, Direction.South, Direction.West];

    /// The player kills a caribou on this tile with a spear in hand: the fight's own aftermath leaves the carcass.
    static Carcass PlayerKill(World w, TileId tile)
    {
        var caribou = Agents.Spawn(w, Species.Caribou, tile);
        var spear = Items.Make("wooden_spear");
        Items.Pack(w, P).Gear.Add(spear);
        var enc = Combat.Create(w, [new Fighter(P) { Weapon = spear }], [new Fighter(caribou)], tile, 2, Awareness.Engaged, Awareness.Unaware);
        w.Agent(caribou).Body.BloodPct = 0;
        Combat.Finish(w, enc, []);
        return w.Tiles[tile].Carcasses!.MaxBy(c => c.Id)!;
    }

    /// A pile of one thing shows taking it as its action; a pile of two is Interact.
    [Fact]
    public void APile_OffersTakingEachThingInIt()
    {
        var w = Camp(out _);
        TestKit.Supply(w);
        Sim.Step(w, new PutDown(P, Resource.Pine, 2), 1);
        Assert.Equal(new PickUp(P, Resource.Pine, 2), Listed(w, LocationFeatureKind.Pile).Action!.Activity);
        Sim.Step(w, new PutDown(P, Resource.Birch, 3), 1);
        var pile = Listed(w, LocationFeatureKind.Pile);
        Assert.True(pile.Interact);
        Assert.Equal(new Activity[] { new PickUp(P, Resource.Pine, 2), new PickUp(P, Resource.Birch, 3) }, pile.Menu.Select(v => v.Activity));
        AssertEveryVerbIsTrue(w, P);
    }

    /// With no pit, lighting one is a tile verb, the best starter and tinder in the pack, with its odds.
    /// A pit is a thing with several verbs: fuel for each fuel carried, melting snow, and lighting it
    /// again once it is out.
    [Fact]
    public void TheFire_IsATileVerbToLight_ThenAPitWithAMenu()
    {
        var w = Camp(out var camp);
        TestKit.Supply(w);
        Sim.Step(w, new Wait(P), 1);
        var light = Verbs.At(w, P).Tile.Single(v => v.Activity is StartFire);
        Assert.Equal(new StartFire(P, ToolType.FireStriker, Resource.BirchBark, 0.05, 1.0), light.Activity);
        var odds = Fire.StartOdds(w, w.Agent(P), ToolType.FireStriker, Resource.BirchBark);
        Assert.Equal(odds.TotalPct, light.Odds!.TotalPct);
        Assert.Equal(odds.Terms, light.Odds.Terms);
        AssertEveryVerbIsTrue(w, P);

        for (int i = 0; i < 20 && !(Fire.PitAt(w, camp) is FireId f && Fire.IsActive(w.Fires[f])); i++) Sim.Step(w, light.Activity, 1);
        var here = Verbs.At(w, P);
        Assert.DoesNotContain(here.Tile, v => v.Activity is StartFire); // the pit's own verb now
        var pit = here.LocationFeatures.Single(t => t.LocationFeature.Kind == LocationFeatureKind.Fire);
        Assert.True(pit.Interact);
        var fuels = Items.Pack(w, P).Kg.Keys.Where(Fire.IsFuel).ToList();
        Assert.Equal(fuels, pit.Menu.Select(v => v.Activity).OfType<AddFuel>().Select(a => a.Fuel));
        Assert.Contains(pit.Menu, v => v.Activity is MeltSnow);
        Assert.Contains(here.Tile, v => v.Activity is LightTorch && v.Allowed);
        AssertEveryVerbIsTrue(w, P);
    }

    /// A thing behind a blocking condition shows it. With no clearing verb yet (step 4.4), the action
    /// shown is the thing's own, refused with the condition's reason.
    [Fact]
    public void ABuriedFind_ShowsItsBlockingCondition_AndItsActionRefusedByIt()
    {
        var w = Camp(out var camp);
        var m = Map.Of(w);
        var (tile, pool) = Enumerable.Range(1, 60).SelectMany(r => new[] { new TileId(camp.X + r, camp.Y), new TileId(camp.X - r, camp.Y), new TileId(camp.X, camp.Y + r) })
            .Where(t => m.InBounds(t) && m.TerrainAt(t).IsPassable())
            .Select(t => (t, Pools.At(w, t).ToList().FindIndex(p => p is StockPool { RevealAtHours: > 0 }))).First(x => x.Item2 >= 0);
        ((PersonMind)w.Agent(P).Mind).Memory.Discovered.Add(new PoolId(tile, pool));
        w.Agent(P).Tile = tile;
        Sim.Step(w, new Wait(P), 1);
        TestKit.EditGround(w, tile, (g, _) => g.Layers.Add(new SurfaceLayer { Material = SurfaceMaterial.Snow, ThicknessM = 3, CompactionPct = 0.3, FrozenFractionPct = 1 }));
        var unblocked = Verbs.At(w, P).LocationFeatures.Single(t => t.LocationFeature == new LocationFeature(LocationFeatureKind.Pool, tile, pool));
        Assert.Empty(unblocked.Conditions); // the gate is not wired: buried bars nothing yet

        w.AccessGates = Access.Gate.All;
        var find = Verbs.At(w, P).LocationFeatures.Single(t => t.LocationFeature == new LocationFeature(LocationFeatureKind.Pool, tile, pool));
        Assert.Equal([new Condition("buried", Blocking: true)], find.Conditions);
        Assert.Equal(Access.BuriedReason, find.Action!.Refused?.Reason);
        AssertEveryVerbIsTrue(w, P);
    }

    /// The same rules for anyone: an NPC's list is as true as the player's.
    [Fact]
    public void AnNpcsVerbs_AreAsTrueAsThePlayers()
    {
        var w = Camp(out var camp);
        var npc = Npc.Spawn(w, camp);
        TestKit.Supply(w, npc);
        Sim.Step(w, new Wait(P), 1);
        Assert.DoesNotContain(Verbs.At(w, npc).LocationFeatures, t => t.LocationFeature == LocationFeatures.OfAgent(w, npc));
        Assert.Contains(Verbs.At(w, npc).LocationFeatures, t => t.LocationFeature == LocationFeatures.OfAgent(w, P));
        AssertEveryVerbIsTrue(w, npc);
    }

    /// A herd on the tile is something to hunt: its one verb is the stalk, and taking it opens the fight
    /// with the best weapon in the pack. A person on the tile offers nothing.
    [Fact]
    public void AHerdHere_OffersAHunt_ThatOpensTheStalk()
    {
        var w = Camp(out var camp);
        Sim.Step(w, new Wait(P), 1);
        var npc = Npc.Spawn(w, camp);
        var herd = Agents.SpawnGroup(w, Species.Caribou, camp, 3)[0];
        Items.Pack(w, P).Gear.Add(Items.Make("wooden_spear"));
        var here = Verbs.At(w, P);
        // Three caribou on the tile are one herd, listed under its first.
        Assert.Single(here.LocationFeatures, t => t.LocationFeature.Kind == LocationFeatureKind.Agent && w.Agent(new AgentId(t.LocationFeature.Index)).Mind is HerdMind);
        var caribou = here.LocationFeatures.Single(t => t.LocationFeature == LocationFeatures.OfAgent(w, herd));
        Assert.Equal(new Hunt(P, herd), caribou.Action!.Activity);
        Assert.True(caribou.Action.Allowed);
        Assert.Empty(here.LocationFeatures.Single(t => t.LocationFeature == LocationFeatures.OfAgent(w, npc)).Menu);
        AssertEveryVerbIsTrue(w, P);

        Sim.Step(w, caribou.Action.Activity, caribou.Action.Minutes);
        Assert.Equal("wooden_spear", Combat.UnitOf(Combat.EncounterOf(w, P)!, P).Weapon!.Kind);
    }

    /// In a fight nothing but the fight's own actions can start (they are the combat screen's, Combat.Actions).
    [Fact]
    public void InAFight_EveryVerbIsRefused_AsStepWouldRefuseIt()
    {
        var w = Camp(out var camp);
        Sim.Step(w, new Wait(P), 1);
        var wolf = Agents.Spawn(w, Species.Wolf, camp);
        Combat.Create(w, [new Fighter(P)], [new Fighter(wolf)], camp, 10, Awareness.Engaged, Awareness.Engaged);
        Assert.All(Verbs.At(w, P).All, v => Assert.Equal(Sim.InAnEncounter, v.Refused?.Reason));
        AssertEveryVerbIsTrue(w, P);
    }

    static LocationFeatureVerbs Listed(World w, LocationFeatureKind kind) => Verbs.At(w, P).LocationFeatures.Single(t => t.LocationFeature.Kind == kind);

    /// A carcass offers butchering three ways: several verbs, so Interact; once begun one way, only that way.
    [Fact]
    public void ACarcass_OffersEachWayToButcherIt_UntilOneIsBegun()
    {
        var w = Camp(out var camp);
        Items.Pack(w, P).Gear.Add(Items.Make("flint_knife"));
        var c = PlayerKill(w, camp);
        Sim.Step(w, new Wait(P), 1);
        var carcass = Listed(w, LocationFeatureKind.Carcass);
        Assert.True(carcass.Interact);
        Assert.Equal(Enum.GetValues<ButcherMode>().Select(mode => (Activity)new Butcher(P, c.Id, mode)), carcass.Menu.Select(v => v.Activity));
        AssertEveryVerbIsTrue(w, P);

        Sim.Step(w, new Butcher(P, c.Id, ButcherMode.Careful), 5);
        var begun = Listed(w, LocationFeatureKind.Carcass);
        Assert.Equal(new Butcher(P, c.Id, ButcherMode.Careful), begun.Action!.Activity);
        AssertEveryVerbIsTrue(w, P);
    }

    // ---- things here ----

    /// What the person has not found is not there for them; what they know shows its one action.
    [Fact]
    public void TheTilesPools_ShowOnlyTheKnownOnes_AndForageOffersItsFoci_AndKeepWalking()
    {
        var w = Camp(out var start);
        var m = Map.Of(w);
        // The nearest tile with forage and something still hidden.
        var camp = Enumerable.Range(1, 60).SelectMany(r => new[] { new TileId(start.X + r, start.Y), new TileId(start.X - r, start.Y), new TileId(start.X, start.Y + r) })
            .First(t => m.InBounds(t) && m.TerrainAt(t).IsPassable() && Pools.At(w, t) is var ps && ps.Count > 0 && ps[0] is ForagePool
                && ps.Select((_, i) => new PoolId(t, i)).Any(id => !Pools.Knows(w, P, id)));
        w.Agent(P).Tile = camp;
        Sim.Step(w, new Wait(P), 1);
        var pools = Pools.At(w, camp);
        Assert.Contains(pools.Select((_, i) => new PoolId(camp, i)), id => !Pools.Knows(w, P, id)); // something here is still hidden
        var listed = Verbs.At(w, P).LocationFeatures.Where(t => t.LocationFeature.Kind == LocationFeatureKind.Pool).ToList();
        Assert.Equal(pools.Select((_, i) => i).Where(i => Pools.Knows(w, P, new PoolId(camp, i))), listed.Select(t => t.LocationFeature.Index));
        var forage = listed.Single(t => pools[t.LocationFeature.Index] is ForagePool);
        var def = (ForagePool)pools[forage.LocationFeature.Index];
        Assert.True(forage.Interact);
        var general = forage.Menu.Single(v => v.Activity == new Gather(P, forage.LocationFeature.Index));
        Assert.Equal("Forage", general.Label); // not foraged here yet: no word for the ground
        Assert.Equal(Verbs.ForageDurations, general.Durations);
        Assert.Contains(general.Minutes, general.Durations!);
        foreach (var focus in new[] { ForageFocus.Fuel, ForageFocus.Food, ForageFocus.Medicine, ForageFocus.Materials })
            Assert.Equal(def.Items.Any(i => Work.InFocus(i.Resource, focus)), forage.Menu.Any(v => v.Activity is Gather g && g.Focus == focus));
        Assert.Contains(forage.Menu, v => v.Activity is WalkAbout && v.Minutes == Work.WalkAboutMinutes);
        AssertEveryVerbIsTrue(w, P);
    }

    // ---- what you carry, and building ----

    /// The pack's verbs: eat each food, put down each thing, wear each garment carried, take off each
    /// one worn. All of them as true as the rest.
    [Fact]
    public void ThePack_OffersEating_PuttingDown_Wearing_AndTakingOff()
    {
        var w = Camp(out _);
        TestKit.Supply(w);
        var pack = Items.Pack(w, P);
        Items.Add(pack, Resource.Berries, 2);
        pack.Gear.Add(Items.Make("fur_chest_wrap"));
        Sim.Step(w, new Wait(P), 1);
        var carried = Verbs.At(w, P).Pack;
        var eat = Assert.Single(carried, v => v.Activity is Eat);
        Assert.Equal(Resource.Berries, ((Eat)eat.Activity).Food);
        Assert.Equal("Eat berries", eat.Label);
        Assert.Equal(pack.Kg.Keys.Where(r => pack.Kg[r] > 0), carried.Select(v => v.Activity).OfType<PutDown>().Select(s => s.Resource));
        Assert.All(carried.Select(v => v.Activity).OfType<PutDown>(), s => Assert.Equal(Items.Have(pack, s.Resource), s.Kg));
        Assert.Equal(new Wear(P, "fur_chest_wrap"), Assert.Single(carried, v => v.Activity is Wear).Activity);
        Assert.Equal(w.Agent(P).Body.Worn.Select(g => g.Kind), carried.Select(v => v.Activity).OfType<TakeOff>().Select(t => t.Gear));
        AssertEveryVerbIsTrue(w, P);
    }

    /// Taking something off puts it in the pack, and wearing it again puts it back on.
    [Fact]
    public void TakingOff_PutsItInThePack()
    {
        var w = Camp(out _);
        var boots = w.Agent(P).Body.Worn.Single(g => g.Kind == "worn_hide_boots");
        var r = Sim.Step(w, new TakeOff(P, "worn_hide_boots"), 1);
        Assert.Equal(StepOutcome.Finished, r.Outcome);
        Assert.DoesNotContain(boots, w.Agent(P).Body.Worn);
        Assert.Contains(boots, Items.Pack(w, P).Gear);
        Assert.Equal("you are not wearing worn hide boots", Sim.Refusal(w, new TakeOff(P, "worn_hide_boots"))?.Reason);
        Sim.Step(w, new Wear(P, "worn_hide_boots"), 1);
        Assert.Contains(boots, w.Agent(P).Body.Worn);
    }

    /// Every camp recipe is offered to build, refused with what it lacks (no hidden recipes), as the rest are.
    [Fact]
    public void EveryStructure_IsOfferedToBuild()
    {
        var w = Camp(out _);
        Sim.Step(w, new Wait(P), 1);
        var builds = Verbs.At(w, P).Make.Where(v => v.Activity is Make m && Recipes.Get(m.Recipe).Result.Placed).ToList();
        Assert.Equal(Recipes.All.Where(r => r.Result.Placed).Select(r => r.Id), builds.Select(v => ((Make)v.Activity).Recipe));
        Assert.Equal("Build a branch frame shelter", builds.Single(v => v.Activity == new Make(P, "branch_frame")).Label);
        Assert.Equal("there is already a shelter here (Overhang)", builds.Single(v => v.Activity == new Make(P, "tent")).Refused?.Reason); // the camp's overhang
        AssertEveryVerbIsTrue(w, P);
    }

    /// A tent in the pack can be pitched from the list, and the pitched tent packed again.
    [Fact]
    public void ATentInThePack_IsPitchedFromTheList()
    {
        var w = Camp(out var camp);
        var m = Map.Of(w);
        var open = Enumerable.Range(1, 30).Select(r => new TileId(camp.X + r, camp.Y)).First(t => m.TerrainAt(t).IsPassable() && Shelters.Peek(w, t) == null);
        w.Agent(P).Tile = open;
        Items.Pack(w, P).Gear.Add(Items.Make("hide_tent"));
        Sim.Step(w, new Wait(P), 1);
        var pitch = Verbs.At(w, P).Make.Single(v => v.Activity == new Make(P, "tent"));
        Assert.Equal("Pitch the tent", pitch.Label);
        Assert.True(pitch.Allowed, pitch.Refused?.Reason);
        AssertEveryVerbIsTrue(w, P);
    }

    // ---- crossing risk ----

    /// A walk onto hazardous ground shows the injury odds the crossing will roll, once, on the tile
    /// walked onto: a careful one a quarter of a quick one's.
    [Fact]
    public void AQuickWalkOverHazardousGround_ShowsTheRiskItRolls()
    {
        var w = Camp(out var camp);
        var m = Map.Of(w);
        var start = Enumerable.Range(1, m.Height - 2).SelectMany(y => Enumerable.Range(1, m.Width - 2).Select(x => new TileId(x, y)))
            .First(t => m.TerrainAt(t).IsPassable() && Directions.Any(d => m.TerrainAt(Map.Step(t, d)).IsPassable() && Traversal.IsHazardous(w, Map.Step(t, d))));
        w.Agent(P).Tile = start;
        Sim.Step(w, new Wait(P), 1);
        var dir = Directions.First(d => m.TerrainAt(Map.Step(start, d)).IsPassable() && Traversal.IsHazardous(w, Map.Step(start, d)));
        var walks = Verbs.At(w, P).Walk;
        var quick = walks.Single(v => v.Activity == new Travel(P, dir));
        var careful = walks.Single(v => v.Activity == new Travel(P, dir, Pace.Careful));
        Assert.True(quick.Risk!.TotalPct > 0);
        Assert.Equal(quick.Risk.TotalPct * 0.25, careful.Risk!.TotalPct, 12);

        Sim.Step(w, quick.Activity, 1);
        Assert.Equal(quick.Risk.TotalPct, w.Agent(P).Crossing!.RiskPct);
    }

    /// A thing on a tile is called the same whoever looks: NameOf, which needs no one standing there,
    /// gives every name Verbs.At lists for the person who is.
    [Fact]
    public void NameOf_GivesTheNamesVerbsAtLists()
    {
        var w = Camp(out var camp);
        Agents.SpawnGroup(w, Species.Wolf, camp, 2);
        Npc.Spawn(w, camp);
        var pit = new FireState { Tile = camp, LastUpdatedMinute = w.Minute, Pit = PitType.Open };
        Fire.AddFuel(pit, Resource.Kindling, 1);
        Fire.IgniteAll(pit);
        w.Fires[new FireId(w.NextFireId++)] = pit;

        var listed = Verbs.At(w, P).LocationFeatures;
        Assert.Contains(listed, t => t.LocationFeature.Kind == LocationFeatureKind.Fire);
        Assert.Contains(listed, t => t.LocationFeature.Kind == LocationFeatureKind.Shelter);
        Assert.True(listed.Count(t => t.LocationFeature.Kind == LocationFeatureKind.Agent) == 2, "the wolves and the NPC");
        foreach (var t in listed) Assert.Equal(t.Name, Verbs.NameOf(w, t.LocationFeature));
    }
}
