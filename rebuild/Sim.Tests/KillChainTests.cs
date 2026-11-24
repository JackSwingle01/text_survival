using Survival;
using Xunit;
using Xunit.Abstractions;
using Survival.Tests;

/// Emergent-world build step 3, part A (the kill chain): stable ids, who killed what, the body and
/// carcass pipeline, the carrion flock, the herds' access gate, and the caches that make whole-map
/// passes cheap without changing a result. Herd renewal is RenewalTests.
public class KillChainTests
{
    static readonly AgentId P = AgentId.Player;

    /// Seed 17 with nothing else alive in it but the player, in a fixed sky (41 °F: rot at its base rate).
    internal static World Bare(double tempC = 5, int seed = 17)
    {
        var w = TestWorlds.Bare(seed);
        w.FixedWeather = new(0, WeatherCondition.Cloudy, tempC, 0.1, 0, 0.8, WindDirection.North);
        return w;
    }

    internal static Carcass Caribou(World w, TileId tile, Cause? cause = null) =>
        Carcasses.Create(w, tile, "Caribou", SpeciesTable.NewBody(Species.Caribou, w.Rng("test")), w.Minute, cause: cause);

    static long Days(double d) => (long)(d * 1440);

    // ---- stable ids ----

    [Fact]
    public void A_Pile_BegunAgain_IsANewThing()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        Items.Add(Items.PileAt(w, camp, new Cause(P, "put down by the player")), Resource.Pine, 1);
        var first = LocationFeatures.PileAt(w, camp)!.Value;
        Assert.Equal(1, Items.TakeFromPile(w, camp, Resource.Pine, 1));
        Assert.False(LocationFeatures.Exists(w, first));
        Items.Add(Items.PileAt(w, camp, new Cause(P, "put down by the player")), Resource.Oak, 1);
        var second = LocationFeatures.PileAt(w, camp)!.Value;
        Assert.NotEqual(first, second);
        Assert.False(LocationFeatures.Exists(w, first), "a held ref to the old pile never names the new one");
        var loaded = TestWorlds.Load(Save.ToJson(w));
        Assert.Equal(second, LocationFeatures.PileAt(loaded, camp));
        Assert.Equal(w.NextPileId, loaded.NextPileId);
    }

    [Fact]
    public void A_Shelter_PlacedAgain_IsANewThing()
    {
        var w = Bare();
        var tile = Map.Step(Map.Of(w).Camp, Direction.East);
        Shelters.Place(w, tile, new ShelterDef("Snow shelter", ShelterKind.SnowShelter));
        var first = LocationFeatures.ShelterAt(w, tile)!.Value;
        Shelters.Remove(w, tile);
        Shelters.Place(w, tile, new ShelterDef("Snow shelter", ShelterKind.SnowShelter));
        var second = LocationFeatures.ShelterAt(w, tile)!.Value;
        Assert.NotEqual(first, second);
        Assert.False(LocationFeatures.Exists(w, first));
        Assert.True(LocationFeatures.Exists(w, second));
        Assert.Equal(second, LocationFeatures.ShelterAt(TestWorlds.Load(Save.ToJson(w)), tile));
    }

    // ---- who killed what ----

    /// Strikes until a blow lands (each seed a fresh world), then hands the fight back.
    static (World W, Encounter Enc, AgentId Prey) Struck(Func<World, TileId, (Encounter Enc, AgentId Prey, CombatUnit Striker, CombatAction Act)> setup)
    {
        for (int seed = 1; seed < 200; seed++)
        {
            var w = Bare(seed: seed);
            var tile = Map.Of(w).Camp;
            var (enc, prey, striker, act) = setup(w, tile);
            var hit = Combat.Apply(w, enc, striker, act, []);
            if (hit.Hit && hit.Damage != null) return (w, enc, prey);
            w.Encounters.Clear();
        }
        throw new InvalidOperationException("no blow landed in 200 tries");
    }

    /// Review: a thrown spear was taken out of the thrower's hands before the aftermath read it, so the
    /// kill was put down to "the player", not the spear.
    [Fact]
    public void A_ThrownSpear_IsNamedOnTheKill()
    {
        var (w, enc, prey) = Struck((w, tile) =>
        {
            var caribou = Agents.Spawn(w, Species.Caribou, tile);
            var spear = Items.Make("wooden_spear");
            Items.Pack(w, P).Gear.Add(spear);
            var e = Combat.Create(w, [new Fighter(P) { Weapon = spear }], [new Fighter(caribou)], tile, 10, Awareness.Engaged, Awareness.Unaware);
            return (e, caribou, Combat.UnitOf(e, P), CombatAction.Throw);
        });
        Assert.Null(Combat.UnitOf(enc, P).Weapon); // thrown: it is on the ground
        w.Agent(prey).Body.BloodPct = 0;
        Combat.Finish(w, enc, []);
        var c = w.Tiles[enc.Tile].Carcasses!.Single();
        Assert.Equal(new Cause(P, "the player's wooden spear"), c.Cause);
    }

    /// Review: a side's dead were put down to its first fighter, so an ally's kill was the player's.
    [Fact]
    public void An_AllysKill_IsTheAllys()
    {
        var (w, enc, prey) = Struck((w, tile) =>
        {
            var caribou = Agents.Spawn(w, Species.Caribou, tile);
            var npc = Npc.Spawn(w, tile);
            var e = Combat.Create(w, [new Fighter(P), new Fighter(npc)], [new Fighter(caribou)], tile, 1, Awareness.Engaged, Awareness.Unaware);
            var ally = Combat.UnitOf(e, npc);
            var at = Combat.UnitOf(e, caribou).Position;
            ally.Position = new Cell(at.X + 1, at.Y); // in reach of bare hands
            return (e, caribou, ally, CombatAction.Attack);
        });
        w.Agent(prey).Body.BloodPct = 0;
        Combat.Finish(w, enc, []);
        var c = w.Tiles[enc.Tile].Carcasses!.Single();
        Assert.NotEqual(P, c.Cause!.Value.Who);
        Assert.Equal("a person", c.Cause!.Value.What);
    }

    /// Review: an animal that bled out after a fight was "its wounds", naming nobody.
    [Fact]
    public void An_AnimalThatBleedsOut_NamesItsHunter()
    {
        var (w, enc, prey) = Struck((w, tile) =>
        {
            var caribou = Agents.Spawn(w, Species.Caribou, tile);
            var spear = Items.Make("wooden_spear");
            Items.Pack(w, P).Gear.Add(spear);
            var e = Combat.Create(w, [new Fighter(P) { Weapon = spear }], [new Fighter(caribou)], tile, 1, Awareness.Engaged, Awareness.Unaware);
            return (e, caribou, Combat.UnitOf(e, P), CombatAction.Attack);
        });
        w.Encounters.Remove(enc); // the fight is over and the caribou walked away from it
        var body = w.Agent(prey).Body;
        Assert.Equal(new Cause(P, "the player's wooden spear"), body.WoundedBy);
        body.BloodPct = 0;
        Sim.Advance(w, 1);
        var c = w.Tiles.Values.SelectMany(t => t.Carcasses ?? []).Single();
        Assert.Equal(new Cause(P, "wounds from the player's wooden spear"), c.Cause);
    }

    [Fact]
    public void A_DeadPerson_KeepsWhatTheyDiedOf()
    {
        var w = Bare();
        var npc = Npc.Spawn(w, Map.Of(w).Camp);
        var body = w.Agent(npc).Body;
        body.WoundedBy = new Cause(new AgentId(999), "a wolf pack");
        body.BloodPct = 0.52;
        Effects.Add(body, EffectKind.Bleeding, 1.0);
        for (int i = 0; i < 24 * 60 && Body.IsAlive(body); i++) Sim.Advance(w, 1);
        Assert.False(Body.IsAlive(body));
        Assert.Equal(new Cause(new AgentId(999), "a wolf pack"), body.DeathCause);
        Assert.NotNull(body.DiedMinute);
        var sign = Signs.At(w, w.Agent(npc).Tile).Single(s => s.Kind == SignKind.Body);
        Assert.Equal(body.DeathCause, sign.Cause);
        Assert.Equal(body.DiedMinute, sign.MadeMinute);

        var cold = BodyState.BaselineHuman();
        LiveUntilDead(cold, b => b.CoreTempF = 80);
        Assert.Equal("the cold", Body.CauseOfDeath(cold).What);
        var starved = BodyState.BaselineHuman();
        double spentKg = starved.StructuralKg / 0.82; // fat and muscle burned down to their floors (3% and 15%)
        (starved.FatKg, starved.MuscleKg) = (0.03 * spentKg, 0.15 * spentKg);
        LiveUntilDead(starved, b => b.CaloriesKcal = 0);
        Assert.Equal("starvation", Body.CauseOfDeath(starved).What);
    }

    /// Lives a body in a mild room, minute by minute, until it dies: each runs before every minute.
    static void LiveUntilDead(BodyState body, Action<BodyState> each)
    {
        var rng = Rng.ForSystem(1, "body");
        for (int m = 0; m < 10 * 1440 && Body.IsAlive(body); m++)
        {
            each(body);
            Body.Update(body, new SurvivalContext { AirTempF = 70 }, rng);
        }
        Assert.False(Body.IsAlive(body), "lived ten days");
    }

    /// Review: seed 99's NPC froze carrying six kilograms of fat at a core of 92.5 °F and was said to
    /// have starved, because its day's calories were gone. The cause is the damage that crossed the line
    /// (body review B5): exhaustion is not one, since effects never kill.
    [Fact]
    public void A_Death_IsNamedForWhatKilled_NotForAnEmptyStomach()
    {
        BodyState Seed99()
        {
            var b = BodyState.BaselineHuman();
            (b.CoreTempF, b.FatKg, b.MuscleKg, b.HydrationMl, b.CaloriesKcal, b.EnergyMinutes) = (92.5, 6.38, 22.5, 1923, 0, 20);
            Effects.Add(b, EffectKind.Hypothermia, 0.24);
            Effects.Add(b, EffectKind.Hungry, 0.98);
            Effects.Add(b, EffectKind.Tired, 0.98);
            return b;
        }
        var cold = Seed99();
        LiveUntilDead(cold, b => { b.CoreTempF = 86; b.CaloriesKcal = 0; });
        Assert.Equal("the cold", Body.CauseOfDeath(cold).What); // fat left to burn: hunger is not killing it

        var spent = Seed99();
        spent.CoreTempF = Body.BaseCoreTempF;
        spent.EnergyMinutes = 0;
        var rng = Rng.ForSystem(1, "body");
        for (int m = 0; m < 6 * 60; m++)
        {
            spent.EnergyMinutes = 0;
            Body.Update(spent, new SurvivalContext { AirTempF = 70 }, rng);
        }
        Assert.True(Body.IsAlive(spent)); // worn out on its feet, not dead of it

        var dry = Seed99();
        LiveUntilDead(dry, b => { b.CoreTempF = 92.5; b.HydrationMl = 0; }); // thirst does more harm an hour than mild cold
        Assert.Equal("thirst", Body.CauseOfDeath(dry).What);

        var bled = Seed99();
        bled.CoreTempF = Body.BaseCoreTempF;
        bled.BloodPct = 0.55;
        Effects.Add(bled, EffectKind.Bleeding, 0.5);
        LiveUntilDead(bled, _ => { });
        Assert.Equal("blood loss", Body.CauseOfDeath(bled).What);
    }

    // ---- the body and carcass pipeline ----

    static RemainsStage StageAt(World w, TileId tile, int id) =>
        Carcasses.Find(w, tile, id) is { } c ? Remains.StageOf(w, tile, c) : RemainsStage.Gone;

    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void A_Carcass_RotsToBonesThenIsGone_KeepingItsCause()
    {
        var w = Bare(tempC: 5); // 41 °F: one effective hour an hour
        var camp = Map.Of(w).Camp;
        var cause = new Cause(new AgentId(999), "a wolf pack");
        var c = Caribou(w, camp, cause);
        double bonesAt = Remains.SkeletalHours(c.BodyWeightKg);
        Sim.Advance(w, (int)(bonesAt * 60) - 120);
        Assert.Equal(RemainsStage.Flesh, StageAt(w, camp, c.Id));
        Assert.True(Carcasses.MeatKg(c) > 0);
        Sim.Advance(w, 240);
        Assert.Equal(RemainsStage.Bones, StageAt(w, camp, c.Id));
        Assert.Equal(0, Carcasses.MeatKg(c)); // the hour's settling rotted the flesh off the ledger
        Assert.True(c.Remaining[Resource.Bone] > 0);
        Assert.Equal(cause, c.Cause);
        var sign = Signs.At(w, camp).Single(s => s.LocationFeature.Kind == LocationFeatureKind.Carcass);
        Assert.Equal(SignKind.Bones, sign.Kind);
        Assert.Equal(cause, sign.Cause);
        Assert.Contains(Report.Finds(w), f => f.Kind == "bones" && f.Tile == camp && f.Category == FindCategory.DeadMatter);
        Assert.False(Tags.Of(w, new LocationFeature(LocationFeatureKind.Carcass, camp, c.Id)).HasFlag(Tag.Edible));
        Sim.Advance(w, (int)((Remains.GoneHours - bonesAt) * 60) + 60);
        Assert.Equal(RemainsStage.Gone, StageAt(w, camp, c.Id));
        Assert.Null(w.Tiles.GetValueOrDefault(camp)?.Carcasses);
    }

    /// The frozen traveller: the same carcass kept by a hard frost is still flesh long after one at a thaw is bones.
    [Fact]
    public void Frost_KeepsACarcass_FarLonger()
    {
        var thaw = Bare(tempC: 5);
        var frost = Bare(tempC: -25); // -13 °F: a tenth of the rot
        var a = Caribou(thaw, Map.Of(thaw).Camp);
        var b = Caribou(frost, Map.Of(frost).Camp);
        int days = (int)Math.Ceiling(Remains.SkeletalHours(a.BodyWeightKg) / 24) + 1;
        Sim.Advance(thaw, days * 1440);
        Sim.Advance(frost, days * 1440);
        Assert.Equal(RemainsStage.Bones, StageAt(thaw, Map.Of(thaw).Camp, a.Id));
        Assert.Equal(RemainsStage.Flesh, StageAt(frost, Map.Of(frost).Camp, b.Id));
        Assert.True(Carcasses.EffectiveHoursNow(frost, Map.Of(frost).Camp, b) < Carcasses.EffectiveHoursNow(thaw, Map.Of(thaw).Camp, a) / 5);
    }

    /// Scavengers speed it: a carcass stripped of its meat goes to bones far sooner than a whole one.
    [Fact]
    public void A_StrippedCarcass_GoesToBonesSooner()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        var whole = Caribou(w, camp);
        var stripped = Caribou(w, camp);
        Carcasses.EatMeat(stripped, Carcasses.MeatKg(stripped) * 0.95);
        Sim.Advance(w, 3 * 1440);
        Assert.Equal(RemainsStage.Flesh, StageAt(w, camp, whole.Id));
        Assert.Equal(RemainsStage.Bones, StageAt(w, camp, stripped.Id));
    }

    /// Review: cutting lowered the flesh left, which aged a two-day-old carcass to bones half-way
    /// through the butchering; the rot then took the uncut hide and sinew and the cut pieces waiting on
    /// it, about twenty kilograms, while YieldKg still counted them. Cutting is not stripping: every
    /// kilogram the carcass yields is in the pack or still waiting on it, and nothing rots uncut.
    [Fact]
    public void Butchering_AnOlderCarcass_KeepsEverythingItCut()
    {
        var w = Bare(); // 41 °F: an effective hour an hour
        w.Minute = Days(2);
        var camp = Map.Of(w).Camp;
        var player = w.Agent(P);
        var pack = Items.Pack(w, P);
        pack.Gear.Add(Items.Make("flint_knife"));
        var c = Carcasses.Create(w, camp, "Caribou", SpeciesTable.NewBody(Species.Caribou, w.Rng("test")), w.Minute - Days(2));
        var onIt = new Dictionary<Resource, double>(c.Remaining);
        var before = Enum.GetValues<Resource>().ToDictionary(r => r, r => Items.Have(pack, r));
        string? stopped = null;
        for (int i = 0; i < 60 && Carcasses.Find(w, camp, c.Id) != null && stopped != Work.PackFull; i++)
        {
            (player.Body.HydrationMl, player.Body.CaloriesKcal, player.Body.EnergyMinutes) = (3000, 2000, 900);
            stopped = Sim.Step(w, new Butcher(P, c.Id, ButcherMode.Careful), 15).InterruptedReason;
        }
        Assert.True(Carcasses.AllCut(c), $"stopped: {stopped}");
        foreach (var (r, kg) in onIt)
            Assert.True(Math.Abs(c.CutKg.GetValueOrDefault(r) - kg) < 1e-6, $"{r}: {kg:0.00} kg on it, {c.CutKg.GetValueOrDefault(r):0.00} cut; the rest rotted uncut");
        foreach (var (r, kg) in c.YieldKg)
            Assert.Equal(kg, Items.Have(pack, r) - before[r] + c.Pending.GetValueOrDefault(r), 6);
        Assert.True(c.YieldKg.GetValueOrDefault(Resource.Hide) > 0);
        // What waits on the carcass is the butcher's, however long it waits and however far it rots.
        var waiting = new Dictionary<Resource, double>(c.Pending);
        Sim.Advance(w, (int)(Remains.SkeletalHours(c.BodyWeightKg) * 60));
        Assert.Equal(RemainsStage.Bones, StageAt(w, camp, c.Id));
        Assert.Equal(waiting, c.Pending);
    }

    [Fact]
    public void A_DeadPerson_GoesToASkeletonThenBones_AndLeavesTheWorld()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        var npc = Npc.Spawn(w, camp);
        var dead = w.Agent(npc);
        var cause = new Cause(null, "the cold");
        dead.Body.BloodPct = 0;
        Body.MarkDead(dead.Body, w.Minute, cause);
        double skeletal = Remains.SkeletalHours(dead.Body.WeightKg);
        Assert.Equal(RemainsStage.Flesh, Remains.StageOf(w, dead));
        Assert.True(Tags.Of(w, LocationFeatures.OfAgent(w, npc)).HasFlag(Tag.ScentSource));
        Sim.Advance(w, (int)(skeletal * 60) + 60);
        Assert.Equal(RemainsStage.Skeletal, Remains.StageOf(w, dead));
        Assert.True(w.Agents.ContainsKey(npc));
        Sim.Advance(w, (int)(Remains.ScatterHours * 60) + 60);
        Assert.False(w.Agents.ContainsKey(npc), "scattered: the person has left the world's agents");
        var bones = w.Tiles[camp].Carcasses!.Single(c => c.Animal == "Human");
        Assert.Equal(cause, bones.Cause);
        Assert.Equal(RemainsStage.Bones, Remains.StageOf(w, camp, bones));
        Assert.Contains(w.Tiles[camp].Pile!.Gear, g => g.Kind == "stone_axe"); // what they carried lies beside them
        Assert.Equal(npc, w.Tiles[camp].PileCause!.Value.Who);
    }

    /// Old bones can weather away while someone works on them: the work stops, nothing breaks.
    [Fact]
    public void Bones_WeatheringAwayUnderTheKnife_EndTheWork()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        Items.Pack(w, P).Gear.Add(Items.Make("flint_knife"));
        var bones = Carcasses.Bones(w, camp, "Bison", 800, 60, w.Minute, new Cause(null, "a wolf pack"), Remains.GoneHours - 0.5);
        var r = Sim.Step(w, new Butcher(P, bones.Id), 180);
        Assert.Equal("there's nothing left to butcher", r.InterruptedReason);
        Assert.Null(Carcasses.Find(w, camp, bones.Id));
    }

    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void The_PlayersBody_StaysInTheWorld()
    {
        var w = Bare();
        var player = w.Agent(P);
        player.Body.BloodPct = 0;
        Body.MarkDead(player.Body, w.Minute, new Cause(null, "the cold"));
        Sim.Advance(w, 40 * 1440);
        Assert.True(w.Agents.ContainsKey(P));
    }

    /// Rot, scattering and feeding are settled on the clock: the same world however it is stepped,
    /// and through a save and load.
    [Fact]
    [Trait("Category", TestCategories.LongRun)]
    public void Remains_AreTheSame_HoweverTheClockIsStepped()
    {
        World Make()
        {
            var w = Bare();
            var camp = Map.Of(w).Camp;
            Caribou(w, camp);
            var npc = Npc.Spawn(w, Map.Step(camp, Direction.East));
            w.Agent(npc).Body.BloodPct = 0;
            Body.MarkDead(w.Agent(npc).Body, w.Minute, new Cause(null, "the cold"));
            Animals.Spawn(w, Species.Raven, Map.Step(Map.Step(camp, Direction.North), Direction.North), 4, [Map.Step(Map.Step(camp, Direction.North), Direction.North), camp]);
            return w;
        }
        var whole = Make();
        Sim.Advance(whole, 25 * 1440);
        var stepped = Make();
        Sim.Advance(stepped, 3 * 1440 + 17);
        stepped = TestWorlds.Load(Save.ToJson(stepped));
        for (int i = 0; i < 7; i++) Sim.Advance(stepped, 1000);
        Sim.Advance(stepped, 25 * 1440 - (3 * 1440 + 17) - 7000);
        Assert.Equal(Save.ToJson(whole), Save.ToJson(stepped));
    }

    // ---- the carrion flock ----

    [Fact]
    public void The_Flock_FindsFlesh_CirclesIt_AndEats()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        var tile = Map.Step(Map.Step(camp, Direction.East), Direction.East);
        var c = Caribou(w, tile);
        double meat = Carcasses.MeatKg(c);
        var start = new TileId(tile.X, tile.Y + 6);
        var flock = Animals.Spawn(w, Species.Raven, start, 5, [start, tile]);
        Assert.False(Tags.Of(w, new LocationFeature(LocationFeatureKind.Carcass, tile, c.Id)).HasFlag(Tag.Beacon));
        for (int i = 0; i < 6 * 60 && w.Agent(flock).Tile != tile; i++) Sim.Advance(w, 1);
        Assert.Equal(tile, w.Agent(flock).Tile);
        Assert.True(Tags.Of(w, new LocationFeature(LocationFeatureKind.Carcass, tile, c.Id)).HasFlag(Tag.Beacon), "circling flesh: a beacon");
        Assert.Contains(Report.Finds(w), f => f.Tile == tile && f.Kind == "carcass" && f.Text.Contains("ravens circling"));
        foreach (var raven in flock.In(w)) TestKit.SetHunger(raven, 1);
        Sim.Advance(w, 60);
        Assert.True(Carcasses.MeatKg(c) < meat, "the flock ate nothing");
    }

    [Fact]
    public void The_Flock_FollowsAPredator_AndReadsAsRavensOverIt()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        var tile = Map.Step(Map.Step(camp, Direction.East), Direction.East);
        var wolves = Animals.Spawn(w, Species.Wolf, tile, 3);
        foreach (var wolf in wolves.In(w)) Fauna.MindOf(wolf).State = HerdState.Resting;
        var start = new TileId(tile.X - 5, tile.Y);
        var flock = Animals.Spawn(w, Species.Raven, start, 4, [start, tile]);
        for (int i = 0; i < 6 * 60 && w.Agent(flock).Tile != w.Agent(wolves).Tile; i++) Sim.Advance(w, 1);
        Assert.Equal(w.Agent(wolves).Tile, w.Agent(flock).Tile);
        var sign = Signs.At(w, w.Agent(flock).Tile).Single(s => s.Kind == SignKind.RavensOver);
        Assert.Equal(wolves, sign.Cause!.Value.Who);
        Assert.Equal(LocationFeatures.OfAgent(w, flock), sign.LocationFeature);
        Assert.Contains(Report.Finds(w), f => f.Kind == "ravens" && f.Category == FindCategory.Sign);
    }

    [Fact]
    public void The_Flock_RaidsAnUnwatchedPile_ThroughAccess()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        var tile = Map.Step(camp, Direction.West);
        Items.Add(Items.PileAt(w, tile, new Cause(P, "put down by the player")), Resource.RawMeat, 5);
        var flock = Animals.Spawn(w, Species.Raven, tile, 4, [tile]);
        foreach (var raven in flock.In(w)) TestKit.SetHunger(raven, 0.2); // not hungry enough to forage: only the pile tempts it
        Assert.Null(Fauna.Refusal(w, new Raid(flock)));
        w.Agent(P).Tile = tile;
        Assert.Equal("someone is keeping it", Fauna.Refusal(w, new Raid(flock))?.Reason);
        w.Agent(P).Tile = camp;
        Animals.Spawn(w, Species.Wolf, tile, 2);
        Assert.Null(Fauna.Refusal(w, new Raid(flock))); // the guard gate is not wired
        w.AccessGates = Access.Gate.All;
        Assert.Equal("a wolf pack guards it", Fauna.Refusal(w, new Raid(flock))?.Reason);
        w.AccessGates = Access.Wired;
        foreach (var id in w.Agents.Values.Where(a => a.Species == Species.Wolf).Select(a => a.Id).ToList()) Agents.Remove(w, id);
        Sim.Advance(w, 5 * 1440);
        Assert.True(Items.Have(w.Tiles[tile].Pile!, Resource.RawMeat) < 5, "five days unwatched and nothing taken");
    }

    /// The flock is its own kind, not the hyena: it never guards, hunts or threatens, and no pack hunts it.
    [Fact]
    public void The_Flock_IsNeitherPredatorNorPrey()
    {
        Assert.False(Fauna.IsPredator(Species.Raven));
        Assert.False(Fauna.IsPrey(Species.Raven));
        Assert.True(Fauna.IsPredator(Species.Hyena));
        Assert.Equal(HerdBehavior.Scavenger, Fauna.BehaviorOf(Species.Hyena));
        var w = Bare();
        var camp = Map.Of(w).Camp;
        var c = Caribou(w, camp);
        Animals.Spawn(w, Species.Raven, camp, 4);
        Assert.False(Tags.Of(w, new LocationFeature(LocationFeatureKind.Carcass, camp, c.Id)).HasFlag(Tag.Occupied));
    }

    // ---- the herds' access gate ----

    [Fact]
    public void Feeding_And_Grazing_AskAccess()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        var tile = Map.Step(Map.Step(camp, Direction.East), Direction.East);
        Caribou(w, tile);
        var flock = Animals.Spawn(w, Species.Raven, tile, 3); // guards nothing, so the pack alone holds the tile
        var caribou = Animals.Spawn(w, Species.Caribou, tile, 2);
        Assert.Null(Fauna.Refusal(w, new Feed(flock)));
        Assert.Null(Fauna.Refusal(w, new Graze(caribou)));
        var wolves = Animals.Spawn(w, Species.Wolf, tile, 3);
        Assert.Null(Fauna.Refusal(w, new Feed(flock))); // unwired: the pack changes nothing yet
        Assert.Null(Fauna.Refusal(w, new Graze(caribou)));
        w.AccessGates = Access.Gate.All;
        Assert.Equal("nothing here it can reach to eat", Fauna.Refusal(w, new Feed(flock))?.Reason);
        Assert.Equal("a wolf pack guards it", Fauna.Refusal(w, new Graze(caribou))?.Reason);
        Assert.Null(Fauna.Refusal(w, new Feed(wolves))); // the guard's own meal
        foreach (var wolf in wolves.In(w)) Fauna.MindOf(wolf).Fear = 0.9; // an afraid pack holds nothing
        Assert.Null(Fauna.Refusal(w, new Feed(flock)));
        Assert.Null(Fauna.Refusal(w, new Graze(caribou)));
    }

    /// Review: adding to a pile went through every gate and starting one through none. Putting down
    /// is one gate now: a guard bars it on someone else's pile, never on your own, begun or not.
    [Fact]
    public void Putting_Down_IsOneGate()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        TestKit.Supply(w);
        var npc = Npc.Spawn(w, camp);
        TestKit.Supply(w, npc);
        Animals.Spawn(w, Species.Wolf, camp, 3);
        w.AccessGates = Access.Gate.All;
        Assert.Null(Work.Refusal(w, new PutDown(P, Resource.Pine, 1))); // a new pile: the player's own
        Sim.Step(w, new PutDown(P, Resource.Pine, 1), 1);
        Assert.Null(Work.Refusal(w, new PutDown(P, Resource.Pine, 1))); // still the player's own
        Assert.Equal("a wolf pack guards it", Work.Refusal(w, new PutDown(npc, Resource.Pine, 1))?.Reason); // someone else's
        Assert.Equal("a wolf pack guards it", Work.Refusal(w, new PickUp(npc, Resource.Pine, 1))?.Reason);
    }

    // ---- the caches ----

    /// A tile's ground carried on from its class's cache is the ground the long way gives, sub-step for sub-step.
    [Fact]
    public void Ground_CarriedOnFromItsClass_IsTheLongWay()
    {
        var w = TestWorlds.New(17);
        var m = Map.Of(w);
        var plain = Enumerable.Range(5, m.Width - 10).SelectMany(x => Enumerable.Range(5, m.Height - 10).Select(y => new TileId(x, y)))
            .Where(t => m.TerrainAt(t) == Terrain.Plain && m.SiteAt(t) == null && m.CaveAt(t) == Cave.None && t != m.Camp).Take(2).ToList();
        static string Json(GroundState g) => System.Text.Json.JsonSerializer.Serialize(g);
        w.Minute = 4 * 1440 + 7;
        Ground.At(w, plain[0]); // warms the class
        w.Minute = 9 * 1440 + 3;
        var fresh = TestWorlds.New(17);
        fresh.Minute = w.Minute;
        Assert.Equal(Json(Ground.At(fresh, plain[1])), Json(Ground.At(w, plain[1])));
    }

    /// The ground cache shares one state among every tile of a class (Ground.At): read
    /// warm, tile after tile, a sample from across the map is the ground each tile gives on its own
    /// with the cache thrown away. A per-tile input the class leaves out (a tile's own weather) would
    /// hand a tile another's ground here, and this fails.
    [Fact]
    public void Ground_FromTheCache_IsEachTilesOwn_AcrossTheMap()
    {
        var w = TestWorlds.New(17);
        var m = Map.Of(w);
        static string Json(GroundState g) => System.Text.Json.JsonSerializer.Serialize(g);
        foreach (int day in new[] { 3, 40, 150 })
        {
            w.Minute = day * 1440L + 7 * day;
            var sample = Enumerable.Range(0, m.Width * m.Height).Where(i => i % 101 == day % 101)
                .Select(i => new TileId(i / m.Height, i % m.Height))
                .Where(t => m.TerrainAt(t).IsPassable()).ToList();
            Assert.True(sample.Count > 40, $"{sample.Count} tiles sampled");
            w.GroundCache = null;
            var warm = sample.Select(t => Json(Ground.At(w, t))).ToList();
            for (int i = 0; i < sample.Count; i++)
            {
                w.GroundCache = null;
                Assert.True(warm[i] == Json(Ground.At(w, sample[i])), $"day {day}: {sample[i]} read another tile's ground from the cache");
            }
        }
    }

    /// A bench's NPC has the same id whatever generation populated (NpcBench), and the herds born
    /// after it come after it.
    [Fact]
    public void A_BenchNpc_KeepsItsId_WhateverTheValleyHolds()
    {
        var a = TestWorlds.New(17);
        var b = TestWorlds.New(5);
        Assert.NotEqual(a.Agents.Count, b.Agents.Count);
        Assert.Equal(new AgentId(NpcBench.FirstId), NpcBench.Spawn(a, Map.Of(a).Camp));
        Assert.Equal(new AgentId(NpcBench.FirstId), NpcBench.Spawn(b, Map.Of(b).Camp));
        Assert.Equal(new AgentId(NpcBench.FirstId + 1), NpcBench.Spawn(b, Map.Of(b).Camp));
        Assert.True(Agents.Spawn(b, Species.Caribou, Map.Of(b).Camp).CompareTo(new AgentId(NpcBench.FirstId + 1)) > 0);
    }

    /// The Nearby index answers as a walk over every agent would, as herds walk, are born and die.
    [Fact]
    public void Nearby_IsExact_AsAgentsMove()
    {
        var w = TestWorlds.New(17);
        var m = Map.Of(w);
        void Check()
        {
            foreach (var a in w.Agents.Values)
            {
                var truth = w.Agents.Values.Where(b => b.Tile == a.Tile).Select(b => b.Id).ToList();
                Assert.Equal(truth, Nearby.At(w, a.Tile).Select(b => b.Id));
                var within = w.Agents.Values.Where(b => MapGen.Manhattan(b.Tile, a.Tile) <= 6).Select(b => b.Id).ToList();
                Assert.Equal(within, Nearby.Within(w, a.Tile, 6).Select(b => b.Id).ToList());
            }
        }
        for (int i = 0; i < 40; i++)
        {
            Sim.Advance(w, 37);
            Check();
        }
        var spawned = Animals.Spawn(w, Species.Caribou, m.Camp, 2);
        Check();
        w.Agent(spawned).Tile = Map.Step(m.Camp, Direction.South); // set by hand, as tests do
        Check();
        Agents.Remove(w, spawned);
        Check();
    }
}

[Trait("Category", TestCategories.Slow)]
public class ArrivalsSeed123Tests(ITestOutputHelper output)
{
    /// A valley with mammoths, all taken out.
    [Fact] public void Herds_ArriveWhereTheValleyIsEmpty_SizedToTheirLand_OutOfSight() => RenewalTests.ArrivalsRun(123, output);
}

[Trait("Category", TestCategories.Slow)]
public class ArrivalsSeed17Tests(ITestOutputHelper output)
{
    [Fact] public void Herds_ArriveWhereTheValleyIsEmpty_SizedToTheirLand_OutOfSight() => RenewalTests.ArrivalsRun(17, output);
}

[Trait("Category", TestCategories.Slow)]
public class ArrivalsSeed42Tests(ITestOutputHelper output)
{
    [Fact] public void Herds_ArriveWhereTheValleyIsEmpty_SizedToTheirLand_OutOfSight() => RenewalTests.ArrivalsRun(42, output);
}
