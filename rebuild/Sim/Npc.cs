using Plan = (Survival.Activity Activity, int Minutes);

namespace Survival;

/// The need an NPC is working on (old NeedType). Ordered as the old hierarchy: Warmth first.
public enum Need { Warmth, Water, Rest, Food }

/// An NPC's own state. Everything it acts on is in the same pack and knowledge the player has.
public sealed partial record NpcMind
{
    /// Home: where it keeps its fire and its stash, and goes back to at night. It moves to the nearest
    /// standing timber it knows of when there is none within two tiles of it (Npc.Wood).
    public TileId Camp { get; set; }
    /// Old Personality.Boldness, 0-1: how far it ranges, whether it stands and fights, whether it helps.
    /// Selfishness and Sociability are not ported: nothing they fed (following, sharing) exists yet.
    public double Boldness { get; init; } = 0.5;
    /// The need it is seeing to (old CurrentNeed): kept until satisfied, not just until it is no longer
    /// pressing, so the NPC does not live pinned at the threshold.
    public Need? Need { get; set; }
    /// Where it is walking to look around, so a search keeps one direction from step to step.
    public TileId? Heading { get; set; }
    /// The ground it is walking to forage, kept from step to step until it gets there or something
    /// clearly better turns up (Npc.ForageTarget), so it does not turn back at every tile.
    public TileId? ForageGoal { get; set; }
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
        world.Agents[id] = new Agent { Id = id, Tile = tile, Mind = mind };
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
        Knowledge.Observe(world, agent, events);
        AdaptTargetFat(mind, agent.Body, world.Minute);

        // In a fight someone else is running (the player's): its rounds move this NPC, not its own steps.
        if (Combat.EncounterOf(world, agent.Id) is Encounter fight && fight.Units.Any(u => u.Agent == agent.Id && u.InField))
            return (new Wait(agent.Id), 1);

        var b = agent.Body;
        // Old thresholds. WarmPct was core temperature over 95-98.6F: .25 critical, .5 wanted, .7 satisfied.
        // Away from the fire it judges the core it will have when it gets home: the walk back is cold too
        // (old: a fixed 97.5F away, so a two-tile trip through deep snow at -15C came home hypothermic - bug, fixed).
        double warmth = Level(me, Need.Warmth, CoreAtHomeF(me), FreezingCoreF, WantedCoreF, SatisfiedCoreF);
        double water = Level(me, Need.Water, b.HydrationPct, 0.2, 0.5, 0.9);
        double rest = Level(me, Need.Rest, b.EnergyPct, 0.1, 0.3, 0.5);
        // Food is judged by fat, not stomach (user's design): how far the body has fallen short of the
        // fat it is used to. Under WantedShortfallKg it is fine; past CriticalShortfallKg, or below the
        // floor, it is critical. The stomach only says when to eat what is carried.
        double food = Math.Max(Level(me, Need.Food, -me.ShortfallKg, -CriticalShortfallKg, -WantedShortfallKg, -SatisfiedShortfallKg),
            b.FatKg < FatFloorKg(b) ? 1 : 0);
        bool hungry = b.CaloriesPct < HungryPct;
        // Firewood by hours of burning against the hours until dawn: wood not yet in for tonight beats
        // wanting food; wood beyond it does not.
        double fuelHours = CampFuelHours(me), neededHours = HoursToDawn(me) + MorningFireHours;

        // A predator close by. On its tile it is fight or flight. Otherwise a burning fire is a place to
        // hold, and a critical need outweighs a predator that is not coming on; short of those, it runs
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
        bool critical = warmth >= 1 || water >= 1 || rest >= 1;
        bool pinned = me.Burning && from is TileId near && MapGen.Manhattan(near, me.Tile) <= 1;
        bool run = threat?.Tile == me.Tile
                   || (!me.Burning && from is TileId t && MapGen.Manhattan(t, me.Tile) < FleeTiles && (approaching || !critical));

        // A scored list: wanted is 5, critical 10, plus the old hierarchy's rank so that within a level
        // Warmth beats Water beats Rest beats Food. Any critical need outranks every merely wanted one.
        var options = new (double Score, Need? Need, Func<Me, Plan?> Act)[]
        {
            (run || AllyInFight(me) != null ? 100 : 0, null, RespondToThreat),
            // Ten minutes to eat what is in the pack is never the wrong time to be hungry (judgment call:
            // the old hierarchy kept a cold NPC from eating the berries it carried).
            (hungry && ReadyFood(me) != null ? 9.5 : 0, null, me => Eat(me, ReadyFood(me)!.Value)),
            // Nor two minutes to drink what it carries, whatever else is pressing (old: a freezing NPC fussed
            // with its fire and died of thirst with three litres on its back - bug, fixed).
            (water >= 1 && me.Has(Resource.Water) >= 0.1 ? 14.5 : 0, Need.Water, me => Try(me, new Drink(me.Id), Work.DrinkMinutes)),
            (warmth * 10 + 4, Need.Warmth, Warm),
            (water * 10 + 3, Need.Water, Water),
            (rest * 10 + 2, Need.Rest, Tired),
            // Evening and night are for being home, with the fire lit while there is light to light it by.
            (me.Hour >= HomeByHour || me.Hour < 5 ? NightScore : 0, null, Night),
            // Tonight's wood outranks wanted food: a night without it kills (the old NPC stockpiled fuel only
            // as optional work, after food). A day's spare is worth less than wanted food.
            (!me.Night && fuelHours < neededHours ? 5 + 1.5 : 0, null, Wood),
            // Critical food beats everything but critical warmth, water and rest. The design said all but warmth and
            // water; ranked over rest, a starving NPC foraged on at zero energy and died of exhaustion (seed 1, day 28).
            (food >= 1 ? 11.5 : food * 10 + 1, Need.Food, Food),
            (!me.Night && fuelHours < neededHours + SpareFireHours ? 5 + 0.5 : 0, null, Wood),
            (1, null, Errands),
        };
        // OrderByDescending is stable: equal scores keep the list's order. A plan only weighs things up;
        // what it means to keep (where it is bound, Me.Commits) is kept once the plan is the one started.
        foreach (var o in options.Where(o => o.Score > 0).OrderByDescending(o => o.Score))
        {
            me.Forget();
            if (o.Act(me) is not { } plan) continue;
            // Old behavior: it set off for the trees a minute before the evening and turned straight back.
            if (plan.Activity is Travel && TooLateToSetOut(me, o.Score)) continue;
            // Going out, the fire is banked first so it is still in when the walk home is cold (judgment
            // call: food trips range further than wood trips, and a cold NPC with a hand drill cannot relight).
            if (plan.Activity is Travel && me.AtCamp && me.Burning && o.Score < 100 && KeepFire(me, 3) is { } bank) plan = bank;
            // Holding a fire with a predator next door: nothing that walks away from it.
            if (plan.Activity is Travel && pinned && o.Score < 100) { me.Forget(); plan = KeepFire(me, 4) ?? (new Rest(me.Id), 30); }
            if (o.Need != null) mind.Need = o.Need;
            return me.Commit(plan);
        }
        me.Forget();
        return me.Commit(me.AtCamp ? (new Rest(agent.Id), 30) : GoHome(me) ?? (new Rest(agent.Id), 30));
    }

    /// 0 when fine, 0.5 when wanted (or still being seen to), 1 when critical. Low values are bad.
    static double Level(Me me, Need need, double value, double critical, double want, double satisfied)
    {
        if (me.Mind.Need == need && value >= satisfied) me.Mind.Need = null;
        if (value < critical) return 1;
        return value < want || me.Mind.Need == need ? 0.5 : 0;
    }

    const int HomeByHour = 19;
    /// Work.TooDarkToWork's night begins.
    const int NightHour = 21;
    /// Evening's place in the scored list: over wanted needs and tonight's wood, under critical ones.
    const double NightScore = 5 + 1.75;
    /// How far it runs from a predator, and how long it keeps one in mind.
    const int FleeTiles = 3;
    public const int ThreatMemoryMinutes = 120;
    const int SightTiles = Sight.RadiusTiles;
    const double MinLightChance = 0.2;
    /// Critical cold (old WarmPct 0.25): any chance of a fire beats none.
    const double FreezingCoreF = 95.9, DesperateLightChance = 0.02;
    /// Below this core (old WarmPct 0.5) it wants to be warm; above SatisfiedCoreF (0.7) it is warm again.
    const double WantedCoreF = 96.8, SatisfiedCoreF = 97.8;
    /// Wanting to be warm, any strike is worth the tinder but one at the 5% floor.
    const double WantedLightChance = 0.06;
    /// Raw meat is eaten raw below this.
    const double StarvingFatPct = 0.06;
    /// Below this the body starts paying for leanness (Body.Speed, Body.ColdResistance); TargetFatKg never goes under it.
    public const double FatFloorPct = 0.10;
    public const double TargetFatHalfLifeDays = 3;
    const double WantedShortfallKg = 0.3, CriticalShortfallKg = 1.0, SatisfiedShortfallKg = 0.1;
    /// The stomach store below which it eats what it carries.
    const double HungryPct = 0.3;
    /// Wood in before dark: enough to burn until dawn plus a morning fire, then a spare half-day
    /// (old FuelStockpileTargetKg was 40 kg; 30 here before these were hours).
    const double MorningFireHours = 2, SpareFireHours = 12;
    /// Fuel at camp below this, a cold NPC by its fire goes for wood rather than wait on the fire.
    const double LowFuelHours = 4;
    /// On the fire before sleeping: near a full open pit, a night of birch.
    const double BankKg = 9;
    /// Kept back from the fire so there is always something to light the next one with.
    const double KindlingReserveKg = 0.6;
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
        /// Work.TooDarkToWork's night.
        public bool Night => Hour < 5 || Hour >= NightHour;
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
        /// The walk the plan being weighed sets out on (Go): where to, the work planned there, and
        /// where it comes home to after.
        public (TileId To, int WorkMinutes, TileId Home)? Trip { get; set; }
        public void Forget() { Commits.Clear(); Trip = null; }
        public Plan Commit(Plan plan)
        {
            foreach (var keep in Commits) keep();
            Forget();
            return plan;
        }
    }

    static double FatFloorKg(BodyState b) => FatFloorPct * b.WeightKg;

    // ---- the plans: each returns an activity, or null when it can do nothing about its need ----

    static Plan? Warm(Me me)
    {
        // Out for the wood its fire is short of, and not yet freezing: get it, then go home (Wood turns for home).
        bool fetching = !me.Night && CampFuelHours(me) < LowFuelHours && CoreAtHomeF(me) >= FreezingCoreF;
        if (!me.AtCamp && fetching && Wood(me) is { } wood) return wood;
        // Home, if there is a fire to be had there; walking back to a cold pit with nothing to light it is no use.
        if (!me.AtCamp && CanLight(me) && MapGen.Manhattan(me.Tile, me.Mind.Camp) <= 12 && GoHome(me) is { } home) return home;
        // At camp, or too far to walk back cold: the fire is here.
        if (me.Burning)
            return KeepFire(me, 5)
                // The fire burning is all the fuel there is: by day, fetch more while it lasts (old: it sat by
                // the fire until it was nearly out, then groped the bare camp ground for sticks - bug, fixed).
                ?? (me.AtCamp && fetching ? FetchWood(me) : null)
                ?? (Fire.TotalKg(me.Pit!) < 1.5 ? Forage(me, minutes: 15, wander: false, ForageFocus.Fuel) : null) // feed it or lose it
                ?? AtFire(me) ?? (new Rest(me.Id), 20);
        if (KeepFire(me, 3) is { } fire) return fire;
        // Nothing to light it with: by day go and get some (working warms you), by night grope for it here.
        return (me.Night ? null : Wood(me)) ?? Forage(me, minutes: 15, wander: false, ForageFocus.Fuel);
    }

    /// Wood, from camp, if it would not come home from the walk freezing: the same line it turns home
    /// at when out (CoreAtHomeF against FreezingCoreF), reckoned before it goes. Old: from camp it set
    /// off for the trees by its core at camp, and one tile out found it freezing and turned home, every
    /// fifteen minutes through a cold morning (seed 17, day 16).
    static Plan? FetchWood(Me me)
    {
        if (Wood(me) is not { } wood) return null;
        if (wood.Activity is not Travel || me.Trip is not { } trip) return wood;
        double walkHours = (WalkMinutes(me, me.Tile, trip.To) + WalkMinutes(me, trip.To, trip.Home)) / 60;
        if (me.Agent.Body.CoreTempF - WalkingCoolingFPerHour(me) * walkHours >= FreezingCoreF) return wood;
        me.Forget();
        return null;
    }

    static Plan? Water(Me me)
    {
        if (me.Has(Resource.Water) >= 0.1 || Work.HasOpenWater(me.World, me.Tile)) return Try(me, new Drink(me.Id), Work.DrinkMinutes);
        if (me.Stashed(Resource.Water) > 0) return Try(me, new Unstash(me.Id, Resource.Water, 1), 1);
        if (me.Burning) return Try(me, new MeltSnow(me.Id), Work.MeltSnowMinutes);
        if (me.AtCamp && KeepFire(me, 3) is { } fire) return fire;
        // Old DetermineGetResource(Water): the nearest open water it knows of.
        var river = me.Mind.Knows.Places.Where(p => p.Value.Water && MapGen.Manhattan(p.Key, me.Tile) <= MaxWoodTiles).Select(p => p.Key)
            .OrderBy(t => MapGen.Manhattan(me.Tile, t)).ThenBy(t => t).Cast<TileId?>().FirstOrDefault();
        return (river is TileId r ? Go(me, r, Work.DrinkMinutes) : null) ?? GoHome(me);
    }

    static Plan? Tired(Me me) => me.AtCamp ? Bed(me)
        : GoHome(me) ?? (me.Agent.Body.EnergyPct < 0.1 ? (new Sleep(me.Id), 60) : null); // too spent to walk home: sleep where it stands

    static Plan? Night(Me me)
    {
        // Too little for the night, and light enough left to fetch more and be back by dark: fetch it
        // (old: home at seven with an hour of wood, it slept beside the fire until it went out).
        if (!me.Dark && CampFuelHours(me) < HoursToDawn(me) && Wood(me) is { } wood && BackByDark(me, wood)) return wood;
        me.Forget();
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
        bool short_ = pit != null && (Fire.IsActive(pit) || pit.HasEmbers) && Fire.TotalKg(pit) < BankKg - 0.5;
        // Logs that will not go on a fire this cool yet: sit up with it until it catches, unless too spent to.
        if (short_ && Fire.IsActive(pit!) && me.Agent.Body.EnergyPct > 0.15 && Woods.Any(r => me.Has(r) + me.Stashed(r) > 0)) return (new Rest(me.Id), 15);
        if (!short_) return (new Sleep(me.Id), 120);
        // Nothing to bank it with. Going out, or in embers: find it something while it will still take it.
        double minutesLeft = pit!.HasEmbers ? 0 : Fire.FuelHours(pit) * 60;
        if (minutesLeft < WakeBeforeOutMinutes && Forage(me, minutes: 15, wander: false, ForageFocus.Fuel) is { } grope) return grope;
        return (new Sleep(me.Id), (int)Math.Clamp(minutesLeft - WakeBeforeOutMinutes, WakeBeforeOutMinutes, 120));
    }

    /// How long before its fire is out a sleeper with nothing to bank it wakes: under the shortest embers (Fire.ToEmbers).
    const int WakeBeforeOutMinutes = 15;

    /// Whether the plan being weighed - the trip it sets out on (Me.Trip), or its work here and the walk
    /// home - has it home again while there is light to work by (Me.Night, Work.TooDarkToWork).
    static bool BackByDark(Me me, Plan plan) => Weather.MinuteOfDay(me.World.Minute)
        + (me.Trip is { } trip ? TripMinutes(me, trip.To, trip.WorkMinutes, trip.Home) : plan.Minutes + WalkMinutes(me, me.Tile, me.Mind.Camp))
        <= NightHour * 60;

    /// Daytime: keep the camp in firewood. Fell a tree, carry it home a load at a time.
    static Plan? Wood(Me me)
    {
        double carried = Woods.Sum(me.Has) + me.Has(Resource.Kindling);
        if (me.AtCamp) { if (Unload(me) is { } stash) return stash; }
        else if (carried >= 5 || me.RoomKg < 1.5) return GoHome(me);
        // What a felled tree left on the ground here.
        if (!me.AtCamp && me.RoomKg >= 1 && First(Woods.Append(Resource.Kindling).OrderByDescending(me.Stashed), r => me.Stashed(r) > 0) is Resource lying)
            return Try(me, new Unstash(me.Id, lying, me.RoomKg), 1);
        bool axe = Items.Tool(me.Pack, ToolType.Axe) != null;
        if (TreesHere(me) is int trees && axe) return Try(me, new Gather(me.Id, trees), 60);
        TileId? Nearest(TileId from, int within) => me.Mind.Knows.Places.Where(p => p.Value.Trees && MapGen.Manhattan(p.Key, from) <= within).Select(p => p.Key)
            .OrderBy(t => MapGen.Manhattan(from, t)).ThenBy(t => t).Cast<TileId?>().FirstOrDefault();
        // A camp with no timber near it is not worth hauling to: home moves to the trees, and what was
        // stashed at the old one stays there (old design: depletion forces camp relocation). Timber out
        // of carrying reach is still timber to move to (old: none within 15 tiles, so it picked deadfall
        // with an axe it never used until it froze - seed 5, nearest stand 24 tiles off).
        var home = me.Mind.Camp;
        if (axe && Nearest(me.Mind.Camp, TimberSearchTiles) is TileId stand && MapGen.Manhattan(stand, me.Mind.Camp) > 2) home = stand;
        if (axe && (Nearest(me.Tile, MaxWoodTiles) ?? (home != me.Mind.Camp ? home : null)) is TileId t && Go(me, t, WoodTripWorkMinutes, home) is { } go)
        {
            if (home != me.Mind.Camp) me.Commits.Add(() => me.Mind.Camp = home);
            return go;
        }
        // No timber known: look for some - past carrying reach, since a stand found there moves the camp - then fallen wood.
        return (axe ? Explore(me, TimberSearchTiles) : null) ?? Forage(me, minutes: 30, wander: true, ForageFocus.Fuel);
    }

    /// Food in the pack fit to eat now, richest first (old HandleFoodNeed: cooked before other food).
    /// Raw meat waits for a fire unless the NPC is starving.
    static Resource? ReadyFood(Me me) => First(
        me.Pack.Kg.Keys.Where(r => Items.Resources[r].Category == ResourceCategory.Food).OrderByDescending(r => Items.Resources[r].KcalPerKg).ThenBy(r => r),
        r => r is not (Resource.RawMeat or Resource.RawFish) || me.Agent.Body.FatPct < StarvingFatPct);

    /// As much as the stomach has room for, and as much again as the fat it is short of, up to a
    /// second store's worth at one sitting: what the store cannot hold goes on as fat (Body.Eat).
    static Plan? Eat(Me me, Resource food)
    {
        double surplusKcal = Math.Clamp(me.ShortfallKg * Body.KcalPerKgFat, 0, Body.MaxCaloriesKcal);
        double kg = Math.Min(me.Has(food), (Body.MaxCaloriesKcal - me.Agent.Body.CaloriesKcal + surplusKcal) / Items.Resources[food].KcalPerKg);
        return kg >= 0.01 ? Try(me, new Eat(me.Id, food, kg), Work.EatMinutes) : null;
    }

    static Plan? Food(Me me)
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
        return FindFood(me);
    }

    /// Where the next meal is likeliest to come from, by expected kilocalories an hour, best first.
    static Plan? FindFood(Me me)
    {
        var options = new List<(double KcalPerHour, Func<Plan?> Act)>();
        bool wander = me.Hour < HomeByHour - 1;
        if (ForageTarget(me, ForageFocus.Food, wander) is { } ground)
            options.Add((ground.PerHour * 1000, () => Forage(me, minutes: 45, wander, ForageFocus.Food)));
        if (Weapon(me) != null && Body.Vitality(me.Agent.Body) >= 0.7)
            foreach (var prey in Herds(me, 3, h => Fauna.BehaviorOf(h.Species) == HerdBehavior.Prey && Unengaged(me, h)).OrderBy(h => h.Id))
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

    /// A stalk of an hour and two of butchering (judgment calls).
    const int HuntWorkMinutes = 3 * 60;
    /// A wood trip: an hour felling or picking up, then a load home.
    const int WoodTripWorkMinutes = 60;

    static Plan? Hunt(Me me, Agent prey)
    {
        if (prey.Tile != me.Tile) return Go(me, prey.Tile, HuntWorkMinutes);
        // The player's hunt: a stalk from Fauna.HuntStartDistanceM at one member, a few herd mates with it.
        return Fought(me, Fauna.BeginHunt(me.World, me.Id, prey.Id, prey.Members.FindIndex(Body.IsAlive), Weapon(me)));
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
        if (Woods.Any(r => me.Has(r) + me.Stashed(r) > 0)) reserve = 0;
        double spare = me.Has(Resource.Kindling) + me.Stashed(Resource.Kindling) - reserve;
        if (spare <= 0.1) return null;
        if (me.Has(Resource.Kindling) - reserve > 0.1) return Try(me, new AddFuel(me.Id, Resource.Kindling, Math.Min(kg, me.Has(Resource.Kindling) - reserve)), 1);
        return me.Stashed(Resource.Kindling) > 0 ? Try(me, new Unstash(me.Id, Resource.Kindling, Math.Min(kg + reserve, me.Stashed(Resource.Kindling))), 1) : null;
    }

    /// Old FireHandler: the best working fire starter, the best tinder, a handful of kindling.
    static Plan? Light(Me me)
    {
        var tool = Starters.Cast<ToolType?>().FirstOrDefault(t => Items.Tool(me.Pack, t!.Value) != null);
        if (tool == null) return null; // the old NPC crafted a fire starter here; there is no crafting yet
        var tinder = First(Tinders, r => me.Has(r) >= 0.05);
        if (tinder == null && First(Tinders, r => me.Stashed(r) > 0) is Resource stashed)
            return Try(me, new Unstash(me.Id, stashed, 0.3), 1);
        if (me.Has(Resource.Kindling) < 0.35 && me.Stashed(Resource.Kindling) > 0)
            return Try(me, new Unstash(me.Id, Resource.Kindling, 1.0), 1);
        tinder ??= me.Has(Resource.Kindling) >= 0.5 ? Resource.Kindling : null; // sticks shaved for tinder, while there are sticks to spare
        if (tinder == null) return null;
        // Not worth the tinder in the dark with a poor drill: wait for light, or for better tinder - unless
        // cold, when waiting is what kills (old behavior: it hauled wood to a fire it would not try to light).
        double minChance = LightChanceWorthIt(me.Agent.Body.CoreTempF);
        // The odds the strike will roll on: the same sheet the player sees (old: hands in the dark and nothing else).
        if (Fire.StartOdds(me.World, me.Agent, tool.Value, tinder.Value).TotalPct < minChance) return null;
        double kindling = Math.Min(0.3, me.Has(Resource.Kindling) - (tinder == Resource.Kindling ? 0.05 : 0));
        if (kindling < 0) return null;
        return Try(me, new StartFire(me.Id, tool.Value, tinder.Value, 0.05, kindling), 1);
    }

    /// The odds a strike must have to be worth the tinder: MinLightChance warm, falling as it gets colder
    /// to WantedLightChance by the time it wants to be warm, and DesperateLightChance when freezing, so it
    /// strikes while its hands are still good. Old: nothing under 20% until critical (95.9F), by when
    /// cold hands had the drill at 13%, then the 5% floor (seed 5).
    static double LightChanceWorthIt(double coreF)
    {
        if (coreF >= SatisfiedCoreF) return MinLightChance;
        if (coreF >= WantedCoreF) return WantedLightChance + (MinLightChance - WantedLightChance) * (coreF - WantedCoreF) / (SatisfiedCoreF - WantedCoreF);
        if (coreF >= FreezingCoreF) return DesperateLightChance + (WantedLightChance - DesperateLightChance) * (coreF - FreezingCoreF) / (WantedCoreF - FreezingCoreF);
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
    static double CampFuelHours(Me me)
    {
        var mem = me.Mind.Knows.Places.GetValueOrDefault(me.Mind.Camp);
        double Stashed(Resource r) => me.AtCamp ? me.Stashed(r) : mem?.Stash?.GetValueOrDefault(r) ?? 0;
        return Woods.Append(Resource.Kindling).Sum(r => Fire.FuelHours(r, Stashed(r) + me.Has(r))) + CampFireHours(me, mem);
    }

    /// Hours of fire on the camp's pit while it burns. Standing beside it, the pit as it is now (Knowledge.Observe
    /// caught it up). Anywhere else, in sight or not, whether it burns as last seen (Knowledge.Glance keeps that
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
        : Fire.Burns(me.Mind.Knows.Places.GetValueOrDefault(me.Mind.Camp)?.Fire);

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
        foreach (var (r, kg) in me.Pack.Kg)
        {
            double keep = Items.Resources[r].Category is ResourceCategory.Food or ResourceCategory.Water ? kg
                : r == Resource.Kindling ? 1.0 : Tinders.Contains(r) ? 0.3 : 0;
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

    static int? Carcass(Me me) =>
        me.World.Tiles.GetValueOrDefault(me.Tile)?.Carcasses?.FirstOrDefault(c => !Carcasses.IsButchered(c))?.Id;

    /// Forages here if it is worth it; otherwise (if it may wander) walks to the best ground it
    /// remembers or has not tried yet, within its range of camp. Old DetermineGetResource.
    static Plan? Forage(Me me, int minutes, bool wander, ForageFocus focus = ForageFocus.General)
    {
        if (me.RoomKg < 1) return GoHome(me);
        wander &= me.Hour < HomeByHour - 1; // not off somewhere new just before turning for home
        if (ForageTarget(me, focus, wander) is not { } best) return null;
        if (best.Tile != me.Tile && Go(me, best.Tile, minutes) is { } go)
        {
            me.Commits.Add(() => me.Mind.ForageGoal = best.Tile);
            return go;
        }
        if (ForageHere(me, focus) <= 0 || Try(me, new Gather(me.Id, 0, focus), minutes) is not { } gather) return null;
        me.Commits.Add(() => me.Mind.ForageGoal = null);
        return gather;
    }

    static double ForageHere(Me me, ForageFocus focus) => Expect(me, me.Tile, focus);

    /// The best ground to forage for this, here or (if it may wander) elsewhere, and what an hour of it
    /// is worth (Value's units), the walk there counted against a stint of ForageStintHours. Every tile is
    /// judged the same way, the one underfoot included: by memory, which looking (Knowledge.Observe) has
    /// just brought up to date for every tile it stands on. Staying is a candidate, and leaving it, or
    /// the ground already being walked to, takes a better hour by MoveMarginFactor. Old game: the tile
    /// underfoot was weighed apart, and the walk by the first step's going toward each tile, so two tiles
    /// each looked better from the other and a starving NPC walked between them all day (bug, fixed).
    /// Only weighs: the ground chosen becomes ForageGoal when the walk there is the plan started (Forage).
    static (TileId Tile, double PerHour)? ForageTarget(Me me, ForageFocus focus, bool wander)
    {
        double here = Expect(me, me.Tile, focus);
        if (!wander) return here > 0 ? (me.Tile, here) : null;
        var candidates = me.Mind.Knows.Places.Where(p => p.Value.Visited && p.Key != me.Tile && InRange(me, p.Key)).Select(p => p.Key)
            .Concat(MapGen.Neighbors(me.Tile).Where(t => Passable(me, me.Tile, t) && InRange(me, t) && me.Mind.Knows.Places.GetValueOrDefault(t)?.Visited != true))
            .ToList();
        if (MapGen.Neighbors(me.Tile).All(n => !Passable(me, me.Tile, n))) return here > 0 ? (me.Tile, here) : null;
        // Nowhere it would come home from colder than it wants to be (Level's wanted warmth): it would turn
        // back on the way, and set out again once warm (old: in deep snow, all afternoon). Ground already
        // being walked to is weighed so too, at every step: the trip that was warm enough setting out can
        // be too cold to go on with (old: a starving NPC walked on to its ground until warmth was critical).
        // Setting out, it wants SetOutMarginF in hand, so that the walk turning out a little colder than
        // reckoned does not turn it round on the way.
        var body = me.Agent.Body;
        double coolingFPerHour = WalkingCoolingFPerHour(me);
        double PerHour(TileId t, double marginF)
        {
            double walkHours = WalkMinutes(me, me.Tile, t) / 60.0;
            double tripHours = walkHours + WalkMinutes(me, t, me.Mind.Camp) / 60.0;
            if (t != me.Tile && body.CoreTempF - coolingFPerHour * tripHours < WantedCoreF + marginF) return 0;
            return Expect(me, t, focus) * ForageStintHours / (ForageStintHours + walkHours);
        }
        var best = candidates.Select(t => (Tile: t, PerHour: PerHour(t, SetOutMarginF))).OrderByDescending(x => x.PerHour).ThenBy(x => x.Tile).FirstOrDefault();
        // Ground already being walked to holds unless the best beats it by the margin too.
        if (me.Mind.ForageGoal is TileId goal && goal != me.Tile && candidates.Contains(goal)
            && PerHour(goal, 0) is var kept && kept > 0 && kept * MoveMarginFactor >= best.PerHour)
            best = (goal, kept);
        if (best.PerHour > 0 && best.PerHour > here * MoveMarginFactor) return best;
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
        var sky = Weather.At(me.World, me.World.Minute);
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
        double density = me.Mind.Knows.Places.TryGetValue(t, out var p) && p.ForageDensity is double d && p.VisitedMinute is long seen
            ? Math.Min(def.Density, d + (me.World.Minute - seen) / 60.0 / Pools.ForageRespawnHours * def.Density) : def.Density;
        return Value(def, density, focus);
    }

    /// How much better an hour elsewhere must be to be worth leaving for (old: within 80%, stay).
    const double MoveMarginFactor = 1.25;
    /// Core temperature over the wanted level it reckons on coming home with, setting out to forage.
    const double SetOutMarginF = 0.3;

    /// A rough tile crossing for planning a hunt (flat ground is one minute, rough two); and how long a
    /// stint of foraging a walk is weighed against.
    const double MinutesPerTile = 2, ForageStintHours = 1;

    /// Six tiles on, toward ground it has seen least of, within radius of camp; null once there is
    /// nowhere left it has not had a look at.
    static Plan? Explore(Me me, int radius)
    {
        if (me.Hour >= HomeByHour - 2) return null;
        var visited = me.Mind.Knows.Places.Where(p => p.Value.Visited).Select(p => p.Key).ToList();
        int Seen(TileId t) => visited.Count(v => MapGen.Manhattan(v, t) <= SightTiles - 2);
        var m = Map.Of(me.World);
        if (me.Mind.Heading is TileId heading && heading != me.Tile && Seen(heading) == 0 && Go(me, heading) is { } onward) return onward;
        // The nearest ring with ground on it it has not had a look at. Old: only the ring six tiles out,
        // so once the ground around camp was seen it never looked further (seed 5: 0 trees within 15 of camp).
        for (int leg = ExploreLegTiles; leg <= radius + MapGen.Manhattan(me.Tile, me.Mind.Camp); leg += ExploreLegTiles)
        {
            int l = leg;
            var ring = Enumerable.Range(-l, 2 * l + 1)
                .SelectMany(dx => new[] { new TileId(me.Tile.X + dx, me.Tile.Y + (l - Math.Abs(dx))), new TileId(me.Tile.X + dx, me.Tile.Y - (l - Math.Abs(dx))) })
                .Distinct().Where(t => m.InBounds(t) && m.TerrainAt(t).IsPassable() && MapGen.Manhattan(t, me.Mind.Camp) <= radius && Seen(t) == 0)
                .OrderBy(t => MapGen.Manhattan(t, me.Mind.Camp)).ThenBy(t => t).ToList();
            foreach (var t in ring.Take(3))
                if (Go(me, t) is { } go)
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
        && (Tinders.Any(r => me.Has(r) >= 0.05) || me.Has(Resource.Kindling) >= 0.5);

    /// Old IsBeyondExploreLeash: bolder NPCs range further from camp, about 2-10 tiles.
    static bool InRange(Me me, TileId t) => MapGen.Manhattan(t, me.Mind.Camp) <= (int)(2 + me.Mind.Boldness * 8);

    // ---- hunting and threats ----

    /// Core temperature on reaching camp, if the open air goes on cooling it as it does now for the whole
    /// walk. The walk: the crossing toward camp from here, once per tile of distance.
    static double CoreAtHomeF(Me me)
    {
        var body = me.Agent.Body;
        if (me.AtCamp) return body.CoreTempF;
        var camp = me.Mind.Camp;
        var step = Enum.GetValues<Direction>().Select(d => Map.Step(me.Tile, d))
            .Where(t => Passable(me, me.Tile, t)).OrderBy(t => MapGen.Manhattan(t, camp)).ThenBy(t => t).Cast<TileId?>().FirstOrDefault();
        if (step is not TileId toward) return body.CoreTempF;
        var pace = Traversal.IsHazardous(me.World, me.Tile) || Traversal.IsHazardous(me.World, toward) ? Pace.Careful : Pace.Quick;
        double minutes = MapGen.Manhattan(me.Tile, camp)
                         * Traversal.CrossingMinutes(me.World, body, me.Tile, toward, pace, Weather.At(me.World, me.World.Minute), Items.EncumbrancePct(me.Agent));
        return body.CoreTempF - WalkingCoolingFPerHour(me, pace) * minutes / 60;
    }

    /// How fast the open air cools it walking, in °F an hour (0 when it warms): as it does now, less any
    /// fire's warmth in this tile's air, which it walks out of at the first step. Judged the same from
    /// camp and from out in the cold, so a trip planned by the fire is the trip it finds outside.
    static double WalkingCoolingFPerHour(Me me, Pace pace = Pace.Quick)
    {
        var walking = Sim.Surroundings(me.World, me.Agent, new Travel(me.Id, Direction.North, pace), me.Events);
        // Walking is never stationary, so no shelter cover: the fire's air share is the open-air one.
        walking = walking with { AirTempF = walking.AirTempF - Fire.WarmthFor(me.World, me.Id).AirHeatF };
        return -Math.Min(0, Body.TemperatureChangeFPerHour(me.Agent.Body, walking));
    }

    /// Whether a trip out of camp - the walk out, the work it is for, the walk home - would bring it home
    /// after it must be in (HomeMinuteOfDay): not worth setting out on unless what it is for outranks the
    /// evening. Old behavior: only the first crossing was checked, against 19:00 whatever the season, so it
    /// set off on an hour's trip at a quarter to seven, or into a winter dusk at four.
    static bool TooLateToSetOut(Me me, double score)
    {
        if (!me.AtCamp || score >= NightScore || me.Trip is not { } trip) return false;
        return Weather.MinuteOfDay(me.World.Minute) + TripMinutes(me, trip.To, trip.WorkMinutes, trip.Home) > HomeMinuteOfDay(me);
    }

    /// Out to a tile, the work there, and home, at today's going (WalkMinutes).
    static double TripMinutes(Me me, TileId to, int workMinutes, TileId home) =>
        WalkMinutes(me, me.Tile, to) + workMinutes + WalkMinutes(me, to, home);

    /// When it must be home by: the evening (HomeByHour), or sunset if that comes first.
    static int HomeMinuteOfDay(Me me) => Math.Min(HomeByHour, Weather.SunsetHour(me.World.Minute)) * 60;

    /// Where the predator it last saw close was, while that is fresh.
    static TileId? FreshThreat(Me me) =>
        me.Mind.ThreatTile is TileId t && me.World.Minute - me.Mind.ThreatMinute < ThreatMemoryMinutes ? t : null;

    /// Old GetPriorityThreat, reaching one tile further: the biggest, healthiest predator here or
    /// next door, not already in a fight.
    // Threat, FindFood's prey and AllyInFight read herds' true tiles and bodies from world.Agents, gated
    // only by a fixed radius (1 and 3 tiles, inside SightTiles, as Knowledge's sight is); use sight rays when those are ported.
    static Agent? Threat(Me me) =>
        Herds(me, 1, h => Fauna.IsPredator(h.Species) && Unengaged(me, h))
            .OrderByDescending(h => h.Members.Where(Body.IsAlive).Max(m => m.WeightKg * Body.Vitality(m))).ThenBy(h => h.Id).FirstOrDefault();

    /// A herd no fight has hold of (in the field or listed at all): one already fighting someone is
    /// neither prey to start a hunt on nor a threat to start a fight with - Combat.Create would refuse
    /// it (old: a hungry NPC beside the player's bison fight called BeginHunt on that bison and threw).
    static bool Unengaged(Me me, Agent herd) => !Sim.Held(me.World, herd.Id) && Combat.EncounterOf(me.World, herd.Id) == null;

    static IEnumerable<Agent> Herds(Me me, int withinTiles, Func<Agent, bool> kind) =>
        me.World.Agents.Values.Where(a => a.Mind is HerdMind && MapGen.Manhattan(a.Tile, me.Tile) <= withinTiles
                                          && a.Members.Any(Body.IsAlive) && kind(a));

    /// Someone else on this tile fighting predators, whom this NPC would stand by (old WouldDefend).
    static (Encounter Fight, int Team)? AllyInFight(Me me)
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
        if (AllyInFight(me) is { } ally)
        {
            Combat.Join(me.World, ally.Fight, new Fighter(me.Id) { Weapon = Weapon(me) }, ally.Team);
            return (new Wait(me.Id), 1); // the fight's rounds move it from here
        }
        var threat = Threat(me);
        if (threat != null && threat.Tile == me.Tile && FightOrFlight(me, threat))
        {
            var lead = threat.Members.FindIndex(Body.IsAlive);
            var enc = Combat.Create(me.World, [new Fighter(me.Id) { Weapon = Weapon(me) }], Combat.SendMembers(me.World, threat.Id, lead),
                me.Tile, 8, Awareness.Engaged, Awareness.Engaged);
            return Fought(me, enc);
        }
        return Flee(me, threat?.Tile ?? FreshThreat(me)!.Value);
    }

    /// Old DecideFlightOrFight: boldness, armed and whole, and the size of the thing, as odds.
    static bool FightOrFlight(Me me, Agent threat)
    {
        var body = me.Agent.Body;
        bool armed = Weapon(me) != null, hurt = Body.Vitality(body) < 0.7;
        double fight = me.Mind.Boldness + (armed && !hurt ? 0.2 : 0) - (hurt ? 0.2 : 0);
        double mine = Body.Vitality(body) * (armed ? 2.0 : 1.0);
        var biggest = threat.Members.Where(Body.IsAlive).MaxBy(m => m.WeightKg * Body.Vitality(m))!;
        double theirs = Body.Vitality(biggest) * biggest.WeightKg / 30.0;
        if (theirs > mine * 1.5) fight -= 0.3;
        if (theirs < mine * 0.5) fight += 0.3;
        return me.World.Rng("npc", me.Agent.Id).NextDouble() < Math.Clamp(fight, 0.1, 0.9);
    }

    /// A fight this NPC started, resolved headless (as herds fight), then a minute to catch its breath.
    // Combat.Refusal lets only the player fight round by round, so an NPC's own fight resolves at once, its
    // rounds lived by its wounds alone (no fighting metabolism, no clock); step it by Fight when combat allows other agents.
    static Plan Fought(Me me, Encounter enc)
    {
        Combat.ResolveHeadless(me.World, enc, me.Events);
        return (new Wait(me.Id), 1);
    }

    /// The hardest-hitting weapon in the pack.
    static Gear? Weapon(Me me) =>
        me.Pack.Gear.Where(g => Items.Def(g).Weapon != null && Items.Works(g)).OrderByDescending(g => Items.Def(g).Weapon!.Damage).FirstOrDefault();

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

    /// Home, giving up any ground it was walking to forage (ForageGoal): it chooses again from there.
    static Plan? GoHome(Me me)
    {
        if (me.AtCamp || Go(me, me.Mind.Camp) is not { } go) return null;
        me.Commits.Add(() => me.Mind.ForageGoal = null);
        return go;
    }

    /// The first step of the shortest walk to target (breadth-first over passable edges, in a box
    /// around both ends), careful onto hazardous ground. Null when there is no way. The walk is the
    /// plan's Trip: workMinutes there, then home (camp, unless the plan moves it).
    static Plan? Go(Me me, TileId target, int workMinutes = 0, TileId? home = null)
    {
        var step = Step(me, target);
        if (step != null) me.Trip = (target, workMinutes, home ?? me.Mind.Camp);
        return step;
    }

    static Plan? Step(Me me, TileId target)
    {
        if (target == me.Tile) return null;
        if (Body.GetCapacities(me.Agent.Body).Moving <= 0.1) return null; // Traversal.Refusal: you can barely move
        int margin = 8;
        int minX = Math.Min(me.Tile.X, target.X) - margin, maxX = Math.Max(me.Tile.X, target.X) + margin;
        int minY = Math.Min(me.Tile.Y, target.Y) - margin, maxY = Math.Max(me.Tile.Y, target.Y) + margin;
        // Not past a predator it has just seen: no tile next to it, unless that is where it is going.
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
                if (threat is TileId t && next != me.Tile && MapGen.Manhattan(next, t) <= 1) continue;
                cameFrom[next] = at;
                if (next == me.Tile)
                {
                    var to = at;
                    var dir = Enum.GetValues<Direction>().First(d => Map.Step(me.Tile, d) == to);
                    // Careful wherever a quick step could hurt: on the way off this tile or onto the next.
                    var pace = Traversal.IsHazardous(me.World, me.Tile) || Traversal.IsHazardous(me.World, to) ? Pace.Careful : Pace.Quick;
                    return Try(me, new Travel(me.Id, dir, pace), 180);
                }
                queue.Enqueue(next);
            }
        }
        return null;
    }

    static Resource? First(IEnumerable<Resource> rs, Func<Resource, bool> p) => rs.Where(p).Cast<Resource?>().FirstOrDefault();

    static bool Passable(Me me, TileId from, TileId to) => Map.Blocked(Map.Of(me.World), from, to) == null;

    /// The activity, if the rules would let it start: an NPC never picks what the player would be refused.
    static Plan? Try(Me me, Activity a, int minutes)
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
        return refused == null ? (a, minutes) : null;
    }
}
