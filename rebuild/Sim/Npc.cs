using Plan = (Survival.Activity Activity, int Minutes);
using static Survival.Tiers;

namespace Survival;

/// An NPC's own state. Everything it acts on is in the same pack and knowledge the player has.
public sealed partial record NpcMind
{
    /// Home: where it keeps its fire and its stash, and goes back to at night. It moves to the nearest
    /// standing timber it knows of when there is none within two tiles of it (Npc.Wood).
    public TileId Camp { get; set; }
    /// Old Personality.Boldness, 0-1: how far it ranges, whether it stands and fights, whether it helps.
    /// Selfishness and Sociability are not ported: nothing they fed (following, sharing) exists yet.
    public double Boldness { get; init; } = 0.5;
    /// The stat it is seeing to (old CurrentNeed): held at Need until it is back over its satisfied line, not
    /// just until it is out of Need, so the NPC does not live pinned at the line (Decide's hysteresis).
    public Stat? SeeingTo { get; set; }
    /// Where it is walking to look around, so a search keeps one direction from step to step.
    public TileId? Heading { get; set; }
    /// The ground it is walking to forage, kept from step to step until it gets there or something
    /// clearly better turns up (Npc.ForageTarget), so it does not turn back at every tile.
    public TileId? ForageGoal { get; set; }
    /// The trees or woodpile a wood trip set out for, kept from step to step while the trip budget still covers going
    /// on (Npc.Wood), and what it picks up there before turning home (Npc.GoHome). Old (this rebuild): the trip was
    /// picked afresh at every step, so one tile out something else won and it walked home with nothing done, and out
    /// again - up to 520 such reversals a seed in 60 days.
    public TileId? WoodGoal { get; set; }
    /// Old RelationshipMemory reduced to an opinion per agent, -1..1; strangers are 0.
    // Nothing writes this yet; the WorldEvent stream will feed it (plan decision 4) when consumers subscribe.
    public SortedDictionary<AgentId, double> Opinions { get; init; } = [];
    /// The fat this body has got used to carrying (Npc.AdaptTargetFat): a running average of its own
    /// fat, never above baseline and never below the floor where leanness starts to cost speed and warmth.
    /// Falling short of it is what makes an NPC go looking for food.
    public double TargetFatKg { get; set; }
    /// The world minute TargetFatKg was last brought up to.
    public long TargetFatMinute { get; set; }
    /// The last predator it saw close: where, and when. Fresh for Npc.ThreatMemoryMinutes, it keeps the
    /// NPC running until it is FleeTiles clear and keeps its walks from passing next to the thing.
    public TileId? ThreatTile { get; set; }
    public long ThreatMinute { get; set; }
}

/// The NPC utility AI (phase 3d): picks an NPC's next activity, the same activities the player picks.
/// Port of old NPC.cs: the need hierarchy, fire keeping, resource memory, fight or flight.
public static partial class Npc
{
    // ---- spawning ----

    /// A person with an NpcMind: the player's body and clothes, the player's starting pack, plus an
    /// axe and a knife. Camp defaults to where it stands.
    // The axe and knife stand in for the old NPC's crafting (it made them in its first hours); the kit
    // shrinks back to the player's when crafting is ported.
    public static AgentId Spawn(World world, TileId tile, TileId? camp = null, double? boldness = null)
    {
        var id = new AgentId(world.NextAgentId++);
        if (world.Agents.ContainsKey(id)) throw new InvalidOperationException($"Agent id {id} is already taken");
        var pack = Items.StartingPack();
        pack.Gear.Add(Items.Make("stone_axe"));
        pack.Gear.Add(Items.Make("flint_knife"));
        var mind = new NpcMind
        {
            Pack = pack,
            Camp = camp ?? tile,
            // Old NPCFactory: boldness 0.3-0.7.
            Boldness = boldness ?? 0.3 + world.Birth("npc", id).NextDouble() * 0.4,
        };
        world.Agents[id] = new Agent { Id = id, Tile = tile, Mind = mind, Members = [SpeciesTable.NewBody(Species.Human, world.Birth("person", id))] };
        mind.TargetFatMinute = world.Minute;
        mind.TargetFatKg = Math.Clamp(world.Agents[id].Body.FatKg, FatFloorKg(world.Agents[id].Body), Body.BaselineFatPct * world.Agents[id].Body.WeightKg);
        return id;
    }

    /// Brings TargetFatKg up to this minute: an exponential average of the body's fat with a
    /// TargetFatHalfLifeDays half-life, clamped to [floor, baseline]. Decay is 2^(-minutes/half-life),
    /// so one call over N minutes is N calls over one (for the same fat), whenever Decide happens to run.
    public static void AdaptTargetFat(NpcMind mind, BodyState body, long minute)
    {
        long minutes = minute - mind.TargetFatMinute;
        if (minutes < 0) throw new InvalidOperationException($"TargetFatKg is at minute {mind.TargetFatMinute}, ahead of {minute}");
        double keep = Math.Pow(0.5, minutes / (TargetFatHalfLifeDays * 1440));
        mind.TargetFatKg = Math.Clamp(body.FatKg + (mind.TargetFatKg - body.FatKg) * keep, FatFloorKg(body), Body.BaselineFatPct * body.WeightKg);
        mind.TargetFatMinute = minute;
    }

    // ---- the decision ----

    /// The next activity for this NPC and how many minutes to give it. Called by the scheduler when
    /// the NPC's previous activity ended or was interrupted. Fights it picks resolve here, headless,
    /// and their events are added to events.
    public static (Activity Activity, int Minutes) Decide(World world, Agent agent, List<WorldEvent> events)
    {
        var mind = agent.Mind as NpcMind ?? throw new ArgumentException($"{agent.Id} has no NPC mind", nameof(agent));
        if (!Body.IsAlive(agent.Body)) throw new InvalidOperationException($"{agent.Id} is dead and cannot decide");
        var me = new Me(world, agent, mind, events);
        // Old ResourceMemory.RememberLocation: every tile it stands on, and what it can see, in the
        // shared memory the player's map is drawn from. Depleted tiles stay, as "nothing here".
        Memories.Observe(world, agent, events);
        AdaptTargetFat(mind, agent.Body, world.Minute);

        // In a fight someone else is running (the player's): its rounds move this NPC, not its own steps.
        if (Combat.EncounterOf(world, agent.Id) is Encounter fight && fight.Units.Any(u => u.Agent == agent.Id && u.InField))
            return (new Wait(agent.Id), 1);

        var b = agent.Body;
        // Hysteresis: the stat it is seeing to stays at Need until it is back over its satisfied line.
        if (mind.SeeingTo is Stat seeing && Satisfied(me, seeing)) mind.SeeingTo = null;
        // Away from the fire it judges the core it will have when it gets home: the walk back is cold too
        // (old: a fixed 97.5F away, so a two-tile trip through deep snow at -15C came home hypothermic - bug, fixed).
        double coreAtHomeF = CoreAtHomeF(me);
        // Tonight's wood is reckoned at every hour: the night, when the fire runs out, is when it matters most. In the
        // dark, Wood answers it with what works in the dark (Me.Lit). Old (this rebuild): only 05:00-19:00, so in winter
        // the dark hours either side counted fuel and the night itself had no fuel tier at all.
        double fuelHours = CampFuelHours(me), tonightHours = HoursToDawn(me) + MorningFireHours;
        // A wood trip's goal is for the trip out: home, it is over.
        if (me.AtCamp) mind.WoodGoal = null;
        var cold = CoreTier(coreAtHomeF, mind.SeeingTo == Stat.Warmth);
        var warmth = WarmthTier(coreAtHomeF, mind.SeeingTo == Stat.Warmth, fuelHours, tonightHours);
        var water = WaterTier(b.HydrationPct, mind.SeeingTo == Stat.Water, WaterToHandL(me), HoursLeftToday(me));
        var food = FoodTier(b, mind.TargetFatKg, mind.SeeingTo == Stat.Food, FoodToHand(me));
        var rest = RestTier(b.EnergyPct, mind.SeeingTo == Stat.Rest);
        me.Warmth = warmth;
        bool hungry = b.CaloriesPct < HungryPct;

        // A predator close by. On its tile it is fight or flight. Otherwise a burning fire is a place to
        // hold, and a Critical stat outweighs a predator that is not coming on; short of those, it runs
        // until FleeTiles clear (old: one tile, then home again beside the bear, until it froze - bug, fixed).
        var threat = Threat(me);
        bool approaching = threat != null && (threat.Tile == me.Tile || FreshThreat(me) is not TileId was
                                              || MapGen.Manhattan(threat.Tile, me.Tile) < MapGen.Manhattan(was, me.Tile));
        if (threat != null) (mind.ThreatTile, mind.ThreatMinute) = (threat.Tile, world.Minute);
        var from = FreshThreat(me);
        // Fled from a predator that has settled by an unlit camp: home is where it fled to.
        if (from is TileId f && !me.AtCamp && MapGen.Manhattan(f, mind.Camp) <= 1 && MapGen.Manhattan(f, me.Tile) >= FleeTiles
            && !CampFireBurning(me))
            mind.Camp = me.Tile;
        // Too far out to make it home before it is spent or past lasting harm, with a fire it can make here (Stranded):
        // home is here tonight, and the fire and the bed are made here (old: 27 tiles out at no energy, it gathered fuel for a camp it could not reach
        // until ten at night, and never lit any - seed 5).
        if (threat == null && Stranded(me)) mind.Camp = me.Tile;
        bool critical = new[] { warmth, water, food, rest }.Contains(Tier.Critical);
        bool pinned = me.Burning && from is TileId near && MapGen.Manhattan(near, me.Tile) <= 1;
        bool run = threat?.Tile == me.Tile
                   || (!me.Burning && from is TileId t && MapGen.Manhattan(t, me.Tile) < FleeTiles && (approaching || !critical));

        // Tier first, then the hierarchy (Rank). Three exceptions, each a moment's act on what is in hand or a
        // threat at hand, where waiting on the tier order is what kills.
        var options = new (double Rank, Stat? Sees, bool StaysOut, Func<Me, Plan?> Act)[]
        {
            // A threat at hand is its own band, over every tier.
            (run || AllyInEncounter(me) != null ? ThreatRank : 0, null, true, RespondToThreat),
            // Two minutes to drink what it carries, whatever else is Critical (old: a freezing NPC fussed with its fire
            // and died of thirst with three litres on its back - bug, fixed).
            (water == Tier.Critical && me.Has(Resource.Water) >= 0.1 ? Rank(Tier.Critical, -1) : 0, Stat.Water, true, me => Try(me, new Drink(me.Id), Work.DrinkMinutes)),
            // Ten minutes to eat what is in the pack is never the wrong time to be hungry, short of a Critical stat
            // (judgment call: the old hierarchy kept a cold NPC from eating the berries it carried).
            (hungry && FoodToHand(me) ? Rank(Tier.Need, -1) : 0, null, true, EatToHand),
            // Warmth: a cold core is seen to at the fire (Warm); a warm one short of wood fetches it (Wood). Wood fetched
            // for tonight is held to the evening, as the evening's own fetch is (StaysOut).
            (Rank(warmth, (double)Stat.Warmth), cold > Tier.None ? Stat.Warmth : null, cold > Tier.None, cold > Tier.None ? Warm : Fuel),
            (water >= Tier.Need ? Rank(water, (double)Stat.Water) : 0, Stat.Water, true, me => Water(me, critical: water == Tier.Critical)),
            // Evening and night are for being home, with the fire lit while there is light to light it by: after the
            // Needs of warmth and water, before those of food and rest (neither is seen to after dark).
            (Evening(me) ? EveningRank : 0, null, false, Night),
            (food >= Tier.Need ? Rank(food, (double)Stat.Food) : 0, Stat.Food, food == Tier.Critical, me => Food(me, starving: food == Tier.Critical)),
            (rest >= Tier.Need ? Rank(rest, (double)Stat.Rest) : 0, Stat.Rest, rest == Tier.Critical, Tired),
            // The Wants of water, food and rest: a day's water carried, food and fuel stockpiled, the rest topped up at night.
            (Rank(Tier.Want, (double)Stat.Rest + 1), null, false, Errands),
        };
        // OrderByDescending is stable: equal ranks keep the list's order. A plan only weighs things up;
        // what it means to keep (where it is bound, Me.Commits) is kept once the plan is the one started.
        foreach (var o in options.Where(o => o.Rank > 0).OrderByDescending(o => o.Rank))
        {
            me.Forget();
            me.StaysOut = o.StaysOut && o.Rank > EveningRank;
            if (o.Act(me) is not { } plan)
            {
                // Nothing it can do about the stat it is seeing to: the hold lets go, so a stat held at Need only by the
                // hysteresis does not outrank, decision after decision, what it can do (old, this rebuild: water held at
                // Need with nothing to drink and no fire to melt snow on, so it walked out for wood and was called home
                // by water's fallback, 98 times - seed 17, day 1.96). Food keeps its hold: it has no plan in the dark by
                // choice (Food), and has one again at first light.
                if (o.Sees is Stat s && s == mind.SeeingTo && s != Stat.Food) mind.SeeingTo = null;
                continue;
            }
            // Going out, the fire is banked first so it is still in when the walk home is cold (judgment
            // call: food trips range further than wood trips, and a cold NPC with a hand drill cannot relight).
            // The trip is not started, so nothing it would have kept in mind is (old: the forage ground or
            // the camp move of a trip replaced by banking the fire was kept all the same - bug, fixed).
            if (plan.Activity is Travel && me.AtCamp && me.Burning && o.Rank < ThreatRank && KeepFire(me, 3) is { } bank) { me.Forget(); plan = bank; }
            // Holding a fire with a predator next door: nothing that walks away from it.
            if (plan.Activity is Travel && pinned && o.Rank < ThreatRank) { me.Forget(); plan = KeepFire(me, 4) ?? (new Rest(me.Id), 30); }
            if (o.Sees != null) mind.SeeingTo = o.Sees;
            return me.Commit(plan);
        }
        me.Forget();
        me.StaysOut = false;
        return me.Commit(me.AtCamp ? (new Rest(agent.Id), 30) : GoHome(me) ?? (new Rest(agent.Id), 30));
    }

    // ---- the tiers: Tiers, shared with the player; the hysteresis (SeeingTo, Satisfied) is the NPC's own ----

    /// The stat being seen to is back over its satisfied line (Decide's hysteresis).
    static bool Satisfied(Me me, Stat stat) => stat switch
    {
        Stat.Warmth => CoreAtHomeF(me) >= WarmAgainCoreF,
        Stat.Water => me.Agent.Body.HydrationPct >= WaterAgainPct,
        Stat.Food => me.ShortfallKg <= SatisfiedShortfallKg && me.Agent.Body.FatKg >= FatFloorKg(me.Agent.Body),
        _ => me.Agent.Body.EnergyPct >= RestAgainPct,
    };

    /// A threat at hand: its own band, over every tier.
    const double ThreatRank = 100;
    /// The evening: in the Need tier, after warmth and water, before food and rest.
    static readonly double EveningRank = Rank(Tier.Need, 1.5);

    /// Water it can drink without going anywhere: carried, on the stash here, or open water underfoot.
    static double WaterToHandL(Me me) =>
        Work.HasOpenWater(me.World, me.Tile) ? double.PositiveInfinity : me.Has(Resource.Water) + me.Stashed(Resource.Water);

    /// Food it can eat without going for it: fit to eat now, in the pack or on the stash here - what the hungry eat
    /// (EatToHand). Old (this rebuild): any food at all, raw or not, while only the pack's was eaten, so beside roots
    /// on a stash a tile out food was no longer Need, nothing ate them, and it walked off and back, the stash and the
    /// ground next to it, thirteen times (seed 31, day 2).
    static bool FoodToHand(Me me) => ReadyFood(me, me.Pack) != null || ReadyFood(me, me.Stash) != null;

    /// Hours until midnight.
    static double HoursLeftToday(Me me) => (1440 - Weather.MinuteOfDay(me.World.Minute)) / 60.0;

    const int HomeByHour = 19;
    /// How far it runs from a predator, and how long it keeps one in mind.
    const int FleeTiles = 3;
    public const int ThreatMemoryMinutes = 120;
    const double MinLightChance = 0.2;
    /// Warmth's lines (CoreTier): Critical at the hypothermia line, where cold damage starts (any chance of a fire
    /// beats none); Need under 97.5F; warm again over WarmAgainCoreF.
    const double HypothermiaCoreF = Tiers.HypothermiaCoreF, WarmNeedCoreF = Tiers.WarmNeedCoreF, WarmAgainCoreF = Tiers.WarmAgainCoreF;
    const double DesperateLightChance = 0.02;
    /// Water's lines (WaterTier), as fractions of a full body.
    const double WaterCriticalPct = Tiers.WaterCriticalPct, WaterNeedPct = Tiers.WaterNeedPct, WaterAgainPct = Tiers.WaterAgainPct;
    /// Rest's lines (RestTier).
    const double RestCriticalPct = Tiers.RestCriticalPct, RestNeedPct = Tiers.RestNeedPct, RestAgainPct = Tiers.RestAgainPct, RestedPct = Tiers.RestedPct;
    /// Wanting to be warm, any strike is worth the tinder but one at the 5% floor.
    const double WantedLightChance = 0.06;
    /// Raw meat is eaten raw below this.
    const double RawMeatFatPct = 0.06;
    /// Below this the body starts paying for leanness (Body.Speed, Body.ColdResistance); TargetFatKg never goes under it.
    public const double FatFloorPct = Tiers.FatFloorPct;
    public const double TargetFatHalfLifeDays = 3;
    /// Food's lines (FoodTier): fat short of its target by WantedShortfallKg is Need, held to SatisfiedShortfallKg.
    /// CriticalShortfallKg is where a hunt's danger stops counting against it (HuntKcalPerHour).
    const double WantedShortfallKg = Tiers.WantedShortfallKg, CriticalShortfallKg = 1.0, SatisfiedShortfallKg = Tiers.SatisfiedShortfallKg;
    /// The stomach store below which it eats what it carries.
    const double HungryPct = Tiers.HungryPct;
    /// Wood in before dark: enough to burn until dawn plus a morning fire, then a spare half-day
    /// (old FuelStockpileTargetKg was 40 kg; 30 here before these were hours).
    const double MorningFireHours = 2, SpareFireHours = Tiers.SpareFireHours;
    /// Fuel at camp below this, a cold NPC by its fire goes for wood rather than wait on the fire.
    const double LowFuelHours = 4;
    /// On the fire before sleeping: near a full open pit, a night of birch.
    const double BankKg = 9;
    /// Kept back from the fire so there is always something to light the next one with.
    const double KindlingReserveKg = 0.6;
    /// Less kindling than this in hand and no pine, a struck tinder has nothing to catch (Light).
    const double MinLightKindlingKg = 0.1;
    /// Kept in the pack when it unloads at camp: kindling, and each kind of tinder.
    const double CarriedKindlingKg = 1.0, CarriedTinderKg = 0.3;
    /// Tinder it wants in hand, its own or waiting where it is going, setting off for makings away from a stash
    /// that has more: twenty strikes, when cold hands have the odds down at the floor (FireMakings).
    const double MakingsWalkTinderKg = 1.0;
    /// A fire burning less than this is still being built: it takes the quickest-catching fuel first.
    const double BuildUpKg = 1.5;
    /// Farther than this, timber is not worth carrying home.
    const int MaxWoodTiles = 15;
    /// How far from camp it looks for timber to move camp to, when there is none within MaxWoodTiles.
    const int TimberSearchTiles = 2 * MaxWoodTiles;

    static readonly Resource[] Woods = [Resource.Oak, Resource.Birch, Resource.Pine, Resource.Bone, Resource.Peat];
    /// Fire starters, best odds first.
    static readonly ToolType[] Starters = [ToolType.FireStriker, ToolType.BowDrill, ToolType.HandDrill];
    static readonly Resource[] Tinders = [Resource.BirchBark, Resource.Amadou, Resource.Tinder, Resource.Usnea, Resource.Chaga];

    /// Everything a plan reads, once.
    sealed class Me(World world, Agent agent, NpcMind mind, List<WorldEvent> events)
    {
        public World World { get; } = world;
        public Agent Agent { get; } = agent;
        public NpcMind Mind { get; } = mind;
        public List<WorldEvent> Events { get; } = events;
        public AgentId Id => Agent.Id;
        public TileId Tile => Agent.Tile;
        public Inventory Pack => Mind.Pack;
        public bool AtCamp => Agent.Tile == Mind.Camp;
        public int Hour => Weather.MinuteOfDay(World.Minute) / 60;
        /// The night of the rule for working without a flame (Weather.IsNight), not when the sun is down (Daylight).
        public bool Night => Weather.IsNight(World.Minute);
        /// The sun is up (Weather.IsDaytime): what "by day" means wherever the NPC plans by it.
        public bool Daylight => Weather.IsDaytime(World.Minute);
        /// Light to walk out and fetch by, and to work by when it gets there: the sun up and out of Work's night, or a
        /// torch in hand. A fire lights only its own tile, where work is judged by Work's rules (Dark). Old (this
        /// rebuild, briefly): the sun alone, so in summer it walked to the trees at four, could not fell before five,
        /// and walked home again.
        public bool Lit => Daylight && !Night || World.Fires.Values.Any(f => f.CarriedBy == Id && Fire.IsActive(f));
        /// The sky now, read once for the decision's planning (crossing times, the cooling forecast).
        public WeatherSample Sky => sky ??= Weather.At(World, World.Minute);
        WeatherSample? sky;
        /// Walking cooling by tile, worked out once a decision (Npc.TileCoolingFPerHour), and the walking surroundings it starts from.
        public Dictionary<TileId, double> CoolingByTile { get; } = [];
        public SurvivalContext? Walking { get; set; }
        /// Work.TooDarkToWork without the flame: too dark to fell or butcher unless a fire is lit here.
        public bool Dark => Night || Map.IsDark(Map.Of(World), Tile);
        /// The pit here, as last caught up. Decide has no event stream to catch it up into, so it
        /// can be a little behind; anything it gets wrong is refused at no cost and re-decided.
        public FireState? Pit => Fire.PitAt(World, Tile) is FireId f ? World.Fires[f] : null;
        public bool Burning => Pit is { } p && Fire.IsActive(p);
        public double RoomKg => Items.CapacityKg(Pack) - Items.CarriedKg(Agent);
        public Inventory? Stash => World.Tiles.GetValueOrDefault(Tile)?.Stash;
        public double Has(Resource r) => Items.Have(Pack, r);
        public double Stashed(Resource r) => Stash is { } s ? Items.Have(s, r) : 0;
        public double ShortfallKg => Mind.TargetFatKg - Agent.Body.FatKg;
        /// What the plan being weighed would have it keep in mind (where it is bound), kept only if
        /// that plan is the one started (Commit): weighing a plan up changes nothing.
        public List<Action> Commits { get; } = [];
        /// Whether the option being weighed may keep it out after it should be home (BudgetMinutes): one that
        /// outranks the evening, but for tonight's wood, which is held to the evening as the evening's own fetch is.
        public bool StaysOut { get; set; }
        /// Its warmth tier, deciding now: Critical, it takes on no long work (Try, Wood).
        public Tier Warmth { get; set; }
        public void Forget() => Commits.Clear();
        public Plan Commit(Plan plan)
        {
            foreach (var keep in Commits) keep();
            Forget();
            return plan;
        }
    }

    // ---- the plans: each returns an activity, or null when it can do nothing about its need ----

    static Plan? Warm(Me me)
    {
        // Out for the wood its fire is short of: get it while the trip budget allows (Afford: work out here is
        // cut to what it can do and still come home warm enough), then go home (Wood turns for home).
        bool fetching = me.Lit && CampFuelHours(me) < LowFuelHours;
        if (!me.AtCamp && fetching && Wood(me, warming: ColdCamp(me)) is { } wood) return wood;
        // At the end of a trip for wood or makings (WoodGoal), home with what it came for, to make the fire there.
        if (!me.AtCamp && me.Mind.WoodGoal == me.Tile && GoHome(me) is { } back) return back;
        // Home, if there is a fire to be had there: one burning, or the means to light one. Walking back to a
        // cold pit with nothing to light it is no use.
        if (!me.AtCamp && (CanLight(me) || CampFireBurning(me)) && MapGen.Manhattan(me.Tile, me.Mind.Camp) <= 12 && GoHome(me) is { } home) return home;
        // At camp, or too far to walk back cold: the fire is here.
        if (me.Burning)
            return KeepFire(me, 5)
                // The fire burning is all the fuel there is: by day, walk out for more while it lasts (old: it sat by
                // the fire until it was nearly out, then groped the bare camp ground for sticks - bug, fixed). Only
                // the walk: what Wood does when it cannot go (groping the camp ground for half an hour) is no way to
                // get warm beside a fire (old, this rebuild: four hours groping, core 96.7 to 95.8, and the fire died - seed 17).
                ?? (me.AtCamp && fetching ? WalkFor(me, Wood(me)) : null)
                ?? (Fire.TotalKg(me.Pit!) < 1.5 ? Grope(me) : null) // feed it or lose it
                ?? AtFire(me) ?? (new Rest(me.Id), 20);
        if (KeepFire(me, 3) is { } fire) return fire;
        // Nothing to light here: the makings it left somewhere near, then by day go and get some (working warms
        // you; a walk from a camp with no fire is held to the warming lines, Afford), by night grope for it here,
        // while it has the legs to (Grope).
        // Old (this rebuild): the makings were only thought of in bed, so a cold NPC groped a bare camp one
        // tile from its own stash of tinder and wood until it died (seed 11).
        if (FireMakings(me) is { } makings) return makings;
        return (me.Lit ? Wood(me, warming: ColdCamp(me)) : null) ?? Grope(me);
    }

    /// A quarter-hour groping the ground here for anything to burn, while it has the legs for it: spent (RestCriticalPct)
    /// it lies down instead, as ColdBed does. Old (this rebuild): Warm groped at any energy, ColdBed only with the legs.
    static Plan? Grope(Me me) =>
        me.Agent.Body.EnergyPct >= RestCriticalPct ? Forage(me, minutes: 15, wander: false, ForageFocus.Fuel) : null;

    /// Tonight's fuel, the core warm: wood (Wood), and from a camp with no fire to be had, first the makings it left a
    /// step or two off (FireMakings), as ColdBed goes to them before lying down.
    static Plan? Fuel(Me me) => (me.AtCamp && ColdCamp(me) ? FireMakings(me, NearMakingsTiles) : null) ?? Wood(me);

    /// The plan only if it is a walk: anything else it would have done instead is forgotten with what it would keep in mind.
    static Plan? WalkFor(Me me, Plan? plan)
    {
        if (plan is { Activity: Travel }) return plan;
        me.Forget();
        return null;
    }

    static Plan? Water(Me me, bool critical = false)
    {
        if (me.Has(Resource.Water) >= 0.1 || Work.HasOpenWater(me.World, me.Tile)) return Try(me, new Drink(me.Id), Work.DrinkMinutes);
        if (me.Stashed(Resource.Water) > 0) return Try(me, new Unstash(me.Id, Resource.Water, 1), 1);
        if (me.Burning) return Try(me, new MeltSnow(me.Id), Work.MeltSnowMinutes);
        if (me.AtCamp && KeepFire(me, 3) is { } fire) return fire;
        // Old DetermineGetResource(Water): the nearest open water it knows of.
        // Water to be had at home (WaterAtHome: some stashed, or a fire to melt snow on) keeps the walk to known water short;
        // without it, any water it knows of within a search's reach.
        bool melting = WaterAtHome(me);
        var river = me.Mind.Memory.Places.Where(p => p.Value.Water && MapGen.Manhattan(p.Key, me.Tile) <= (melting ? MaxWoodTiles : TimberSearchTiles)).Select(p => p.Key)
            .OrderBy(t => MapGen.Manhattan(me.Tile, t)).ThenBy(t => t).Cast<TileId?>().FirstOrDefault();
        // Critical, the walk to it is held only to the legs and to lasting harm, as a warming walk is (Afford): dying of
        // thirst two tiles from a stream is not saved by staying home rested. Old (this rebuild): the wanted lines, so
        // spent and cold it never set out, and died of thirst two tiles from water (seed 5, day 7).
        if (river is TileId r && Go(me, r, Work.DrinkMinutes, warming: critical) is { } go) return go;
        // Home only if there is water to be had there: some left on the stash, or a fire burning or to be lit to melt
        // snow on. Old (this rebuild): home whatever was there, so from one tile out this fallback outranked every
        // lower plan and walked it back to a camp with nothing to drink, and out, and back (seed 17, day 1.96).
        if (melting) return !me.AtCamp ? GoHome(me) : null;
        // Nothing to drink and no way to melt snow: it goes looking for water, by day, as for timber (Explore). Old (this
        // rebuild): nothing, so with no fire from day 8.5 and no water it knew of, seed 5 foraged on at a treeless camp
        // until it died of thirst (day 10.7).
        return Explore(me, TimberSearchTiles, warming: critical);
    }

    /// Water to be had at home, as far as it can tell: some on the stash as it left it, or a fire there burning or to
    /// be lit, what it carries included (CampStock), to melt snow on.
    static bool WaterAtHome(Me me) =>
        me.Mind.Memory.Places.GetValueOrDefault(me.Mind.Camp)?.Stash?.GetValueOrDefault(Resource.Water) > 0
        || CampFireBurning(me) || CanLight(me) && Makings(CampStock(me));

    static Plan? Tired(Me me) => me.AtCamp ? Bed(me)
        : GoHome(me) ?? (me.Agent.Body.EnergyPct < 0.1 ? (new Sleep(me.Id), 60) : null); // too spent to walk home: sleep where it stands

    /// Evening and night: from the hour it must be home by (HomeMinuteOfDay) until the sun is up.
    static bool Evening(Me me)
    {
        int now = Weather.MinuteOfDay(me.World.Minute);
        return now >= HomeMinuteOfDay(me) || now < Weather.SunriseHour(me.World.Minute) * 60;
    }

    /// Home for the night. Wood short of tonight is warmth's (WarmthTier, at every hour), and outranks this.
    // Old (this rebuild): its own evening fetch, while warmth's fuel tier ran 05:00-19:00 only.
    static Plan? Night(Me me)
    {
        if (!me.AtCamp) return GoHome(me);
        // Old DetermineIdle: at night, sleep unless well rested.
        if (me.Agent.Body.EnergyPct < 0.8) return Bed(me);
        return KeepFire(me, 4) ?? AtFire(me) ?? (new Rest(me.Id), 30);
    }

    /// The fire banked first (old CanSleep wanted two hours of it), then sleep in stretches, waking to feed it.
    /// With too little to bank it, the stretches are short, so it wakes while the embers still take fuel
    /// and gropes about the camp for some (old: two hours at a time beside a fire with nothing to feed it,
    /// and woke to a cold pit and a hand drill - seed 5, day 4).
    static Plan? Bed(Me me)
    {
        if (KeepFire(me, BankKg) is { } bank) return bank;
        var pit = me.Pit;
        if (pit == null || !Fire.IsActive(pit) && !pit.HasEmbers) return ColdBed(me);
        bool short_ = Fire.TotalKg(pit) < BankKg - 0.5;
        // Logs that will not go on a fire this cool yet: sit up with it until it catches, unless too spent to.
        if (short_ && Fire.IsActive(pit!) && me.Agent.Body.EnergyPct > 0.15 && Woods.Any(r => me.Has(r) + me.Stashed(r) > 0)) return (new Rest(me.Id), 15);
        if (!short_) return (new Sleep(me.Id), SleepMinutes);
        // Nothing to bank it with. Going out, or in embers: find it something while it will still take it.
        // The fire's own reckoning of how long it will burn, not the fuel on it: logs too big for a fire this
        // cool never catch (old: Fire.FuelHours, so beside a kindling fire and five kilos of oak it slept two
        // hours and woke to a cold pit and the oak unburned).
        double minutesLeft = pit!.HasEmbers ? 0 : Fire.HoursItWillBurn(pit) * 60;
        if (minutesLeft < WakeBeforeOutMinutes && Grope(me) is { } grope) return grope;
        return (new Sleep(me.Id), (int)Math.Clamp(minutesLeft - WakeBeforeOutMinutes, WakeBeforeOutMinutes, SleepMinutes));
    }

    /// How long before its fire is out a sleeper with nothing to bank it wakes: under the shortest embers (Fire.ToEmbers).
    const int WakeBeforeOutMinutes = 15;

    /// No fire here and nothing to light one (KeepFire found nothing). Where a stretch asleep here would
    /// not take its core under the freezing line - a mild night, a shelter, a bed - it sleeps as by a fire.
    /// Otherwise it does not lie still in the cold for long: it goes where it left tinder and wood if the
    /// trip budget allows (Afford; that place is home now), or fetches wood by day; failing those it
    /// sleeps only as long as the cold allows, and with too little of that to be worth lying down for,
    /// keeps moving here groping for tinder and fuel while it has the legs. Old (this rebuild): two hours
    /// beside a cold pit whatever the cold (seed 11: core 96.3 to 93.8 asleep at two in the morning, dead by four).
    // Makings close by come before any sleep, safe or not: a fire beats a cold bed. The walks out of here - to
    // makings, or by day to the trees - are warming walks (WarmingWalk, Wood's warming trip), held to the legs and
    // to lasting harm, not to the wanted lines. Old (this rebuild): makings only on a bitter night with the legs
    // to spare, and every walk held to the wanted lines, so a cold, tired NPC napped fifteen minutes at a time -
    // never enough to lift it back to the rested line - and groped a dead camp for days, a tile from its own stash
    // (seeds 11, 99, 777, 2025, 256, 5, 17).
    static Plan? ColdBed(Me me)
    {
        if (FireMakings(me, NearMakingsTiles) is { } near) return near;
        double safeMinutes = MinutesUntilCoreF(me, HypothermiaCoreF + ColdSleepBufferF, CoolingFPerHour(me, new Sleep(me.Id)));
        if (safeMinutes >= SleepMinutes) return (new Sleep(me.Id), SleepMinutes);
        if (FireMakings(me) is { } go) return go;
        if (me.Lit && Wood(me, warming: true) is { } wood) return wood;
        if (safeMinutes < MinColdSleepMinutes && Grope(me) is { } grope) return grope;
        return (new Sleep(me.Id), (int)Math.Clamp(safeMinutes, WakeBeforeOutMinutes, SleepMinutes));
    }

    /// A sleeper beside a cold pit wakes this far above the Critical line, not on it: asleep it notices nothing, and
    /// it wants hands good enough to strike with when it wakes (the old freezing line, 95.9F; judgment call).
    const double ColdSleepBufferF = 0.9;

    /// Makings this close are gone back to before lying down at a camp with no fire (ColdBed).
    const int NearMakingsTiles = 2;

    /// A night's stretch of sleep by a banked fire, or anywhere the cold allows it.
    const int SleepMinutes = 120;
    /// Less sleep than this before the cold has it at the freezing line is not worth lying down for, unless spent.
    const int MinColdSleepMinutes = 30;

    /// The nearest place within reach it remembers leaving the makings of a fire - tinder to light it and
    /// wood to keep it - and the first step there, when it has a fire starter and the walk is a warming walk
    /// it can make (WarmingWalk): that place is home once it sets out, so the walk is judged as a walk home is.
    /// Old (this rebuild): held to the whole trip budget, which refuses every walk while the core is under the
    /// wanted line or energy under the rested one - exactly when a cold NPC at a dead camp needs one (seed 11).
    // The makings are what it will have there: what it carries, what it takes off the stash here first (Provision),
    // and what it left there. Old (this rebuild): the stash there alone, so it walked off from five kilos of tinder
    // with 0.2 of its own to a woodpile holding a pinch, struck at the odds' floor until the pinch was gone, and froze
    // (seed 7, day 29).
    static Plan? FireMakings(Me me, int withinTiles = MaxWoodTiles)
    {
        if (!Starters.Any(t => Items.Tool(me.Pack, t) != null)) return null;
        // Standing on makings already, lighting is what is wanted, not other makings (old, this rebuild: it walked
        // between two stashes all night, lighting neither - seed 11).
        if (Makings(ToHand(me))) return null;
        var near = me.Mind.Memory.Places.Where(p => p.Key != me.Tile && p.Value.Stash != null && MapGen.Manhattan(p.Key, me.Tile) <= withinTiles)
            .Select(p => p.Key).OrderBy(t => MapGen.Manhattan(t, me.Tile)).ThenBy(t => t);
        bool keep = KeepCamp(me);
        foreach (var t in near)
        {
            // Not from a camp worth keeping to a bare tile: its kindling is fetched instead (Wood's round trip).
            if (keep && !Sheltered(me, t)) continue;
            // The least it may find there (AtLeast), as home is judged from away. Old (this rebuild): as remembered, so a
            // pinch remembered at the line was makings from here and not on arrival.
            var there = AtLeast(me.Mind.Memory.Places[t].Stash)!;
            // Home under a shelter, the makings a step off on a bare tile: fetched home, not moved to (old, this rebuild:
            // home moved to a woodpile one tile off the overhang whenever the overhang's stash fell short of a morning
            // fire, and back to the overhang once it was lit - 26 shelter, bare, shelter loops on the 16 seeds; seed 256,
            // day 8.69). The trip's goal is the woodpile, and the walk home picks the makings up (GoHome).
            if (me.AtCamp && Sheltered(me, me.Tile) && !Sheltered(me, t) && MapGen.Manhattan(t, me.Tile) <= FetchMakingsTiles)
            {
                var home = ToHand(me);
                if (!Makings(Lighting.ToDictionary(r => r, r => home[r] + there.GetValueOrDefault(r))) || FetchWalk(me, t) is not { } fetch) continue;
                me.Commits.Add(() => me.Mind.WoodGoal = t);
                return fetch;
            }
            var take = Provision(me, there);
            if (!Makings(Lighting.ToDictionary(r => r, r => me.Has(r) + there.GetValueOrDefault(r) + take.Where(x => x.Resource == r).Sum(x => x.Kg)))) continue;
            if (WarmingWalk(me, t) is not { } go) continue;
            // What it takes is taken before it sets out; nothing is kept in mind until it does.
            if (take.Count > 0 && TakeFromStash(me, take[0].Resource, take[0].Kg) is { } pick) return pick;
            me.Commits.Add(() => me.Mind.Camp = t);
            return go;
        }
        return null;
    }

    /// Makings this close to a sheltered home are fetched, not moved to (FireMakings).
    const int FetchMakingsTiles = 1;

    /// The first step of a walk to fetch something and carry it home: there and back with the legs left for a fire
    /// (WarmingReserveMinutes) and short of lasting harm (LethalCoreF), as a warming walk is, the walk back included.
    static Plan? FetchWalk(Me me, TileId target)
    {
        if (RouteMinutes(me, target) is not double walk) return null;
        if (me.Agent.Body.EnergyMinutes < 2 * walk + WarmingReserveMinutes
            || CoreAfterF(me, 2 * walk, TripCoolingFPerHour(me, target, walk, 0, me.Mind.Camp, walk)) < LethalCoreF) return null;
        return Step(me, target);
    }

    /// What it takes off the stash here before walking to makings elsewhere, counting what it carries and what waits
    /// there: tinder, best first, up to MakingsWalkTinderKg, and kindling up to what it carries (CarriedKindlingKg).
    static List<(Resource Resource, double Kg)> Provision(Me me, IReadOnlyDictionary<Resource, double> there)
    {
        var take = new List<(Resource Resource, double Kg)>();
        double tinder = Tinders.Sum(r => me.Has(r) + there.GetValueOrDefault(r));
        foreach (var r in Tinders)
        {
            double kg = Math.Min(me.Stashed(r), MakingsWalkTinderKg - tinder);
            if (kg < 0.05) continue;
            take.Add((r, kg));
            tinder += kg;
        }
        double kindling = Math.Min(me.Stashed(Resource.Kindling), CarriedKindlingKg - me.Has(Resource.Kindling) - there.GetValueOrDefault(Resource.Kindling));
        if (kindling >= 0.1) take.Add((Resource.Kindling, kindling));
        return take;
    }

    /// Tinder to light a fire, something a tinder fire catches to follow it (as Light asks), and MorningFireHours of
    /// fuel to keep it. Old (this rebuild): tinder and wood of any kind, so tinder and oak counted, and a tinder fire
    /// never takes oak (review probe P6).
    static bool Makings(IReadOnlyDictionary<Resource, double> s) => Lights(s) && Follows(s) && Keeps(s);

    /// Something to strike: tinder, or sticks to shave for it.
    static bool Lights(IReadOnlyDictionary<Resource, double> s) =>
        Tinders.Any(r => s.GetValueOrDefault(r) >= StrikeTinderKg) || s.GetValueOrDefault(Resource.Kindling) >= KindlingAsTinderKg;

    /// Something a tinder fire catches to follow it (as Light asks).
    static bool Follows(IReadOnlyDictionary<Resource, double> s) =>
        s.GetValueOrDefault(Resource.Kindling) >= MinLightKindlingKg || s.GetValueOrDefault(Resource.Pine) > 0;

    /// MorningFireHours of fuel it can keep burning (FireHours).
    static bool Keeps(IReadOnlyDictionary<Resource, double> s) => FireHours(s) >= MorningFireHours;

    /// Hours of fire in this stock that it can keep burning from cold: its kindling, and the wood a fire built from it
    /// takes (Lit). Wood its kindling cannot get going is no fire. Old (this rebuild): every kilo of wood counted, so a
    /// camp of tinder, oak and half a kilo of kindling - a fire too small ever to take oak - was a night's fire, and it
    /// went cold beside the oak (seeds 7 and 2024; seed 11, day 13).
    static double FireHours(IReadOnlyDictionary<Resource, double> s) =>
        Fire.FuelHours(Resource.Kindling, s.GetValueOrDefault(Resource.Kindling)) + Lit(s).Sum(r => Fire.FuelHours(r, s.GetValueOrDefault(r)));

    /// The wood in this stock a fire lit from it would take, by the fire's own catch rule (Fire.CanAddFuel): struck with
    /// tinder, all the kindling on it, then each wood that catches added to it, until no more will. So oak counts with the
    /// kindling, or the pine or birch, to make a fire hot enough for it, and not otherwise; how much that is is the fire
    /// model's, not a number here.
    static List<Resource> Lit(IReadOnlyDictionary<Resource, double> s)
    {
        var fire = new FireState { Tile = default, LastUpdatedMinute = 0 };
        fire.BurningKg[Resource.Tinder] = StrikeTinderKg;
        if (s.GetValueOrDefault(Resource.Kindling) > 0) fire.BurningKg[Resource.Kindling] = s.GetValueOrDefault(Resource.Kindling);
        var lit = new List<Resource>();
        for (bool caught = true; caught;)
        {
            caught = false;
            foreach (var r in Woods)
            {
                if (lit.Contains(r) || s.GetValueOrDefault(r) <= 0 || !Fire.CanAddFuel(fire, r)) continue;
                fire.BurningKg[r] = Math.Min(s.GetValueOrDefault(r), Math.Max(0, Fire.MaxFuelKg(fire.Pit) - Fire.TotalKg(fire)));
                lit.Add(r);
                caught = true;
            }
        }
        return lit;
    }

    /// Daytime: keep the camp in firewood. Fell a tree, carry it home a load at a time. Warming: the walk to
    /// the trees leaves a camp with no fire to be had, and is held to the warming lines (Afford); the trees are
    /// home once it sets out, as makings are (FireMakings), so every plan after the first step - the walk on, a
    /// rest, the evening - takes it on to where the fire will be made, and work there is work at camp. Old (this
    /// rebuild): the trip was judged afresh a tile out by the ordinary budget, which refused it, so rest won and
    /// took it home - 32 walks of one tile and back in five hours, never reaching the trees (review probe P5).
    static Plan? Wood(Me me, bool warming = false)
    {
        double carried = Woods.Sum(me.Has) + me.Has(Resource.Kindling);
        if (me.AtCamp) { if (Unload(me) is { } stash) return stash; }
        // A load turns it home: its pack full, or LoadKg in hand off a trip of no goal. On a trip to a goal (WoodGoal) the
        // pack is the measure, so wood it set out holding does not turn it round at the first step (old, this rebuild:
        // it kept a bank's worth in hand by the fire, set out with it, and at the first step had "a load" and turned
        // home - 59 reversals in a row, seed 3, day 11.97).
        else if (carried >= LoadKg && me.Mind.WoodGoal == null || me.RoomKg < 1.5) return GoHome(me);
        // No room for a load: a walk out would turn straight back (old: with a pack full of food it walked
        // camp to trees and back, again and again).
        bool room = me.RoomKg >= 1.5;
        // What a felled tree left on the ground here: what home is short of to light a fire first - tinder, then
        // something to follow it - and with wood enough at home, only that (old, this rebuild: the heaviest first, so it
        // carried six kilos of oak home to eighteen hours of oak and no kindling, again and again - seed 99, day 22).
        if (!me.AtCamp && me.RoomKg >= 1 && First(LyingOrder(me), r => me.Stashed(r) > 0) is Resource lying)
            return Try(me, new Unstash(me.Id, lying, Math.Min(me.RoomKg, me.Stashed(lying))), 1);
        bool axe = Items.Tool(me.Pack, ToolType.Axe) != null;
        // Where home is: moved to the camp NewCamp chooses, if any, once the plan is started (felling here included:
        // what it fells it carries to the new home).
        var home = axe ? NewCamp(me) ?? me.Mind.Camp : me.Mind.Camp;
        // No hour of felling with warmth Critical: logs do nothing for a core past the line without a fire to put them
        // on (old, this rebuild: an hour's felling whatever the core, seed 31 from 93.2F to 88.5F by a cold pit).
        if (TreesHere(me) is int trees && axe && me.Warmth < Tier.Critical && Try(me, new Gather(me.Id, trees), 60, warming) is { } fell)
        {
            if (home != me.Mind.Camp) me.Commits.Add(() => me.Mind.Camp = home);
            return fell;
        }
        // A warming walk ends where the fire will be made: at the trees, unless home is a camp worth keeping (KeepCamp),
        // or a shelter with the trees in hauling reach (NearTimberTiles), or moves to a new one: then it is a round trip
        // held to the warming lines, the walk back reckoned, and the fire is made back under the shelter (old, this
        // rebuild: home moved to the trees on every warming walk, even one tile off the overhang - 115 of 117 moves to
        // a bare tile on the review seeds).
        // Walks out need light to see by (Me.Lit): in the dark, only what can be done here, by the fire's light or by feel.
        bool walk = axe && room && me.Lit;
        // A trip under way goes on to the trees it set out for while the budget covers going on, not to whatever looks
        // nearest from each step (WoodGoal).
        if (walk && !me.AtCamp && me.Mind.WoodGoal is TileId goal && goal != me.Tile && me.Mind.Memory.Places.GetValueOrDefault(goal)?.Trees == true
            && Go(me, goal, WoodTripWorkMinutes, me.Mind.Camp, warming) is { } on) return on;
        if (walk && (NearestTrees(me, me.Tile, MaxWoodTiles) ?? (home != me.Mind.Camp ? home : null)) is TileId t
            && (warming && home == me.Mind.Camp && (Sheltered(me, t) || !KeepCamp(me) && !(Sheltered(me, home) && MapGen.Manhattan(home, t) <= NearTimberTiles)) ? t : home) is var bound
            && Go(me, t, WoodTripWorkMinutes, bound, warming) is { } go)
        {
            if (bound != me.Mind.Camp) me.Commits.Add(() => me.Mind.Camp = bound);
            me.Commits.Add(() => me.Mind.WoodGoal = t);
            return go;
        }
        // No timber known: look for some - past carrying reach, since a stand found there moves the camp - then fallen wood.
        // Groping for fallen wood is warming work only at the trees a warming walk was for; anywhere else out there the
        // ordinary budget turns it home (old, this rebuild, briefly: warming everywhere, so seed 5 groped the open
        // grassland five tiles out until it froze on its first evening).
        bool atTrees = me.Mind.Memory.Places.GetValueOrDefault(me.Tile)?.Trees == true;
        // In the dark, groping here for fuel is worth it only while an hour of it finds more than an hour of fire (birch the
        // yardstick): ground picked over gives up less than the fire burns while it looks, and the night is better slept.
        bool worthGroping = me.Lit || ForageHere(me, ForageFocus.Fuel) * Fire.FuelHours(Resource.Birch, 1) >= 1;
        return (walk ? Explore(me, TimberSearchTiles) : null)
            ?? (worthGroping ? Forage(me, minutes: 30, wander: room && me.Lit, ForageFocus.Fuel, warming && atTrees) : null);
    }

    /// A load of wood to carry home (Wood turns for home with it).
    const double LoadKg = 5;

    /// What it takes of the wood and tinder lying away from home (Wood): what home lacks to light a fire and keep it
    /// (Makings), and only that while home lacks it; otherwise wood, heaviest first.
    static IEnumerable<Resource> LyingOrder(Me me)
    {
        var stock = CampStock(me);
        var lacking = new List<Resource>();
        if (!Lights(stock)) lacking.AddRange(Tinders);
        if (!Follows(stock)) lacking.AddRange([Resource.Kindling, Resource.Pine]);
        // Short of a fire it can keep: kindling first, which lights the wood home has, then what catches soonest.
        if (!Keeps(stock)) lacking.AddRange(Woods.Prepend(Resource.Kindling).OrderBy(r => r == Resource.Kindling ? -1 : Fire.Props[r].MinFireTempF));
        return lacking.Count > 0 && Keeps(stock) ? lacking : lacking.Concat(Woods.Append(Resource.Kindling).OrderByDescending(me.Stashed).ThenBy(r => r)).Distinct();
    }

    /// The nearest place it knows standing timber, within this many tiles of a tile.
    static TileId? NearestTrees(Me me, TileId from, int within) =>
        me.Mind.Memory.Places.Where(p => p.Value.Trees && MapGen.Manhattan(p.Key, from) <= within).Select(p => p.Key)
            .OrderBy(t => MapGen.Manhattan(from, t)).ThenBy(t => t).Cast<TileId?>().FirstOrDefault();

    /// Where home moves when it goes for wood, or null to stay: the nearest known shelter with standing timber within
    /// NearTimberTiles of it, when home has no shelter or no timber that near; failing that, with no timber that near
    /// home, the nearest stand within TimberSearchTiles (old design: depletion forces camp relocation; old, this
    /// rebuild: none within 15 tiles, so it picked deadfall with an axe it never used until it froze - seed 5, nearest
    /// stand 24 tiles off). Never from a camp worth keeping (KeepCamp) to a bare tile. Old (this rebuild): the nearest
    /// stand whatever was there, so 115 of 117 moves on the review seeds went to a bare tile, most of them away from the
    /// starting overhang.
    // From a bare camp to a shelter only while the camp is short of tonight's fuel and its fire is out: a camp with its
    // night's wood in, or a fire going, is not left for a better one (old, this rebuild, briefly: home went back to the
    // overhang from beside a lit fire - seed 7, day 13).
    static TileId? NewCamp(Me me)
    {
        var camp = me.Mind.Camp;
        bool timberNear = NearestTrees(me, camp, NearTimberTiles) != null;
        if (timberNear && (Sheltered(me, camp) || CampHasWarmth(me) || CampFuelHours(me) >= HoursToDawn(me) + MorningFireHours)) return null;
        var shelter = me.Mind.Memory.Places.Where(p => p.Key != camp && p.Value.Shelter != null && MapGen.Manhattan(p.Key, camp) <= TimberSearchTiles
                && NearestTrees(me, p.Key, NearTimberTiles) != null)
            .Select(p => p.Key).OrderBy(t => MapGen.Manhattan(t, camp)).ThenBy(t => t).Cast<TileId?>().FirstOrDefault();
        if (shelter != null) return shelter;
        if (timberNear || KeepCamp(me)) return null;
        return NearestTrees(me, camp, TimberSearchTiles);
    }

    /// Timber this close to home is hauled, not moved to (NewCamp).
    const int NearTimberTiles = 2;

    /// A shelter on the tile, as it remembers it.
    static bool Sheltered(Me me, TileId t) => me.Mind.Memory.Places.GetValueOrDefault(t)?.Shelter != null;

    /// Home is a camp worth keeping: a shelter, with fuel for a morning fire and tinder (or sticks to shave) to light it,
    /// counting what it carries. It goes for kindling to follow the tinder rather than leave it for a bare tile (old,
    /// this rebuild: Makings asked for kindling or pine, so seed 31 left an overhang holding 16.3 kg of oak and 5.8 kg
    /// of tinder for the trees, and froze there three tiles away).
    static bool KeepCamp(Me me)
    {
        if (!Sheltered(me, me.Mind.Camp)) return false;
        var s = CampStock(me);
        // Wood of any kind: a shelter with the wood for a morning fire is worth keeping while it fetches the kindling to
        // light it (FireHours is what it can burn now).
        return Lights(s) && Woods.Append(Resource.Kindling).Sum(r => Fire.FuelHours(r, s.GetValueOrDefault(r))) >= MorningFireHours;
    }

    /// What it will have to make a fire with at home: ToHand there, or from away the pack and the stash as it left it.
    /// Away, the stash is read as the least memory allows: it keeps a stash to the nearest StashMemoryKg (Memories.Observe),
    /// so a remembered amount may be up to half of that more than is there. Old (this rebuild): read as remembered, so
    /// 0.094 kg of kindling, remembered as 0.1, was enough to follow the tinder from a tile out (MinLightKindlingKg) and
    /// not at camp: warm a tile out, short of fuel at home, it walked to the trees and home and back, sixty-six times
    /// (seed 2024, day 21).
    static Dictionary<Resource, double> CampStock(Me me)
    {
        if (me.AtCamp) return ToHand(me);
        var stash = AtLeast(me.Mind.Memory.Places.GetValueOrDefault(me.Mind.Camp)?.Stash);
        return Lighting.ToDictionary(r => r, r => me.Has(r) + (stash?.GetValueOrDefault(r) ?? 0));
    }

    /// A remembered stash, read as the least it may hold: memory keeps it to the nearest StashMemoryKg.
    static Dictionary<Resource, double>? AtLeast(IReadOnlyDictionary<Resource, double>? remembered) =>
        remembered?.ToDictionary(x => x.Key, x => Math.Max(0, x.Value - StashMemoryKg / 2));

    /// The resolution an NPC remembers a stash at (Memories.Observe rounds to a tenth of a kilo).
    const double StashMemoryKg = 0.1;

    /// For a test: the fuel hours an NPC reckons its camp holds, from where it stands (CampFuelHours).
    internal static double ReckonedCampFuelHours(World world, Agent agent) =>
        CampFuelHours(new Me(world, agent, (NpcMind)agent.Mind!, []));

    /// For a test: where a search from here would head (Explore), within radius of camp; null when nowhere is left unseen.
    internal static TileId? ExploreHeading(World world, Agent agent, int radius)
    {
        var me = new Me(world, agent, (NpcMind)agent.Mind!, []);
        if (Explore(me, radius) is not { } plan) return null;
        me.Commit(plan);
        return me.Mind.Heading;
    }

    /// Home has no fire to be had: none burning or in embers there, as far as it can tell, and not the makings of one,
    /// what it carries included. A trip for wood from such a camp is a warming trip, held to the warming lines (Afford),
    /// the work out there included - staying is colder than going. Old (this rebuild): only the first step's walk was,
    /// so at the trees a cold NPC was refused the felling by the ordinary budget and walked home empty-handed, and back,
    /// until it froze by a rocky overhang with no kindling on its floor and tinder in its pack (seed 99).
    // Embers count only with fuel they will take; seen from camp and from the trees alike, so the two ends of the walk
    // agree (old, this rebuild: embers with nothing to feed them were warmth from the trees and not from camp, so it
    // walked out warming and was turned home by the ordinary budget, back and forth - seed 99, day 22).
    static bool ColdCamp(Me me)
    {
        var stock = CampStock(me);
        if (Makings(stock)) return false;
        if (!me.AtCamp && !Sight.InSight(me.World, me.Agent, me.Mind.Camp))
            return CampFireHours(me, me.Mind.Memory.Places.GetValueOrDefault(me.Mind.Camp)) <= 0;
        var pit = me.AtCamp ? me.Pit : Fire.CurrentPitAt(me.World, me.Mind.Camp);
        return !(pit != null && (Fire.IsActive(pit) || pit.HasEmbers && Woods.Append(Resource.Kindling).Any(r => stock[r] > 0 && Fire.CanAddFuel(pit, r))));
    }

    /// Food in the pack fit to eat now, richest first (old HandleFoodNeed: cooked before other food).
    /// Raw meat waits for a fire unless the NPC is starving.
    static Resource? ReadyFood(Me me) => ReadyFood(me, me.Pack);

    /// The same, in this inventory.
    static Resource? ReadyFood(Me me, Inventory? inv) => inv == null ? null : First(
        inv.Kg.Where(x => x.Value > 0 && Items.Resources[x.Key].Category == ResourceCategory.Food).Select(x => x.Key)
            .OrderByDescending(r => Items.Resources[r].KcalPerKg).ThenBy(r => r),
        r => r is not (Resource.RawMeat or Resource.RawFish) || me.Agent.Body.FatPct < RawMeatFatPct);

    /// Hungry, what it can eat without going for it (FoodToHand): from the pack, or off the stash here first.
    static Plan? EatToHand(Me me) =>
        ReadyFood(me) is Resource carried ? Eat(me, carried)
        : ReadyFood(me, me.Stash) is Resource stashed ? Try(me, new Unstash(me.Id, stashed, Math.Min(2, me.Stashed(stashed))), 1) : null;

    /// As much as the stomach has room for, and as much again as the fat it is short of, up to a
    /// second store's worth at one sitting: what the store cannot hold goes on as fat (Body.Eat).
    static Plan? Eat(Me me, Resource food)
    {
        double surplusKcal = Math.Clamp(me.ShortfallKg * Body.KcalPerKgFat, 0, Body.MaxCaloriesKcal);
        double kg = Math.Min(me.Has(food), (Body.MaxCaloriesKcal - me.Agent.Body.CaloriesKcal + surplusKcal) / Items.Resources[food].KcalPerKg);
        return kg >= 0.01 ? Try(me, new Eat(me.Id, food, kg), Work.EatMinutes) : null;
    }

    static Plan? Food(Me me, bool starving)
    {
        if (me.Burning && AtFire(me) is { Activity: Cook } cook) return cook;
        if (ReadyFood(me) is Resource ready && Eat(me, ready) is { } eat) return eat;
        if (me.Has(Resource.RawMeat) + me.Has(Resource.RawFish) > 0)
            return me.AtCamp ? KeepFire(me, 3) : GoHome(me);
        if (me.Stash is { } stash && First(stash.Kg.Keys, r => Items.Resources[r].Category == ResourceCategory.Food) is Resource stored)
            return Try(me, new Unstash(me.Id, stored, 2), 1);
        if (Carcass(me) is int carcass && (!me.Dark || me.Burning) && me.RoomKg >= 1) return Try(me, new Butcher(me.Id, carcass), 60);
        if (Carcass(me) != null && me.RoomKg < 1) return GoHome(me);
        // No foraging or stalking blind (judgment call: the night is for the fire and sleep, even hungry).
        if (me.Dark) return null;
        if (me.RoomKg < 1) return GoHome(me);
        return FindFood(me, starving);
    }

    /// Where the next meal is likeliest to come from, by expected kilocalories an hour, best first.
    static Plan? FindFood(Me me, bool starving)
    {
        var options = new List<(double KcalPerHour, Func<Plan?> Act)>();
        bool wander = me.Hour < HomeByHour - 1;
        if (ForageTarget(me, ForageFocus.Food, wander, FoodForageMinutes) is { } ground)
            options.Add((ground.PerHour * 1000, () => Forage(me, minutes: FoodForageMinutes, wander, ForageFocus.Food)));
        if (Weapon(me) != null && Body.Vitality(me.Agent.Body) >= 0.7)
            foreach (var prey in Herds(me, 3, h => Fauna.BehaviorOf(h.Species) == HerdBehavior.Prey && Unengaged(me, h) && WouldHunt(h, starving)).OrderBy(h => h.Id))
                options.Add((HuntKcalPerHour(me, prey), () => Hunt(me, prey)));
        foreach (var o in options.Where(o => o.KcalPerHour > 0).OrderByDescending(o => o.KcalPerHour))
            if (o.Act() is { } plan) return plan;
        return null;
    }

    /// Hunting by the same yardstick as foraging: the meat it would carry home, at the odds of a kill,
    /// over the stalk, the butchering and the walk; less what the animal could do to it, a risk it
    /// accepts more of the further it has fallen short. Judgment calls: kill odds, hours, meat yield.
    static double HuntKcalPerHour(Me me, Agent prey)
    {
        const double KillPct = 0.4, MeatPct = 0.4;
        var stats = SpeciesTable.Of(prey.Species);
        double meatKg = Math.Min(prey.Members.First(Body.IsAlive).WeightKg * MeatPct, me.RoomKg);
        double hours = HuntWorkMinutes / 60.0 + MapGen.Manhattan(me.Tile, prey.Tile) * MinutesPerTile / 60.0;
        double urgency = Math.Clamp(me.ShortfallKg / CriticalShortfallKg, 0, 1);
        double danger = Math.Clamp(stats.Threat / 2, 0, 1); // caribou 0.15, bison 0.9, mammoth 1
        return KillPct * meatKg * Items.Resources[Resource.RawMeat].KcalPerKg / hours * (1 - danger * (1 - urgency));
    }

    /// Whether a lone NPC takes this herd on, by what the species does when hunted (Role): prey that runs is
    /// worth a stalk whenever food is wanted; prey that turns and fights (bison, megaloceros) only when
    /// starving; and nothing past TooBigToHuntAloneKg ever, alone. Old (this rebuild): FightOrFlight's size
    /// rule, which every herd animal fails against one armed person (a caribou rates 4 to its 2), so it never
    /// hunted until starving and then took on anything, mammoths included (seeds 123 day 5, then day 34.9).
    static bool WouldHunt(Agent herd, bool starving)
    {
        var stats = SpeciesTable.Of(herd.Species);
        if (stats.WeightKg > TooBigToHuntAloneKg) return false;
        return stats.Role switch
        {
            Role.Prey => true,
            Role.DangerousPrey => starving,
            _ => false,
        };
    }

    /// Heavier than this is a hunt for a band, not one person: a mammoth (6 t), not a bison (800 kg). Judgment call.
    const double TooBigToHuntAloneKg = 2000;

    /// A food forage stint.
    const int FoodForageMinutes = 45;
    /// A stalk of an hour and two of butchering (judgment calls).
    const int HuntWorkMinutes = 3 * 60;
    /// A wood trip: an hour felling or picking up, then a load home.
    const int WoodTripWorkMinutes = 60;

    static Plan? Hunt(Me me, Agent prey)
    {
        if (prey.Tile != me.Tile) return Go(me, prey.Tile, HuntWorkMinutes);
        // The player's hunt: a stalk from Fauna.HuntStartDistanceM at one member, a few herd mates with it.
        return Fought(me, Fauna.BeginHunt(me.World, me.Id, prey.Id, prey.Members.FindIndex(Body.IsAlive), Weapon(me), me.Events));
    }

    /// Nothing pressing: unload, keep the fire, top up water, forage the day away.
    static Plan? Errands(Me me)
    {
        if (me.AtCamp && Unload(me) is { } stash) return stash;
        if (me.AtCamp && me.Burning && (KeepFire(me, 3) ?? AtFire(me)) is { } chore) return chore;
        if (me.RoomKg < 1.5) return GoHome(me);
        return me.Dark ? null : Forage(me, minutes: 45, wander: true);
    }

    // ---- fire ----

    /// Lights the fire here if it is out, or feeds it up to targetKg. Fuel comes from the pack, then
    /// the stash here. Null when the fire is fed or there is nothing to do it with.
    static Plan? KeepFire(Me me, double targetKg)
    {
        var pit = me.Pit;
        if (pit == null || !Fire.IsActive(pit))
        {
            // Ember awareness: hot embers take fuel and catch again, no drill needed.
            if (pit is { HasEmbers: true } && Stoke(me, pit, 1.0, reserve: 0) is { } blow) return blow;
            return Light(me);
        }
        double want = targetKg - Fire.TotalKg(pit);
        return want > 0.5 ? Stoke(me, pit, want) : null;
    }

    /// Slowest-burning fuel the fire will take, from the pack or the stash here; kindling only when no
    /// log will catch, and never the reserve - unless logs are waiting on a fire too cool for them, when
    /// the reserve is what gets them on. A small fire is built up first, quickest-catching fuel first.
    /// Old: a kindling fire with only oak to follow it was left to die, the oak beside it (seed 5).
    static Plan? Stoke(Me me, FireState pit, double kg, double reserve = KindlingReserveKg)
    {
        bool small = Fire.BurningKg(pit) < BuildUpKg;
        foreach (var r in small ? Woods.OrderBy(r => Fire.Props[r].MinFireTempF) : Woods.AsEnumerable())
        {
            if (!Fire.CanAddFuel(pit, r)) continue;
            if (me.Has(r) > 0) return Try(me, new AddFuel(me.Id, r, Math.Min(kg, me.Has(r))), 1);
            if (me.Stashed(r) > 0 && me.RoomKg > 0.5) return Try(me, new Unstash(me.Id, r, Math.Min(kg, me.Stashed(r))), 1);
        }
        if (!Fire.CanAddFuel(pit, Resource.Kindling)) return null;
        // Logs in hand, on the stash or already on the fire waiting for it to be hot enough: the reserve gets them going.
        if (Woods.Any(r => me.Has(r) + me.Stashed(r) + pit.UnburnedKg.GetValueOrDefault(r) > 0)) reserve = 0;
        double spare = me.Has(Resource.Kindling) + me.Stashed(Resource.Kindling) - reserve;
        if (spare <= 0.1) return null;
        if (me.Has(Resource.Kindling) - reserve > 0.1) return Try(me, new AddFuel(me.Id, Resource.Kindling, Math.Min(kg, me.Has(Resource.Kindling) - reserve)), 1);
        // Off the stash no more than the pack keeps (CarriedKindlingKg): more, and unloading at camp put it straight back
        // before it reached the fire (old, this rebuild: seed 17, day 4, 1.7 kg off the stash and 0.7 back, three rounds).
        double take = Math.Min(Math.Min(kg + reserve, CarriedKindlingKg) - me.Has(Resource.Kindling), me.Stashed(Resource.Kindling));
        return take > 0.05 ? Try(me, new Unstash(me.Id, Resource.Kindling, take), 1) : null;
    }

    /// Old FireHandler: the best working fire starter, the best tinder, a handful of kindling.
    static Plan? Light(Me me)
    {
        var tool = Starters.Cast<ToolType?>().FirstOrDefault(t => Items.Tool(me.Pack, t!.Value) != null);
        if (tool == null) return null; // the old NPC crafted a fire starter here; there is no crafting yet
        var tinder = First(Tinders, r => me.Has(r) >= StrikeTinderKg);
        if (tinder == null && First(Tinders, r => me.Stashed(r) > 0) is Resource stashed)
            return TakeFromStash(me, stashed, 0.3);
        if (me.Has(Resource.Kindling) < 0.35 && me.Stashed(Resource.Kindling) > 0)
            return TakeFromStash(me, Resource.Kindling, 1.0);
        // Sticks shaved for tinder, with enough left to follow it (old: only with half a kilo of sticks, so 0.47 kg of
        // kindling and no tinder was no fire at all, and seed 5 groped its camp four hours as its core fell to 93F).
        // With tinder down to its reserve, the sticks go first and the tinder is kept for Critical warmth, when the
        // odds it adds matter most (old, this rebuild: tinder spent on every strike, failed ones included, until a
        // freezing night found none - seed 5, day 3).
        bool sticks = me.Has(Resource.Kindling) >= KindlingAsTinderKg;
        if (tinder == null || sticks && me.Warmth < Tier.Critical && Tinders.Sum(me.Has) < TinderReserveKg + StrikeTinderKg)
            tinder = sticks ? Resource.Kindling : tinder;
        if (tinder == null) return null;
        // Tinder is kept for a fire that will last: by day, short of Critical warmth, it strikes only over a fire's
        // makings, the fuel to keep it included (Makings), and goes on gathering until it has them; by night there is
        // no gathering them, and waiting only takes its hands down to Critical's odds. Old (this rebuild): it struck
        // over half a kilo of kindling that burned out in twenty minutes, again and again, until the tinder was gone
        // (seed 5, days 0-3: eighteen fires on a grassland camp with no wood).
        if (me.Warmth < Tier.Critical && !me.Night && !Makings(ToHand(me))) return null;
        // Not worth the tinder in the dark with a poor drill: wait for light, or for better tinder - unless
        // cold, when waiting is what kills (old behavior: it hauled wood to a fire it would not try to light).
        double minChance = LightChanceWorthIt(me.Agent.Body.CoreTempF);
        // The odds the strike will roll on: the same sheet the player sees (old: hands in the dark and nothing else).
        if (Fire.StartOdds(me.World, me.Agent, tool.Value, tinder.Value).TotalPct < minChance) return null;
        double kindling = Math.Min(0.3, me.Has(Resource.Kindling) - (tinder == Resource.Kindling ? StrikeTinderKg : 0));
        if (kindling < 0) return null;
        // Tinder alone burns out in a minute or two and catches nothing but pine (Content/fire.json): without kindling
        // or pine to follow it, a strike only spends the tinder. Old (this rebuild): it struck tinder over a camp with
        // no wood, or over oak a tinder fire never takes, until the tinder was gone (seed 7, day 27; the review's day 29).
        if (kindling < MinLightKindlingKg && me.Has(Resource.Pine) + me.Stashed(Resource.Pine) <= 0) return null;
        return Try(me, new StartFire(me.Id, tool.Value, tinder.Value, StrikeTinderKg, kindling), 1);
    }

    /// What a strike spends of tinder; and the kindling that is tinder enough, the strike's worth shaved from it and
    /// MinLightKindlingKg left to follow it.
    const double StrikeTinderKg = 0.05, KindlingAsTinderKg = StrikeTinderKg + MinLightKindlingKg;
    /// Tinder kept back for Critical warmth while sticks can stand in for it (Light): three strikes.
    const double TinderReserveKg = 0.15;

    static readonly Resource[] Lighting = [.. Woods, Resource.Kindling, .. Tinders];

    /// What it has to make a fire with here: the pack, the stash here, and what waits unburned on the pit.
    static Dictionary<Resource, double> ToHand(Me me) =>
        Lighting.ToDictionary(r => r, r => me.Has(r) + me.Stashed(r) + (me.Pit?.UnburnedKg.GetValueOrDefault(r) ?? 0));

    /// Off the stash here; with too little room in the pack for it, the heaviest thing carried that is not for
    /// lighting a fire, nor its water, goes down first. Old (this rebuild): a full pack was refused the kindling, and Light gave up,
    /// so it stood at its own stash of tinder and kindling and froze (seed 11, day 33).
    static Plan? TakeFromStash(Me me, Resource r, double kg)
    {
        if (me.RoomKg < Math.Min(kg, me.Stashed(r)) && me.Pack.Kg.Where(x => x.Value > 0 && x.Key is not (Resource.Kindling or Resource.Water) && !Tinders.Contains(x.Key))
                .OrderByDescending(x => x.Value).ThenBy(x => x.Key).Select(x => (Resource?)x.Key).FirstOrDefault() is Resource heaviest)
            return Try(me, new Stash(me.Id, heaviest, me.Has(heaviest)), 1);
        return Try(me, new Unstash(me.Id, r, kg), 1);
    }

    /// The odds a strike must have to be worth the tinder: MinLightChance warm, falling as it gets colder
    /// to WantedLightChance by the time it wants to be warm, and DesperateLightChance when freezing, so it
    /// strikes while its hands are still good. Old: nothing under 20% until critical (95.9F), by when
    /// cold hands had the drill at 13%, then the 5% floor (seed 5).
    static double LightChanceWorthIt(double coreF)
    {
        if (coreF >= WarmAgainCoreF) return MinLightChance;
        if (coreF >= WarmNeedCoreF) return WantedLightChance + (MinLightChance - WantedLightChance) * (coreF - WarmNeedCoreF) / (WarmAgainCoreF - WarmNeedCoreF);
        if (coreF >= HypothermiaCoreF) return DesperateLightChance + (WantedLightChance - DesperateLightChance) * (coreF - HypothermiaCoreF) / (WarmNeedCoreF - HypothermiaCoreF);
        return DesperateLightChance;
    }

    /// What is worth doing while the fire does its work (old HandleWarmthNeed): melt a water reserve
    /// to carry, cook what is raw.
    static Plan? AtFire(Me me)
    {
        if (!me.Burning) return null;
        foreach (var raw in new[] { Resource.RawMeat, Resource.RawFish })
            if (me.Has(raw) > 0) return Try(me, new Cook(me.Id, raw, me.Has(raw)), Math.Min(60, (int)Math.Ceiling(me.Has(raw) * Work.CookMeatMinutesPerKg) + 1));
        // Old WaterReserveLiters: a day's water loss, carried out of camp.
        if (me.Has(Resource.Water) < Body.BaseWaterLossMlPerDay / 1000 && me.RoomKg > Work.MeltSnowLiters + 0.5)
            return Try(me, new MeltSnow(me.Id), Work.MeltSnowMinutes);
        return null;
    }

    /// Hours of fire in the camp's wood, the pit's and the pack's, as the NPC believes them (plan
    /// decision 7): the stash as it stands in camp, or as it left it; the pit as seen (Fire.FuelHours).
    /// Wood it cannot light is no fire: none at a cold camp (ColdCamp), and only the wood its kindling can get going
    /// (FireHours), so tonight's fuel is short - warmth's Need - until it has the kindling, and the wood plan fetches it. Old (this rebuild): eighteen hours of oak with no kindling to follow the
    /// tinder counted as a night's fire, so nothing went for kindling and it froze beside the oak (seed 99, day 22).
    static double CampFuelHours(Me me)
    {
        if (ColdCamp(me)) return 0;
        var mem = me.Mind.Memory.Places.GetValueOrDefault(me.Mind.Camp);
        IReadOnlyDictionary<Resource, double>? stash = me.AtCamp ? me.Stash?.Kg : AtLeast(mem?.Stash);
        return FireHours(Lighting.ToDictionary(r => r, r => me.Has(r) + (stash?.GetValueOrDefault(r) ?? 0))) + CampFireHours(me, mem);
    }

    /// Hours of fire on the camp's pit while it burns. Standing beside it, the pit as it is now (Memories.Observe
    /// caught it up). Anywhere else, in sight or not, whether it burns as last seen (Memories.Glance keeps that
    /// current in sight) and the reckoning from the last time it stood beside it, burned down since: from a
    /// distance a fire's flame shows, not how much is on it (TileMemory.FireHours).
    // A dead pit's unburned logs are left out: they wait for a fire, and from a distance nobody can tell they are there.
    static double CampFireHours(Me me, TileMemory? mem)
    {
        if (me.AtCamp) return me.Pit is { } pit && Fire.IsActive(pit) ? Fire.FuelHours(pit) : 0;
        return mem is { FireHours: double hours, VisitedMinute: long visited } && Fire.Burns(mem.Fire)
            ? Math.Max(0, hours - (me.World.Minute - visited) / 60.0) : 0;
    }

    /// CampFuelHours for a test: the hours of fire this NPC believes its camp holds, deciding now.
    internal static double BelievedCampFuelHours(World world, Agent agent) =>
        CampFuelHours(new Me(world, agent, (NpcMind)agent.Mind!, []));

    /// Whether the camp fire is lit, as far as the NPC can tell: as it is, in sight; as last seen, out of it.
    static bool CampFireBurning(Me me) => Sight.InSight(me.World, me.Agent, me.Mind.Camp)
        ? Fire.CurrentPitAt(me.World, me.Mind.Camp) is { } pit && Fire.IsActive(pit)
        : Fire.Burns(me.Mind.Memory.Places.GetValueOrDefault(me.Mind.Camp)?.Fire);

    /// Until the sun is next up (Weather.SunriseHour).
    static double HoursToDawn(Me me)
    {
        int now = Weather.MinuteOfDay(me.World.Minute), dawn = Weather.SunriseHour(me.World.Minute) * 60;
        return ((dawn - now + 1440) % 1440) / 60.0;
    }

    /// At camp, everything goes on the stash but food, water and what lights a fire; wood in hand goes
    /// on a fire short of it first. Old behavior: banking the fire took wood off the stash, and the next
    /// choice stashed it again before it went on, two minutes at a time all afternoon (bug, fixed).
    static Plan? Unload(Me me)
    {
        if (me.Burning && KeepFire(me, 3) is { Activity: AddFuel } feed) return feed;
        // Wood in hand beside a fire that is lit or in embers stays in hand, up to a bank's worth: it is going on the
        // fire as the fire takes it (old, this rebuild: the evening took birch off the stash for a fire too cool for it,
        // and at dawn the wood plan put it straight back - seed 17 at the bench id).
        bool fireHere = me.Pit is { } pit && (Fire.IsActive(pit) || pit.HasEmbers);
        foreach (var (r, kg) in me.Pack.Kg)
        {
            if (fireHere && Woods.Contains(r) && kg <= BankKg) continue;
            double keep = Items.Resources[r].Category is ResourceCategory.Food or ResourceCategory.Water ? kg
                : r == Resource.Kindling ? CarriedKindlingKg : Tinders.Contains(r) ? CarriedTinderKg : 0;
            if (kg - keep > 0.05) return Try(me, new Stash(me.Id, r, kg - keep), 1);
        }
        return null;
    }

    // ---- gathering ----

    static int? TreesHere(Me me)
    {
        if (me.Dark && !me.Burning) return null;
        var pools = Pools.At(me.World, me.Tile);
        for (int i = 0; i < pools.Count; i++)
            if (pools[i] is TreePool && Pools.HasTrees(me.World, new PoolId(me.Tile, i))) return i;
        return null;
    }

    /// A carcass here with work left on it for this butcher: something it can cut, or cut pieces waiting to be carried.
    /// Old (this rebuild): any carcass not yet butchered, so bare-handed it went back to tusks and hide only a blade
    /// can take (Work.NeedsABlade) and was refused.
    static int? Carcass(Me me)
    {
        bool blade = Items.HasCuttingTool(me.Pack);
        return me.World.Tiles.GetValueOrDefault(me.Tile)?.Carcasses?.FirstOrDefault(c => !Carcasses.AllCut(c, blade) || c.Pending.Count > 0)?.Id;
    }

    /// Forages here if it is worth it; otherwise (if it may wander) walks to the best ground it
    /// remembers or has not tried yet, within its range of camp. Old DetermineGetResource.
    static Plan? Forage(Me me, int minutes, bool wander, ForageFocus focus = ForageFocus.General, bool warming = false)
    {
        if (me.RoomKg < 1) return GoHome(me);
        wander &= me.Hour < HomeByHour - 1; // not off somewhere new just before turning for home
        if (ForageTarget(me, focus, wander, minutes) is not { } best) return null;
        if (best.Tile != me.Tile && Go(me, best.Tile, minutes) is { } go)
        {
            me.Commits.Add(() => me.Mind.ForageGoal = best.Tile);
            return go;
        }
        if (ForageHere(me, focus) <= 0 || Try(me, new Gather(me.Id, 0, focus), minutes, warming) is not { } gather) return null;
        me.Commits.Add(() => me.Mind.ForageGoal = null);
        return gather;
    }

    static double ForageHere(Me me, ForageFocus focus) => Expect(me, me.Tile, focus);

    /// The best ground to forage for this, here or (if it may wander) elsewhere, and what an hour of it
    /// is worth (Value's units), the walk there counted against a stint of ForageStintHours. Every tile is
    /// judged the same way, the one underfoot included: by memory, which looking (Memories.Observe) has
    /// just brought up to date for every tile it stands on. Staying is a candidate, and leaving it, or
    /// the ground already being walked to, takes a better hour by MoveGainFactor. Old game: the tile
    /// underfoot was weighed apart, and the walk by the first step's going toward each tile, so two tiles
    /// each looked better from the other and a starving NPC walked between them all day (bug, fixed).
    /// Only weighs: the ground chosen becomes ForageGoal when the walk there is the plan started (Forage).
    static (TileId Tile, double PerHour)? ForageTarget(Me me, ForageFocus focus, bool wander, int minutes)
    {
        double here = Expect(me, me.Tile, focus);
        if (!wander) return here > 0 ? (me.Tile, here) : null;
        var candidates = me.Mind.Memory.Places.Where(p => p.Value.Visited && p.Key != me.Tile && InRange(me, p.Key)).Select(p => p.Key)
            .Concat(MapGen.Neighbors(me.Tile).Where(t => Passable(me, me.Tile, t) && InRange(me, t) && me.Mind.Memory.Places.GetValueOrDefault(t)?.Visited != true))
            .ToList();
        if (MapGen.Neighbors(me.Tile).All(n => !Passable(me, me.Tile, n))) return here > 0 ? (me.Tile, here) : null;
        // Nowhere the trip budget will not stretch to (Afford: home warm enough, rested enough and in time,
        // the stint included), or it would turn back on the way and set out again once warm (old: in deep
        // snow, all afternoon). Ground already being walked to is weighed so too, at every step: the trip
        // that was warm enough setting out can be too cold to go on with (old: a starving NPC walked on to
        // its ground until warmth was critical). Setting out for new ground, it wants the whole stint and
        // SetOutBufferF in hand; ground already being walked to holds while any worthwhile stint there is
        // still affordable, so that the walk turning out a little colder or slower than reckoned does not
        // turn it round on the way.
        double PerHour(TileId t, bool going)
        {
            if (Afford(me, t, minutes, me.Mind.Camp, going ? 0 : SetOutBufferF) is not int work || (!going && work < minutes)) return 0;
            double walkHours = WalkMinutes(me, me.Tile, t) / 60.0;
            return Expect(me, t, focus) * ForageStintHours / (ForageStintHours + walkHours);
        }
        var best = candidates.Select(t => (Tile: t, PerHour: PerHour(t, going: false))).OrderByDescending(x => x.PerHour).ThenBy(x => x.Tile).FirstOrDefault();
        // Ground already being walked to holds unless the best beats it by the margin too.
        if (me.Mind.ForageGoal is TileId goal && goal != me.Tile && candidates.Contains(goal)
            && PerHour(goal, going: true) is var kept && kept > 0 && kept * MoveGainFactor >= best.PerHour)
            best = (goal, kept);
        if (best.PerHour > 0 && best.PerHour > here * MoveGainFactor) return best;
        return here > 0 ? (me.Tile, here) : null;
    }

    /// A walk at today's going, not a flat rate (old: two minutes a tile, so in deep snow it set off three
    /// tiles for fresh ground, took an hour a tile, and froze walking home): the first crossing at the
    /// going off the tile it starts from, the rest at the going off camp, the ground it knows best - each
    /// the crossings off that tile, averaged, so the same whichever way the ground lies. Old reckoning (this
    /// rebuild, before): every tile at the going off the first, so one bog or scree tile on the way out
    /// made a trip look twice as long from there as from camp, and turned the walk round.
    // The going off camp reads camp's ground as it is from anywhere: the place it lives, felt as weather is.
    static double WalkMinutes(Me me, TileId from, TileId to)
    {
        int tiles = MapGen.Manhattan(from, to);
        if (tiles == 0) return 0;
        return MinutesOff(me, from == me.Tile ? me.Tile : me.Mind.Camp) + (tiles - 1) * MinutesOff(me, me.Mind.Camp);
    }

    /// The crossings off a tile to its passable neighbours, averaged (careful where a quick step could hurt).
    static double MinutesOff(Me me, TileId tile)
    {
        var sky = me.Sky;
        var steps = MapGen.Neighbors(tile).Where(n => Map.Of(me.World).InBounds(n) && Passable(me, tile, n)).ToList();
        if (steps.Count == 0) return MinutesPerTile;
        return steps.Average(n => (double)Traversal.CrossingMinutes(me.World, me.Agent.Body, tile, n,
            Traversal.IsHazardous(me.World, tile) || Traversal.IsHazardous(me.World, n) ? Pace.Careful : Pace.Quick, sky, Items.EncumbrancePct(me.Agent)));
    }

    /// What an hour's forage on a tile is expected to be worth, from memory: as it was when it last stood
    /// there, regrown since (Pools.ForageRespawnHours); never stood there, by the look of the country.
    static double Expect(Me me, TileId t, ForageFocus focus)
    {
        if (ForagePoolAt(me, t) is not { } def) return 0;
        double density = me.Mind.Memory.Places.TryGetValue(t, out var p) && p.ForageDensity is double d && p.VisitedMinute is long seen
            ? Math.Min(def.Density, d + (me.World.Minute - seen) / 60.0 / Pools.ForageRespawnHours * def.Density) : def.Density;
        return Value(def, density, focus);
    }

    /// How much better an hour elsewhere must be to be worth leaving for (old: within 80%, stay).
    const double MoveGainFactor = 1.25;
    /// Core temperature over the trip line (TripHomeCoreF) it reckons on coming home with, setting out from camp.
    const double SetOutBufferF = 0.3;

    /// A rough tile crossing for planning a hunt (flat ground is one minute, rough two); and how long a
    /// stint of foraging a walk is weighed against.
    const double MinutesPerTile = 2, ForageStintHours = 1;

    /// Six tiles on, toward ground it has seen least of, within radius of camp; null once there is
    /// nowhere left it has not had a look at. Looked at is what it has seen (Memory.Explored, the sight rays). Old (this
    /// rebuild): any tile within three of a place it stood, so ground behind a ridge beside its path counted as seen and
    /// was never gone to look at (seed 5, day 8: the only timber, five tiles past a mountain it had walked beside).
    static Plan? Explore(Me me, int radius, bool warming = false)
    {
        if (me.Hour >= HomeByHour - 2) return null;
        var visited = me.Mind.Memory.Places.Where(p => p.Value.Visited).Select(p => p.Key).ToList();
        var explored = me.Mind.Memory.Explored;
        bool Seen(TileId t) => explored?.Contains(t) == true;
        var m = Map.Of(me.World);
        if (me.Mind.Heading is TileId heading && heading != me.Tile && !Seen(heading) && Go(me, heading, warming: warming) is { } onward) return onward;
        // The nearest ring with ground on it it has not had a look at. Old: only the ring six tiles out,
        // so once the ground around camp was seen it never looked further (seed 5: 0 trees within 15 of camp).
        for (int leg = ExploreLegTiles; leg <= radius + MapGen.Manhattan(me.Tile, me.Mind.Camp); leg += ExploreLegTiles)
        {
            int l = leg;
            var ring = Enumerable.Range(-l, 2 * l + 1)
                .SelectMany(dx => new[] { new TileId(me.Tile.X + dx, me.Tile.Y + (l - Math.Abs(dx))), new TileId(me.Tile.X + dx, me.Tile.Y - (l - Math.Abs(dx))) })
                .Distinct().Where(t => m.InBounds(t) && m.TerrainAt(t).IsPassable() && MapGen.Manhattan(t, me.Mind.Camp) <= radius && !Seen(t))
                // Toward the ground it has seen least of: nearest camp first, then the fewest places visited around it
                // (old, this rebuild: then by tile id, so every search swept west and south first - seed 5's only
                // timber is 24 tiles east).
                .OrderBy(t => MapGen.Manhattan(t, me.Mind.Camp)).ThenBy(t => visited.Count(v => MapGen.Manhattan(v, t) <= 2 * ExploreLegTiles)).ThenBy(t => t).ToList();
            foreach (var t in ring.Take(3))
                if (Go(me, t, warming: warming) is { } go)
                {
                    me.Commits.Add(() => me.Mind.Heading = t);
                    return go;
                }
        }
        return null;
    }

    /// How far each look around goes before it picks the next unseen ground.
    const int ExploreLegTiles = 6;

    static ForagePool? ForagePoolAt(Me me, TileId t) => Pools.At(me.World, t) is { Count: > 0 } pools ? pools[0] as ForagePool : null;

    /// What an hour of this forage is worth to this search, at this density: kilocalories (in
    /// thousands) of food, kilograms of fuel, or both for a general search.
    static double Value(ForagePool def, double density, ForageFocus focus) => density * def.Items.Sum(i =>
    {
        var category = Items.Resources[i.Resource].Category;
        double worth = category == ResourceCategory.Food && focus is ForageFocus.Food or ForageFocus.General ? Items.Resources[i.Resource].KcalPerKg / 1000
            : category == ResourceCategory.Fuel && focus is ForageFocus.Fuel or ForageFocus.General ? 1 : 0;
        return i.Abundance * (i.MinKg + i.MaxKg) / 2 * worth;
    });

    /// A fire starter and something to light with, here or in the pack.
    static bool CanLight(Me me) =>
        Starters.Any(t => Items.Tool(me.Pack, t) != null)
        && (Tinders.Any(r => me.Has(r) >= StrikeTinderKg) || me.Has(Resource.Kindling) >= KindlingAsTinderKg);

    /// Old IsBeyondExploreLeash: bolder NPCs range further from camp, about 2-10 tiles.
    static bool InRange(Me me, TileId t) => MapGen.Manhattan(t, me.Mind.Camp) <= (int)(2 + me.Mind.Boldness * 8);

    // ---- hunting and threats ----

    /// Core temperature on reaching camp, walking home from here at today's going (WalkMinutes) through the
    /// open air of the tiles it crosses (WalkCoolingFPerHour). At camp, the core itself. The warmth need and the
    /// trip budget read the same reckoning, so a trip it set out on warm enough does not look too cold one tile out
    /// (old: the walk home's chill without the heat the clothes had stored, so one tile out it was "cold",
    /// turned home, was warm again at camp and set out again - ten times in an afternoon, seed 17 day 16).
    static double CoreAtHomeF(Me me) =>
        CoreAfterF(me, WalkMinutes(me, me.Tile, me.Mind.Camp), WalkCoolingFPerHour(me, me.Tile, me.Mind.Camp));

    /// Core temperature after this many minutes cooling at this rate (°F an hour; 0 when it warms): the core gives up
    /// its heat above the setpoint first, then the clothes theirs (Body.ClothingHeatStoredF), then the core again -
    /// ProcessTemperature's order. A straight line from the rate now (the rate falls a little as the skin cools, so
    /// it errs cold).
    static double CoreAfterF(Me me, double minutes, double coolingFPerHour)
    {
        var body = me.Agent.Body;
        double chillF = coolingFPerHour * minutes / 60;
        double aboveF = Math.Max(0, body.CoreTempF - Body.BaseCoreTempF);
        return body.CoreTempF - Math.Min(chillF, aboveF) - Math.Max(0, chillF - aboveF - Body.ClothingHeatStoredF(body));
    }

    /// CoreAfterF the other way round: how many minutes at this rate before the core is down to lineF.
    static double MinutesUntilCoreF(Me me, double lineF, double rateFPerHour)
    {
        var body = me.Agent.Body;
        // Warming, never: under the line already or not (old, this rebuild: -1 under it before asking whether it
        // warms, so a walk that warms was refused to a cold NPC).
        if (rateFPerHour <= 0) return double.PositiveInfinity;
        double dropF = body.CoreTempF - lineF;
        if (dropF < 0) return -1;
        double aboveF = Math.Max(0, body.CoreTempF - Body.BaseCoreTempF);
        double chillF = dropF <= aboveF ? dropF : dropF + Body.ClothingHeatStoredF(body);
        return chillF / rateFPerHour * 60;
    }

    /// How fast a walk from one tile to another cools it, in °F an hour (0 when it warms): the tiles it crosses, each
    /// for a crossing's share of the walk. A crossing is lived on the tile it leaves (Traversal.Advance moves it on
    /// the last minute), so the tiles counted are the ones left, the start among them and the end not. The way is the
    /// straight staircase between them, not Route's search: a forecast is asked for every tile a trip is weighed
    /// against, and in open country the two agree. A walk of no tiles cools as the tile it stands on.
    // Old (this rebuild): the tile it stood on for the whole walk, which it claimed matched the trip outside; from a
    // camp at a quarter of a plain's wind (0.4 against 1.2) it forecast -3.93 F/h for a walk that cooled it -8.04.
    static double WalkCoolingFPerHour(Me me, TileId from, TileId to)
    {
        double sum = 0, weight = 0;
        AddWalk(me, from, to, WalkMinutes(me, from, to), ref sum, ref weight);
        return weight > 0 ? sum / weight : TileCoolingFPerHour(me, from);
    }

    /// A trip's cooling (Afford): the walk out, the work at the far end at that tile's walking rate, and the walk home.
    static double TripCoolingFPerHour(Me me, TileId to, double outMinutes, int workMinutes, TileId home, double backMinutes)
    {
        double sum = 0, weight = 0;
        AddWalk(me, me.Tile, to, outMinutes, ref sum, ref weight);
        if (workMinutes > 0) { sum += workMinutes * TileCoolingFPerHour(me, to); weight += workMinutes; }
        AddWalk(me, to, home, backMinutes, ref sum, ref weight);
        return weight > 0 ? sum / weight : TileCoolingFPerHour(me, me.Tile);
    }

    static void AddWalk(Me me, TileId from, TileId to, double minutes, ref double sum, ref double weight)
    {
        int tiles = MapGen.Manhattan(from, to);
        if (tiles == 0 || minutes <= 0) return;
        double each = minutes / tiles;
        for (var at = from; at != to; at = StepToward(at, to))
        {
            sum += each * TileCoolingFPerHour(me, at);
            weight += each;
        }
    }

    /// One tile along the staircase toward a tile, the longer way first.
    static TileId StepToward(TileId at, TileId to)
    {
        int dx = to.X - at.X, dy = to.Y - at.Y;
        return Math.Abs(dx) >= Math.Abs(dy) ? new TileId(at.X + Math.Sign(dx), at.Y) : new TileId(at.X, at.Y + Math.Sign(dy));
    }

    /// How fast walking across a tile cools it, in °F an hour (0 when it warms): that tile's open air and wind
    /// (Exposure.Walking), no fire's warmth and no shelter, on its own body, clothes and pace now. Once a decision.
    static double TileCoolingFPerHour(Me me, TileId t)
    {
        if (me.CoolingByTile.TryGetValue(t, out double known)) return known;
        me.Walking ??= Surroundings(me, new Travel(me.Id, Direction.North, Pace.Quick));
        var (airF, windLevel) = Exposure.Walking(me.World, t, me.Sky);
        double rate = -Math.Min(0, Body.TemperatureChangeFPerHour(me.Agent.Body, me.Walking with { AirTempF = airF, WindLevel = windLevel }));
        return me.CoolingByTile[t] = rate;
    }

    /// For a test: the cooling an NPC forecasts for a walk from where it stands to a tile, °F an hour.
    internal static double ForecastWalkCoolingFPerHour(World world, Agent agent, TileId to) =>
        WalkCoolingFPerHour(new Me(world, agent, (NpcMind)agent.Mind!, []), agent.Tile, to);

    /// How fast doing this here, keeping still or not, cools it, in °F an hour (0 when it warms): shelter,
    /// bedding and any fire here as they are now.
    static double CoolingFPerHour(Me me, Activity a) =>
        -Math.Min(0, Body.TemperatureChangeFPerHour(me.Agent.Body, Surroundings(me, a)));

    /// What doing this here would surround it with, the fires here caught up to now first: a forecast reads a fire as
    /// it is, wherever the forecast is asked from. Old (this rebuild): it read the pit as last caught up, and a forecast
    /// asked outside a decision (ForageTripFits) threw on a fire minutes behind.
    static SurvivalContext Surroundings(Me me, Activity a)
    {
        Fire.CatchUpAround(me.World, me.Agent, me.Events);
        return Sim.Surroundings(me.World, me.Agent, a, me.Events);
    }

    // ---- the trip budget ----

    /// The one budget every trip is held to - wood, forage, water, exploring, a hunt, and work away from
    /// camp (Try): the walk out, the work there and the walk home must have it home warm enough (TripHomeCoreF,
    /// plus bufferF, at the cooling of the tiles the trip crosses), rested enough (wanted rest) and, unless what it is for outranks the
    /// evening, by HomeMinuteOfDay. The minutes of work there it can afford, up to workMinutes; null when
    /// it cannot afford the walks, or affords too little work to be worth them. Old: only a wood trip from
    /// camp was checked for cold, by the walks alone, against freezing; the forage trip by the walks
    /// against wanting warmth; the evening fetch against 21:00; work out at the trees not at all; and
    /// nothing reckoned energy, so a search for timber walked 27 tiles out and on at no energy (seeds 5, 7, 17).
    // Tried: energy budgeted down to critical rest rather than wanted; the 16-seed 60-day mean death day
    // fell from 34.8 to 31.6, seeds 5 and 7 dying out on long walks.
    // Warming: a walk from a camp with no fire to be had, for what will make one (Wood's warming trip). Staying
    // is colder than going, so it is held to what WarmingWalk asks - there short of lasting harm (LethalCoreF), with
    // WarmingReserveMinutes of legs left - not to the wanted lines, which refuse every walk to an NPC already cold
    // or tired. The trees are home on a warming trip (Wood), so there is no walk back to reckon.
    static int? Afford(Me me, TileId to, int workMinutes, TileId home, double bufferF, bool warming = false)
    {
        double outMinutes = WalkMinutes(me, me.Tile, to), backMinutes = WalkMinutes(me, to, home);
        double cooling = TripCoolingFPerHour(me, to, outMinutes, workMinutes, home, backMinutes);
        double spare = BudgetMinutes(me, bufferF, warming, cooling) - outMinutes - backMinutes;
        if (spare < 0) return null;
        int work = (int)Math.Min(workMinutes, Math.Floor(spare));
        return work < Math.Min(workMinutes, MinWorkMinutes) ? null : work;
    }

    /// Minutes from now it can be out, cooling at this rate, before it must be home (Afford). Before sunrise there is
    /// no light to be out by, short of what outranks the evening (StaysOut).
    static double BudgetMinutes(Me me, double bufferF, bool warming, double coolingFPerHour)
    {
        double warm = MinutesUntilCoreF(me, warming ? LethalCoreF : TripHomeCoreF + bufferF, coolingFPerHour);
        double rested = me.Agent.Body.EnergyMinutes - (warming ? WarmingReserveMinutes : RestNeedPct * Body.MaxEnergyMinutes);
        int now = Weather.MinuteOfDay(me.World.Minute);
        double light = me.StaysOut ? double.PositiveInfinity
            : now < Weather.SunriseHour(me.World.Minute) * 60 ? -1 : HomeMinuteOfDay(me) - now;
        return Math.Min(warm, Math.Min(rested, light));
    }

    /// The core a trip must bring it home above (Afford), before any setting-out margin: the old wanted line, inside
    /// warmth's Need (WarmNeedCoreF). Need means what it takes to get through the day, and a wood trip is exactly
    /// that, so trips are not held to the line where Need begins. Old (this rebuild, briefly): WarmNeedCoreF, which
    /// left a blizzard-bound NPC 0.17F of budget at its fire, so no wood trip ever fit, its fire died and it froze on
    /// day 16 (seed 17); at this line it lives the 60 days.
    const double TripHomeCoreF = 96.8;

    /// For a test: whether a food forage stint on this ground, from where it stands now, still fits the trip budget.
    internal static bool ForageTripFits(World world, Agent agent, TileId ground) =>
        Afford(new Me(world, agent, (NpcMind)agent.Mind!, []), ground, FoodForageMinutes, ((NpcMind)agent.Mind!).Camp, 0) != null;

    /// For a test: whether the warming trip to these trees (Wood's, the trees home once it sets out) fits the budget from here.
    internal static bool WarmingTripFits(World world, Agent agent, TileId trees) =>
        Afford(new Me(world, agent, (NpcMind)agent.Mind!, []), trees, WoodTripWorkMinutes, trees, 0, warming: true) != null;

    /// The longest stint of work it takes on with warmth Critical, or with the core that near the line (Try).
    const int CriticalWorkMinutes = 15;

    /// Less work than this is not worth a walk (Afford).
    const int MinWorkMinutes = 15;

    /// Out from camp, and the walk home - the way it would really go (RouteMinutes) - would have it spent, or past
    /// lasting harm (LethalCoreF), before it got there, and it carries what it takes to make a fire where it
    /// stands (CanMakeFireHere): then home is here. Never within NeverStrandedTiles of camp, and never while camp
    /// has a fire burning or in embers (CampHasWarmth), unless it is too spent to walk there at all: a fire there
    /// beats one it has yet to make. Old (this rebuild): any walk home of an hour or more, reckoned by every crossing off the
    /// tile averaged (63 minutes for a 36-minute step, seed 7) against the critical line, not a lethal one - so it
    /// moved camp a tile from its own roaring fire, shelter and stash, and died making a new one (seeds 1000, 31,
    /// 7, 11: the four earliest deaths, days 26.5-30.8).
    static bool Stranded(Me me)
    {
        if (me.AtCamp || MapGen.Manhattan(me.Tile, me.Mind.Camp) <= NeverStrandedTiles || !CanMakeFireHere(me)) return false;
        double walk = RouteMinutes(me, me.Mind.Camp) ?? double.PositiveInfinity;
        if (me.Agent.Body.EnergyMinutes < walk) return true;
        return !CampHasWarmth(me) && CoreAfterF(me, walk, WalkCoolingFPerHour(me, me.Tile, me.Mind.Camp)) < LethalCoreF;
    }

    /// This close to camp it always walks home (Stranded).
    const int NeverStrandedTiles = 2;

    /// A fire starter, tinder, and MorningFireHours of fuel in the pack: a fire it can make wherever it stands.
    static bool CanMakeFireHere(Me me) =>
        CanLight(me) && Woods.Append(Resource.Kindling).Sum(r => Fire.FuelHours(r, me.Has(r))) >= MorningFireHours;

    /// Whether camp has a fire waiting, as far as the NPC can tell (plan decision 7): in sight, burning or in embers
    /// as it is; out of it, the fire it left reckoned burned down since (CampFireHours). A shelter is not warmth
    /// against a lethal walk: without a fire it only slows the cooling at the end of it. Old (this rebuild): any
    /// remembered phase but Cold, however long ago, or a remembered shelter, so a fire left roaring two days ago
    /// sent it ten tiles home through -35 to a cold pit with the makings of a fire on its back (review probe P2b).
    static bool CampHasWarmth(Me me)
    {
        if (Sight.InSight(me.World, me.Agent, me.Mind.Camp))
            return Fire.CurrentPitAt(me.World, me.Mind.Camp) is { } pit && (Fire.IsActive(pit) || pit.HasEmbers);
        return CampFireHours(me, me.Mind.Memory.Places.GetValueOrDefault(me.Mind.Camp)) > 0;
    }

    /// The first step of a walk that ends at warmth - home, or makings it will light where it arrives - when it
    /// has the legs for the walk and will get there short of lasting harm (LethalCoreF), the walk reckoned the way it
    /// would really go (RouteMinutes). Not held to the trip budget: there is no walk back to budget for, and
    /// staying where it is cold is what it is walking out of.
    static Plan? WarmingWalk(Me me, TileId target)
    {
        if (RouteMinutes(me, target) is not double walk) return null;
        if (me.Agent.Body.EnergyMinutes < walk + WarmingReserveMinutes || CoreAfterF(me, walk, WalkCoolingFPerHour(me, me.Tile, target)) < LethalCoreF) return null;
        return Step(me, target);
    }

    /// Energy a warming walk keeps in hand on arrival, to strike, stoke or start felling (WarmingWalk, Afford(warming)):
    /// a walk that ends spent ends with no fire made. Judgment call, under the critical rest line (RestCriticalPct,
    /// 96 minutes), which would refuse the walk next door to a spent NPC's own makings (seed 11). Old (this rebuild):
    /// none, harmless while a warming walk lasted one step and live once it carries on to the trees.
    const double WarmingReserveMinutes = 45;

    /// Under this core a walk does lasting harm: where the body's cold harm begins (Body.HypothermiaThresholdF, the
    /// start of BodySurvival's cold curve). Old (this rebuild): 93.5F, a judgment call under that line from when the
    /// old body killed fast below it; with a core that can now fall to 85F alive, every walk was barred for hours
    /// below 93.5 while the cold was already doing harm above it.
    const double LethalCoreF = Body.HypothermiaThresholdF;

    /// When it must be home by: the evening (HomeByHour), or sunset if that comes first.
    static int HomeMinuteOfDay(Me me) => Math.Min(HomeByHour, Weather.SunsetHour(me.World.Minute)) * 60;

    /// Where the predator it last saw close was, while that is fresh.
    static TileId? FreshThreat(Me me) =>
        me.Mind.ThreatTile is TileId t && me.World.Minute - me.Mind.ThreatMinute < ThreatMemoryMinutes ? t : null;

    /// Old GetPriorityThreat, reaching one tile further: the biggest, healthiest predator here or
    /// next door, not already in a fight.
    // Threat, FindFood's prey and AllyInEncounter read herds' true tiles and bodies from world.Agents, gated
    // only by a fixed radius (1 and 3 tiles); use the sight rays (Sight.InSight) for them too.
    static Agent? Threat(Me me) =>
        Herds(me, 1, h => Fauna.IsPredator(h.Species) && Unengaged(me, h))
            .OrderByDescending(h => h.Members.Where(Body.IsAlive).Max(m => m.WeightKg * Body.Vitality(m))).ThenBy(h => h.Id).FirstOrDefault();

    /// A herd no fight has hold of (in the field or listed at all): one already fighting someone is
    /// neither prey to start a hunt on nor a threat to start a fight with - Combat.Create would refuse
    /// it (old: a hungry NPC beside the player's bison fight called BeginHunt on that bison and threw).
    static bool Unengaged(Me me, Agent herd) => !Sim.Held(me.World, herd.Id) && Combat.EncounterOf(me.World, herd.Id) == null;

    static IEnumerable<Agent> Herds(Me me, int withinTiles, Func<Agent, bool> kind) =>
        Nearby.Within(me.World, me.Tile, withinTiles).Where(a => a.Mind is HerdMind && a.Members.Any(Body.IsAlive) && kind(a));

    /// Someone else on this tile fighting predators, whom this NPC would stand by (old WouldDefend).
    static (Encounter Encounter, int Team)? AllyInEncounter(Me me)
    {
        foreach (var enc in me.World.Encounters.Where(e => e.Tile == me.Tile && e.Units.All(u => u.Agent != me.Id)))
            foreach (var ally in Combat.Field(enc).Where(u => me.World.Agent(u.Agent).Mind is PersonMind))
            {
                var foe = Combat.Field(enc).FirstOrDefault(u => u.Team != ally.Team && Fauna.IsPredator(me.World.Agent(u.Agent).Species));
                if (foe != null && WouldDefend(me, ally.Agent, Combat.BodyOf(me.World, foe))) return (enc, ally.Team);
            }
        return null;
    }

    /// Old NPC.WouldDefend: opinion, being armed and whole, boldness, and how big the threat is.
    static bool WouldDefend(Me me, AgentId ally, BodyState threat)
    {
        double opinion = me.Mind.Opinions.GetValueOrDefault(ally);
        if (opinion < -0.3) return false;
        double willing = (opinion + 1) / 2;
        double self = (Weapon(me) != null ? 1.2 : 0.6) * (Body.Vitality(me.Agent.Body) < 0.7 ? 0.7 : 1.0);
        double bold = 0.5 + me.Mind.Boldness * 0.5;
        double size = 1.0 / (1.0 + Body.Vitality(threat) * threat.WeightKg / 100);
        return willing * self * bold * size > 0.35;
    }

    static Plan? RespondToThreat(Me me)
    {
        if (AllyInEncounter(me) is { } ally)
        {
            Combat.Join(me.World, ally.Encounter, new Fighter(me.Id) { Weapon = Weapon(me) }, ally.Team);
            return (new Wait(me.Id), 1); // the fight's rounds move it from here
        }
        var threat = Threat(me);
        var self = new Fighter(me.Id) { Weapon = Weapon(me), BoldnessDelta = me.Mind.Boldness - 0.5 };
        if (threat != null && threat.Tile == me.Tile && StandsAgainst(me, self, threat))
        {
            var lead = threat.Members.FindIndex(Body.IsAlive);
            var enc = Combat.Create(me.World, [self], Combat.SendMembers(me.World, threat.Id, lead),
                me.Tile, ThreatMetFromM, Awareness.Engaged, Awareness.Engaged, events: me.Events);
            return Fought(me, enc);
        }
        return Flee(me, threat?.Tile ?? FreshThreat(me)!.Value);
    }

    /// A threat on its own tile is met at a few metres (old RespondToThreat's 8 m).
    const int ThreatMetFromM = 8;

    /// Fight or flight, from the fight's own state machine (combat round 2): it stands if its resolve at
    /// the distance the threat is met from leaves it pressing or standing off, its nature (Boldness, 0.3-0.7)
    /// in its courage, and runs if it would give ground. Old: DecideFlightOrFight's dice roll on boldness,
    /// being armed and the threat's size.
    static bool StandsAgainst(Me me, Fighter self, Agent threat) =>
        Combat.WouldStand(me.World, self, threat, ThreatMetFromM) is FighterState.Pressing or FighterState.Standoff;

    /// A fight this NPC started, resolved headless (as herds fight), then a minute to catch its breath.
    // Combat.Refusal lets only the player fight round by round, so an NPC's own fight resolves at once, its
    // rounds lived by its wounds alone (no fighting metabolism, no clock); step it by Fight when combat allows other agents.
    static Plan Fought(Me me, Encounter enc)
    {
        Combat.ResolveHeadless(me.World, enc, me.Events);
        return (new Wait(me.Id), 1);
    }

    /// The best weapon in the pack (Combat.WeaponWorth).
    static Gear? Weapon(Me me) => Combat.BestWeapon(me.Agent);

    /// One tile away from the threat, quick: toward camp when that is also away.
    static Plan? Flee(Me me, TileId from)
    {
        var dir = Enum.GetValues<Direction>()
            .Select(d => (Dir: d, To: Map.Step(me.Tile, d)))
            .Where(x => Passable(me, me.Tile, x.To))
            .OrderByDescending(x => MapGen.Manhattan(x.To, from)).ThenBy(x => MapGen.Manhattan(x.To, me.Mind.Camp)).ThenBy(x => x.Dir)
            .Select(x => (Direction?)x.Dir).FirstOrDefault();
        return dir is Direction d ? Try(me, new Travel(me.Id, d, Pace.Quick), 180) : null;
    }

    // ---- moving ----

    /// Home, giving up any ground it was walking to forage (ForageGoal): it chooses again from there. At the end of a
    /// wood trip (WoodGoal), what it came for is picked up first, what home lacks to make a fire before the rest
    /// (LyingOrder), whatever plan turns it for home: the trip is what brought it here.
    static Plan? GoHome(Me me)
    {
        if (me.AtCamp) return null;
        if (me.Mind.WoodGoal == me.Tile && me.RoomKg >= 1 && First(LyingOrder(me), r => me.Stashed(r) > 0) is Resource lying
            && Try(me, new Unstash(me.Id, lying, Math.Min(me.RoomKg, me.Stashed(lying))), 1) is { } pick) return pick;
        if (Go(me, me.Mind.Camp) is not { } go) return null;
        me.Commits.Add(() => (me.Mind.ForageGoal, me.Mind.WoodGoal) = (null, null));
        return go;
    }

    /// The first step of the shortest walk to target (breadth-first over passable edges, in a box
    /// around both ends), careful onto hazardous ground. Null when there is no way. Every walk but the
    /// one home is a trip held to the budget (Afford): the walk there, workMinutes of work (or as much of
    /// it as it can afford), and the walk home (camp, unless the plan moves it). Setting out from camp it
    /// wants SetOutBufferF of warmth in hand.
    static Plan? Go(Me me, TileId target, int workMinutes = 0, TileId? home = null, bool warming = false)
    {
        if (target != me.Mind.Camp && Afford(me, target, workMinutes, home ?? me.Mind.Camp, me.AtCamp && !warming ? SetOutBufferF : 0, warming) == null) return null;
        return Step(me, target);
    }

    static Plan? Step(Me me, TileId target)
    {
        if (Route(me, target) is not [var to, ..]) return null;
        var dir = Enum.GetValues<Direction>().First(d => Map.Step(me.Tile, d) == to);
        return Try(me, new Travel(me.Id, dir, PaceFor(me, me.Tile, to)), 180);
    }

    /// Careful wherever a quick step could hurt: on the way off this tile or onto the next.
    static Pace PaceFor(Me me, TileId from, TileId to) =>
        Traversal.IsHazardous(me.World, from) || Traversal.IsHazardous(me.World, to) ? Pace.Careful : Pace.Quick;

    /// Minutes for the walk to target the way Route goes, each crossing at today's going (Traversal.CrossingMinutes);
    /// null when there is no way. Not WalkMinutes, which reckons every tile at the average going off it, and so
    /// makes a 36-minute step 63 when the tile's other ways out are worse (seed 7).
    static double? RouteMinutes(Me me, TileId target)
    {
        if (target == me.Tile) return 0;
        if (Route(me, target) is not { } route) return null;
        var sky = me.Sky;
        double encumbrancePct = Items.EncumbrancePct(me.Agent), minutes = 0;
        var from = me.Tile;
        foreach (var to in route)
        {
            minutes += Traversal.CrossingMinutes(me.World, me.Agent.Body, from, to, PaceFor(me, from, to), sky, encumbrancePct);
            from = to;
        }
        return minutes;
    }

    /// The tiles of the shortest walk to target (breadth-first over passable edges, in a box around both ends),
    /// from the first step on to target; null when there is no way, or it is here, or it can barely move.
    static List<TileId>? Route(Me me, TileId target)
    {
        if (target == me.Tile) return null;
        if (Body.GetCapacities(me.Agent.Body).Moving <= 0.1) return null; // Traversal.Refusal: you can barely move
        int padTiles = 8;
        int minX = Math.Min(me.Tile.X, target.X) - padTiles, maxX = Math.Max(me.Tile.X, target.X) + padTiles;
        int minY = Math.Min(me.Tile.Y, target.Y) - padTiles, maxY = Math.Max(me.Tile.Y, target.Y) + padTiles;
        // Not past a predator it has just seen: no tile it would run from (FleeTiles, RespondToThreat), unless that is
        // where it is going. Old (this rebuild): only the tiles next to it, so a forage walk stepped back to two tiles off,
        // the flee took it back to three, and the walk stepped in again (seed 5, day 15.9: nineteen times).
        var threat = FreshThreat(me);
        var cameFrom = new Dictionary<TileId, TileId> { [target] = target };
        var queue = new Queue<TileId>([target]);
        // Searched from the target back, so the first time we reach our own tile we know our first step.
        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            foreach (var next in MapGen.Neighbors(at))
            {
                if (next.X < minX || next.X > maxX || next.Y < minY || next.Y > maxY || cameFrom.ContainsKey(next)) continue;
                if (!Passable(me, next, at)) continue;
                if (threat is TileId t && next != me.Tile && MapGen.Manhattan(next, t) < FleeTiles) continue;
                cameFrom[next] = at;
                if (next == me.Tile)
                {
                    var route = new List<TileId>();
                    for (var tile = at; ; tile = cameFrom[tile])
                    {
                        route.Add(tile);
                        if (tile == target) return route;
                    }
                }
                queue.Enqueue(next);
            }
        }
        return null;
    }

    static Resource? First(IEnumerable<Resource> rs, Func<Resource, bool> p) => rs.Where(p).Cast<Resource?>().FirstOrDefault();

    static bool Passable(Me me, TileId from, TileId to) => Map.Blocked(Map.Of(me.World), from, to) == null;

    /// The activity, if the rules would let it start: an NPC never picks what the player would be refused.
    static Plan? Try(Me me, Activity a, int minutes, bool warming = false)
    {
        string? refused = a switch
        {
            StartFire or AddFuel or LightTorch or GatherCharcoal => Fire.Refusal(me.World, a),
            Travel t => Traversal.Refusal(me.World, t),
            // Work.Refusal reads light from the fires here, which must be caught up: night work that
            // needs light is screened by the plans themselves (Me.Dark) and refused by Step if wrong.
            Butcher or Gather when me.Dark => null,
            _ when Survival.Work.Handles(a) => Survival.Work.Refusal(me.World, a),
            _ => null,
        };
        if (refused != null) return null;
        // Work is held to the warmth tier: never long enough to take the core past the Critical line at the work's
        // own chill, and with warmth Critical already, no more than a short stint before it decides again.
        if (a is Gather or Butcher)
        {
            double untilCritical = me.Warmth == Tier.Critical ? 0 : MinutesUntilCoreF(me, HypothermiaCoreF, CoolingFPerHour(me, a));
            minutes = (int)Math.Min(minutes, Math.Max(CriticalWorkMinutes, untilCritical));
        }
        // Work out here is held to the trip budget, the walk home after it (Afford).
        if (a is Gather or Butcher && !me.AtCamp && !me.Burning)
        {
            if (Afford(me, me.Tile, minutes, me.Mind.Camp, 0, warming) is not int work) return null;
            minutes = work;
        }
        return (a, minutes);
    }
}
