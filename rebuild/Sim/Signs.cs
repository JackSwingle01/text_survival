namespace Survival;

/// What a sign is evidence of.
/// RavensOver: the carrion flock over a predator herd (no flesh on the tile), its cause the predator.
public enum SignKind { Body, Carcass, Stash, FirePit, Shelter, Prints, Trail, WorkedGround, Grazed, Felled, Picked, Bones, RavensOver }

/// One piece of evidence on a tile (emergent-world-design §3.2): the thing it is, what kind of sign,
/// when it was made and by what. MadeMinute is null where the thing keeps no clock of its making
/// (a stash, a fire pit, a shelter, a body, felled stumps): adding one would be a new saved field.
/// For prints and desire paths it is the last passage, which is what their fading counts from.
public sealed record Sign(ThingRef Thing, SignKind Kind, long? MadeMinute, Cause? Cause);

/// Signs are a projection over state that already exists; none is stored (no thing has its home
/// here yet). Reads only: age and fading come from the clocks the things keep, as Report's do.
// Kill sites (bones taken or weathered away) and stored signs (scat, claw marks) wait for build step 5.
public static class Signs
{
    public static IEnumerable<Sign> At(World world, TileId tile)
    {
        var signs = new List<Sign>();
        var state = world.Tiles.GetValueOrDefault(tile);
        foreach (var t in Things.At(world, tile))
            signs.AddRange(t.Kind switch
            {
                ThingKind.Agent => OfAgent(world, t), // the living are there, not signs: but ravens over a pack point to it
                ThingKind.Body => [OfBody(world.Agent(new AgentId(t.Index)).Body, t)],
                ThingKind.Carcass => [Of(world, t, Carcasses.Find(world, tile, t.Index)!)],
                ThingKind.Pool => OfPool(world, t),
                ThingKind.Stash => [new Sign(t, SignKind.Stash, null, state!.StashCause)],
                ThingKind.Fire => [new Sign(t, SignKind.FirePit, null, world.Fires[new FireId(t.Index)].Cause)],
                ThingKind.Shelter => [new Sign(t, SignKind.Shelter, null, Shelters.Peek(world, tile)!.Cause)],
                ThingKind.Track => [OfTrack(world, t)],
                ThingKind.Sign => [], // none stored yet (Things.At names none)
                ThingKind.Item => [], // carried gear is not a sign
            });
        return signs;
    }

    static Sign Of(World world, ThingRef t, Carcass c) =>
        new(t, Remains.StageOf(world, t.Tile, c) == RemainsStage.Flesh ? SignKind.Carcass : SignKind.Bones, c.DiedMinute, c.Cause);

    static Sign OfBody(BodyState body, ThingRef t) => new(t, SignKind.Body, body.DiedMinute, body.DeathCause);

    /// The carrion flock over a predator herd, where there is no flesh to hold it: the sign is the
    /// flock, its cause the predator it follows (the lowest id, if more than one).
    static IEnumerable<Sign> OfAgent(World world, ThingRef t)
    {
        var a = world.Agent(new AgentId(t.Index));
        if (Fauna.BehaviorOf(a.Species) != HerdBehavior.Carrion || Fauna.MeatHere(world, t.Tile) > 0) yield break;
        var followed = Fauna.HerdsAt(world, t.Tile).FirstOrDefault(h => Fauna.IsPredator(h.Species) && Sim.IsAlive(h));
        if (followed != null) yield return new Sign(t, SignKind.RavensOver, null, Cause.By(world, followed.Id));
    }

    static Sign OfTrack(World world, ThingRef t)
    {
        if (t.TrailDirection is Direction dir)
        {
            var wear = world.Trails[new EdgeId(t.Tile, Map.Step(t.Tile, dir))];
            return new Sign(t, SignKind.Trail, wear.Minute, null);
        }
        var prints = Tracks.Read(world, t.Tile).First(s => (int)s.Maker == t.Index);
        return new Sign(t, SignKind.Prints, world.Minute - prints.AgeMinutes, null);
    }

    /// Ground that shows it was worked: forage thinned by searching or grazing, felled stumps, a
    /// stock picked below what it holds. Nobody is recorded for these, so no cause.
    static IEnumerable<Sign> OfPool(World world, ThingRef t)
    {
        var id = new PoolId(t.Tile, t.Index);
        var def = Pools.Def(world, id);
        var state = Pools.StateOf(world, id);
        switch (def, state)
        {
            case (ForagePool f, ForageState s):
                if (s.HoursForaged > 0 && Pools.ForageDensity(world, id) < f.Density)
                    yield return new Sign(t, SignKind.WorkedGround, s.LastForagedMinute, null);
                var grazed = s.Grazed.Where(g => Pools.GrazedLevel(world, id, g.Key) > 0).ToList();
                if (grazed.Count > 0) yield return new Sign(t, SignKind.Grazed, grazed.Max(g => g.Value.Minute), null);
                break;
            case (TreePool, TreeState { Felled: > 0 }):
                yield return new Sign(t, SignKind.Felled, null, null);
                break;
            case (StockPool p, StockState s):
                var units = Pools.UnitsNow(world, id);
                if (units.Where((u, i) => u < p.Items[i].Units).Any())
                    yield return new Sign(t, SignKind.Picked, s.LastTakenMinute.Max(), null);
                break;
        }
    }
}
