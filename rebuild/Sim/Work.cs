namespace Survival;

/// Old ForageFocus: a focused search keeps what it looks for and drops about half of the rest.
public enum ForageFocus { General, Fuel, Food, Medicine, Materials }

/// One step of work in progress: what Begin worked out, and what the minutes have added up to.
/// Transient: lives for one Sim.Step, never saved.
public sealed class WorkSession
{
    public required Activity Activity { get; init; }
    /// Old AbilityCalculator.GetWorkImpairments, fixed for the session as the old game fixed it.
    public double ImpairmentFactor { get; init; } = 1.0;
    public double EffectiveMinutes { get; set; }
    public int Minutes { get; set; }
    public Inventory Got { get; } = new();
    /// What did not fit in the pack and went on the tile's stash.
    public Inventory Dropped { get; } = new();
    public PoolId? Pool { get; init; }
    public double ForageYieldFactor { get; init; } = 1.0;
    public double Perception { get; init; } = 1.0;
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

    public static bool Handles(Activity a) => a is Gather or Butcher or Eat or Drink or Cook or MeltSnow or Stash or Unstash or Wear;

    // ---- light and ability ----

    /// Old AbilityContext.CalculateDarkness: by the hour, whatever the season; caves are always dark.
    public static double DarknessLevel(World world, TileId tile)
    {
        if (Map.Of(world).CaveAt(tile) == Cave.Floor) return 1.0;
        return (Weather.MinuteOfDay(world.Minute) / 60) switch
        {
            < 5 => 1.0,
            < 6 => 0.5,
            < 17 => 0.0,
            < 20 => 0.2,
            < 21 => 0.6,
            _ => 1.0,
        };
    }

    /// Old WorkRunner.CheckDarknessBlocking: night (old TimeOfDay.Night) or a cave, with no flame or torch.
    public static bool TooDarkToWork(World world, Agent agent)
    {
        int hour = Weather.MinuteOfDay(world.Minute) / 60;
        bool dark = Map.Of(world).CaveAt(agent.Tile) == Cave.Floor || hour < 5 || hour >= 21;
        return dark && !Fire.HasLight(world, agent.Id);
    }

    /// Old AbilityContext.FromFullContext. Fires around the agent must be caught up.
    public static AbilityContext AbilityContextFor(World world, Agent agent) =>
        new(Items.EncumbrancePct(agent), DarknessLevel(world, agent.Tile), Fire.HasLight(world, agent.Id));

    /// Old GetWorkImpairments: bad legs +20%, bad lungs +15%, bad hands +25% time.
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
                if (def is not ForagePool && TooDarkToWork(world, agent)) return "too dark to work";
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
                return TooDarkToWork(world, agent) ? "too dark to work" : null;
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
            {
                var id = new PoolId(agent.Tile, g.Pool);
                var def = Pools.Def(world, id);
                if (def is TreePool) Items.Use(Items.Tool(pack, ToolType.Axe)!); // once a session, as old ChoppingStrategy
                double perception = Body.Perception(body, AbilityContextFor(world, agent));
                // old game: the axe and shovel bonuses were applied only when the total multiplier came out
                // below 1, so a healthy forager with an axe never got the +10% it was promised (bug, fixed).
                double yieldFactor = Math.Min(perception, 1.0)
                    * (Items.Tool(pack, ToolType.Axe) != null ? 1.10 : 1.0) * (Items.Tool(pack, ToolType.Shovel) != null ? 1.10 : 1.0);
                return new WorkSession
                {
                    Activity = activity, Pool = id, Perception = perception, ForageYieldFactor = yieldFactor,
                    ImpairmentFactor = def switch
                    {
                        ForagePool => ImpairmentFactor(body, moving: true, breathing: true),
                        TreePool => ImpairmentFactor(body, moving: true, manipulation: true),
                        StockPool { Kind: StockKind.Salvage } => ImpairmentFactor(body, moving: true),
                        _ => 1.0,
                    },
                };
            }
            case Butcher b:
            {
                var c = Carcasses.Find(world, agent.Tile, b.Carcass)!;
                Carcasses.CatchUp(world, agent.Tile, c);
                c.Mode ??= b.Mode ?? ButcherMode.Careful;
                return new WorkSession
                {
                    Activity = activity,
                    ImpairmentFactor = ImpairmentFactor(body, manipulation: true) * (Carcasses.IsFrozen(world, c) ? 1.5 : 1.0),
                    TargetMinutes = Carcasses.RemainingMinutes(c, c.Mode.Value),
                };
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
                var stash = Items.StashAt(world, agent.Tile);
                double kg = Math.Min(Math.Min(u.Kg, Items.Have(stash, u.Resource)), Items.CapacityKg(pack) - Items.CarriedKg(agent));
                Items.Add(pack, u.Resource, Items.Take(stash, u.Resource, kg));
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

    /// One minute of work lived. Returns why to stop, if anything: done, or nothing left.
    public static string? Minute(World world, Agent agent, WorkSession s, List<WorldEvent> events)
    {
        s.Minutes++;
        double effective = 1.0 / s.ImpairmentFactor;
        s.EffectiveMinutes += effective;
        var pack = Items.PackOf(agent)!;
        switch (s.Activity)
        {
            case Gather g:
            {
                var id = s.Pool!.Value;
                switch (Pools.Def(world, id))
                {
                    case ForagePool:
                        var rng = world.Rng("work");
                        var finds = Pools.Forage(world, id, effective / 60.0, rng);
                        var got = new Inventory();
                        foreach (var (r, kg) in Focus(finds, g.Focus, rng)) Items.Add(got, r, kg * s.ForageYieldFactor);
                        Keep(world, agent, s, got, events);
                        Pools.Search(world, agent, s.Perception / 60.0, events);
                        return null;
                    case TreePool:
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
            case Butcher:
                return s.EffectiveMinutes >= s.TargetMinutes - 1e-9 ? "butchered" : null;
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

    /// Old ButcherStrategy.Execute for the minutes this step lasted: the yield, the blood on your
    /// hands, the scent on the carcass. What the pack cannot hold stays on the carcass.
    static void EndButcher(World world, Agent agent, Butcher b, WorkSession s, List<WorldEvent> events)
    {
        if (s.Minutes == 0) return;
        var c = Carcasses.Find(world, agent.Tile, b.Carcass)!;
        Carcasses.CatchUp(world, agent.Tile, c);
        var pack = Items.PackOf(agent)!;
        var mode = Carcasses.ModeProps(c.Mode!.Value);
        bool handsImpaired = Body.Dexterity(agent.Body, AbilityContextFor(world, agent)) < 0.7;
        var yield = Carcasses.Harvest(c, s.EffectiveMinutes, Items.HasCuttingTool(pack), handsImpaired, c.Mode.Value);
        double bloody = Math.Min(0.5, mode.BloodySeverity * (s.Minutes / 60.0));
        if (bloody > 0.05) Effects.Add(agent.Body, EffectKind.Bloody, bloody);
        c.ScentBonus += mode.ScentIncrease;
        var leftovers = Items.Receive(world, agent, yield, events, dropExcess: false);
        Carcasses.Restore(c, leftovers);
        foreach (var (r, kg) in yield.Kg) Items.Add(s.Got, r, kg - Items.Have(leftovers, r));
        bool finished = Carcasses.IsButchered(c);
        if (finished) world.Tiles[agent.Tile].Carcasses!.Remove(c);
        events.Add(new Butchered(world.Minute, agent.Id, c.Id, finished));
    }

    static void Keep(World world, Agent agent, WorkSession s, Inventory got, List<WorldEvent> events)
    {
        if (got.Kg.Count == 0 && got.Gear.Count == 0) return;
        Items.AddAll(s.Got, got);
        Items.AddAll(s.Dropped, Items.Receive(world, agent, got, events: null));
    }

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
            if (Items.Resources[group.Key].Category == target) { kept.AddRange(group.Select(f => (f.Resource, f.Kg))); continue; }
            double keepPct = 0.4 + rng.NextDouble() * 0.2;
            foreach (var f in group) if (rng.NextDouble() < keepPct) kept.Add((f.Resource, f.Kg));
        }
        return kept;
    }
}
