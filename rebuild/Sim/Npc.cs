using Plan = (Survival.Activity Activity, int Minutes);

namespace Survival;

/// The need an NPC is working on (old NeedType). Ordered as the old hierarchy: Warmth first.
public enum Need { Warmth, Water, Rest, Food }

/// What one tile offered when this NPC last saw it (old ResourceMemory, per tile). Standing on it
/// tells how picked-over its ground is; from a distance only whether trees stand there or water
/// runs open. A belief, not the truth: it goes stale while the NPC is elsewhere (plan decision 7).
public sealed record Place(long SeenMinute, bool Trees, double? ForageDensity = null, bool Water = false)
{
    [System.Text.Json.Serialization.JsonIgnore] public bool Visited => ForageDensity != null;
}

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
    /// Old ResourceMemory: every tile it has stood on. Depleted tiles stay, as "nothing here", so an
    /// exploring NPC does not go back to them.
    public SortedDictionary<TileId, Place> Places { get; init; } = [];
    /// Old RelationshipMemory reduced to an opinion per agent, -1..1; strangers are 0.
    // ponytail: nothing writes this yet; the WorldEvent stream will feed it (plan decision 4) when consumers subscribe.
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
    // ponytail: the axe and knife stand in for the old NPC's crafting (it made them in its first hours);
    // the kit shrinks back to the player's when crafting is ported (phase 4).
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
            Boldness = boldness ?? 0.3 + world.Rng("npc").NextDouble() * 0.4,
        };
        world.Agents[id] = new Agent { Id = id, Tile = tile, Mind = mind };
        mind.TargetFatMinute = world.Minute;
        mind.TargetFatKg = Math.Clamp(world.Agents[id].Body.FatKg, FatFloorKg(world.Agents[id].Body), Body.BaselineFatPct * world.Agents[id].Body.WeightKg);
        Remember(world, world.Agents[id], mind);
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
        Remember(world, agent, mind);
        AdaptTargetFat(mind, agent.Body, world.Minute);

        // In a fight someone else is running (the player's): its rounds move this NPC, not its own steps.
        if (Combat.EncounterOf(world, agent.Id) is Encounter fight && fight.Units.Any(u => u.Agent == agent.Id && u.InField))
            return (new Wait(agent.Id), 1);

        var b = agent.Body;
        // Old thresholds. WarmPct was core temperature over 95-98.6F: .25 critical, .5 wanted, .7 satisfied.
        // Away from the fire it judges the core it will have when it gets home: the walk back is cold too
        // (old: a fixed 97.5F away, so a two-tile trip through deep snow at -15C came home hypothermic - bug, fixed).
        double warmth = Level(me, Need.Warmth, CoreAtHomeF(me), FreezingCoreF, 96.8, 97.8);
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
            && !(Fire.PitAt(world, mind.Camp) is FireId pit && Fire.IsActive(world.Fires[pit])))
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
            (me.Hour >= HomeByHour || me.Hour < 5 ? 5 + 1.75 : 0, null, Night),
            // Tonight's wood outranks wanted food: a night without it kills (the old NPC stockpiled fuel only
            // as optional work, after food). A day's spare is worth less than wanted food.
            (!me.Night && fuelHours < neededHours ? 5 + 1.5 : 0, null, Wood),
            // Critical food beats everything but critical warmth, water and rest. The design said all but warmth and
            // water; ranked over rest, a starving NPC foraged on at zero energy and died of exhaustion (seed 1, day 28).
            (food >= 1 ? 11.5 : food * 10 + 1, Need.Food, Food),
            (!me.Night && fuelHours < neededHours + SpareFireHours ? 5 + 0.5 : 0, null, Wood),
            (1, null, Errands),
        };
        // OrderByDescending is stable: equal scores keep the list's order.
        foreach (var o in options.Where(o => o.Score > 0).OrderByDescending(o => o.Score))
        {
            if (o.Act(me) is not { } plan) continue;
            // Going out, the fire is banked first so it is still in when the walk home is cold (judgment
            // call: food trips range further than wood trips, and a cold NPC with a hand drill cannot relight).
            if (plan.Activity is Travel && me.AtCamp && me.Burning && o.Score < 100 && KeepFire(me, 3) is { } bank) plan = bank;
            // Holding a fire with a predator next door: nothing that walks away from it.
            if (plan.Activity is Travel && pinned && o.Score < 100) plan = KeepFire(me, 4) ?? (new Rest(me.Id), 30);
            if (o.Need != null) mind.Need = o.Need;
            return plan;
        }
        return me.AtCamp ? (new Rest(agent.Id), 30) : GoHome(me) ?? (new Rest(agent.Id), 30);
    }

    /// 0 when fine, 0.5 when wanted (or still being seen to), 1 when critical. Low values are bad.
    static double Level(Me me, Need need, double value, double critical, double want, double satisfied)
    {
        if (me.Mind.Need == need && value >= satisfied) me.Mind.Need = null;
        if (value < critical) return 1;
        return value < want || me.Mind.Need == need ? 0.5 : 0;
    }

    const int HomeByHour = 19;
    /// How far it runs from a predator, and how long it keeps one in mind.
    const int FleeTiles = 3;
    public const int ThreatMemoryMinutes = 120;
    const int SightTiles = 5;
    const double MinLightChance = 0.2;
    /// Critical cold (old WarmPct 0.25): any chance of a fire beats none.
    const double FreezingCoreF = 95.9, DesperateLightChance = 0.02;
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
    /// A banked fire's burn rate over its fuel's table rate (Fire.BurnSizeFactor runs 0.9-1.2).
    const double BankedBurnFactor = 1.1;
    /// Fuel at camp below this, a cold NPC by its fire goes for wood rather than wait on the fire.
    const double LowFuelHours = 4;
    /// On the fire before sleeping: near a full open pit, a night of birch.
    const double BankKg = 9;
    /// Kept back from the fire so there is always something to light the next one with.
    const double KindlingReserveKg = 0.6;
    /// Farther than this, timber is not worth carrying home.
    const int MaxWoodTiles = 15;

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
        public bool Night => Hour < 5 || Hour >= 21;
        /// Work.TooDarkToWork without the flame: too dark to fell or butcher unless a fire is lit here.
        public bool Dark => Night || Map.Of(World).CaveAt(Tile) == Cave.Floor;
        /// The pit here, as last caught up. Decide has no event stream to catch it up into, so it
        /// can be a little behind; anything it gets wrong is refused at no cost and re-decided.
        public FireState? Pit => Fire.PitAt(World, Tile) is FireId f ? World.Fires[f] : null;
        public bool Burning => Pit is { } p && Fire.IsActive(p);
        public double RoomKg => Items.CapacityKg(Pack) - Items.CarriedKg(Agent);
        public Inventory? Stash => World.Tiles.GetValueOrDefault(Tile)?.Stash;
        public double Has(Resource r) => Items.Have(Pack, r);
        public double Stashed(Resource r) => Stash is { } s ? Items.Have(s, r) : 0;
        public double ShortfallKg => Mind.TargetFatKg - Agent.Body.FatKg;
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
                ?? (me.AtCamp && fetching ? Wood(me) : null)
                ?? (Fire.TotalKg(me.Pit!) < 1.5 ? Forage(me, minutes: 15, wander: false, ForageFocus.Fuel) : null) // feed it or lose it
                ?? AtFire(me) ?? (new Rest(me.Id), 20);
        if (KeepFire(me, 3) is { } fire) return fire;
        // Nothing to light it with: by day go and get some (working warms you), by night grope for it here.
        return (me.Night ? null : Wood(me)) ?? Forage(me, minutes: 15, wander: false, ForageFocus.Fuel);
    }

    static Plan? Water(Me me)
    {
        if (me.Has(Resource.Water) >= 0.1 || Work.HasOpenWater(me.World, me.Tile)) return Try(me, new Drink(me.Id), Work.DrinkMinutes);
        if (me.Stashed(Resource.Water) > 0) return Try(me, new Unstash(me.Id, Resource.Water, 1), 1);
        if (me.Burning) return Try(me, new MeltSnow(me.Id), Work.MeltSnowMinutes);
        if (me.AtCamp && KeepFire(me, 3) is { } fire) return fire;
        // Old DetermineGetResource(Water): the nearest open water it knows of.
        var river = me.Mind.Places.Where(p => p.Value.Water && MapGen.Manhattan(p.Key, me.Tile) <= MaxWoodTiles).Select(p => p.Key)
            .OrderBy(t => MapGen.Manhattan(me.Tile, t)).ThenBy(t => t).Cast<TileId?>().FirstOrDefault();
        return (river is TileId r ? Go(me, r) : null) ?? GoHome(me);
    }

    static Plan? Tired(Me me) => me.AtCamp ? Bed(me)
        : GoHome(me) ?? (me.Agent.Body.EnergyPct < 0.1 ? (new Sleep(me.Id), 60) : null); // too spent to walk home: sleep where it stands

    static Plan? Night(Me me)
    {
        if (!me.AtCamp) return GoHome(me);
        // Old DetermineIdle: at night, sleep unless well rested.
        if (me.Agent.Body.EnergyPct < 0.8) return Bed(me);
        return KeepFire(me, 4) ?? AtFire(me) ?? (new Rest(me.Id), 30);
    }

    /// The fire banked first (old CanSleep wanted two hours of it), then sleep in stretches, waking to feed it.
    static Plan? Bed(Me me) => KeepFire(me, BankKg)
        // Logs that will not go on a fire this cool yet: sit up with it until it catches, unless too spent to.
        ?? (me.Burning && Fire.TotalKg(me.Pit!) < BankKg - 0.5 && me.Agent.Body.EnergyPct > 0.15
            && Woods.Any(r => me.Has(r) + me.Stashed(r) > 0) ? (new Rest(me.Id), 15) : (Plan?)null)
        ?? (new Sleep(me.Id), 120);

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
        TileId? Nearest(TileId from) => me.Mind.Places.Where(p => p.Value.Trees && MapGen.Manhattan(p.Key, from) <= MaxWoodTiles).Select(p => p.Key)
            .OrderBy(t => MapGen.Manhattan(from, t)).ThenBy(t => t).Cast<TileId?>().FirstOrDefault();
        // A camp with no timber near it is not worth hauling to: home moves to the trees, and what was
        // stashed at the old one stays there (old design: depletion forces camp relocation).
        if (axe && Nearest(me.Mind.Camp) is TileId stand && MapGen.Manhattan(stand, me.Mind.Camp) > 2)
            me.Mind.Camp = stand;
        if (axe && Nearest(me.Tile) is TileId t && Go(me, t) is { } go) return go;
        // No timber known: look for some, then fallen wood.
        return (axe ? Explore(me, MaxWoodTiles) : null) ?? Forage(me, minutes: 30, wander: true, ForageFocus.Fuel);
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
        // A pack full of the logs a food search turns up: they go down here, not home (old: it walked back
        // to camp to unload every hour or two, and spent the day carrying wood instead of looking for food).
        if (me.RoomKg < 1) return DropLogs(me) ?? GoHome(me);
        return FindFood(me);
    }

    /// Logs off the pack onto this tile's stash, heaviest load first; never onto a woodpile, which Wood
    /// would only pick up again (that is wood on its way home, and goes home).
    static Plan? DropLogs(Me me) =>
        Woods.Any(r => me.Stashed(r) > 0) ? null
        : First(Woods.OrderByDescending(me.Has), r => me.Has(r) > 0) is Resource log ? Try(me, new Stash(me.Id, log, me.Has(log)), 1) : null;

    /// Where the next meal is likeliest to come from, by expected kilocalories an hour, best first.
    static Plan? FindFood(Me me)
    {
        var options = new List<(double KcalPerHour, Func<Plan?> Act)>();
        bool wander = me.Hour < HomeByHour - 1;
        if (ForageTarget(me, ForageFocus.Food, wander) is { } ground)
            options.Add((ground.PerHour * 1000, () => Forage(me, minutes: 45, wander, ForageFocus.Food)));
        if (Weapon(me) != null && Body.Vitality(me.Agent.Body) >= 0.7)
            foreach (var prey in Herds(me, 3, h => Fauna.BehaviorOf(h.Species) == HerdBehavior.Prey).OrderBy(h => h.Id))
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
        const double KillPct = 0.4, StalkHours = 1, ButcherHours = 2, MeatPct = 0.4;
        var stats = SpeciesTable.Of(prey.Species);
        double meatKg = Math.Min(prey.Members.First(Body.IsAlive).WeightKg * MeatPct, me.RoomKg);
        double hours = StalkHours + ButcherHours + MapGen.Manhattan(me.Tile, prey.Tile) * MinutesPerTile / 60.0;
        double urgency = Math.Clamp(me.ShortfallKg / CriticalShortfallKg, 0, 1);
        double danger = Math.Clamp(stats.Threat / 2, 0, 1); // caribou 0.15, bison 0.9, mammoth 1
        return KillPct * meatKg * Items.Resources[Resource.RawMeat].KcalPerKg / hours * (1 - danger * (1 - urgency));
    }

    static Plan? Hunt(Me me, Agent prey)
    {
        if (prey.Tile != me.Tile) return Go(me, prey.Tile);
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
    /// log will catch, and never the reserve.
    static Plan? Stoke(Me me, FireState pit, double kg, double reserve = KindlingReserveKg)
    {
        foreach (var r in Woods)
        {
            if (!Fire.CanAddFuel(pit, r)) continue;
            if (me.Has(r) > 0) return Try(me, new AddFuel(me.Id, r, Math.Min(kg, me.Has(r))), 1);
            if (me.Stashed(r) > 0 && me.RoomKg > 0.5) return Try(me, new Unstash(me.Id, r, Math.Min(kg, me.Stashed(r))), 1);
        }
        if (!Fire.CanAddFuel(pit, Resource.Kindling)) return null;
        double spare = me.Has(Resource.Kindling) + me.Stashed(Resource.Kindling) - reserve;
        if (spare <= 0.1) return null;
        if (me.Has(Resource.Kindling) - reserve > 0.1) return Try(me, new AddFuel(me.Id, Resource.Kindling, Math.Min(kg, me.Has(Resource.Kindling) - reserve)), 1);
        return me.Stashed(Resource.Kindling) > 0 ? Try(me, new Unstash(me.Id, Resource.Kindling, Math.Min(kg + reserve, me.Stashed(Resource.Kindling))), 1) : null;
    }

    /// Old FireHandler: the best working fire starter, the best tinder, a handful of kindling.
    static Plan? Light(Me me)
    {
        var tool = Starters.Cast<ToolType?>().FirstOrDefault(t => Items.Tool(me.Pack, t!.Value) != null);
        if (tool == null) return null; // ponytail: the old NPC crafted a fire starter here; crafting is phase 4.
        var tinder = First(Tinders, r => me.Has(r) >= 0.05);
        if (tinder == null && First(Tinders, r => me.Stashed(r) > 0) is Resource stashed)
            return Try(me, new Unstash(me.Id, stashed, 0.3), 1);
        if (me.Has(Resource.Kindling) < 0.35 && me.Stashed(Resource.Kindling) > 0)
            return Try(me, new Unstash(me.Id, Resource.Kindling, 1.0), 1);
        tinder ??= me.Has(Resource.Kindling) >= 0.5 ? Resource.Kindling : null; // sticks shaved for tinder, while there are sticks to spare
        if (tinder == null) return null;
        // Not worth the tinder in the dark with a poor drill: wait for light, or for better tinder - unless
        // freezing, when waiting is what kills (old behavior: it hauled wood to a fire it would not try to light).
        var hands = new AbilityContext(Items.EncumbrancePct(me.Agent), Weather.DarknessLevel(me.World, me.Tile));
        double minChance = me.Agent.Body.CoreTempF < FreezingCoreF ? DesperateLightChance : MinLightChance;
        if (Fire.StartChancePct(tool.Value, tinder.Value, dexterityPct: Body.Dexterity(me.Agent.Body, hands)) < minChance) return null;
        double kindling = Math.Min(0.3, me.Has(Resource.Kindling) - (tinder == Resource.Kindling ? 0.05 : 0));
        if (kindling < 0) return null;
        return Try(me, new StartFire(me.Id, tool.Value, tinder.Value, 0.05, kindling), 1);
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

    /// Hours of fire in the camp's wood, the pit's and the pack's: each fuel's kilograms over its burn
    /// rate (Fire.Props) at a banked fire's size.
    // ponytail: ignores a stone pit's efficiency and that a small fire burns slower; both under 15%.
    static double CampFuelHours(Me me)
    {
        var stash = me.World.Tiles.GetValueOrDefault(me.Mind.Camp)?.Stash;
        var pit = Fire.PitAt(me.World, me.Mind.Camp) is FireId f ? me.World.Fires[f] : null;
        double Hours(Resource r, double kg) => kg / (Fire.Props[r].BurnRateKgPerHour * BankedBurnFactor);
        return Woods.Append(Resource.Kindling).Sum(r => Hours(r, (stash != null ? Items.Have(stash, r) : 0) + me.Has(r)))
            + (pit == null ? 0 : pit.UnburnedKg.Concat(pit.BurningKg).Sum(x => Hours(x.Key, x.Value)));
    }

    /// Until the sun is next up (Weather.SunriseHour).
    static double HoursToDawn(Me me)
    {
        int now = Weather.MinuteOfDay(me.World.Minute), dawn = Weather.SunriseHour(me.World.Minute) * 60;
        return ((dawn - now + 1440) % 1440) / 60.0;
    }

    /// At camp, everything goes on the stash but food, water and what lights a fire.
    static Plan? Unload(Me me)
    {
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
        if (best.Tile != me.Tile && Go(me, best.Tile) is { } go) return go;
        return ForageHere(me, focus) > 0 ? Try(me, new Gather(me.Id, 0, focus), minutes) : null;
    }

    static double ForageHere(Me me, ForageFocus focus) =>
        ForagePoolAt(me, me.Tile) is { } def ? Value(def, Pools.ForageDensity(me.World, new PoolId(me.Tile, 0)), focus) : 0;

    /// The best ground to forage for this, here or (if it may wander) elsewhere, and what an hour of it
    /// is worth (Value's units), the walk there counted against a stint of ForageStintHours.
    static (TileId Tile, double PerHour)? ForageTarget(Me me, ForageFocus focus, bool wander)
    {
        double here = ForageHere(me, focus);
        if (!wander) return here > 0 ? (me.Tile, here) : null;
        // Ground it has worked regrows while it is away (Pools.ForageRespawnHours); ground it has not is
        // judged by the look of it (the kind of country, and how thick it grows).
        double Expect(TileId t)
        {
            if (ForagePoolAt(me, t) is not { } def) return 0;
            double density = me.Mind.Places.TryGetValue(t, out var p) && p.ForageDensity is double d
                ? Math.Min(def.Density, d + (me.World.Minute - p.SeenMinute) / 60.0 / Pools.ForageRespawnHours * def.Density) : def.Density;
            return Value(def, density, focus);
        }
        // Ground it has worked, and ground it has only seen (trees or water in sight), within its range.
        var candidates = me.Mind.Places.Where(p => p.Value.Visited && p.Key != me.Tile && InRange(me, p.Key)).Select(p => p.Key)
            .Concat(MapGen.Neighbors(me.Tile).Where(t => Passable(me, me.Tile, t) && InRange(me, t) && me.Mind.Places.GetValueOrDefault(t)?.Visited != true));
        var best = candidates.Select(t => (Tile: t, PerHour: Expect(t) * ForageStintHours / (ForageStintHours + MapGen.Manhattan(me.Tile, t) * MinutesPerTile / 60.0)))
            .OrderByDescending(x => x.PerHour).ThenBy(x => x.Tile).FirstOrDefault();
        // Somewhere new has to be clearly better to be worth the walk.
        if (here > 0 && here >= best.PerHour * 0.8) return (me.Tile, here);
        if (best.PerHour > 0) return best;
        return here > 0 ? (me.Tile, here) : null;
    }

    /// A rough tile crossing for planning (flat ground is one minute, rough two); and how long a stint
    /// of foraging the walk is weighed against.
    const double MinutesPerTile = 2, ForageStintHours = 1;

    /// Six tiles on, toward ground it has seen least of, within radius of camp; null once there is
    /// nowhere left it has not had a look at.
    static Plan? Explore(Me me, int radius)
    {
        if (me.Hour >= HomeByHour - 2) return null;
        var visited = me.Mind.Places.Where(p => p.Value.Visited).Select(p => p.Key).ToList();
        int Seen(TileId t) => visited.Count(v => MapGen.Manhattan(v, t) <= SightTiles - 2);
        var m = Map.Of(me.World);
        const int Leg = 6;
        if (me.Mind.Heading is TileId heading && heading != me.Tile && Seen(heading) == 0 && Go(me, heading) is { } onward) return onward;
        me.Mind.Heading = null;
        var ring = Enumerable.Range(-Leg, 2 * Leg + 1)
            .SelectMany(dx => new[] { new TileId(me.Tile.X + dx, me.Tile.Y + (Leg - Math.Abs(dx))), new TileId(me.Tile.X + dx, me.Tile.Y - (Leg - Math.Abs(dx))) })
            .Distinct().Where(t => m.InBounds(t) && m.TerrainAt(t).IsPassable() && MapGen.Manhattan(t, me.Mind.Camp) <= radius && Seen(t) == 0)
            .OrderBy(t => MapGen.Manhattan(t, me.Mind.Camp)).ThenBy(t => t);
        foreach (var t in ring.Take(3))
            if (Go(me, t) is { } go)
            {
                me.Mind.Heading = t;
                return go;
            }
        return null;
    }

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

    /// Old ResourceMemory.RememberLocation: refreshed, not appended, on every visit. A stand of trees
    /// is plain to see from a distance, so tiles in sight are remembered for their trees too.
    // ponytail: sight is a fixed SightTiles; it should be the player's sight rays (terrain, weather) once those are ported.
    static void Remember(World world, Agent agent, NpcMind mind)
    {
        bool Trees(TileId t)
        {
            var pools = Pools.At(world, t);
            return Enumerable.Range(0, pools.Count).Any(i => pools[i] is TreePool && Pools.HasTrees(world, new PoolId(t, i)));
        }
        var here = Pools.At(world, agent.Tile);
        double forage = here.Count > 0 && here[0] is ForagePool ? Pools.ForageDensity(world, new PoolId(agent.Tile, 0)) : 0;
        mind.Places[agent.Tile] = new Place(world.Minute, Trees(agent.Tile), forage, Work.HasOpenWater(world, agent.Tile));
        var m = Map.Of(world);
        for (int dx = -SightTiles; dx <= SightTiles; dx++)
            for (int dy = Math.Abs(dx) - SightTiles; dy <= SightTiles - Math.Abs(dx); dy++)
            {
                var t = new TileId(agent.Tile.X + dx, agent.Tile.Y + dy);
                if (t == agent.Tile || !m.InBounds(t)) continue;
                bool trees = Trees(t), water = m.TerrainAt(t).IsPassable() && Work.HasOpenWater(world, t);
                var seen = mind.Places.GetValueOrDefault(t);
                if (seen == null ? trees || water : seen.Trees != trees || seen.Water != water)
                    mind.Places[t] = (seen ?? new Place(world.Minute, false)) with { SeenMinute = world.Minute, Trees = trees, Water = water };
            }
    }

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
        var walking = Sim.Surroundings(me.World, me.Agent, new Travel(me.Id, Direction.North, pace));
        return body.CoreTempF + Math.Min(0, Body.TemperatureChangeFPerHour(body, walking)) * minutes / 60;
    }

    /// Where the predator it last saw close was, while that is fresh.
    static TileId? FreshThreat(Me me) =>
        me.Mind.ThreatTile is TileId t && me.World.Minute - me.Mind.ThreatMinute < ThreatMemoryMinutes ? t : null;

    /// Old GetPriorityThreat, reaching one tile further: the biggest, healthiest predator here or
    /// next door, not already in a fight.
    // ponytail: Threat, FindFood's prey and AllyInFight read herds' true tiles and bodies from world.Agents, gated
    // only by a fixed radius (1 and 3 tiles, inside SightTiles, as Remember is); use sight rays when those are ported.
    static Agent? Threat(Me me) =>
        Herds(me, 1, h => Fauna.IsPredator(h.Species) && Combat.EncounterOf(me.World, h.Id) == null)
            .OrderByDescending(h => h.Members.Where(Body.IsAlive).Max(m => m.WeightKg * Body.Vitality(m))).ThenBy(h => h.Id).FirstOrDefault();

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
        return me.World.Rng("npc").NextDouble() < Math.Clamp(fight, 0.1, 0.9);
    }

    /// A fight this NPC started, resolved headless (as herds fight), then a minute to catch its breath.
    // ponytail: Combat.Refusal lets only the player fight round by round, so an NPC's own fight resolves at once, its
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

    static Plan? GoHome(Me me) => me.AtCamp ? null : Go(me, me.Mind.Camp);

    /// The first step of the shortest walk to target (breadth-first over passable edges, in a box
    /// around both ends), careful onto hazardous ground. Null when there is no way.
    static Plan? Go(Me me, TileId target)
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
