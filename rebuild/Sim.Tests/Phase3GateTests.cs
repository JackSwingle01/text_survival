using Survival;
using Xunit;

/// Phase 3a/3c gate: everything phase 3 added, mixed in one run (foraging, felling, a fire fed
/// three woods, travel, butchering, the player's fights, a headless pack hunt that empties a herd,
/// a herd member leaving), saved and loaded every few steps, comes out byte-identical to the same
/// run straight through. A Dictionary walked after Remove-then-Add, or one object reachable from
/// two places, is what this catches.
[Trait("Category", TestCategories.Slow)]
public class Phase3GateTests
{
    static readonly AgentId P = AgentId.Player;
    const int Steps = 160;

    internal sealed class Tally { public int Felled, Butchered, PlayerFights, HerdsEmptied, Splits; public HashSet<Resource> Fuels = []; public int Moves; public List<WorldEvent> Events = []; }

    /// Reads only world state, so a load resumes it exactly. Seed 17: the tile east of camp is forest.
    static void Play(World w, int i, Tally t)
    {
        var me = w.Agent(P);
        if (!Body.IsAlive(me.Body)) me.Members[0] = BodyState.BaselineHuman();
        Body.Eat(me.Body, 2000);
        me.Body.HydrationMl = Body.MaxHydrationMl;
        me.Body.SleepDebtMinutes = 0;
        var pack = Items.Pack(w, P);
        var tile = me.Tile;
        void S(Survival.Activity a, int minutes)
        {
            // A player who died in an earlier call of this step waits for the next step to be revived.
            if (!Body.IsAlive(w.Agent(P).Body)) return;
            var r = Sim.Step(w, a, minutes);
            t.Events.AddRange(r.Events);
            t.Felled += r.Events.OfType<TreeFelled>().Count();
            t.Butchered += r.Events.OfType<Butchered>().Count();
            t.PlayerFights += r.Events.OfType<EncounterEnded>().Count(f => f.Agent == P);
            t.Moves += r.Events.OfType<Moved>().Count();
            foreach (var f in r.Events.OfType<FuelAdded>().Where(f => f.Kg > 0)) t.Fuels.Add(f.Fuel);
        }

        if (Combat.EncounterOf(w, P) is Encounter enc)
        {
            var acts = Combat.Actions(w, enc, Combat.UnitOf(enc, P));
            S(new Fight(P, acts.Contains(CombatAction.Attack) ? CombatAction.Attack : acts.Contains(CombatAction.Advance) ? CombatAction.Advance : CombatAction.Wait), 1);
            return;
        }
        var camp = Map.Of(w).Camp;
        bool Burning() => Fire.PitAt(w, tile) is FireId f && Fire.IsActive(w.Fires[f]);
        switch (i % 10)
        {
            case 0: S(new Gather(P, 0), 30); break;
            case 1:
                {
                    // The fire is kept at camp only: a predator stays clear of a burning fire, so the fights are out in the wood.
                    if (tile != camp) break;
                    if (!Burning())
                    {
                        if (Items.Have(pack, Resource.Tinder) < 0.05) TestKit.Give(pack, Resource.Tinder, 0.2);
                        if (Items.Have(pack, Resource.Stick) < 0.5) TestKit.Give(pack, Resource.Stick, 1);
                        S(new StartFire(P, ToolType.FireStriker, Resource.Tinder, 0.05, 0.3), 3);
                    }
                    if (Burning())
                        foreach (var r in new[] { Resource.Stick, Resource.PineBranch, Resource.BirchBranch, Resource.OakBranch })
                        {
                            if (Items.Have(pack, r) <= 0) TestKit.Give(pack, r, 2);
                            S(new AddFuel(P, r, 1.0), 2);
                        }
                    break;
                }
            case 2:
                {
                    if (Burning()) break;
                    var wolves = Agents.SpawnGroup(w, Species.Wolf, tile, 2);
                    Combat.Create(w, [new Fighter(P) { Weapon = Items.Tool(pack, ToolType.Spear) }], [new Fighter(wolves[0])], tile, 8, Awareness.Engaged, Awareness.Engaged);
                    break;
                }
            case 4:
                {
                    // A pack of five, four of which ambush a lone caribou. The caribou comes first, so the
                    // agent its death removes is not the newest one.
                    var caribou = Agents.Spawn(w, Species.Caribou, tile);
                    var wolves = Agents.SpawnGroup(w, Species.Wolf, tile, 5);
                    t.Splits++;
                    var hunt = Combat.Create(w, [.. wolves.Take(4).Select(id => new Fighter(id))], [new Fighter(caribou)], tile, 5, Awareness.Engaged, Awareness.Unaware);
                    Combat.ResolveHeadless(w, hunt, []);
                    if (!w.Agents.ContainsKey(caribou)) t.HerdsEmptied++;
                    break;
                }
            case 5:
                {
                    var carcasses = w.Tiles.GetValueOrDefault(tile)?.Carcasses;
                    if (carcasses is { Count: > 0 }) S(new Butcher(P, carcasses[0].Id), 90);
                    else S(new Rest(P), 5);
                    break;
                }
            case 6:
                {
                    // Out to the wood east of camp and back on alternate visits, felling while there.
                    int trees = Pools.At(w, tile).ToList().FindIndex(p => p is TreePool);
                    // Back to the axe after an interruption, a few times, so the run fells what it is here to fell.
                    for (int tries = 0; tries < 4 && trees >= 0 && !Work.TooDarkToWork(w, me) && Combat.EncounterOf(w, P) == null
                        && Sim.Refusal(w, new Gather(P, trees)) == null; tries++)
                    {
                        int felledBefore = t.Felled;
                        S(new Gather(P, trees), 120);
                        if (t.Felled > felledBefore) break;
                    }
                    foreach (var (r, kg) in Items.KgByResource(pack).ToList())
                        if (r is not (Resource.Tinder or Resource.Stick) && Items.Resources[r].Category != ResourceCategory.Food) S(new PutDown(P, r, kg), 1);
                    var dir = tile == camp ? Direction.East : Direction.West;
                    for (int tries = 0; tries < 5 && w.Agent(P).Tile == tile && Combat.EncounterOf(w, P) == null; tries++)
                        S(new Travel(P, dir, Pace.Careful), 30);
                    break;
                }
            case 8:
                if (Items.KgByResource(pack).Keys.FirstOrDefault(r => Items.Resources[r].Category == ResourceCategory.Food, Resource.Water) is var food && food != Resource.Water)
                    S(new Eat(P, food, 0.5), 10);
                S(new Sleep(P), 60);
                break;
            default: S(new Wait(P), 10); break;
        }
    }

    /// The busy day (Run(Steps)), built once for the tests that only read it. A test that looks for objects
    /// shared in memory builds its own: a save/load copy would hide them.
    internal static readonly SharedWorld BusyDay = new(() => Run(Steps).World);

    internal static (World World, Tally Tally) Run(int steps, int saveEvery = 0)
    {
        var w = TestWorlds.New(17);
        // An NPC living on its own mind beside all this: its mind, memory and work in hand are saved state too.
        NpcBench.Spawn(w, Map.Of(w).Camp);
        var pack = Items.Pack(w, P);
        foreach (var g in new[] { "ground_stone_axe", "wooden_spear", "fire_striker", "flint_knife", "large_bag", "large_bag" }) pack.Gear.Add(Items.Make(g));
        var t = new Tally();
        for (int i = 0; i < steps; i++)
        {
            if (saveEvery > 0 && i % saveEvery == 0) w = TestWorlds.Load(Save.ToJson(w));
            Play(w, i, t);
        }
        return (w, t);
    }

    /// The mixed run's events, every one the player's steps raised, with the herds' and the NPC's.
    internal static List<WorldEvent> RunCollecting(int steps)
    {
        var w = TestWorlds.New(17);
        NpcBench.Spawn(w, Map.Of(w).Camp);
        var pack = Items.Pack(w, P);
        foreach (var g in new[] { "ground_stone_axe", "wooden_spear", "fire_striker", "flint_knife", "large_bag", "large_bag" }) pack.Gear.Add(Items.Make(g));
        var t = new Tally();
        for (int i = 0; i < steps; i++) Play(w, i, t);
        return t.Events;
    }

    [Fact]
    public void MixedRun_SavedAndLoadedEveryFewSteps_IsByteIdenticalToAStraightRun()
    {
        var (straight, t) = Run(Steps);
        var (resumed, _) = Run(Steps, saveEvery: 3);
        // It did what it is here to cover.
        Assert.True(t.Felled > 0, "no tree felled");
        Assert.True(t.Butchered > 0, "nothing butchered");
        Assert.True(t.PlayerFights > 0, "the player never fought");
        Assert.True(t.HerdsEmptied > 0, "no herd was emptied by a hunt");
        Assert.True(t.Splits > 0 && t.Moves > 0);
        Assert.True(t.Fuels.Count >= 3, $"fed only {string.Join(", ", t.Fuels)}");
        Assert.Equal(Save.ToJson(straight), Save.ToJson(resumed));
    }

    /// Review 2: cases that step several times in a row must not step a player who died in one of them.
    [Fact]
    public void ALongMixedRun_NeverStepsADeadPlayer()
    {
        var (w, t) = Run(1500);
        Assert.True(t.PlayerFights > 0 && w.Minute > 0);
    }
}
