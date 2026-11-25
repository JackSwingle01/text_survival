using System.Text.RegularExpressions;

namespace Survival.Tests;

/// Build step 2 (emergent-world-design §9): one reference to any thing, Cause on what persists,
/// signs as a view, one access gate, tags and capabilities read off the world.
public class SubstrateTests
{
    static readonly AgentId P = AgentId.Player;

    static World Camp(out TileId camp)
    {
        var w = TestWorlds.Bare(17);
        camp = Map.Of(w).Camp;
        return w;
    }

    /// On to the next clock noon: light to work by. Fires are left to catch up when read.
    static void Midday(World w)
    {
        while (Weather.MinuteOfDay(w.Minute) != 12 * 60) w.Minute++;
    }

    /// A herd of this many on the tile, by its first animal.
    static AgentId Herd(World w, Species s, int animals, TileId tile) => Group(w, s, animals, tile)[0];

    static List<AgentId> Group(World w, Species s, int animals, TileId tile) => Agents.SpawnGroup(w, s, tile, animals);

    /// The player kills a caribou on this tile with a spear in hand: the fight's own aftermath leaves the carcass.
    static Carcass PlayerKill(World w, TileId tile)
    {
        var caribou = Herd(w, Species.Caribou, 1, tile);
        var spear = Items.Make("wooden_spear");
        Items.Pack(w, P).Gear.Add(spear);
        var enc = Combat.Create(w, [new Fighter(P) { Weapon = spear }], [new Fighter(caribou)], tile, 2, Awareness.Engaged, Awareness.Unaware);
        w.Agent(caribou).Body.BloodPct = 0;
        Combat.Finish(w, enc, []);
        return w.Tiles[tile].Carcasses!.MaxBy(c => c.Id)!;
    }

    // ---- LocationFeature and LocationFeatures.At ----

    [Fact]
    public void LocationFeaturesAt_NamesEveryKindOnTheTile_Sorted()
    {
        var w = Camp(out var camp);
        TestKit.Supply(w);
        PlayerKill(w, camp);
        Sim.Step(w, new PutDown(P, Resource.PineBranch, 1), 1);
        Sim.Step(w, new StartFire(P, ToolType.FireStriker, Resource.BirchBark, 0.05, 0.3), 1);
        var things = LocationFeatures.At(w, camp).ToList();
        Assert.Equal(things.OrderBy(t => t).ToList(), things);
        var kinds = things.Select(t => t.Kind).ToHashSet();
        foreach (var k in new[] { LocationFeatureKind.Agent, LocationFeatureKind.Carcass, LocationFeatureKind.Pool, LocationFeatureKind.Pile, LocationFeatureKind.Shelter })
            Assert.Contains(k, kinds);
        // A pit is a thing; a carried torch never is.
        if (Fire.PitAt(w, camp) is FireId pit) Assert.Contains(new LocationFeature(LocationFeatureKind.Fire, camp, pit.Value), things);
        Assert.Contains(new LocationFeature(LocationFeatureKind.Agent, camp, P.Value), things);
        Assert.All(things, t => Assert.True(LocationFeatures.Exists(w, t), t.ToString()));
    }

    [Fact]
    public void LocationFeatures_SurviveSaveAndLoad_AndTheRemovalOfOthers()
    {
        var w = Camp(out var camp);
        var first = PlayerKill(w, camp);
        var second = PlayerKill(w, camp);
        var secondRef = new LocationFeature(LocationFeatureKind.Carcass, camp, second.Id);
        Carcasses.Remove(w, camp, first);
        Assert.True(LocationFeatures.Exists(w, secondRef));
        Assert.Same(second, Carcasses.Find(w, camp, secondRef.Index));
        var loaded = TestWorlds.Load(Save.ToJson(w));
        Assert.Equal(LocationFeatures.At(w, camp), LocationFeatures.At(loaded, camp));
        Assert.Equal(second.Animal, Carcasses.Find(loaded, camp, secondRef.Index)!.Animal);
    }

    [Fact]
    public void ADeadPerson_IsABody_NotAnAgent()
    {
        var w = Camp(out var camp);
        var npc = Npc.Spawn(w, camp);
        Assert.Equal(LocationFeatureKind.Agent, LocationFeatures.OfAgent(w, npc).Kind);
        w.Agent(npc).Body.BloodPct = 0;
        var body = LocationFeatures.OfAgent(w, npc);
        Assert.Equal(new LocationFeature(LocationFeatureKind.Body, camp, npc.Value), body);
        Assert.False(LocationFeatures.Exists(w, body with { Kind = LocationFeatureKind.Agent }));
        Assert.Contains(Signs.At(w, camp), s => s.Kind == SignKind.Body && s.LocationFeature == body);
    }

    [Fact]
    public void Trails_AreTracksIndexedByDirection()
    {
        var w = Camp(out var camp);
        var east = new TileId(camp.X + 1, camp.Y);
        for (int i = 0; i < 10; i++) Tracks.RecordMove(w, camp, east, TrackMaker.Human);
        var trail = LocationFeature.Trail(camp, Direction.East);
        Assert.Equal(Direction.East, trail.TrailDirection);
        Assert.Contains(trail, LocationFeatures.At(w, camp));
        // One edge is one ref (review): named from either end it is the same thing, listed once, on its west end.
        Assert.Equal(trail, LocationFeature.Trail(east, Direction.West));
        Assert.DoesNotContain(LocationFeatures.At(w, east), t => t.TrailDirection != null);
        Assert.True(LocationFeatures.Exists(w, trail));
        Assert.False(LocationFeatures.Exists(w, new LocationFeature(LocationFeatureKind.Track, east, LocationFeature.TrailIndexBase + (int)Direction.West)));
        Assert.Contains(new LocationFeature(LocationFeatureKind.Track, camp, (int)TrackMaker.Human), LocationFeatures.At(w, camp));
    }

    // ---- Cause ----

    [Fact]
    public void Carcasses_KeepWhoKilledThemAndHow()
    {
        var w = Camp(out var camp);
        var byPlayer = PlayerKill(w, camp);
        Assert.Equal(new Cause(P, "the player's wooden spear"), byPlayer.Cause);

        var caribou = Herd(w, Species.Caribou, 1, camp);
        var pack = Group(w, Species.Wolf, 4, camp);
        var wolves = pack[0];
        var hunt = Combat.Create(w, [.. pack.Select(id => new Fighter(id))], [new Fighter(caribou)], camp, 2, Awareness.Engaged, Awareness.Unaware);
        w.Agent(caribou).Body.BloodPct = 0;
        Combat.ResolveHeadless(w, hunt, []);
        var byPack = w.Tiles[camp].Carcasses!.Single(c => c.Id != byPlayer.Id);
        Assert.Equal(new Cause(wolves, "a wolf pack"), byPack.Cause);

        // Bled out on its own: no one is named.
        var lone = Herd(w, Species.Caribou, 1, camp);
        w.Agent(lone).Body.BloodPct = 0.0;
        w.Minute++; // a minute its wounds have not lived yet
        Fauna.Tick(w, w.Agent(lone), []);
        Assert.Contains(w.Tiles[camp].Carcasses!, c => c.Cause == new Cause(null, "its wounds"));
    }

    [Fact]
    public void Piles_FirePitsAndShelters_KeepWhoMadeThem()
    {
        var w = Camp(out var camp);
        TestKit.Supply(w);
        var away = new TileId(camp.X + 6, camp.Y); // an NPC to name, out of the way of the player's pile
        var npc = Npc.Spawn(w, away, away);
        Assert.Equal(new Cause(null, Cause.Natural), w.Shelters[camp].Cause);

        Sim.Step(w, new PutDown(P, Resource.PineBranch, 1), 1);
        Assert.Equal(new Cause(P, "put down by the player"), w.Tiles[camp].PileCause);
        // The pile keeps its maker while it stands; emptied, it is gone with its cause.
        TestKit.Give(Items.PileAt(w, camp, new Cause(npc, "put down by a person")), Resource.OakBranch, 1);
        Assert.Equal(P, w.Tiles[camp].PileCause!.Value.Who);
        Items.MoveFromPile(w, camp, new Inventory(), Resource.PineBranch, 9);
        Items.MoveFromPile(w, camp, new Inventory(), Resource.OakBranch, 9);
        Assert.Null(w.Tiles.GetValueOrDefault(camp)?.PileCause);

        for (int i = 0; i < 20 && Fire.PitAt(w, camp) == null; i++)
            Sim.Step(w, new StartFire(P, ToolType.FireStriker, Resource.BirchBark, 0.05, 0.3), 1);
        Assert.Equal(new Cause(P, "lit by the player with a fire striker"), w.Fires[Fire.PitAt(w, camp)!.Value].Cause);

        var east = new TileId(camp.X + 1, camp.Y);
        w.Agent(P).Tile = east;
        TestKit.GivePieces(Items.Pack(w, P), Resource.PineBranch, 6);
        var r = Sim.Step(w, new Make(P, "lean_to"), 60);
        Assert.True(w.Shelters.ContainsKey(east), $"{r.InterruptedReason} at {w.Minute}");
        Assert.Equal(new Cause(P, "built by the player"), w.Shelters[east].Cause);
    }

    [Fact]
    public void Causes_AreSavedAndLoaded_AndNameTheGoneAsLabels()
    {
        var w = Camp(out var camp);
        var wolves = Herd(w, Species.Wolf, 1, camp);
        var caribou = Herd(w, Species.Caribou, 1, camp);
        var hunt = Combat.Create(w, [new Fighter(wolves)], [new Fighter(caribou)], camp, 2, Awareness.Engaged, Awareness.Unaware);
        w.Agent(caribou).Body.BloodPct = 0;
        Combat.ResolveHeadless(w, hunt, []);
        Agents.Remove(w, wolves); // the killer is gone; its name stays on what it left
        var loaded = TestWorlds.Load(Save.ToJson(w));
        var c = loaded.Tiles[camp].Carcasses!.Single();
        Assert.Equal(new Cause(wolves, "a wolf"), c.Cause);
        Assert.Equal(Save.ToJson(w), Save.ToJson(loaded));
        Assert.Contains(Signs.At(loaded, camp), s => s.Kind == SignKind.Carcass && s.Cause == c.Cause);
        Assert.Equal(Tag.None, Tags.Of(loaded, new LocationFeature(LocationFeatureKind.Carcass, camp, c.Id)) & Tag.Owned);
    }

    // ---- Signs ----

    [Fact]
    public void SignsAt_AgreesWithTheFindDump()
    {
        var w = Phase3GateTests.BusyDay.Copy();
        var finds = Report.Finds(w);
        int signRows = 0;
        foreach (var t in w.Tiles.Keys.Concat(w.Agents.Values.Select(a => a.Tile)).Distinct())
            foreach (var s in Signs.At(w, t))
            {
                string? kind = s.Kind switch
                {
                    SignKind.Carcass => "carcass",
                    SignKind.Pile => "pile",
                    SignKind.FirePit => "fire pit",
                    SignKind.Shelter => "shelter",
                    SignKind.Prints => "tracks",
                    SignKind.Body when s.LocationFeature.Index != P.Value => "body",
                    SignKind.Trail when s.LocationFeature.TrailDirection is Direction.East or Direction.South => "trail",
                    _ => null,
                };
                if (kind == null) continue;
                signRows++;
                Assert.Contains(finds, f => f.Tile == t && f.Kind == kind && f.Cause == s.Cause?.ToString()
                    && f.AgeMinutes == (s.MadeMinute is long m ? w.Minute - m : null));
            }
        Assert.True(signRows > 5, $"only {signRows} signs");
        Assert.Contains(finds, f => f.Kind == "carcass" && f.Cause != null);
        Assert.Contains(finds, f => f.Kind == "shelter" && f.Cause == Cause.Natural);
    }

    [Fact]
    public void WorkedAndGrazedGround_AreSigns()
    {
        var w = Camp(out var camp);
        var forage = new PoolId(camp, 0);
        for (int i = 0; i < 12 && Pools.StateOf(w, forage) is not ForageState { HoursForaged: > 0 }; i++) Sim.Step(w, new Gather(P, 0), 60);
        Assert.Equal([SignKind.WorkedGround], Signs.At(w, camp).Where(s => s.LocationFeature == new LocationFeature(LocationFeatureKind.Pool, camp, 0)).Select(s => s.Kind));
        Pools.SetGrazed(w, forage, ((ForagePool)Pools.At(w, camp)[0]).Items[0].Resource, 0.5);
        Assert.Contains(SignKind.Grazed, Signs.At(w, camp).Select(s => s.Kind));
    }

    // ---- reading never writes ----

    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void LocationFeaturesSignsAndTags_NeverChangeTheWorld()
    {
        var w = Phase3GateTests.BusyDay.Copy();
        var camp = Map.Of(w).Camp;
        // Somewhere snowed under and never looked at: Tags peeks at the ground without keeping it.
        var hidden = HiddenStockTile(w, camp, 40, untouched: true);
        var tiles = w.Tiles.Keys.Concat(w.Agents.Values.Select(a => a.Tile)).Append(hidden).Distinct().ToList();
        string before = Save.ToJson(w);
        int read = 0;
        foreach (var t in tiles)
        {
            _ = Signs.At(w, t).ToList();
            foreach (var thing in LocationFeatures.At(w, t))
            {
                Tags.Of(w, thing);
                Tags.Of(w, thing, P);
                Access.Refusal(w, P, thing, Access.Gate.All);
                read++;
            }
        }
        Assert.True(read > 50, $"read only {read} things");
        Assert.Equal(before, Save.ToJson(w));
    }

    // ---- tags and capabilities ----

    [Fact]
    public void Capabilities_AreTheWorkingGearsRowsTogether()
    {
        var pack = new Inventory();
        Assert.Equal(Capability.None, Capabilities.Of(pack));
        pack.Gear.Add(Items.Make("flint_axe"));
        pack.Gear.Add(Items.Make("bone_shovel"));
        pack.Gear.Add(Items.Make("stone"));
        Assert.Equal(Capability.Cutting | Capability.Striking | Capability.Digging, Capabilities.Of(pack));
        pack.Gear[1].Durability = 0; // broken: it does nothing
        Assert.Equal(Capability.Cutting | Capability.Striking, Capabilities.Of(pack));
        Assert.All(Items.GearKinds, g => Assert.NotNull(g.Value.Capabilities));
    }

    [Fact]
    public void Carcass_Tags_ReadOffItsStateAndWhoIsThere()
    {
        var w = Camp(out var camp);
        var c = PlayerKill(w, camp);
        var t = new LocationFeature(LocationFeatureKind.Carcass, camp, c.Id);
        var tags = Tags.Of(w, t);
        Assert.True(tags.HasFlag(Tag.Edible | Tag.Harvestable | Tag.Fresh | Tag.ScentSource), tags.ToString());
        Assert.True(tags.HasFlag(Tag.Owned), "the player's kill, to anyone else");
        Assert.False(Tags.Of(w, t, P).HasFlag(Tag.Owned), "not to the player");
        Assert.False(tags.HasFlag(Tag.Occupied));

        var wolves = Herd(w, Species.Wolf, 3, camp);
        var occupied = Tags.Of(w, t);
        Assert.True(occupied.HasFlag(Tag.Occupied | Tag.Contested), occupied.ToString());
        Assert.False(Tags.Of(w, t, wolves).HasFlag(Tag.Occupied), "a pack does not guard its kill against itself");
        Assert.True(Tags.Of(w, t, P).HasFlag(Tag.Occupied), "a pack on the player's kill holds it against the player too");

        w.Minute += 3 * 24 * 60; // days later, in the cold or not, it is no longer fresh
        Assert.False(Tags.Of(w, t).HasFlag(Tag.Fresh));
    }

    [Fact]
    public void Agent_Tags_WoundedUnawareAndHostile()
    {
        var w = Camp(out var camp);
        var caribou = Herd(w, Species.Caribou, 2, camp);
        var t = LocationFeatures.OfAgent(w, caribou);
        Assert.True(Tags.Of(w, t).HasFlag(Tag.Unaware));
        Assert.False(Tags.Of(w, t).HasFlag(Tag.Wounded));
        w.Agent(caribou).Members[0].Regions[0].Muscle.Condition = 0.5;
        Assert.True(Tags.Of(w, t).HasFlag(Tag.Wounded));
        Fauna.MindOf(w.Agent(caribou)).FleeFrom = camp;
        Assert.False(Tags.Of(w, t).HasFlag(Tag.Unaware));

        var wolves = Herd(w, Species.Wolf, 2, camp);
        Combat.Create(w, [new Fighter(P)], [new Fighter(wolves)], camp, 8, Awareness.Engaged, Awareness.Engaged);
        Assert.True(Tags.Of(w, LocationFeatures.OfAgent(w, wolves), P).HasFlag(Tag.Hostile));
        Assert.False(Tags.Of(w, LocationFeatures.OfAgent(w, wolves)).HasFlag(Tag.Hostile), "hostile is to someone");
        Assert.False(Tags.Of(w, LocationFeatures.OfAgent(w, caribou), P).HasFlag(Tag.Hostile));
    }

    /// A hidden stock near camp, found by the player, with the player standing on it. untouched:
    /// one on a tile nothing has observed, left as it is.
    static TileId HiddenStockTile(World w, TileId near, int radius, bool untouched = false)
    {
        for (int r = 1; r <= radius; r++)
            for (int dx = -r; dx <= r; dx++)
                foreach (int dy in new[] { r - Math.Abs(dx), -(r - Math.Abs(dx)) })
                {
                    var t = new TileId(near.X + dx, near.Y + dy);
                    if (!Map.Of(w).InBounds(t) || untouched && w.Tiles.ContainsKey(t)) continue;
                    if (Pools.At(w, t).Any(p => p is StockPool { RevealAtHours: > 0 })) return t;
                }
        throw new InvalidOperationException("no hidden stock near camp");
    }

    static (World W, TileId Tile, int Pool) OnFoundStock()
    {
        var w = Camp(out var camp);
        var tile = HiddenStockTile(w, camp, 40);
        int pool = Pools.At(w, tile).ToList().FindIndex(p => p is StockPool { RevealAtHours: > 0 });
        ((PersonMind)w.Agent(P).Mind).Memory.Discovered.Add(new PoolId(tile, pool));
        w.Agent(P).Tile = tile;
        Midday(w);
        return (w, tile, pool);
    }

    static void SnowUnder(World w, TileId tile, double metres)
    {
        TestKit.EditGround(w, tile, (g, _) => g.Layers.Add(new SurfaceLayer { Material = SurfaceMaterial.Snow, ThicknessM = metres, CompactionPct = 0.3, FrozenFractionPct = 1 }));
    }

    [Fact]
    public void AHiddenStock_UnderDeepSnow_IsBuried()
    {
        var (w, tile, pool) = OnFoundStock();
        var t = new LocationFeature(LocationFeatureKind.Pool, tile, pool);
        Assert.False(Tags.Of(w, t).HasFlag(Tag.Buried));
        SnowUnder(w, tile, 3);
        Assert.True(Tags.Of(w, t).HasFlag(Tag.Buried));
    }

    /// Every gear row states what it can do, through the strict loader.
    [Theory]
    [InlineData("\"capabilities\": [\"Binding\", \"Sewing\"], ", "", "capabilities")]
    [InlineData("\"capabilities\": [\"Binding\", \"Sewing\"]", "\"capabilities\": [\"Sewing\", \"Sewing\"]", "listed twice")]
    [InlineData("\"capabilities\": [\"Binding\", \"Sewing\"]", "\"capabilities\": [\"None\"]", "names None")]
    [InlineData("\"capabilities\": [\"Binding\", \"Sewing\"]", "\"capabilities\": [\"Binding, Sewing\"]", "no Capability 'Binding, Sewing'")]
    [InlineData("\"capabilities\": [\"Binding\", \"Sewing\"]", "\"capabilities\": [\"cutting\"]", "no Capability 'cutting'")]
    public void Loader_RefusesAGearRow_WithBadCapabilities(string from, string to, string named)
    {
        var files = Content.EmbeddedFiles();
        Assert.Contains(from, files["items.json"]);
        int at = files["items.json"].IndexOf(from, StringComparison.Ordinal);
        files["items.json"] = files["items.json"][..at] + to + files["items.json"][(at + from.Length)..];
        Assert.Contains(named, Assert.Throws<InvalidDataException>(() => Content.Parse(files)).Message);
    }

    // ---- the access gate ----

    /// With every condition switched on, each refusal that reaches a thing says what Access says;
    /// with the wired ones (none yet), the same scene refuses nothing new.
    [Fact]
    public void EveryLocationFeatureRefusal_AsksAccess_Gather()
    {
        var (w, tile, pool) = OnFoundStock();
        SnowUnder(w, tile, 3);
        var gather = new Gather(P, pool);
        Assert.NotEqual(Access.BuriedReason, Work.Refusal(w, gather)?.Reason);
        w.AccessGates = Access.Gate.All;
        Assert.Equal(Access.BuriedReason, Work.Refusal(w, gather)?.Reason);
        Items.Pack(w, P).Gear.Add(Items.Make("bone_shovel")); // something to dig with
        Assert.NotEqual(Access.BuriedReason, Work.Refusal(w, gather)?.Reason);
    }

    [Fact]
    public void EveryLocationFeatureRefusal_AsksAccess_WhatAPredatorOccupies()
    {
        var w = Camp(out var camp);
        TestKit.Supply(w);
        var pack = Items.Pack(w, P);
        pack.Gear.Add(Items.Make("flint_knife"));
        var c = PlayerKill(w, camp);
        Sim.Step(w, new PutDown(P, Resource.PineBranch, 1), 1);
        for (int i = 0; i < 20 && !(Fire.PitAt(w, camp) is FireId f && Fire.IsActive(w.Fires[f])); i++)
            Sim.Step(w, new StartFire(P, ToolType.FireStriker, Resource.BirchBark, 0.05, 0.3), 1);
        Midday(w);
        foreach (var (id, _) in w.Fires.ToList()) Fire.CatchUp(w, id, []);
        Assert.False(Fire.Burns(Fire.Phase(w.Fires[Fire.PitAt(w, camp)!.Value])), "the fire is down by noon: no fire to defend the tile");
        // Everything here is the player's, and nothing is held against the one who made or killed it
        // (Tags.Occupants): the one reaching for it is someone else, who made none of it.
        var npc = Npc.Spawn(w, camp);
        TestKit.Supply(w, npc);
        Items.Pack(w, npc).Gear.Add(Items.Make("flint_knife"));
        var refusals = new (string Name, Func<string?> Refusal)[]
        {
            ("butcher", () => Work.Refusal(w, new Butcher(npc, c.Id))?.Reason),
            ("pick up", () => Work.Refusal(w, new PickUp(npc, Resource.PineBranch, 1))?.Reason),
            ("put down", () => Work.Refusal(w, new PutDown(npc, Resource.BirchBranch, 1))?.Reason),
            ("gather", () => Work.Refusal(w, new Gather(npc, 0))?.Reason),
            ("add fuel", () => Fire.Refusal(w, new AddFuel(npc, Resource.BirchBranch, 1))?.Reason),
            ("charcoal", () => Fire.Refusal(w, new GatherCharcoal(npc))?.Reason),
            ("start fire", () => Fire.Refusal(w, new StartFire(npc, ToolType.FireStriker, Resource.BirchBark, 0.05, 0.3))?.Reason),
            ("improve shelter", () => Shelters.Refusal(w, new ImproveShelter(npc, ShelterStat.Insulation, Resource.PineBranch))?.Reason),
        };
        var unguarded = refusals.Select(r => r.Refusal()).ToList();
        var wolves = Herd(w, Species.Wolf, 3, camp);
        // Wired as before: the pack on the tile changes no answer.
        Assert.Equal(unguarded, refusals.Select(r => r.Refusal()).ToList());
        w.AccessGates = Access.Gate.All;
        foreach (var (name, refusal) in refusals) Assert.True(refusal() == "a wolf pack guards it", $"{name}: {refusal()}");
        // A fight is the confrontation: a guard does not bar striking at it.
        var enc = Combat.Create(w, [new Fighter(P)], [new Fighter(wolves)], camp, 1, Awareness.Engaged, Awareness.Engaged);
        Assert.Null(Combat.Refusal(w, enc, Combat.UnitOf(enc, P), CombatAction.Attack));
    }

    /// The guard's exemptions (implemented with the gate, wired with it): a guard does not hold what
    /// the viewer made (a pile, a fire, a shelter), a fire burning on the tile keeps it off, and a pack
    /// that is afraid, running, or busy fighting someone else holds nothing. What the viewer killed is
    /// not exempt: a pack can take your kill.
    [Fact]
    public void Occupied_HasItsExemptions()
    {
        var w = Camp(out var camp);
        var npc = Npc.Spawn(w, camp);
        var theirs = Carcasses.Create(w, camp, "Caribou", SpeciesTable.NewBody(Species.Caribou, w.Rng("test")), w.Minute, cause: new Cause(npc, "a person"));
        var t = new LocationFeature(LocationFeatureKind.Carcass, camp, theirs.Id);
        var pack = Group(w, Species.Wolf, 3, camp);
        var wolves = pack[0];
        Assert.True(Tags.Of(w, t, P).HasFlag(Tag.Occupied), "someone else's kill, with a pack on it");
        Assert.True(Tags.Of(w, t, npc).HasFlag(Tag.Occupied), "the viewer's own kill: the wolves have taken it");
        var goods = Items.PileAt(w, camp, new Cause(npc, "put down by a person"));
        TestKit.Give(goods, Resource.PineBranch, 1);
        var pile = LocationFeatures.PileAt(w, camp)!.Value;
        Assert.False(Tags.Of(w, pile, npc).HasFlag(Tag.Occupied), "what the viewer made");
        Assert.True(Tags.Of(w, pile, P).HasFlag(Tag.Occupied), "what someone else made");

        // Every wolf of the pack: each one guards on its own.
        var minds = pack.Select(id => Fauna.MindOf(w.Agent(id))).ToList();
        foreach (var mind in minds) mind.Fear = 0.9;
        Assert.False(Tags.Of(w, t, P).HasFlag(Tag.Occupied), "an afraid pack");
        foreach (var mind in minds) (mind.Fear, mind.FleeFrom) = (0, camp);
        Assert.False(Tags.Of(w, t, P).HasFlag(Tag.Occupied), "a running pack");
        foreach (var mind in minds) mind.FleeFrom = null;
        var enc = Combat.Create(w, [new Fighter(npc)], [.. pack.Select(id => new Fighter(id))], camp, 8, Awareness.Engaged, Awareness.Engaged);
        Assert.False(Tags.Of(w, t, P).HasFlag(Tag.Occupied), "a pack fighting someone else");
        w.Encounters.Remove(enc);
        Assert.True(Tags.Of(w, t, P).HasFlag(Tag.Occupied));

        TestKit.Supply(w);
        Midday(w);
        for (int i = 0; i < 20 && !(Fire.PitAt(w, camp) is FireId f && Fire.Burns(Fire.Phase(w.Fires[f]))); i++)
            Fire.Act(w, new StartFire(P, ToolType.FireStriker, Resource.BirchBark, 0.05, 1.0), []);
        Assert.True(Fire.Burns(Fire.Phase(w.Fires[Fire.PitAt(w, camp)!.Value])));
        Assert.False(Tags.Of(w, t, P).HasFlag(Tag.Occupied), "a fire burning on the tile");
    }

    [Fact]
    public void EveryLocationFeatureRefusal_AsksAccess_PackingATent()
    {
        var w = Camp(out var camp);
        var east = new TileId(camp.X + 1, camp.Y);
        w.Agent(P).Tile = east;
        Midday(w);
        Items.Pack(w, P).Gear.Add(Items.Make("hide_tent"));
        Sim.Step(w, new Make(P, "tent"), 600);
        Assert.NotNull(w.Shelters[east].Tent);
        Assert.Equal(new Cause(P, "pitched by the player"), w.Shelters[east].Cause);
        Herd(w, Species.Bear, 1, east);
        Assert.Null(Shelters.Refusal(w, new PackTent(P)));
        w.AccessGates = Access.Gate.All;
        // The player's own tent is not held against them (Occupied_HasItsExemptions); someone else's is.
        Assert.Null(Shelters.Refusal(w, new PackTent(P)));
        var npc = Npc.Spawn(w, east);
        Assert.Equal("a bear guards it", Shelters.Refusal(w, new PackTent(npc))?.Reason);
    }

    /// The things' own wording, from before the gate: unchanged.
    [Fact]
    public void ExistingReasons_AreUnchanged()
    {
        var w = Camp(out var camp);
        w.AccessGates = Access.Gate.All; // nothing there to gate: the thing's own refusal answers
        Assert.Equal("no such carcass here", Work.Refusal(w, new Butcher(P, 99))?.Reason);
        Assert.Equal("nothing like that here", Work.Refusal(w, new Gather(P, 99))?.Reason);
        Assert.Equal("no pine branches in the pile here", Work.Refusal(w, new PickUp(P, Resource.PineBranch, 1))?.Reason);
        TestKit.Give(Items.Pack(w, P), Resource.PineBranch, 1);
        Assert.Equal("no fire here", Fire.Refusal(w, new AddFuel(P, Resource.PineBranch, 1))?.Reason);
        Assert.Equal("no charcoal here", Fire.Refusal(w, new GatherCharcoal(P))?.Reason);
        Assert.Equal("there's no tent to pack up here", Shelters.Refusal(w, new PackTent(P))?.Reason);
        w.Agent(P).Tile = new TileId(camp.X + 1, camp.Y);
        Assert.Equal("there's no shelter here to improve", Shelters.Refusal(w, new ImproveShelter(P, ShelterStat.Insulation, Resource.PineBranch))?.Reason);
    }

    [Fact]
    public void Access_ReachesOnlyTheTileAndNeighbours_NotAcrossWater()
    {
        var w = Camp(out var camp);
        var m = Map.Of(w);
        var far = Enumerable.Range(2, 20).Select(d => new TileId(camp.X + d, camp.Y)).First(t => Pools.At(w, t).Count > 0);
        Assert.Equal(Access.OutOfReach, Access.Refusal(w, P, new LocationFeature(LocationFeatureKind.Pool, far, 0))?.Reason);
        // A neighbour across open water: refused only with the condition in force.
        var (water, land) = Enumerable.Range(1, m.Width - 2).SelectMany(x => Enumerable.Range(1, m.Height - 2).Select(y => new TileId(x, y)))
            .Where(t => m.TerrainAt(t) == Terrain.Water)
            .SelectMany(t => Enum.GetValues<Direction>().Select(d => (Water: t, Land: Map.Step(t, d))))
            .First(p => m.TerrainAt(p.Land).IsPassable() && m.TerrainAt(p.Land) != Terrain.Water && Pools.At(w, p.Land).Count > 0);
        w.Agent(P).Tile = water;
        var across = new LocationFeature(LocationFeatureKind.Pool, land, 0);
        Assert.Null(Access.Refusal(w, P, across));
        Assert.Equal(Access.AcrossWaterReason, Access.Refusal(w, P, across, Access.Gate.All)?.Reason);
    }

    [Fact]
    public void Access_AsksOnlyOfThingsThatAreThere()
    {
        var w = Camp(out var camp);
        Assert.Throws<ArgumentException>(() => Access.Refusal(w, P, new LocationFeature(LocationFeatureKind.Carcass, camp, 12345)));
        Assert.Throws<ArgumentException>(() => Tags.Of(w, new LocationFeature(LocationFeatureKind.Item, camp, 0)));
    }

    // ---- the switch rule ----

    /// Every switch over LocationFeatureKind is a switch expression that names every kind and has no default
    /// arm, so adding a kind fails the build (CS8509, an error in Sim) wherever it is not handled.
    [Fact]
    public void EveryLocationFeatureKindSwitch_IsAnExpressionNamingEveryKind_WithNoDefault()
    {
        var dir = ArchitectureTests.SimSourceDir();
        var csproj = File.ReadAllText(Path.Combine(dir, "Sim.csproj"));
        Assert.Matches(@"<WarningsAsErrors>[^<]*CS8509[^<]*</WarningsAsErrors>", csproj);
        var kinds = Enum.GetNames<LocationFeatureKind>();
        int switches = 0;
        var problems = new List<string>();
        foreach (var file in Directory.GetFiles(dir, "*.cs"))
        {
            var text = string.Join("\n", File.ReadLines(file).Select(l => Regex.Replace(l, @"//.*$", "")));
            if (Regex.IsMatch(text, @"case\s+LocationFeatureKind\.")) problems.Add($"{Path.GetFileName(file)}: a switch statement over LocationFeatureKind");
            foreach (Match sw in Regex.Matches(text, @"\bswitch\s*\{"))
            {
                int depth = 0, end = sw.Index + sw.Length - 1;
                for (int i = end; i < text.Length; i++)
                {
                    if (text[i] == '{') depth++;
                    else if (text[i] == '}' && --depth == 0) { end = i; break; }
                }
                string body = text[(sw.Index + sw.Length)..end];
                // Only the arms of this switch, not of switches nested in it.
                string top = Regex.Replace(body, @"\{[^{}]*\}", "");
                if (!Regex.IsMatch(top, @"LocationFeatureKind\.\w+(\s+or\s+LocationFeatureKind\.\w+)*\s*=>")) continue;
                switches++;
                var named = Regex.Matches(top, @"LocationFeatureKind\.(\w+)").Select(x => x.Groups[1].Value).ToHashSet();
                var missing = kinds.Where(k => !named.Contains(k)).ToList();
                if (missing.Count > 0) problems.Add($"{Path.GetFileName(file)}: a LocationFeatureKind switch without {string.Join(", ", missing)}");
                if (Regex.IsMatch(top, @"(^|[\s,{])_\s*=>")) problems.Add($"{Path.GetFileName(file)}: a LocationFeatureKind switch with a default arm");
            }
        }
        Assert.True(switches >= 4, $"found only {switches} LocationFeatureKind switches");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// Each refusal that reaches a thing calls Access, the fights as the confrontation they are.
    [Fact]
    public void EveryLocationFeatureRefusal_CallsAccess_InTheSource()
    {
        var dir = ArchitectureTests.SimSourceDir();
        // Work's put-down (PutDown) asks Access.PutDownRefusal; herds' Feed, Graze and Raid ask it too.
        foreach (var (file, calls) in new[] { ("Work.cs", 3), ("Fire.cs", 1), ("Shelter.cs", 2), ("Combat.cs", 1), ("Fauna.cs", 4), ("Scavenging.cs", 1) })
        {
            int n = Regex.Matches(File.ReadAllText(Path.Combine(dir, file)), @"Access\.Refusal\(").Count;
            Assert.True(n >= calls, $"{file} asks Access {n} times, not {calls}");
        }
        Assert.Contains("confronting: true", File.ReadAllText(Path.Combine(dir, "Combat.cs")));
        Assert.Contains("confronting: true", File.ReadAllText(Path.Combine(dir, "Fauna.cs")));
        Assert.Contains("Access.PutDownRefusal(", File.ReadAllText(Path.Combine(dir, "Work.cs")));
    }
}
