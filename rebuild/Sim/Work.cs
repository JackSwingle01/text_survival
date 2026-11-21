namespace Survival;

/// Old ForageFocus: a focused search keeps what it looks for and drops about half of the rest.
public enum ForageFocus { General, Fuel, Food, Medicine, Materials }

/// One step of work in progress: what Begin worked out, and what the minutes have added up to.
/// The player's lives for one Sim.Step; an NPC's is saved with what it is doing (Agent.Doing).
public sealed class WorkSession
{
    public required Activity Activity { get; init; }
    public int Minutes { get; set; }
    public Inventory Got { get; init; } = new();
    /// What did not fit in the pack and went on the tile's stash.
    public Inventory Dropped { get; init; } = new();
    public PoolId? Pool { get; init; }
    public double TargetMinutes { get; init; }
    public double DoneKg { get; set; }
}

/// Work activities, shared by the player and NPCs: gathering from pools, butchering, eating,
/// drinking, cooking, the stash. Port of the old WorkStrategies, ConsumptionHandler and CookingHandler.
public static class Work
{
    public const double MaxDrinkLiters = 1.0;
    public const int EatMinutes = 10, DrinkMinutes = 2, MeltSnowMinutes = 10, CookMeatMinutesPerKg = 15, CookFishMinutesPerKg = 12;
    public const double MeltSnowLiters = 1.0;
    /// One use of the axe per hour of chopping (old: one per session, and its sessions ran 30-120 minutes).
    public const double ChopMinutesPerAxeUse = 60;

    public static bool Handles(Activity a) => a is Gather or Butcher or Eat or Drink or Cook or MeltSnow or Stash or Unstash or Wear;

    // ---- light and ability ----

    public const string TooDark = "too dark to work";

    /// Old WorkRunner.CheckDarknessBlocking: night (old TimeOfDay.Night) or a cave, with no flame or torch.
    public static bool TooDarkToWork(World world, Agent agent)
    {
        int hour = Weather.MinuteOfDay(world.Minute) / 60;
        bool dark = Map.Of(world).CaveAt(agent.Tile) == Cave.Floor || hour < 5 || hour >= 21;
        return dark && !Fire.HasLight(world, agent.Id);
    }

    /// Work that needs to see: everything but foraging (old: foraging by feel was allowed in the dark).
    static bool NeedsLight(World world, Agent agent, Activity activity) => activity switch
    {
        Butcher => true,
        Gather g => Pools.Def(world, new PoolId(agent.Tile, g.Pool)) is not ForagePool,
        _ => false,
    };

    /// Old GetWorkImpairments: bad legs +20%, bad lungs +15%, bad hands +25% time; a frozen carcass
    /// +50%. Read every minute, not fixed for the session as the old game did, so how the work is
    /// split into steps cannot change what it gets done.
    static double ImpairmentFactor(World world, Agent agent, Activity activity)
    {
        var body = agent.Body;
        return activity switch
        {
            Gather g => Pools.Def(world, new PoolId(agent.Tile, g.Pool)) switch
            {
                ForagePool => ImpairmentFactor(body, moving: true, breathing: true),
                TreePool => ImpairmentFactor(body, moving: true, manipulation: true),
                StockPool { Kind: StockKind.Salvage } => ImpairmentFactor(body, moving: true),
                _ => 1.0,
            },
            // CutMinute catches the carcass up first: frozen is the air it felt last.
            Butcher b => ImpairmentFactor(body, manipulation: true) * (Carcasses.IsFrozen(world, Carcasses.Find(world, agent.Tile, b.Carcass)!) ? 1.5 : 1.0),
            _ => 1.0,
        };
    }

    static double ImpairmentFactor(BodyState body, bool moving = false, bool breathing = false, bool manipulation = false)
    {
        var c = Body.GetCapacities(body);
        double f = 1.0;
        if (moving && c.Moving < 0.5) f *= 1.20;
        if (breathing && c.Breathing < 0.75) f *= 1.15;
        if (manipulation && c.Manipulation < 0.5) f *= 1.25;
        return f;
    }

    // ---- refusal: before any time passes ----

    /// Why this work cannot start, or null. Fires around the agent must be caught up.
    public static string? Refusal(World world, Activity activity)
    {
        var agent = world.Agent(activity.Who);
        var pack = Items.PackOf(agent);
        if (pack == null) return "carries nothing to work with";
        switch (activity)
        {
            case Gather g:
            {
                var pools = Pools.At(world, agent.Tile);
                if (g.Pool < 0 || g.Pool >= pools.Count) return "nothing like that here";
                var id = new PoolId(agent.Tile, g.Pool);
                if (!Pools.Knows(world, agent.Id, id)) return "nothing like that here"; // a hidden pool names nothing yet
                var def = pools[g.Pool];
                if (NeedsLight(world, agent, activity) && TooDarkToWork(world, agent)) return TooDark;
                return def switch
                {
                    ForagePool => null,
                    TreePool when !Pools.HasTrees(world, id) => "all the trees here have been felled",
                    TreePool => Items.Tool(pack, ToolType.Axe) == null ? "you need an axe to fell trees" : null,
                    StockPool s when !Pools.HasStock(world, id) => $"the {s.Name} is picked clean",
                    // old game: the player's harvest never checked the tool a harvestable required; only NPCs did (bug, fixed).
                    StockPool { RequiresTool: ToolType t } when Items.Tool(pack, t) == null => $"you need a {t}",
                    _ => null,
                };
            }
            case Butcher b:
            {
                var c = Carcasses.Find(world, agent.Tile, b.Carcass);
                if (c == null) return "no such carcass here";
                if (Carcasses.IsButchered(c)) return "there's nothing left to butcher";
                if (b.Mode is ButcherMode asked && c.Mode is ButcherMode started && asked != started)
                    return $"this carcass is being butchered {started}, not {asked}";
                if (Carcasses.AllCut(c) && Items.CarriedKg(agent) >= Items.CapacityKg(pack)) return PackFull;
                return NeedsLight(world, agent, activity) && TooDarkToWork(world, agent) ? TooDark : null;
            }
            case Eat e:
                if (e.Kg <= 0) throw new ArgumentOutOfRangeException(nameof(activity), e.Kg, "Eat a positive amount");
                if (Items.Resources[e.Food].Category != ResourceCategory.Food) return $"{e.Food} is not food";
                return Items.Have(pack, e.Food) > 0 ? null : $"no {e.Food} to eat";
            case Drink d:
                if (d.Liters <= 0) throw new ArgumentOutOfRangeException(nameof(activity), d.Liters, "Drink a positive amount");
                if (Items.Have(pack, Resource.Water) <= 0 && !HasOpenWater(world, agent.Tile)) return "no water here";
                return (Body.MaxHydrationMl - agent.Body.HydrationMl) / 1000 < 0.01 ? "not thirsty" : null;
            case Cook c:
                if (c.Kg <= 0) throw new ArgumentOutOfRangeException(nameof(activity), c.Kg, "Cook a positive amount");
                if (Cooked(c.Raw) == null) return $"{c.Raw} is not something to cook";
                if (Items.Have(pack, c.Raw) <= 0) return $"no {c.Raw} to cook";
                return HasCookingFire(world, agent.Tile) ? null : "no fire to cook on";
            case MeltSnow:
                return HasCookingFire(world, agent.Tile) ? null : "no fire to melt snow on";
            case Stash s:
                if (s.Kg <= 0) throw new ArgumentOutOfRangeException(nameof(activity), s.Kg, "Stash a positive amount");
                return Items.Have(pack, s.Resource) > 0 ? null : $"no {s.Resource} to stash";
            case Unstash u:
                if (u.Kg <= 0) throw new ArgumentOutOfRangeException(nameof(activity), u.Kg, "Take a positive amount");
                var stash = world.Tiles.GetValueOrDefault(agent.Tile)?.Stash;
                if (stash == null || Items.Have(stash, u.Resource) <= 0) return $"no {u.Resource} stashed here";
                return Items.CarriedKg(agent) >= Items.CapacityKg(pack) ? "your pack is full" : null;
            case Wear w:
                var gear = pack.Gear.FirstOrDefault(x => x.Kind == w.Gear);
                if (gear == null) return $"no {w.Gear} in your pack";
                return Items.Def(gear).Slot == null ? $"{Items.Def(gear).Name} is not worn" : null;
            default:
                throw new NotSupportedException($"{activity.GetType().Name} is not work");
        }
    }

    /// Rivers run open and lakes give water here. ponytail: a frozen lake should need a hole cut
    /// (old IceCuttingStrategy); it needs ice thickness, which arrives with Ground's ice in phase 4.
    public static bool HasOpenWater(World world, TileId tile)
    {
        var m = Map.Of(world);
        return m.BesideRiver(tile) || m.TerrainAt(tile) == Terrain.Water;
    }

    static bool HasCookingFire(World world, TileId tile) => Fire.PitAt(world, tile) is FireId id && Fire.IsActive(world.Fires[id]);

    static Resource? Cooked(Resource raw) => raw switch { Resource.RawMeat => Resource.CookedMeat, Resource.RawFish => Resource.CookedFish, _ => null };

    // ---- the session ----

    /// Starts work Refusal allowed. Instant work (stash, wear, eating and drinking) happens here.
    public static WorkSession Begin(World world, Activity activity, List<WorldEvent> events)
    {
        if (Refusal(world, activity) is string refused) throw new InvalidOperationException($"Work.Begin on a refused activity: {refused}");
        var agent = world.Agent(activity.Who);
        var pack = Items.PackOf(agent)!;
        var body = agent.Body;
        switch (activity)
        {
            case Gather g:
                return new WorkSession { Activity = activity, Pool = new PoolId(agent.Tile, g.Pool) };
            case Butcher b:
            {
                var c = Carcasses.Find(world, agent.Tile, b.Carcass)!;
                c.Mode ??= b.Mode ?? ButcherMode.Careful;
                return new WorkSession { Activity = activity };
            }
            case Eat e:
            {
                double kg = Items.Take(pack, e.Food, e.Kg);
                var def = Items.Resources[e.Food];
                double kcal = kg * def.KcalPerKg;
                Body.Eat(body, kcal);
                body.HydrationMl = Math.Clamp(body.HydrationMl + kg * def.HydrationMlPerKg, 0, Body.MaxHydrationMl);
                events.Add(new Ate(world.Minute, agent.Id, e.Food, kg, kcal));
                return new WorkSession { Activity = activity, TargetMinutes = EatMinutes };
            }
            case Drink d:
            {
                double room = (Body.MaxHydrationMl - body.HydrationMl) / 1000;
                double liters = Math.Round(Math.Min(Math.Min(d.Liters, MaxDrinkLiters), room), 2);
                if (Items.Have(pack, Resource.Water) > 0) liters = Items.Take(pack, Resource.Water, liters);
                Body.Drink(body, liters * 1000);
                // Old ConsumptionHandler: cold water takes the edge off overheating.
                if (body.Effects.FirstOrDefault(x => x.Kind == EffectKind.Hyperthermia) is { } hot)
                    hot.Severity = Math.Max(0, hot.Severity - 0.15 * (liters / 0.25));
                events.Add(new Drank(world.Minute, agent.Id, liters));
                return new WorkSession { Activity = activity, TargetMinutes = DrinkMinutes };
            }
            case Stash s:
                Items.Add(Items.StashAt(world, agent.Tile), s.Resource, Items.Take(pack, s.Resource, s.Kg));
                return new WorkSession { Activity = activity };
            case Unstash u:
            {
                double kg = Math.Min(u.Kg, Items.CapacityKg(pack) - Items.CarriedKg(agent));
                Items.Add(pack, u.Resource, Items.TakeFromStash(world, agent.Tile, u.Resource, kg));
                return new WorkSession { Activity = activity };
            }
            case Wear w:
            {
                var gear = pack.Gear.First(x => x.Kind == w.Gear);
                var slot = Items.Def(gear).Slot!.Value;
                pack.Gear.Remove(gear);
                foreach (var off in body.Worn.Where(x => Items.Def(x).Slot == slot).ToList())
                {
                    body.Worn.Remove(off);
                    pack.Gear.Add(off);
                }
                body.Worn.Add(gear);
                return new WorkSession { Activity = activity };
            }
            case Cook or MeltSnow:
                return new WorkSession { Activity = activity };
            default:
                throw new NotSupportedException($"{activity.GetType().Name} is not work");
        }
    }

    /// One minute of work lived. Returns why to stop, if anything: done, nothing left, or too dark
    /// for the next minute - the same check Refusal makes, so a run split into steps stops where an
    /// unbroken one does.
    public static string? Minute(World world, Agent agent, WorkSession s, List<WorldEvent> events)
    {
        s.Minutes++;
        if (WorkMinute(world, agent, s, events) is string done) return done;
        return NeedsLight(world, agent, s.Activity) && TooDarkToWork(world, agent) ? TooDark : null;
    }

    static string? WorkMinute(World world, Agent agent, WorkSession s, List<WorldEvent> events)
    {
        if (s.Activity is Butcher b) return CutMinute(world, agent, b, s, events);
        double effective = 1.0 / ImpairmentFactor(world, agent, s.Activity);
        var pack = Items.PackOf(agent)!;
        switch (s.Activity)
        {
            case Gather g:
            {
                var id = s.Pool!.Value;
                switch (Pools.Def(world, id))
                {
                    case ForagePool:
                    {
                        // This minute's eyes, light and tools, as impairment is: not fixed when the step began.
                        double perception = Body.Perception(agent.Body, Fire.AbilityContextFor(world, agent));
                        // old game: the axe and shovel bonuses were applied only when the total multiplier came out
                        // below 1, so a healthy forager with an axe never got the +10% it was promised (bug, fixed).
                        double yieldFactor = Math.Min(perception, 1.0)
                            * (Items.Tool(pack, ToolType.Axe) != null ? 1.10 : 1.0) * (Items.Tool(pack, ToolType.Shovel) != null ? 1.10 : 1.0);
                        var rng = world.Rng("work");
                        var finds = Pools.Forage(world, id, effective / 60.0, rng);
                        var got = new Inventory();
                        foreach (var (r, kg) in Focus(finds, g.Focus, rng)) Items.Add(got, r, kg * yieldFactor);
                        Keep(world, agent, s, got, events);
                        Pools.Search(world, agent, perception / 60.0, events);
                        return null;
                    }
                    case TreePool:
                        if (Items.Tool(pack, ToolType.Axe) is not { } axe) return "your axe is broken";
                        Items.UseFor(axe, 1, ChopMinutesPerAxeUse);
                        if (Pools.Chop(world, id, effective, world.Rng("work")) is not { } tree) return null;
                        Keep(world, agent, s, tree, events);
                        events.Add(new TreeFelled(world.Minute, agent.Id, id));
                        return "tree felled";
                    case StockPool stock:
                        Keep(world, agent, s, Pools.WorkStock(world, id, effective), events);
                        return Pools.HasStock(world, id) ? null : $"the {stock.Name} is picked clean";
                    default:
                        throw new NotSupportedException("Unknown pool kind");
                }
            }
            case Eat:
                return s.Minutes >= s.TargetMinutes ? "ate" : null;
            case Drink:
                return s.Minutes >= s.TargetMinutes ? "drank" : null;
            case Cook c:
            {
                if (!HasCookingFire(world, agent.Tile)) return "the fire went out";
                // Old CookingHandler: a piece (about a kilo) of meat in 15 minutes, of fish in 12.
                double perMinute = 1.0 / (c.Raw == Resource.RawFish ? CookFishMinutesPerKg : CookMeatMinutesPerKg);
                double kg = Items.Take(pack, c.Raw, Math.Min(perMinute, c.Kg - s.DoneKg));
                Items.Add(pack, Cooked(c.Raw)!.Value, kg);
                s.DoneKg += kg;
                return s.DoneKg >= c.Kg - 1e-9 || Items.Have(pack, c.Raw) <= 0 ? "cooked" : null;
            }
            case MeltSnow:
                if (!HasCookingFire(world, agent.Tile)) return "the fire went out";
                if (s.Minutes < MeltSnowMinutes) return null;
                // ponytail: old game: snow "is assumed freely available", even in July; Ground knows the real depth now.
                var water = new Inventory();
                Items.Add(water, Resource.Water, MeltSnowLiters);
                Keep(world, agent, s, water, events);
                return "melted";
            case Stash or Unstash or Wear:
                return null; // done on the first minute; the agent waits out the rest
            default:
                throw new NotSupportedException($"{s.Activity.GetType().Name} is not work");
        }
    }

    /// The step is over, however it ended: what accrued to it is settled.
    public static void End(World world, Agent agent, WorkSession s, List<WorldEvent> events)
    {
        if (s.Activity is Butcher b) EndButcher(world, agent, b, s, events);
        if (s.Got.Kg.Count > 0 || s.Got.Gear.Count > 0)
            events.Add(new Gathered(world.Minute, agent.Id, agent.Tile, Items.Describe(s.Got)));
        if (s.Dropped.Kg.Count > 0 || s.Dropped.Gear.Count > 0)
            events.Add(new Dropped(world.Minute, agent.Id, agent.Tile, Items.Describe(s.Dropped)));
    }

    /// A minute of old ButcherStrategy: the setup once per carcass, then the cutting, at this
    /// minute's decay and hands. The blood on you and the scent on the carcass build with the
    /// minutes worked. What is ready goes in the pack now; the rest waits on the carcass.
    static string? CutMinute(World world, Agent agent, Butcher b, WorkSession s, List<WorldEvent> events)
    {
        var c = Carcasses.Find(world, agent.Tile, b.Carcass)!;
        Carcasses.CatchUp(world, agent.Tile, c); // before anything reads whether it is frozen
        double effective = 1.0 / ImpairmentFactor(world, agent, b);
        var mode = c.Mode!.Value;
        var props = Carcasses.ModeProps(mode);
        bool handsImpaired = Body.Dexterity(agent.Body, Fire.AbilityContextFor(world, agent)) < 0.7;
        Carcasses.Cut(c, Carcasses.Prepare(c, effective, mode), Items.HasCuttingTool(Items.PackOf(agent)!), handsImpaired, mode);
        var ready = Carcasses.TakeReady(c);
        var leftovers = Items.Receive(world, agent, ready, events, dropExcess: false);
        Carcasses.PutBack(c, leftovers);
        foreach (var (r, kg) in ready.Kg) if (kg - Items.Have(leftovers, r) > 0) Items.Add(s.Got, r, kg - Items.Have(leftovers, r));
        // Old: BloodySeverity an hour, at most 0.5 from butchering; ScentIncrease a session, now an hour.
        double bloody = Effects.Severity(agent.Body, EffectKind.Bloody);
        if (bloody < 0.5) Effects.Set(agent.Body, EffectKind.Bloody, Math.Min(0.5, bloody + props.BloodySeverity / 60));
        c.ScentBonus += props.ScentIncrease / 60;
        if (!Carcasses.AllCut(c)) return null;
        if (!Carcasses.IsButchered(c)) return PackFull; // all cut; what the pack could not take waits on the carcass
        Carcasses.Remove(world, agent.Tile, c);
        return "butchered";
    }

    public const string PackFull = "your pack is full";

    static void EndButcher(World world, Agent agent, Butcher b, WorkSession s, List<WorldEvent> events)
    {
        if (s.Minutes == 0) return;
        events.Add(new Butchered(world.Minute, agent.Id, b.Carcass, Finished: Carcasses.Find(world, agent.Tile, b.Carcass) == null));
    }

    static void Keep(World world, Agent agent, WorkSession s, Inventory got, List<WorldEvent> events)
    {
        if (got.Kg.Count == 0 && got.Gear.Count == 0) return;
        Items.AddAll(s.Got, got);
        Items.AddAll(s.Dropped, Items.Receive(world, agent, got, events: null));
    }

    /// What a focused search finds of what it looks for, over a general one: each such find comes with
    /// another like it on these odds. Old FocusProcessor only filtered, so focus cost the rest of the haul
    /// and bought nothing - a focused food search fed no better than a general one (2026-11-20 forage retune).
    /// Grounding: the eyes and hours a general search spends on sticks and stones go on the berries and roots.
    /// Why this lever: per hour, fresh forest gives ~170 kcal of food (berries 75%), thinning as 1/(1+2h);
    /// kcal per kg are already realistic (berries 500, nuts 6000), and density and depletion are shared
    /// with fuel and are what pushes a forager outward. Measured (14 days, six gate seeds, a scripted
    /// player foraging food 06-18h from camp within six tiles, moving on when ground thins): forest fat
    /// went from -0.06..-0.13 kg/day to -0.00..-0.06; open plain stays near -0.18 and one worked-over
    /// tile near -0.12..-0.20 - poor country and staying put still starve you, and a general search
    /// (the old yield) still runs a deficit.
    public const double FocusGain = 1.3;

    /// Old FocusProcessor.ApplyFocus: each find outside the focus survives on a 40-60% roll.
    static IEnumerable<(Resource, double)> Focus(List<(Resource Resource, double Kg)> finds, ForageFocus focus, Rng rng)
    {
        if (focus == ForageFocus.General) return finds;
        ResourceCategory target = focus switch
        {
            ForageFocus.Fuel => ResourceCategory.Fuel,
            ForageFocus.Food => ResourceCategory.Food,
            ForageFocus.Medicine => ResourceCategory.Medicine,
            _ => ResourceCategory.Material,
        };
        var kept = new List<(Resource, double)>();
        foreach (var group in finds.GroupBy(f => f.Resource))
        {
            if (Items.Resources[group.Key].Category == target)
            {
                foreach (var f in group)
                {
                    kept.Add(f);
                    if (rng.NextDouble() < FocusGain - 1) kept.Add(f);
                }
                continue;
            }
            double keepPct = 0.4 + rng.NextDouble() * 0.2;
            foreach (var f in group) if (rng.NextDouble() < keepPct) kept.Add((f.Resource, f.Kg));
        }
        return kept;
    }
}
