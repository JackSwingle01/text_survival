using System.Text;

namespace Survival.Web;

/// Builds one view from the player's side of the world: what Verbs.At offers, what the player
/// remembers (Memory: explored tiles, remembered places, sightings), what they can see now, their
/// body by its priorities, the sky. The map never reads the truth about a place out of sight: a fire is
/// drawn as it was last seen (clean-room decision 7).
sealed class ViewBuilder(World world, int gen)
{
    /// Every verb this view lists, by id, for the host to start.
    public readonly List<(string Id, Verb Verb)> Verbs = [];
    /// Every combat round action this view lists, by id.
    public readonly List<(string Id, CombatVerb Fight)> CombatVerbs = [];

    public sealed record CombatVerb(CombatAction Action, int? Target);

    /// Predators seen within this many tiles in the last few hours make an alert.
    public const int NearbyTiles = 6;
    public const long NearbyMinutes = 180;

    int _n;
    readonly Agent _player = world.Agent(AgentId.Player);
    GeneratedMap M => Map.Of(world);

    public ViewDto Build(GameHost.Job? job, ReceiptDto? receipt, IReadOnlyList<LogDto> log, IReadOnlyList<LogDto> fightLines)
    {
        var p = _player;
        var body = p.Body;
        var outcome = Map.Outcome(world);
        var here = Survival.Verbs.At(world, AgentId.Player);
        var priorities = Priorities.Of(world, AgentId.Player);
        var sky = Weather.At(world, world.Minute);
        // What the player feels doing what they are doing, sheltered as it keeps them (Sim.Feels); idle, waiting.
        var feeling = Sim.Feels(world, AgentId.Player, job is { Finished: false } ? job.Activity : new Wait(AgentId.Player));
        var exposure = feeling.Exposure;

        var features = here.LocationFeatures.Select(t =>
        {
            var menu = t.Menu.Select(Dto).ToList();
            string? action = t.Action is { } a ? menu[IndexIn(t.Menu, a)].Id : null;
            return new LocationFeatureDto(Words.Code(t.LocationFeature.Kind), t.Name, [.. t.Conditions.Select(c => new ConditionDto(c.Name, c.Blocking))], action, t.Interact, menu);
        }).ToList();
        var walk = here.Walk.Select(Dto).ToList();
        var tile = here.Tile.Select(Dto).ToList();
        var carried = here.Pack.Select(Dto).ToList();
        var make = here.Make.Select(Dto).ToList();

        return new ViewDto(
            Gen: gen,
            Minute: world.Minute,
            Clock: ClockOf(world, sky),
            Feels: new FeelsDto(Math.Round(feeling.FeltF, 1), [.. feeling.Terms.Select(t => new FeelsTermDto(t.Factor, Math.Round(t.DeltaF, 1)))]),
            Vitals: Vitals(body, priorities, exposure),
            Alerts: Alerts(body, priorities, sky),
            Place: Place(),
            Fire: FireHere(),
            LocationFeatures: features, Walk: walk, Tile: tile, PackVerbs: carried, Make: make,
            Pack: Pack(),
            Body: BodyOf(body),
            Map: MapView.Remembered(world, _player),
            Combat: CombatOf(fightLines),
            Forage: ForageOf(here, features),
            Job: job is null ? null : JobOf(job),
            Receipt: receipt,
            Log: [.. log.TakeLast(40)],
            Outcome: Words.Code(outcome),
            DeathCause: outcome == RunOutcome.Died ? body.DeathCause?.What : null);
    }

    static int IndexIn(IReadOnlyList<Verb> menu, Verb v)
    {
        for (int i = 0; i < menu.Count; i++) if (ReferenceEquals(menu[i], v)) return i;
        throw new InvalidOperationException($"'{v.Label}' is shown for a thing but is not on its menu");
    }

    // ---- verbs ----

    VerbDto Dto(Verb v)
    {
        string id = $"{gen}.{_n++}";
        Verbs.Add((id, v));
        int? minutes = v.Minutes == Survival.Verbs.UntilDoneMinutes ? null : v.Minutes;
        var travel = v.Activity as Travel;
        return new VerbDto(id, v.Label, KindOf(v.Activity), minutes, v.Refused?.Reason,
            Odds: v.Odds is { } o ? OddsOf(o) : null,
            Trip: v.Trip is { } trip && travel != null ? TripOf(trip, travel, v.Risk) : null,
            Make: v.Activity is Make mk ? MakeOf(mk, v.Outcomes) : null,
            Dir: travel is null ? null : Words.Code(travel.Dir),
            Careful: travel?.Pace == Pace.Careful,
            Uses: v.Activity switch
            {
                StartFire f => [Load(f.Tinder, f.TinderKg), .. f.KindlingKg > 0 ? new[] { Load(Resource.Stick, f.KindlingKg) } : [], Names.Of(f.Tool)],
                AddFuel f => [Load(f.Fuel, f.Kg)],
                _ => null,
            },
            Durations: v.Durations,
            Progress: v.Progress is { } p ? new ProgressDto(p.FinishMinute, p.Why is Slowed why ? Narration.Why(why) : p.Faster is Sped sped ? Narration.Why(sped) : null) : null);
    }

    /// The activity's family, for the page's icons and keys. Not a vocabulary: the page acts by id.
    public static string KindOf(Activity a) => a switch
    {
        Wait => "wait",
        Rest => "rest",
        Sleep => "sleep",
        StartFire => "fire",
        AddFuel => "fuel",
        LightTorch => "torch",
        GatherCharcoal => "charcoal",
        Gather => "gather",
        WalkAbout => "walkabout",
        Butcher => "butcher",
        Eat => "eat",
        Drink => "drink",
        Wash => "wash",
        PutDown => "putdown",
        PickUp => "take",
        Wear => "wear",
        TakeOff => "takeoff",
        Fight => "fight",
        Travel => "walk",
        ImproveShelter => "improve",
        PackTent => "pack",
        Make m when Recipes.Get(m.Recipe).Result.Placed => "build", // camp work: the page lists it as building here
        Make => "make",
        Abandon => "abandon",
        PutOn p when Stations.FromNothing(p.Recipe) => "melt",
        PutOn => "puton",
        StopWork => "stopwork",
        TakeFinished => "take",
        TakeBack => "takeback",
        Hunt => "hunt",
        _ => "other",
    };

    /// Sim's odds names their base and factors in the player's words (Names): passed on as they are.
    static OddsDto OddsOf(Odds o)
    {
        var deltas = o.Contributions;
        return new OddsDto(o.Base, o.BasePct,
            [.. o.Terms.Select((t, i) => new OddsTermDto(t.Factor, t.Op == OddsOp.Add ? "add" : "scale", t.Value, deltas[i].DeltaPct))],
            o.RawPct, o.TotalPct, o.Capped);
    }

    TripDto TripOf(TripTime t, Travel go, Odds? risk)
    {
        var to = Map.Step(_player.Tile, go.Dir);
        bool known = MapView.Explored(world, _player, to);
        return new TripDto(t.TotalMinutes, [.. t.Terms.Where(x => x.Minutes != 0).Select(x => new TripTermDto(x.Factor, x.Minutes))],
            Trip.PaceMatters(world, go.Who, go.Dir),
            known ? Map.Describe(M, to) : "unexplored ground", known,
            risk is { TotalPct: > 0 } r ? OddsOf(r) : null);
    }

    MakeDto MakeOf(Make mk, MakeOdds? odds)
    {
        var r = Recipes.Get(mk.Recipe);
        double? progress = Making.MinutesDone(world, _player, r, mk.Target) / r.Minutes;
        var materials = r.Materials.Select(Making.Words)
            .Concat(r.Items.Select(Names.GearWords)).ToList();
        string result = r.Result.Repair is { } repair && mk.Target is { } kind ? $"{Names.Gear(kind)}, {Names.Pct(repair.RestoresPct)} of its wear made good"
            : r.Result.Gear is { } g ? Names.Gear(g) : r.Result.Resource is { } res ? (Items.IsDiscrete(res) || r.Result.Kg > 0 ? Names.Amount(res, r.Result.Kg, r.Result.Count) : Names.Of(res)) : r.Name;
        return new MakeDto(r.Id, Making.Label(r, mk.Target), Names.Of(r.Category), r.Minutes, materials,
            [.. r.Tools.Select(t => Names.Of(t))], [.. r.Requires.Select(q => Names.Of(q))], result,
            odds?.PlainPct ?? 1,
            odds == null ? [] : [.. odds.Outcomes.Select(o => new MakeOutcomeDto(o.Outcome.Words, o.Outcome.Kind == OutcomeKind.Succeed, o.Outcome.FailureLike, OddsOf(o.Odds)))],
            progress, mk.Target);
    }

    // ---- the always-on layer ----

    /// The clock and sky: the same for the player's view and the observer's.
    public static ClockDto ClockOf(World world, WeatherSample sky)
    {
        int mod = Weather.MinuteOfDay(world.Minute);
        return new ClockDto(Weather.DayNumber(world.Minute), mod, Names.Title(Weather.SeasonAt(world.Minute)), Names.Title(sky.Condition),
            Math.Round(sky.TemperatureF, 1), Math.Round(sky.WindMph, 1), Names.Of(sky.WindDirection),
            Weather.LightFromHour * 60, Weather.DarkFromHour * 60, Weather.LightLeftMinutes(world.Minute));
    }

    static VitalDto[] Vitals(BodyState b, StatPriorities priorities, Exposure e)
    {
        // Warmth's bar spans the core from the hypothermia line's far side (90F) to normal.
        double warmth = Math.Clamp((b.CoreTempF - 90.0) / (Body.BaseCoreTempF - 90.0), 0, 1);
        bool warming = e.Warmth.PitHeatF + e.Warmth.AirHeatF > 1;
        return
        [
            new("warmth", warmth, Words.Code(priorities.Warmth), $"Body {b.CoreTempF:0.0}°F{(warming ? ", by a fire" : "")}"),
            new("food", Math.Clamp(b.CaloriesPct, 0, 1), Words.Code(priorities.Food), $"{b.CaloriesKcal:0} kcal in the stomach · fat {b.FatKg:0.0} kg"),
            new("water", Math.Clamp(b.HydrationPct, 0, 1), Words.Code(priorities.Water), $"{b.HydrationPct:P0} · {b.HydrationMl / 1000:0.0} L"),
            new("rest", Math.Clamp(b.EnergyPct, 0, 1), Words.Code(priorities.Rest), $"{b.EnergyPct:P0}"),
        ];
    }

    /// Only what is past a line, by danger (ui-design §Alerts). Mild stages are the bar's colour, not a chip.
    List<AlertDto> Alerts(BodyState b, StatPriorities priorities, WeatherSample sky)
    {
        var list = new List<AlertDto>();
        if (priorities.Warmth == Priority.Critical)
            list.Add(new("hypothermia", "Hypothermia", "danger", 90, "ui/temperature", [$"Body {b.CoreTempF:0.0}°F", "Get to a fire now"]));
        else if (b.Effects.FirstOrDefault(x => x.Kind == EffectKind.Shivering && x.Severity > 0.05) is { } shiver)
            list.Add(new("shivering", "Shivering", "cold", 60, "ui/temperature", [$"Body {b.CoreTempF:0.0}°F", "Stops by a fire"]));
        if (b.Effects.FirstOrDefault(x => x.Kind == EffectKind.Bleeding && x.Severity > 0) is { } bleed)
            list.Add(new("bleeding", "Bleeding", "danger", 85, "ui/blood", [$"Blood {b.BloodPct:P0}"]));
        if (b.Effects.FirstOrDefault(x => x.Kind == EffectKind.Wet && x.Severity >= 0.2) is { } wet)
            list.Add(new("wet", "Wet", "cold", 55, "ui/water", [$"Soaked {wet.Severity:P0}", "Dries by a fire; freezes in the cold"]));
        if (priorities.Water == Priority.Critical) list.Add(new("dehydrated", "Dehydrated", "danger", 80, "ui/water", ["Drink, or melt snow by a fire"]));
        if (priorities.Food == Priority.Critical) list.Add(new("starving", "Starving", "danger", 70, "ui/food", ["Your body is burning muscle"]));
        if (priorities.Rest == Priority.Critical) list.Add(new("exhausted", "Exhausted", "danger", 65, "ui/energy", ["You will collapse soon"]));
        foreach (var (capacity, v) in BodyReadOut.Of(b))
        {
            if (capacity is not (Capacity.Moving or Capacity.Manipulation or Capacity.Sight)) continue;
            string name = capacity.ToString();
            if (v < 0.9 && b.Effects.All(x => x.Kind != EffectKind.Shivering)) // shivering's cost is its own chip
                list.Add(new("impaired-" + name.ToLowerInvariant(), $"{name} impaired", "warn", 40, "ui/capacities", [$"{name} {v:P0}"]));
        }
        if (Items.EncumbrancePct(_player) > 1)
            list.Add(new("overloaded", "Overloaded", "warn", 35, "ui/backpack", [$"{Items.CarriedKg(_player):0.0} kg carried", "Moving slower"]));
        var memory = Memories.Of(_player);
        foreach (var (id, s) in memory.Sightings)
            if (id != AgentId.Player && Fauna.IsPredator(s.Species) && world.Minute - s.Minute <= NearbyMinutes
                && Math.Abs(s.Tile.X - _player.Tile.X) + Math.Abs(s.Tile.Y - _player.Tile.Y) <= NearbyTiles)
            {
                list.Add(new("predator-" + id.Value, $"{Names.Title(s.Species)} nearby", "danger", 75, Words.SpeciesIcon(s.Species),
                    [$"{s.Count} seen {Ago(world.Minute - s.Minute)}", "A fire keeps it off"]));
                break; // one chip for the nearest threat is enough
            }
        var gear = (Items.PackOf(_player)?.Gear ?? []).Concat(_player.Body.Worn).Where(g => g.MaxDurability > 0 && Items.ConditionPct(g) < 0.2).ToList();
        if (gear.Count > 0)
            list.Add(new("gear", "Gear failing", "warn", 20, "ui/gear", [.. gear.Select(g => $"{Items.Def(g).Name} {Items.ConditionPct(g):P0}")]));
        return [.. list.OrderByDescending(a => a.Rank)];
    }

    PlaceDto Place()
    {
        var t = _player.Tile;
        return new PlaceDto(Map.Describe(M, t), Names.Title(M.TerrainAt(t)), t.X, t.Y, t == M.Camp, Map.IsDark(M, t), Map.DiscoveryText(world, t));
    }

    FireDto? FireHere()
    {
        if (Fire.PitAt(world, _player.Tile) is not FireId id) return null;
        return FireOf(world.Fires[id]);
    }

    public static FireDto FireOf(FireState f)
    {
        var phase = Fire.Phase(f);
        return new FireDto(Names.Title(phase), Fire.Burns(phase), Math.Round(Fire.FuelHours(f), 2), Math.Round(Fire.TemperatureF(f)), Math.Round(f.CharcoalKg, 2));
    }

    /// What an activity would take from the pack, in words: the whole pieces it would move, or the amount.
    string Load(Resource r, double kg) => Items.PackOf(_player) is { } pack
        ? Names.Amount(r, Items.WouldMoveKg(pack, r, kg), Items.WouldMovePieces(pack, r, kg)) : Names.Kg(kg, r);

    PackDto Pack()
    {
        var pack = Items.PackOf(_player) ?? new Inventory();
        GearDto G(Gear g) => new(g.Kind, Items.Def(g).Name, Items.ConditionPct(g), Items.Def(g).Slot is EquipSlot slot ? Names.Title(slot) : null);
        return new PackDto(Math.Round(Items.CarriedKg(_player), 2), Items.CapacityKg(pack),
            [.. Items.Totals(pack).Where(x => x.Kg > 0.001).Select(x => new StackDto(x.Resource.ToString(), Names.Title(x.Resource), Math.Round(x.Kg, 2), Names.Amount(x.Resource, x.Kg, Items.Count(pack, x.Resource))))],
            [.. pack.Gear.Select(G)], [.. _player.Body.Worn.Select(G)],
            [.. (pack.Projects ?? []).Select(p => new ProjectDto(p.Recipe, Making.Label(Recipes.Get(p.Recipe), p.Target),
                Math.Clamp(p.MinutesDone / Recipes.Get(p.Recipe).Minutes, 0, 1)))]);
    }

    BodyDto BodyOf(BodyState b)
    {
        var shown = BodyReadOut.Of(b);
        var skills = (_player.Mind as PersonMind)?.Skills ?? [];
        return new BodyDto(Math.Round(b.CoreTempF, 1), b.BloodPct, Math.Round(b.CaloriesKcal), Math.Round(b.HydrationMl), b.EnergyPct,
            [.. b.Effects.Where(e => e.Severity > 0.01).Select(e => new EffectDto(Names.Title(e.Kind), Math.Round(e.Severity, 2)))],
            [.. shown.Where(c => c.Pct < 0.995).Select(c => new CapacityDto(Names.Title(c.Capacity), Math.Round(c.Pct, 3)))],
            [.. skills.Where(s => s.Value.Level > 0).Select(s => new SkillDto(Names.Title(s.Key), s.Value.Level))],
            Math.Round(b.ClothingClo, 2));
    }

    public static string TrackIcon(TrackMaker m) => m switch
    {
        TrackMaker.Human => "tracks/boot",
        TrackMaker.Paw => "tracks/paw",
        TrackMaker.Hoof => "tracks/hoof",
        TrackMaker.Bird => "tracks/bird",
        _ => throw new ArgumentOutOfRangeException(nameof(m), m, null),
    };

    // ---- the fight ----

    CombatDto? CombatOf(IReadOnlyList<LogDto> lines)
    {
        if (Combat.EncounterOf(world, AgentId.Player) is not { } enc) return null;
        var me = Combat.UnitOf(enc, AgentId.Player);
        var units = enc.Units.Select((u, i) =>
        {
            var agent = world.Agent(u.Agent);
            var body = Combat.BodyOf(world, u);
            string name = u.Agent == AgentId.Player ? "You" : Words.Cap(Words.Of(agent.Species));
            return new UnitDto(i, u.Team, Words.SpeciesIcon(agent.Species), name, u.Position.X, u.Position.Y, u.InField, Combat.IsPlayer(u),
                Words.Of(Combat.StateOf(world, u)), Condition(body, agent.Species), u.Weapon is { } w ? Items.Def(w).Name : null);
        }).ToList();
        var facing = Combat.TargetOf(enc, me);
        double distance = facing != null ? me.Position.DistanceTo(facing.Position) : 0;
        int? target = facing != null ? enc.Units.IndexOf(facing) : null;
        var actions = new List<VerbDto>();
        var targets = new List<TargetDto>();
        foreach (var t in Combat.Targets(world, enc, me))
        {
            string tid = $"{gen}.{_n++}";
            CombatVerbs.Add((tid, new CombatVerb(CombatAction.Turn, t.Index)));
            var verb = new VerbDto(tid, $"Face {units[t.Index].Name.ToLowerInvariant()}", "fight", 1, t.Facing ? "You are already facing them." : t.TurnRefusal?.Reason);
            targets.Add(new TargetDto(t.Index, units[t.Index].Name, Math.Round(t.DistanceM, 1), t.Facing, verb));
        }
        foreach (var a in Enum.GetValues<CombatAction>())
        {
            if (a is CombatAction.Move or CombatAction.Turn) continue; // a cell, a target: Move is listed below, Turn with each target
            string id = $"{gen}.{_n++}";
            CombatVerbs.Add((id, new CombatVerb(a, null)));
            actions.Add(new VerbDto(id, Words.Cap(Words.Of(a)), "fight", 1, Combat.Refusal(world, enc, me, a)?.Reason));
        }
        string moveId = $"{gen}.{_n++}";
        CombatVerbs.Add((moveId, new CombatVerb(CombatAction.Move, null)));
        actions.Add(new VerbDto(moveId, "Move", "fight-move", 1, me.InField ? null : "You are out of the fight."));
        var chase = Combat.ChaseOffer(world, enc, me) is var (fleer, odds)
            ? new ChaseDto(enc.Units.IndexOf(fleer), units[enc.Units.IndexOf(fleer)].Name, Math.Round(odds.Pct, 2), Math.Round(odds.Seconds), Math.Round(odds.PursuerMetres))
            : null;
        return new CombatDto(Combat.MapSize, enc.Rounds, units, actions, Words.Of(Combat.ZoneOf(distance)), Math.Round(distance, 1), target,
            MoveAllowance(enc, me), targets, chase, lines);
    }

    /// How far a Move may go: the largest whole distance Combat.Refusal accepts, probed along the row.
    double MoveAllowance(Encounter enc, CombatUnit me)
    {
        double best = 0;
        for (int d = 1; d <= 12; d++)
        {
            var c = new Cell(Math.Clamp(me.Position.X + d, 0, Combat.MapSize - 1), me.Position.Y);
            if (c.X - me.Position.X != d) c = new Cell(me.Position.X - d, me.Position.Y);
            if (Combat.Refusal(world, enc, me, CombatAction.Move, to: c) == null) best = d; else break;
        }
        return best;
    }

    static string Condition(BodyState b, Species species)
    {
        if (!Body.IsAlive(b)) return "dead";
        double v = Body.Vitality(b, species);
        return v > 0.9 ? "unhurt" : v > 0.65 ? "wounded" : v > 0.35 ? "badly hurt" : "near death";
    }

    /// The forage ground's screen, read off the forage feature's verbs (Verbs.At) and the player's memory.
    ForageDto? ForageOf(Here here, List<LocationFeatureDto> features)
    {
        for (int i = 0; i < here.LocationFeatures.Count; i++)
        {
            var f = here.LocationFeatures[i];
            if (f.LocationFeature.Kind != LocationFeatureKind.Pool) continue;
            var id = new PoolId(f.LocationFeature.Tile, f.LocationFeature.Index);
            if (Pools.Def(world, id) is not ForagePool def) continue;
            var menu = features[i].Menu;
            string VerbOf(Func<Activity, bool> which) => menu[f.Menu.ToList().FindIndex(v => which(v.Activity))].Id;
            var foci = Pools.Foci(def).Select(x => new FocusDto(Names.Of(x.Focus), x.Hint,
                VerbOf(a => a is Gather g && g.Focus == x.Focus))).ToList();
            var offered = Pools.Foci(def).Select(x => x.Focus).ToHashSet();
            var clues = Clues.At(world, AgentId.Player).Select(c => new ClueDto(c.Def.Text, Words.Code(c.Def.Kind),
                Clues.Suggests(c.Def) is ForageFocus s && offered.Contains(s) ? Names.Of(s) : null)).ToList();
            bool foraged = Memories.Of(_player).Places.GetValueOrDefault(id.Tile)?.Foraged == true;
            return new ForageDto(foraged ? Pools.QualityWord(Pools.ForageDensity(world, id)) : null, Pools.SearchEffort(world, AgentId.Player, id.Tile),
                foci, clues, VerbOf(a => a is WalkAbout), OddsOf(Work.ForageYield(world, _player)));
        }
        return null;
    }

    JobDto JobOf(GameHost.Job job)
    {
        int? total = job.Budget;
        if (job.Activity is Travel && _player.Crossing is { } c) total = c.TotalMinutes;
        else if (job.Activity is Travel && job.Finished) total = job.Done;
        if (job.Activity is Make mk && Items.PackOf(_player) is { } pack && Making.MinutesDone(world, _player, Recipes.Get(mk.Recipe), mk.Target) is double done)
        {
            var r = Recipes.Get(mk.Recipe);
            double rate = Making.WorkPerMinute(world, _player, pack, r);
            if (rate > 0) total = job.Done + (int)Math.Ceiling((r.Minutes - done) / rate);
        }
        var session = job.Session;
        var finds = session?.LuckFactor > 0
            ? session.Finds.GroupBy(x => x.Resource).OrderBy(g => g.Min(x => x.Minute))
                .Select(g => new FindDto(g.Key.ToString(), Names.Of(g.Key), Words.Code(Items.Resources[g.Key].Category), g.Count(), Math.Round(g.Sum(x => x.Kg), 2))).ToList()
            : null;
        return new JobDto(job.Label, job.Kind, job.Done, total, job.Finished, job.Outcome, job.Reason, job.From.X, job.From.Y, job.Dir is { } dir ? Words.Code(dir) : null,
            finds, finds == null ? null : Math.Round(session!.Finds.Sum(x => x.Kg), 2), session == null ? null : Work.Mood(session),
            finds == null ? null : Words.Code(Pools.TierOf(session!.LuckFactor)));
    }

    static string Ago(long minutes) => minutes < 60 ? $"{minutes} min ago" : $"{minutes / 60} h ago";
}
