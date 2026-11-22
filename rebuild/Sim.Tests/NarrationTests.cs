namespace Survival.Tests;

/// Narration: the player's words for what happens, shared by the CLI and the web host. Second person,
/// present tense, short; only what the player could know of.
public class NarrationTests
{
    static readonly AgentId P = AgentId.Player;

    static World Camp(out TileId camp)
    {
        var w = World.New(17);
        camp = Map.Of(w).Camp;
        return w;
    }

    [Fact]
    public void ThePlayersOwnDoings_ReadInTheSecondPersonPresent()
    {
        var w = Camp(out var camp);
        Assert.Equal("You light a fire.", Narration.ForPlayer(w, new FireLit(0, new FireId(0), camp, P))!.Text);
        Assert.Equal("You add 1 kg birch to the fire.", Narration.ForPlayer(w, new FuelAdded(0, new FireId(0), camp, P, Resource.Birch, 1))!.Text);
        Assert.Equal("You eat 0.5 kg berries.", Narration.ForPlayer(w, new Ate(0, P, camp, Resource.Berries, 0.5, 250))!.Text);
        Assert.Equal("You are shivering.", Narration.ForPlayer(w, new EffectStarted(0, P, camp, EffectKind.Shivering))!.Text);
        Assert.Equal("You are no longer wet.", Narration.ForPlayer(w, new EffectEnded(0, P, camp, EffectKind.Wet))!.Text);
        Assert.Equal("The fire goes out.", Narration.ForPlayer(w, new FireDied(0, new FireId(0), camp))!.Text);
        Assert.Equal(Tone.Bad, Narration.ForPlayer(w, new FireDied(0, new FireId(0), camp))!.Tone);
    }

    /// Another's private business, or a fire out of reach, is not the player's to hear.
    [Fact]
    public void WhatThePlayerCannotKnow_IsNotNarrated()
    {
        var w = Camp(out var camp);
        var npc = Npc.Spawn(w, camp);
        var far = new TileId(camp.X + 5, camp.Y);
        Assert.Null(Narration.ForPlayer(w, new Ate(0, npc, camp, Resource.Berries, 0.5, 250)));
        Assert.Null(Narration.ForPlayer(w, new FireDied(0, new FireId(0), far)));
        Assert.Null(Narration.ForPlayer(w, new PlanRefused(0, npc, camp, "Eat", "no food")));
        // Told at camp (a receipt), another's doing reads in the third person.
        Assert.Equal("A person lights a fire.", Narration.Of(w, new FireLit(0, new FireId(0), camp, npc))!.Text);
    }

    /// Every event a real day at camp and on the trail produces for the player is narrated, and every
    /// line is a sentence: a capital, a full stop, no code keys.
    [Fact]
    public void EveryLineOfARealDay_IsASentence()
    {
        var w = Camp(out var camp);
        TestKit.Supply(w);
        Items.Add(Items.Pack(w, P), Resource.Berries, 1);
        var events = new List<WorldEvent>();
        var light = Verbs.At(w, P).Tile.Single(v => v.Activity is StartFire);
        for (int i = 0; i < 20 && !(Fire.PitAt(w, camp) is FireId f && Fire.IsActive(w.Fires[f])); i++) events.AddRange(Sim.Step(w, light.Activity, 1).Events);
        events.AddRange(Sim.Step(w, new AddFuel(P, Resource.Birch, 1), 1).Events);
        events.AddRange(Sim.Step(w, new Eat(P, Resource.Berries, 0.5), 10).Events);
        // A search's luck is one roll: a lean one at camp can come up empty, so search until something turns up.
        for (int i = 0; i < 6 && !events.OfType<Gathered>().Any(g => g.What.Length > 0); i++) events.AddRange(Sim.Step(w, new Gather(P, 0), 60).Events);
        var walk = Verbs.At(w, P).Walk.First(v => v.Allowed);
        events.AddRange(Sim.Step(w, walk.Activity, walk.Minutes).Events);
        var lines = events.Select(e => Narration.ForPlayer(w, e)).OfType<Narrated>().ToList();
        Assert.Contains(lines, l => l.Text.StartsWith("You reach ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Text.StartsWith("You gather ", StringComparison.Ordinal));
        Assert.All(lines, l => Assert.Matches(@"^[A-Z].*[.!]$", l.Text));
        Assert.All(lines, l => Assert.DoesNotMatch("[a-z][A-Z]", l.Text));
    }

    [Fact]
    public void AReceiptsChanges_AndSigns_ReadInWords()
    {
        Assert.Equal("The fire was roaring. Now it is embers.", Narration.Change(new Change(ChangeKind.Fire, "fire", "Roaring", "Embers")));
        Assert.Equal("The birch bark in the stash is gone.", Narration.Change(new Change(ChangeKind.Stash, "BirchBark", "1.0", null)));
        var sign = new TrackSign(TrackMaker.Paw, 0.9, 3, TrafficPattern.Heading, Direction.North, 60, 0.02, 3, 0);
        Assert.Equal("Paw prints, heading north, fresh.", Narration.Sign(sign));
    }
}
