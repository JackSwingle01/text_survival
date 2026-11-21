using Survival;
using Xunit;

/// Emergent-world build step 3, part A (the kill chain): stable ids, who killed what, the body and
/// carcass pipeline, herd renewal, the carrion flock, the herds' access gate, and the caches that
/// make whole-map passes cheap without changing a result.
public class KillChainTests
{
    static readonly AgentId P = AgentId.Player;

    /// Seed 17 with nothing else alive in it but the player, in a fixed sky (41 °F: rot at its base rate).
    static World Bare(double tempC = 5, int seed = 17)
    {
        var w = World.New(seed);
        w.FixedWeather = new(0, WeatherCondition.Cloudy, tempC, 0.1, 0, 0.8, WindDirection.North);
        foreach (var id in w.Agents.Keys.Where(id => id != P).ToList()) Agents.Remove(w, id);
        return w;
    }

    static Carcass Caribou(World w, TileId tile, Cause? cause = null) =>
        Carcasses.Create(w, tile, "Caribou", SpeciesTable.NewBody(Species.Caribou, w.Rng("test")), w.Minute, cause: cause);

    static long Days(double d) => (long)(d * 1440);

    // ---- stable ids ----

    [Fact]
    public void A_Stash_BegunAgain_IsANewThing()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        Items.Add(Items.StashAt(w, camp, new Cause(P, "stashed by the player")), Resource.Pine, 1);
        var first = Things.StashAt(w, camp)!.Value;
        Assert.Equal(1, Items.TakeFromStash(w, camp, Resource.Pine, 1));
        Assert.False(Things.Exists(w, first));
        Items.Add(Items.StashAt(w, camp, new Cause(P, "stashed by the player")), Resource.Oak, 1);
        var second = Things.StashAt(w, camp)!.Value;
        Assert.NotEqual(first, second);
        Assert.False(Things.Exists(w, first), "a held ref to the old stash never names the new one");
        var loaded = Save.FromJson(Save.ToJson(w));
        Assert.Equal(second, Things.StashAt(loaded, camp));
        Assert.Equal(w.NextStashId, loaded.NextStashId);
    }

    [Fact]
    public void A_Shelter_PlacedAgain_IsANewThing()
    {
        var w = Bare();
        var tile = Map.Step(Map.Of(w).Camp, Direction.East);
        Shelters.Place(w, tile, new ShelterDef("Snow shelter", ShelterKind.SnowShelter));
        var first = Things.ShelterAt(w, tile)!.Value;
        Shelters.Remove(w, tile);
        Shelters.Place(w, tile, new ShelterDef("Snow shelter", ShelterKind.SnowShelter));
        var second = Things.ShelterAt(w, tile)!.Value;
        Assert.NotEqual(first, second);
        Assert.False(Things.Exists(w, first));
        Assert.True(Things.Exists(w, second));
        Assert.Equal(second, Things.ShelterAt(Save.FromJson(Save.ToJson(w)), tile));
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
            var caribou = Agents.Spawn(w, Species.Caribou, tile, 1);
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
            var caribou = Agents.Spawn(w, Species.Caribou, tile, 1);
            var npc = Npc.Spawn(w, tile);
            var e = Combat.Create(w, [new Fighter(P), new Fighter(npc)], [new Fighter(caribou)], tile, 1, Awareness.Engaged, Awareness.Unaware);
            return (e, caribou, Combat.UnitOf(e, npc), CombatAction.Attack);
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
            var caribou = Agents.Spawn(w, Species.Caribou, tile, 1);
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
        cold.CoreTempF = 80;
        Assert.Equal("the cold", Body.CauseOfDeath(cold).What);
        var starved = BodyState.BaselineHuman();
        starved.CaloriesKcal = 0;
        Assert.Equal("starvation", Body.CauseOfDeath(starved).What);
    }

    // ---- the body and carcass pipeline ----

    static RemainsStage StageAt(World w, TileId tile, int id) =>
        Carcasses.Find(w, tile, id) is { } c ? Remains.StageOf(w, tile, c) : RemainsStage.Gone;

    [Fact]
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
        var sign = Signs.At(w, camp).Single(s => s.Thing.Kind == ThingKind.Carcass);
        Assert.Equal(SignKind.Bones, sign.Kind);
        Assert.Equal(cause, sign.Cause);
        Assert.Contains(Report.Finds(w), f => f.Kind == "bones" && f.Tile == camp && f.Category == FindCategory.DeadMatter);
        Assert.False(Tags.Of(w, new ThingRef(ThingKind.Carcass, camp, c.Id)).HasFlag(Tag.Edible));
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
        Assert.True(Tags.Of(w, Things.OfAgent(w, npc)).HasFlag(Tag.ScentSource));
        Sim.Advance(w, (int)(skeletal * 60) + 60);
        Assert.Equal(RemainsStage.Skeletal, Remains.StageOf(w, dead));
        Assert.True(w.Agents.ContainsKey(npc));
        Sim.Advance(w, (int)(Remains.ScatterHours * 60) + 60);
        Assert.False(w.Agents.ContainsKey(npc), "scattered: the person has left the world's agents");
        var bones = w.Tiles[camp].Carcasses!.Single(c => c.Animal == "Human");
        Assert.Equal(cause, bones.Cause);
        Assert.Equal(RemainsStage.Bones, Remains.StageOf(w, camp, bones));
        Assert.Contains(w.Tiles[camp].Stash!.Gear, g => g.Kind == "stone_axe"); // what they carried lies beside them
        Assert.Equal(npc, w.Tiles[camp].StashCause!.Value.Who);
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
            Agents.Spawn(w, Species.Raven, Map.Step(Map.Step(camp, Direction.North), Direction.North), 4, [Map.Step(Map.Step(camp, Direction.North), Direction.North), camp]);
            return w;
        }
        var whole = Make();
        Sim.Advance(whole, 25 * 1440);
        var stepped = Make();
        Sim.Advance(stepped, 3 * 1440 + 17);
        stepped = Save.FromJson(Save.ToJson(stepped));
        for (int i = 0; i < 7; i++) Sim.Advance(stepped, 1000);
        Sim.Advance(stepped, 25 * 1440 - (3 * 1440 + 17) - 7000);
        Assert.Equal(Save.ToJson(whole), Save.ToJson(stepped));
    }

    // ---- renewal ----

    /// Day-of-year to world minute, at the hour the clock starts (09:00).
    static long MinuteOfDayOfYear(int dayOfYear) => (dayOfYear - 182 + 365) % 365 * 1440L;

    static AgentId CaribouHerd(World w, int members)
    {
        var land = Fauna.MindOf(World.New(17).Agents.Values.First(a => a.Species == Species.Caribou)).Territory;
        return Agents.Spawn(w, Species.Caribou, land[0], members, [.. land]);
    }

    [Fact]
    public void Prey_CalveInTheirSeason_TowardWhatTheLandCarries()
    {
        var w = Bare();
        w.Minute = MinuteOfDayOfYear(140);
        var herd = CaribouHerd(w, 3);
        double capacity = Fauna.CapacityOf(w, w.Agent(herd));
        Assert.True(capacity > 3, $"capacity {capacity:0.0}");
        for (int d = 0; d < 40; d++)
        {
            Sim.Advance(w, 1440);
            Assert.InRange(w.Agent(herd).Members.Count, 3, Math.Max(3, (int)Math.Floor(capacity)));
        }
        Assert.True(w.Agent(herd).Members.Count > 3, "nothing born in forty days of the calving season");

        var off = Bare();
        off.Minute = MinuteOfDayOfYear(250);
        var idle = CaribouHerd(off, 3);
        Sim.Advance(off, 40 * 1440);
        Assert.Equal(3, off.Agent(idle).Members.Count);
    }

    [Fact]
    public void A_HerdAtCapacity_HasNoYoung()
    {
        var w = Bare();
        w.Minute = MinuteOfDayOfYear(140);
        var land = Fauna.MindOf(World.New(17).Agents.Values.First(a => a.Species == Species.Caribou)).Territory;
        var probe = Agents.Spawn(w, Species.Caribou, land[0], 1, [.. land]);
        int full = (int)Math.Ceiling(Fauna.CapacityOf(w, w.Agent(probe)));
        Agents.Remove(w, probe);
        var herd = Agents.Spawn(w, Species.Caribou, land[0], full, [.. land]);
        Sim.Advance(w, 30 * 1440);
        Assert.True(w.Agent(herd).Members.Count <= full);
    }

    [Fact]
    public void A_PackLongUnfed_Starves()
    {
        var w = Bare();
        var land = Fauna.MindOf(World.New(17).Agents.Values.First(a => a.Species == Species.Wolf)).Territory;
        var wolves = Agents.Spawn(w, Species.Wolf, land[0], 4, [.. land]);
        Fauna.MindOf(w.Agent(wolves)).LastFedMinute = w.Minute - Days(30);
        Sim.Advance(w, 20 * 1440);
        Assert.True(!w.Agents.ContainsKey(wolves) || w.Agent(wolves).Members.Count < 4, "a pack thirty days unfed lost no one");
        Assert.Contains(w.Tiles.Values.SelectMany(t => t.Carcasses ?? []), c => c.Animal == "Wolf" && c.Cause?.What == "starvation");
    }

    [Fact]
    public void Herds_ArriveWhereTheValleyIsEmpty_ButNeverMegafauna()
    {
        var w = Bare(seed: 123); // a valley with mammoths, all taken out
        Assert.True(Fauna.ValleyCapacity(w, Species.Caribou) > 0);
        Assert.Equal(0, Fauna.ValleyCapacity(w, Species.Mammoth));
        Sim.Advance(w, 120 * 1440);
        var herds = w.Agents.Values.Where(a => a.Mind is HerdMind).ToList();
        Assert.Contains(herds, h => h.Species == Species.Caribou);
        Assert.DoesNotContain(herds, h => Fauna.IsMegafauna(h.Species));
        var camp = Map.Of(w).Camp;
        // Arrivals come in outside the safe zone; where they have walked since is their own business.
        Assert.DoesNotContain("apacity", Save.ToJson(w)); // capacity is derived, never saved
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
        var flock = Agents.Spawn(w, Species.Raven, start, 5, [start, tile]);
        Assert.False(Tags.Of(w, new ThingRef(ThingKind.Carcass, tile, c.Id)).HasFlag(Tag.Beacon));
        for (int i = 0; i < 6 * 60 && w.Agent(flock).Tile != tile; i++) Sim.Advance(w, 1);
        Assert.Equal(tile, w.Agent(flock).Tile);
        Assert.True(Tags.Of(w, new ThingRef(ThingKind.Carcass, tile, c.Id)).HasFlag(Tag.Beacon), "circling flesh: a beacon");
        Assert.Contains(Report.Finds(w), f => f.Tile == tile && f.Kind == "carcass" && f.Text.Contains("ravens circling"));
        Fauna.MindOf(w.Agent(flock)).Hunger = 1;
        Sim.Advance(w, 60);
        Assert.True(Carcasses.MeatKg(c) < meat, "the flock ate nothing");
    }

    [Fact]
    public void The_Flock_FollowsAPredator_AndReadsAsRavensOverIt()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        var tile = Map.Step(Map.Step(camp, Direction.East), Direction.East);
        var wolves = Agents.Spawn(w, Species.Wolf, tile, 3);
        Fauna.MindOf(w.Agent(wolves)).State = HerdState.Resting;
        var start = new TileId(tile.X - 5, tile.Y);
        var flock = Agents.Spawn(w, Species.Raven, start, 4, [start, tile]);
        for (int i = 0; i < 6 * 60 && w.Agent(flock).Tile != w.Agent(wolves).Tile; i++) Sim.Advance(w, 1);
        Assert.Equal(w.Agent(wolves).Tile, w.Agent(flock).Tile);
        var sign = Signs.At(w, w.Agent(flock).Tile).Single(s => s.Kind == SignKind.RavensOver);
        Assert.Equal(wolves, sign.Cause!.Value.Who);
        Assert.Equal(Things.OfAgent(w, flock), sign.Thing);
        Assert.Contains(Report.Finds(w), f => f.Kind == "ravens" && f.Category == FindCategory.Sign);
    }

    [Fact]
    public void The_Flock_RaidsAnUnwatchedStash_ThroughAccess()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        var tile = Map.Step(camp, Direction.West);
        Items.Add(Items.StashAt(w, tile, new Cause(P, "stashed by the player")), Resource.RawMeat, 5);
        var flock = Agents.Spawn(w, Species.Raven, tile, 4, [tile]);
        Fauna.MindOf(w.Agent(flock)).Hunger = 0.2; // not hungry enough to forage: only the stash tempts it
        Assert.Null(Fauna.Refusal(w, new Raid(flock)));
        w.Agent(P).Tile = tile;
        Assert.Equal("someone is keeping it", Fauna.Refusal(w, new Raid(flock)));
        w.Agent(P).Tile = camp;
        Agents.Spawn(w, Species.Wolf, tile, 2);
        Assert.Null(Fauna.Refusal(w, new Raid(flock))); // the guard gate is not wired
        w.AccessGates = Access.Gate.All;
        Assert.Equal("a wolf pack guards it", Fauna.Refusal(w, new Raid(flock)));
        w.AccessGates = Access.Wired;
        foreach (var id in w.Agents.Values.Where(a => a.Species == Species.Wolf).Select(a => a.Id).ToList()) Agents.Remove(w, id);
        Sim.Advance(w, 5 * 1440);
        Assert.True(Items.Have(w.Tiles[tile].Stash!, Resource.RawMeat) < 5, "five days unwatched and nothing taken");
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
        Agents.Spawn(w, Species.Raven, camp, 4);
        Assert.False(Tags.Of(w, new ThingRef(ThingKind.Carcass, camp, c.Id)).HasFlag(Tag.Guarded));
    }

    // ---- the herds' access gate ----

    [Fact]
    public void Feeding_And_Grazing_AskAccess()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        var tile = Map.Step(Map.Step(camp, Direction.East), Direction.East);
        Caribou(w, tile);
        var flock = Agents.Spawn(w, Species.Raven, tile, 3); // guards nothing, so the pack alone holds the tile
        var caribou = Agents.Spawn(w, Species.Caribou, tile, 2);
        Assert.Null(Fauna.Refusal(w, new Feed(flock)));
        Assert.Null(Fauna.Refusal(w, new Graze(caribou)));
        var wolves = Agents.Spawn(w, Species.Wolf, tile, 3);
        Assert.Null(Fauna.Refusal(w, new Feed(flock))); // unwired: the pack changes nothing yet
        Assert.Null(Fauna.Refusal(w, new Graze(caribou)));
        w.AccessGates = Access.Gate.All;
        Assert.Equal("nothing here it can reach to eat", Fauna.Refusal(w, new Feed(flock)));
        Assert.Equal("a wolf pack guards it", Fauna.Refusal(w, new Graze(caribou)));
        Assert.Null(Fauna.Refusal(w, new Feed(wolves))); // the guard's own meal
        Fauna.MindOf(w.Agent(wolves)).Fear = 0.9; // an afraid pack holds nothing
        Assert.Null(Fauna.Refusal(w, new Feed(flock)));
        Assert.Null(Fauna.Refusal(w, new Graze(caribou)));
    }

    /// Review: adding to a stash went through every gate and starting one through none. Putting down
    /// is one gate now: a guard bars it on someone else's pile, never on your own, begun or not.
    [Fact]
    public void Putting_Down_IsOneGate()
    {
        var w = Bare();
        var camp = Map.Of(w).Camp;
        TestKit.Supply(w);
        var npc = Npc.Spawn(w, camp);
        TestKit.Supply(w, npc);
        Agents.Spawn(w, Species.Wolf, camp, 3);
        w.AccessGates = Access.Gate.All;
        Assert.Null(Work.Refusal(w, new Stash(P, Resource.Pine, 1))); // a new pile: the player's own
        Sim.Step(w, new Stash(P, Resource.Pine, 1), 1);
        Assert.Null(Work.Refusal(w, new Stash(P, Resource.Pine, 1))); // still the player's own
        Assert.Equal("a wolf pack guards it", Work.Refusal(w, new Stash(npc, Resource.Pine, 1))); // someone else's
        Assert.Equal("a wolf pack guards it", Work.Refusal(w, new Unstash(npc, Resource.Pine, 1)));
    }

    // ---- the caches ----

    /// A never-observed tile's ground carried on from its class's cache is the ground the long way
    /// gives, sub-step for sub-step, through Peek and through CatchUp.
    [Fact]
    public void Ground_CarriedOnFromItsClass_IsTheLongWay()
    {
        var w = World.New(17);
        var m = Map.Of(w);
        var plain = Enumerable.Range(5, m.Width - 10).SelectMany(x => Enumerable.Range(5, m.Height - 10).Select(y => new TileId(x, y)))
            .Where(t => m.TerrainAt(t) == Terrain.Plain && m.SiteAt(t) == null && m.CaveAt(t) == Cave.None && t != m.Camp
                && w.Tiles.GetValueOrDefault(t)?.Ground == null).Take(3).ToList();
        w.Minute = 4 * 1440 + 7;
        Ground.Peek(w, plain[0]); // warms the class
        w.Minute = 9 * 1440 + 3;
        var carried = Save.ToJson(new World { Seed = 17, Tiles = new() { [plain[1]] = new TileState { Ground = Ground.Peek(w, plain[1]) } } });
        var fresh = World.New(17);
        fresh.Minute = w.Minute;
        var longWay = Save.ToJson(new World { Seed = 17, Tiles = new() { [plain[1]] = new TileState { Ground = Ground.Peek(fresh, plain[1]) } } });
        Assert.Equal(longWay, carried);
        Ground.CatchUp(w, plain[2]);
        Ground.CatchUp(fresh, plain[2]);
        static string Json(GroundState g) => System.Text.Json.JsonSerializer.Serialize(g);
        Assert.Equal(Json(fresh.Tiles[plain[2]].Ground!), Json(w.Tiles[plain[2]].Ground!));
    }

    /// The Nearby index answers as a walk over every agent would, as herds walk, are born and die.
    [Fact]
    public void Nearby_IsExact_AsAgentsMove()
    {
        var w = World.New(17);
        var m = Map.Of(w);
        void Check()
        {
            foreach (var a in w.Agents.Values)
            {
                var truth = w.Agents.Values.Where(b => b.Tile == a.Tile).Select(b => b.Id).ToList();
                Assert.Equal(truth, Nearby.At(w, a.Tile));
                var within = w.Agents.Values.Where(b => MapGen.Manhattan(b.Tile, a.Tile) <= 6).Select(b => b.Id).ToList();
                Assert.Equal(within, Nearby.Within(w, a.Tile, 6).Select(b => b.Id).ToList());
            }
        }
        for (int i = 0; i < 40; i++)
        {
            Sim.Advance(w, 37);
            Check();
        }
        var spawned = Agents.Spawn(w, Species.Caribou, m.Camp, 2);
        Check();
        w.Agent(spawned).Tile = Map.Step(m.Camp, Direction.South); // set by hand, as tests do
        Check();
        Agents.Remove(w, spawned);
        Check();
    }
}
