using System.Text.Json;
using text_survival.Actions;
using text_survival.Actions.Events;
using text_survival.Actions.Tensions;
using text_survival.Actors.Animals;
using text_survival.Actors.Player;
using text_survival.Environments;
using text_survival.Environments.Features;
using text_survival.Environments.Grid;
using text_survival.Items;
using text_survival.Persistence;
using text_survival.Tests.Support;

namespace text_survival.Tests.Animals;

public class PredatorInteractionTests
{
    private static GameContext World()
    {
        var weather = new Weather(50, GameContext.StartTime);
        var map = new GameMap(40, 2) { Weather = weather };
        for (int x = 0; x < 40; x++)
            for (int y = 0; y < 2; y++)
                map.SetLocation(x, y, new Location($"Ground {x},{y}", "", weather, 0));
        map.CurrentPosition = new GridPosition(0, 0);
        var home = map.GetLocationAt(0, 0)!;
        var player = new Player { CurrentLocation = home, Map = map };
        return new GameContext(player, home, weather) { Map = map, Ui = new ScriptedUi { FrameSeconds = 1 } };
    }

    private static Herd Pack(GameContext ctx, int x = 0, AnimalType type = AnimalType.Wolf)
    {
        var at = ctx.Map!.GetLocationAt(x, 0)!;
        var h = Herd.Create(type, at, ctx.Map, [ctx.Map.CurrentPosition, new GridPosition(x, 0)]);
        for (int i = 0; i < 2; i++) h.AddMember(AnimalFactory.FromType(type, at, ctx.Map)!);
        h.Hunger = 0.8;
        ctx.Herds.Add(h);
        return h;
    }

    private static EventResult Choice(GameContext ctx, Herd h, string name) =>
        PredatorEventFactory.Create(ctx, h).GetAvailableChoices(ctx).Single(c => c.Label == name).Results.Single();

    [Fact]
    public void TerritoryMembershipDoesNotMeanDetectionOrAnEncounter()
    {
        var ctx = World();
        var h = Pack(ctx, 39);
        Assert.Contains(h, AnimalPresence.Near(ctx));
        Assert.False(PredatorInteractions.CanDetect(ctx, h, ctx.player));
        Assert.False(PredatorInteractions.CanObserve(ctx, h));
        Assert.Null(PredatorInteractions.Update(h, 1, ctx));
        ctx.QueueEncounter(new EncounterConfig(h.AnimalType, 5, 1, h.Members[0]));
        Assert.False(ctx.HasPendingEncounter);
    }

    [Fact]
    public void HungerAndActualContactStartFollowingWithoutAnyTension()
    {
        var ctx = World(); var h = Pack(ctx, 1);
        PredatorInteractions.Update(h, 1, ctx);
        Assert.Same(ctx.player, h.Pursuit!.Target);
        Assert.Same(ctx.CurrentLocation, h.Pursuit.LastDetectedLocation);
        Assert.True(ctx.Check(EventCondition.PredatorFollowing));
        Assert.False(ctx.Check(EventCondition.PredatorWithinReach));
        Assert.Empty(ctx.Tensions.GetAllTensions());
    }

    [Fact]
    public void FullPredatorDoesNotStartFollowing()
    {
        var ctx = World(); var h = Pack(ctx); h.Hunger = 0.1;
        Assert.Null(PredatorInteractions.Update(h, 1, ctx));
        Assert.Null(h.Pursuit);
    }

    [Fact]
    public void SearchUsesLastKnownLocationAndExpires()
    {
        var ctx = World(); var h = Pack(ctx);
        PredatorInteractions.Update(h, 1, ctx);
        var last = ctx.CurrentLocation;
        ctx.Map!.CurrentPosition = new GridPosition(39, 0);
        ctx.player.CurrentLocation = ctx.Map.GetLocationAt(39, 0)!;
        ctx.GameTime = ctx.GameTime.AddMinutes(1);
        PredatorInteractions.Update(h, 1, ctx);
        Assert.Equal(PredatorIntent.Searching, h.Pursuit!.Intent);
        Assert.Same(last, h.Pursuit.LastDetectedLocation);
        ctx.GameTime = ctx.GameTime.AddMinutes(20);
        PredatorInteractions.Update(h, 1, ctx);
        Assert.Null(h.Pursuit);
    }

    [Fact]
    public void BeingAtCampDoesNotClearContact()
    {
        var ctx = World(); var h = Pack(ctx);
        PredatorInteractions.Update(h, 1, ctx);
        Assert.True(ctx.IsAtCamp);
        Assert.NotNull(h.Pursuit);
        Assert.True(ctx.Check(EventCondition.PredatorWithinReach));
    }

    [Fact]
    public void ReadingSignsDoesNotCreateAnimalsOrPursuit()
    {
        var ctx = World();
        foreach (var detail in new[] { EnvironmentalDetail.AnimalTracks(AnimalType.Wolf), EnvironmentalDetail.AnimalDroppings(), EnvironmentalDetail.BentBranches() })
        {
            var (_, _, tension) = detail.Interact();
            Assert.Null(tension);
        }
        Assert.Empty(ctx.Herds);
        Assert.Empty(ctx.Tensions.GetAllTensions());
    }

    [Fact]
    public async Task BaitTransfersExactMeatAndFeedingConsumesItOnce()
    {
        var ctx = World(); var h = Pack(ctx);
        ctx.Inventory.Add(Resource.RawMeat, 0.4);
        ctx.Inventory.Add(Resource.CookedMeat, 0.6);
        var result = Choice(ctx, h, "Leave your meat");
        await result.WorldAction!(ctx);
        Assert.Equal(0, ctx.Inventory.Weight(Resource.RawMeat) + ctx.Inventory.Weight(Resource.CookedMeat));
        Assert.Equal(1, PredatorInteractions.FoodAt(ctx.CurrentLocation), 6);
        double hunger = h.Hunger;
        PredatorInteractions.Update(h, 1, ctx);
        Assert.Equal(HerdState.Feeding, h.State);
        Assert.True(h.Hunger < hunger);
        Assert.Null(h.Pursuit);
        double remaining = PredatorInteractions.FoodAt(ctx.CurrentLocation);
        Assert.Equal(remaining, PredatorInteractions.EatAt(ctx.CurrentLocation, 10), 6);
        Assert.Equal(0, PredatorInteractions.EatAt(ctx.CurrentLocation, 10));
    }

    [Fact]
    public void PredatorSafeCacheIsNotBait()
    {
        var ctx = World();
        var cache = new CacheFeature("Protected", default, protectsFromPredators: true);
        cache.Storage.Add(Resource.RawMeat, 2);
        ctx.CurrentLocation.Features.Add(cache);
        Assert.Equal(0, PredatorInteractions.FoodAt(ctx.CurrentLocation));
        Assert.Equal(0, PredatorInteractions.EatAt(ctx.CurrentLocation, 2));
        Assert.Equal(2, cache.Storage.Weight(Resource.RawMeat));
    }

    [Fact]
    public async Task AResponseOnlyAffectsItsBoundPack()
    {
        var ctx = World(); var first = Pack(ctx); var second = Pack(ctx);
        await Choice(ctx, first, "Shout and stand tall").WorldAction!(ctx);
        Assert.True(first.Fear > 0);
        Assert.Equal(0, second.Fear);
    }

    [Fact]
    public async Task DeadSourceCancelsQueuedSceneAndDoesNotConsumeFoodOrTime()
    {
        var ctx = World(); var h = Pack(ctx);
        ctx.Inventory.Add(Resource.RawMeat, 1);
        var evt = PredatorEventFactory.Create(ctx, h);
        var response = Choice(ctx, h, "Leave your meat");
        ctx.Herds.Remove(h);
        var time = ctx.GameTime;
        await GameEventRegistry.HandleEvent(ctx, evt); // No UI prompt can occur.
        await GameEventRegistry.HandleOutcome(ctx, response);
        Assert.Equal(time, ctx.GameTime);
        Assert.Equal(1, ctx.Inventory.Weight(Resource.RawMeat));
        Assert.Equal(0, PredatorInteractions.FoodAt(ctx.CurrentLocation));
    }

    [Fact]
    public async Task PendingEncounterRevalidatesSourceInsteadOfManufacturingAnimal()
    {
        var ctx = World(); var h = Pack(ctx);
        ctx.QueueEncounter(new EncounterConfig(h.AnimalType, 20, 1, h.Members[0]));
        Assert.True(ctx.HasPendingEncounter);
        ctx.Herds.Remove(h);
        await ctx.HandlePendingEncounter();
        Assert.False(ctx.HasPendingEncounter);
        Assert.Empty(ctx.Herds);
    }

    [Fact]
    public void SpeciesOnlyEncounterCannotSpawnPredator()
    {
        var ctx = World(); Pack(ctx);
        ctx.QueueEncounter(new EncounterConfig(AnimalType.Wolf, 10, 1));
        Assert.False(ctx.HasPendingEncounter);
    }

    [Fact]
    public void ObservationQueriesArePureAndRepeatedObservationDoesNotSpam()
    {
        var ctx = World(); var h = Pack(ctx, 1);
        PredatorInteractions.Update(h, 1, ctx);
        var time = ctx.GameTime;
        for (int i = 0; i < 20; i++)
        {
            Assert.True(PredatorInteractions.ObservedFollowing(ctx));
            PredatorInteractions.Observe(ctx);
        }
        Assert.Equal(time, ctx.GameTime);
        Assert.Single(ctx.PredatorObservations);
        Assert.Equal(1, ctx.EventQueue.Count);
    }

    [Fact]
    public void SaveRoundTripPreservesTargetSourceAndObservationCooldown()
    {
        var ctx = World(); var h = Pack(ctx, 1);
        PredatorInteractions.Update(h, 1, ctx);
        PredatorInteractions.Observe(ctx);
        var json = JsonSerializer.Serialize(ctx, SaveManager.Options);
        var loaded = JsonSerializer.Deserialize<GameContext>(json, SaveManager.Options)!;
        PredatorInteractions.Restore(loaded);
        var herd = Assert.Single(loaded.Herds);
        Assert.Same(loaded.player, herd.Pursuit!.Target);
        Assert.Same(herd, Assert.Single(loaded.PredatorObservations).Source);
        PredatorInteractions.Observe(loaded);
        Assert.True(loaded.EventQueue.IsEmpty);
    }

    [Theory]
    [InlineData("Stalked")]
    [InlineData("Hunted")]
    [InlineData("PackNearby")]
    public void LegacyMetersCannotBeCreatedAndLoadCleanupIsIdempotent(string type)
    {
        var ctx = World();
        ctx.Tensions.AddTension(ActiveTension.Custom(type, 0.8, 0, false));
        ctx.Tensions.EscalateTension(type, 0.5);
        Assert.False(ctx.Tensions.HasTension(type));
        // Simulate an old save: serializer restores the private list without AddTension.
        var other = new TensionRegistry(); other.AddTension(ActiveTension.SmokeSpotted(0.8));
        var oldJson = JsonSerializer.Serialize(other, SaveManager.Options).Replace("SmokeSpotted", type);
        ctx.Tensions = JsonSerializer.Deserialize<TensionRegistry>(oldJson, SaveManager.Options)!;
        Assert.True(ctx.Tensions.HasTension(type));
        ctx.Tensions.AddTension(ActiveTension.Infested(0.3));
        PredatorInteractions.Restore(ctx); PredatorInteractions.Restore(ctx);
        Assert.False(ctx.Tensions.HasTension(type));
        Assert.True(ctx.Tensions.HasTension("Infested"));
        Assert.Empty(ctx.Herds);
    }

    [Fact]
    public void MissingOrDanglingMemoryIsClearedOnRestore()
    {
        var ctx = World(); var h = Pack(ctx);
        h.Pursuit = new PredatorInteraction { Target = new Player(), LastDetectedLocation = ctx.CurrentLocation };
        PredatorInteractions.Restore(ctx);
        Assert.Null(h.Pursuit);
    }

    [Fact]
    public void EveryFactoryHasUsablePositiveWeightChoicesOrIsIneligible()
    {
        var ctx = World(); Pack(ctx);
        foreach (var factory in GameEventRegistry.AllEventFactories)
        {
            var evt = factory(ctx);
            evt.BindPredatorEncounters(ctx);
            foreach (var choice in evt.GetAvailableChoices(ctx))
                Assert.True(choice.Results.Where(r => r.Validate?.Invoke(ctx) ?? true).Sum(r => r.Weight) > 0, evt.Name + ": " + choice.Label);
        }
    }
    [Fact]
    public async Task ScriptedWolfSceneLeavesFoodAndTheSamePackWalksToEatIt()
    {
        var ctx = World(); var h = Pack(ctx, 1);
        ctx.Inventory.Add(Resource.RawMeat, 5);
        var ui = (ScriptedUi)ctx.Ui;
        ui.SelectEventChoice = evt => evt.Choices.Single(c => c.Label == "Leave your meat").Id;
        PredatorInteractions.Update(h, 1, ctx);
        PredatorInteractions.Observe(ctx);
        Assert.True(ctx.EventQueue.TryDequeue(out _));
        await GameEventRegistry.HandleEvent(ctx, PredatorEventFactory.Create(ctx, h));
        Assert.Single(ui.EventsShown);
        Assert.Equal(0, ctx.Inventory.Weight(Resource.RawMeat));
        for (int minute = 0; minute < 40 && h.State != HerdState.Feeding; minute++)
            ctx.UpdateWithoutEvents(1, ActivityType.Resting);
        Assert.Same(ctx.CurrentLocation, h.CurrentLocation);
        Assert.Equal(HerdState.Feeding, h.State);
        Assert.Null(h.Pursuit);
        Assert.True(h.Hunger < 0.8);
        Assert.True(PredatorInteractions.FoodAt(ctx.CurrentLocation) < 5);
        Assert.Same(h, Assert.Single(ctx.Herds));
        Assert.Empty(ctx.Tensions.GetAllTensions());
    }

    [Fact]
    public async Task RetreatCrossesRealNeighborAndChargesTravelTime()
    {
        var ctx = World(); var h = Pack(ctx);
        h.LastCombatMinutes = ctx.TotalMinutesElapsed; // An intimidated survivor won't attack during the crossing.
        ctx.IsHandlingEvent = true;
        var origin = ctx.CurrentLocation;
        var choice = PredatorEventFactory.Create(ctx, h).GetAvailableChoices(ctx).Single(c => c.Label.StartsWith("Retreat toward"));
        await GameEventRegistry.HandleOutcome(ctx, choice.Results.Single());
        Assert.NotSame(origin, ctx.CurrentLocation);
        Assert.Equal(1, ctx.Map!.GetPosition(origin).ManhattanDistance(ctx.Map.CurrentPosition));
        Assert.True(ctx.TotalMinutesElapsed >= 5);
        Assert.Same(ctx.Map.CurrentLocation, ctx.player.CurrentLocation);
    }

    [Fact]
    public async Task PendingAttackInterruptsWorkTimeInsteadOfWaitingForTheWholeAction()
    {
        var ctx = World(); var h = Pack(ctx);
        ctx.QueueEncounter(new EncounterConfig(h.AnimalType, 20, 1, h.Members[0]));
        var (elapsed, interrupted) = await Pacing.PassTime(ctx, 60, ActivityType.Resting, null);
        Assert.True(interrupted);
        Assert.InRange(elapsed, 0, 1);
    }

    [Fact]
    public async Task FoodCanInvalidateAnAttackThatRequiredPursuit()
    {
        var ctx = World(); var h = Pack(ctx);
        PredatorInteractions.Update(h, 1, ctx);
        ctx.QueueEncounter(new EncounterConfig(h.AnimalType, 20, 1, h.Members[0], RequiresPursuit: true));
        var bait = new Inventory(); bait.Add(Resource.RawMeat, 4);
        ctx.CurrentLocation.AddGroundItems(bait);
        PredatorInteractions.Update(h, 1, ctx);
        Assert.Null(h.Pursuit);
        await ctx.HandlePendingEncounter();
        Assert.False(ctx.HasPendingEncounter);
    }

    [Fact]
    public async Task ASecondPackCannotOverwriteTheFirstPendingEncounter()
    {
        var ctx = World(); var first = Pack(ctx); var second = Pack(ctx);
        ctx.QueueEncounter(new EncounterConfig(first.AnimalType, 20, 1, first.Members[0]));
        ctx.QueueEncounter(new EncounterConfig(second.AnimalType, 20, 1, second.Members[0]));
        ctx.Herds.Remove(first);
        await ctx.HandlePendingEncounter();
        Assert.False(ctx.HasPendingEncounter);
    }

    [Fact]
    public void BatchedGameTimeMatchesMinuteByMinutePursuit()
    {
        (int time, double hunger, GridPosition position, int travel) Run(bool batched)
        {
            text_survival.Utils.Seed(811);
            var ctx = World(); var h = Pack(ctx, 2);
            var bait = new Inventory(); bait.Add(Resource.RawMeat, 10);
            ctx.CurrentLocation.AddGroundItems(bait);
            if (batched) ctx.UpdateWithoutEvents(12, ActivityType.Resting);
            else for (int i = 0; i < 12; i++) ctx.UpdateWithoutEvents(1, ActivityType.Resting);
            return (ctx.TotalMinutesElapsed, h.Hunger, h.Position, h.TravelTimeRemainingMinutes);
        }
        Assert.Equal(Run(true), Run(false));
    }

    [Fact]
    public void SleepingOrBlindPlayerDoesNotReceiveVisualPredatorKnowledge()
    {
        var ctx = World(); Pack(ctx);
        ctx.UpdateWithoutEvents(1, ActivityType.Sleeping);
        Assert.Empty(ctx.PredatorObservations);
    }

    [Fact]
    public void NoiseAndTorchChoicesRequireAnActualOpportunity()
    {
        var ctx = World(); var h = Pack(ctx, 1);
        var labels = PredatorEventFactory.Create(ctx, h).GetAvailableChoices(ctx).Select(c => c.Label).ToList();
        Assert.DoesNotContain("Raise your torch", labels);
        Assert.DoesNotContain("Light a torch", labels);
        Assert.DoesNotContain("Shout and stand tall", labels);
        Assert.DoesNotContain("Leave your meat", labels);
    }

    [Fact]
    public async Task StaleFollowingNarrativeIsDiscardedWhenThePackStartsFeeding()
    {
        var ctx = World(); var h = Pack(ctx);
        PredatorInteractions.Update(h, 1, ctx);
        var evt = PredatorEventFactory.Create(ctx, h);
        h.Pursuit = null; h.TransitionTo(HerdState.Feeding);
        await GameEventRegistry.HandleEvent(ctx, evt);
        Assert.Empty(((ScriptedUi)ctx.Ui).EventsShown);
    }

    [Fact]
    public void BaitSatiatesRatherThanLeavingAPermanentKillDefenseState()
    {
        var ctx = World(); var h = Pack(ctx);
        var bait = new Inventory(); bait.Add(Resource.RawMeat, 10);
        ctx.CurrentLocation.AddGroundItems(bait);
        for (int minute = 0; minute < 60 && h.Hunger > 0.1; minute++)
        {
            ctx.GameTime = ctx.GameTime.AddMinutes(1);
            PredatorInteractions.Update(h, 1, ctx);
        }
        Assert.True(h.Hunger <= 0.1);
        PredatorInteractions.Update(h, 1, ctx);
        Assert.Equal(HerdState.Resting, h.State);
        Assert.Null(h.Pursuit);
        Assert.False(ctx.HasPendingEncounter);
    }

    [Theory]
    [InlineData(AnimalType.Wolf)]
    [InlineData(AnimalType.Hyena)]
    [InlineData(AnimalType.Bear)]
    public void ExistingKillDefenseRemainsWithTheSpeciesBehavior(AnimalType species)
    {
        var ctx = World(); var h = Pack(ctx, type: species);
        var prey = AnimalFactory.FromType(AnimalType.Caribou, ctx.CurrentLocation, ctx.Map!)!;
        ctx.CurrentLocation.AddFeature(new CarcassFeature(prey));
        h.State = HerdState.Feeding;
        Assert.Null(PredatorInteractions.Update(h, 1, ctx));
    }

    [Fact]
    public void ContextualScenesRequireTheActualActivityAndFeature()
    {
        var ctx = World(); var h = Pack(ctx);
        ctx.CurrentLocation.AddFeature(new WaterFeature());
        ctx.UpdateWithoutEvents(0, ActivityType.Foraging);
        Assert.Equal("Movement at Camp Edge", PredatorEventFactory.Create(ctx, h).Name);
        ctx.UpdateWithoutEvents(0, ActivityType.Fishing);
        var fishing = PredatorEventFactory.Create(ctx, h);
        Assert.Equal("Movement by the Water", fishing.Name);
        Assert.Contains("fishing", fishing.Description);
        ctx.UpdateWithoutEvents(0, ActivityType.Butchering);
        Assert.False(fishing.Validate!(ctx));
        Assert.NotEqual("carcass", PredatorEventFactory.Scene(ctx));
        var carcass = new CarcassFeature(AnimalFactory.FromType(AnimalType.Caribou, ctx.CurrentLocation, ctx.Map!)!);
        ctx.CurrentLocation.AddFeature(carcass);
        var scene = PredatorEventFactory.Create(ctx, h);
        Assert.Equal("Company at the Carcass", scene.Name);
        carcass.MeatRemainingKg = 0;
        Assert.False(scene.Validate!(ctx));
    }

    [Fact]
    public void TrapSceneRequiresLocalTrapAndRelevantActivity()
    {
        var ctx = World(); Pack(ctx);
        ctx.UpdateWithoutEvents(0, ActivityType.Foraging);
        Assert.NotEqual("traps", PredatorEventFactory.Scene(ctx));
        ctx.CurrentLocation.AddFeature(new SnareLineFeature());
        Assert.Equal("traps", PredatorEventFactory.Scene(ctx));
        ctx.UpdateWithoutEvents(0, ActivityType.Sleeping);
        Assert.NotEqual("traps", PredatorEventFactory.Scene(ctx));
    }

    [Fact]
    public void ObservationSelectsOneContextualSceneAndDoesNotRepeatIt()
    {
        var ctx = World(); var h = Pack(ctx);
        h.Pursuit = new PredatorInteraction { Target = ctx.player, LastDetectedLocation = ctx.CurrentLocation };
        PredatorInteractions.Observe(ctx);
        Assert.True(ctx.EventQueue.TryDequeue(out var scene));
        Assert.Equal("Movement at Camp Edge", scene!.Name);
        PredatorInteractions.Observe(ctx);
        Assert.False(ctx.EventQueue.TryDequeue(out _));
        ctx.IsHandlingEvent = true;
        h.State = HerdState.Feeding;
        PredatorInteractions.Observe(ctx);
        Assert.False(ctx.EventQueue.TryDequeue(out _));
    }

    [Fact]
    public async Task WatchingPausesWorkAndAdvancesExactlyFiveMinutes()
    {
        var ctx = World(); var h = Pack(ctx);
        h.Hunger = 0;
        h.Fear = 0.9;
        ctx.UpdateWithoutEvents(0, ActivityType.Fishing);
        var watch = Choice(ctx, h, "Watch");
        var before = ctx.TotalMinutesElapsed;
        var result = await GameEventRegistry.HandleOutcome(ctx, watch);
        Assert.Equal(before + 5, ctx.TotalMinutesElapsed);
        Assert.Equal(ActivityType.Resting, ctx.CurrentActivity);
        Assert.Contains("You watch", result.Message);
        Assert.NotEqual(watch.Message, result.Message);
    }

    [Fact]
    public void PayoffUsesObservedStateWithoutPromisingEscapeOrBaitAcceptance()
    {
        var ctx = World(); var h = Pack(ctx);
        h.State = HerdState.Feeding;
        Assert.Contains("feeding", PredatorEventFactory.ObserveResult(ctx, h));
        h.State = HerdState.Fleeing;
        Assert.Contains("retreating", PredatorEventFactory.ObserveResult(ctx, h));
        ctx.Herds.Remove(h);
        Assert.Contains("does not tell you", PredatorEventFactory.ObserveResult(ctx, h));
    }

    private static GameEvent Authored(GameContext ctx, string name)
    {
        var evt = GameEventRegistry.AllEventFactories.Select(f => f(ctx)).First(e => e.Name == name);
        evt.BindPredatorEncounters(ctx);
        return evt;
    }

    [Fact]
    public void OriginalPredatorCatalogAndDistinctChoicesAreRestored()
    {
        var ctx = World();
        string[] expected = ["Pack Signs", "Eyes in the Treeline", "Circling", "The Pack Commits",
            "Stalker Circling", "The Predator Revealed", "Ambush", "Shadow Movement", "Cut Off",
            "Exposed", "Seen and Seeing", "Escape Route", "Bear at Fishing Hole", "Wolves at the Nets",
            "Rustle at Camp Edge", "Predator at Trap Line", "Wolves Smell Blood", "Scavenger's Gambit", "The Followers"];
        foreach (var name in expected)
        {
            var evt = Authored(ctx, name);
            Assert.False(evt.IsEligible(ctx));
            Assert.NotEmpty(evt.AuthoredChoices);
        }
        Assert.Contains(Authored(ctx, "Wolves Smell Blood").AuthoredChoices, c => c.Label == "Work Faster, Grab What You Can");
        Assert.Contains(Authored(ctx, "Scavenger's Gambit").AuthoredChoices, c => c.Label == "Steal While They're Distracted");
    }

    [Fact]
    public void FishingSceneNeedsFishingIceHoleAndTheCorrectRealAnimal()
    {
        var ctx = World(); Pack(ctx);
        ctx.CurrentLocation.AddFeature(new WaterFeature { _hasIceHole = true });
        ctx.UpdateWithoutEvents(0, ActivityType.Fishing);
        Assert.False(Authored(ctx, "Bear at Fishing Hole").IsEligible(ctx));
        var bear = Pack(ctx, type: AnimalType.Bear);
        var scene = Authored(ctx, "Bear at Fishing Hole");
        Assert.True(scene.IsEligible(ctx));
        Assert.Same(bear, scene.SourceHerd);
        ctx.UpdateWithoutEvents(0, ActivityType.Foraging);
        Assert.False(scene.IsEligible(ctx));
        ctx.UpdateWithoutEvents(0, ActivityType.Fishing);
        ctx.Herds.Remove(bear);
        Pack(ctx, type: AnimalType.Bear);
        Assert.False(scene.IsEligible(ctx)); // Does not switch to the replacement bear.
    }

    [Fact]
    public void CarcassSceneRequiresActualMammothAndButcheringWithoutFoodScentMeter()
    {
        var ctx = World(); var wolf = Pack(ctx);
        ctx.UpdateWithoutEvents(0, ActivityType.Butchering);
        Assert.False(Authored(ctx, "Wolves Smell Blood").IsEligible(ctx));
        var carcass = new CarcassFeature(AnimalFactory.FromType(AnimalType.Mammoth, ctx.CurrentLocation, ctx.Map!)!);
        ctx.CurrentLocation.AddFeature(carcass);
        var scene = Authored(ctx, "Wolves Smell Blood");
        Assert.True(scene.IsEligible(ctx));
        Assert.Empty(ctx.Tensions.GetAllTensions());
        carcass.MeatRemainingKg = 0;
        Assert.False(scene.IsEligible(ctx));
    }

    [Fact]
    public void AuthoredHarvestConsumesExistingCarcassInsteadOfGeneratingRewardMeat()
    {
        var ctx = World(); Pack(ctx);
        ctx.UpdateWithoutEvents(0, ActivityType.Butchering);
        var carcass = new CarcassFeature(AnimalFactory.FromType(AnimalType.Mammoth, ctx.CurrentLocation, ctx.Map!)!);
        ctx.CurrentLocation.AddFeature(carcass);
        var result = Authored(ctx, "Wolves Smell Blood").GetAvailableChoices(ctx)
            .First(c => c.Label == "Work Faster, Grab What You Can").Results[0];
        var before = carcass.MeatRemainingKg;
        Assert.Equal(text_survival.Items.RewardPool.None, result.RewardPool);
        result.AfterTimeAction!(ctx);
        Assert.True(carcass.MeatRemainingKg < before);
        Assert.True(ctx.Inventory.Weight(Resource.RawMeat) > 0);
        Assert.True(ctx.Inventory.Weight(Resource.RawMeat) + carcass.MeatRemainingKg <= before + 0.0001);
    }

    [Fact]
    public void RestoredIncidentalBranchBindsSourceAndRejectsDisappearance()
    {
        var ctx = World(); var wolf = Pack(ctx);
        var result = new EventResult("A wolf follows the trail.").ObservesPredator(AnimalType.Wolf);
        var evt = new GameEvent("An incidental sighting", "Tracks in the snow", 1).Choice("Look", "Look", [result]);
        evt.BindPredatorEncounters(ctx);
        Assert.Single(evt.GetAvailableChoices(ctx));
        ctx.Herds.Remove(wolf);
        Pack(ctx);
        Assert.Empty(evt.GetAvailableChoices(ctx));
    }

    [Fact]
    public void AuthoredWithdrawalChangesActualAnimalWithoutASeverityMeter()
    {
        var ctx = World(); var wolf = Pack(ctx);
        wolf.Pursuit = new PredatorInteraction { Target = ctx.player, LastDetectedLocation = ctx.CurrentLocation };
        var outcome = new EventResult("The wolf retreats.").PredatorWithdraws();
        var evt = new GameEvent("Reaction", "A wolf", 1).Choice("Shout", "Shout", [outcome]);
        evt.BindPredatorEncounters(ctx);
        outcome.Apply(ctx);
        Assert.Null(wolf.Pursuit);
        Assert.Equal(HerdState.Fleeing, wolf.State);
        Assert.Empty(ctx.Tensions.GetAllTensions());
    }

    [Fact]
    public void FishBaitIsConsumedFromTheWorld()
    {
        var ctx = World();
        var fish = new Inventory(); fish.Add(Resource.RawFish, 2);
        ctx.CurrentLocation.AddGroundItems(fish);
        Assert.Equal(2, PredatorInteractions.FoodAt(ctx.CurrentLocation));
        Assert.Equal(0.5, PredatorInteractions.EatAt(ctx.CurrentLocation, 0.5));
        Assert.Equal(1.5, PredatorInteractions.FoodAt(ctx.CurrentLocation));
    }

    [Fact]
    public void ScavengerGambitNeedsARealFeedingWolfAndVisibleHyenas()
    {
        var ctx = World(); ctx.Camp = ctx.Map!.GetLocationAt(39, 0)!;
        var wolf = Pack(ctx); wolf.State = HerdState.Feeding;
        ctx.CurrentLocation.AddFeature(new CarcassFeature(AnimalFactory.FromType(AnimalType.Caribou, ctx.CurrentLocation, ctx.Map)!));
        Assert.False(Authored(ctx, "Scavenger's Gambit").IsEligible(ctx));
        var hyenas = Pack(ctx, 1, AnimalType.Hyena);
        var scene = Authored(ctx, "Scavenger's Gambit");
        Assert.True(scene.IsEligible(ctx));
        Assert.Same(wolf, scene.SourceHerd);
        Assert.False(scene.AuthoredChoices.SelectMany(c => c.Results)
            .Single(r => r.Message.StartsWith("A second wolf pack")).Validate!(ctx));
        ctx.Herds.Remove(hyenas);
        Assert.False(scene.IsEligible(ctx));
    }

    [Fact]
    public void ObservationUsesAuthoredCampChoicesInsteadOfTheGenericMenu()
    {
        var ctx = World(); var wolf = Pack(ctx);
        ctx.Inventory.Add(Resource.RawMeat, 1);
        GameEventRegistry.ClearTriggerTimes();
        var scene = GameEventRegistry.GetPredatorObservationEvent(ctx, wolf);
        Assert.NotNull(scene);
        Assert.Equal("Rustle at Camp Edge", scene.Name);
        Assert.Contains(scene.GetAvailableChoices(ctx), c => c.Label == "Investigate");
        Assert.DoesNotContain(scene.GetAvailableChoices(ctx), c => c.Label == "Watch");
    }

    [Fact]
    public async Task GrabAndRunHarvestsThenActuallyLeavesTheCarcass()
    {
        var ctx = World(); var wolf = Pack(ctx);
        wolf.Hunger = 0; wolf.LastCombatMinutes = ctx.TotalMinutesElapsed;
        ctx.UpdateWithoutEvents(0, ActivityType.Butchering);
        var origin = ctx.CurrentLocation;
        var carcass = new CarcassFeature(AnimalFactory.FromType(AnimalType.Mammoth, origin, ctx.Map!)!);
        origin.AddFeature(carcass);
        var outcome = Authored(ctx, "Wolves Smell Blood").GetAvailableChoices(ctx)
            .Single(c => c.Label == "Work Faster, Grab What You Can").Results[0];
        outcome.TimeAddedMinutes = 1;
        var before = carcass.MeatRemainingKg;
        ctx.IsHandlingEvent = true;
        await GameEventRegistry.HandleOutcome(ctx, outcome);
        Assert.NotSame(origin, ctx.CurrentLocation);
        Assert.True(carcass.MeatRemainingKg < before);
        Assert.True(ctx.Inventory.Weight(Resource.RawMeat) > 0);
        Assert.True(outcome.AbortsAction);
        Assert.True(ctx.TotalMinutesElapsed > 1);
    }

}
