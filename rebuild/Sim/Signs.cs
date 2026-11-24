namespace Survival;

/// RavensOver: ravens over a predator herd (no flesh on the tile), its cause the predator.
public enum SignKind { Body, Carcass, Pile, FirePit, Shelter, Prints, Trail, WorkedGround, Grazed, Felled, Picked, Bones, RavensOver }

/// One piece of evidence on a tile (emergent-world-design §3.2). MadeMinute is null where the location feature keeps
/// no clock of its making (pile, fire pit, shelter, body, stumps): adding one would be a new saved field.
/// For prints and desire paths it is the last passage, which is what their fading counts from.
public sealed record Sign(LocationFeature LocationFeature, SignKind Kind, long? MadeMinute, Cause? Cause);

/// Signs are a projection over state that already exists; none is stored. Age and fading come from the location features' own clocks.
public static class Signs
{
    public static IEnumerable<Sign> At(World world, TileId tile)
    {
        var signs = new List<Sign>();
        var state = world.Tiles.GetValueOrDefault(tile);
        foreach (var t in LocationFeatures.At(world, tile))
            signs.AddRange(t.Kind switch
            {
                LocationFeatureKind.Agent => OfAgent(world, t), // the living are not signs, but ravens over a pack point to it
                LocationFeatureKind.Body => [OfBody(world.Agent(new AgentId(t.Index)).Body, t)],
                LocationFeatureKind.Carcass => [Of(world, t, Carcasses.Find(world, tile, t.Index)!)],
                LocationFeatureKind.Pool => OfPool(world, t),
                LocationFeatureKind.Pile => [new Sign(t, SignKind.Pile, null, state!.PileCause)],
                LocationFeatureKind.Fire => [new Sign(t, SignKind.FirePit, null, world.Fires[new FireId(t.Index)].Cause)],
                LocationFeatureKind.Shelter => [new Sign(t, SignKind.Shelter, null, Shelters.Peek(world, tile)!.Cause)],
                LocationFeatureKind.Track => [OfTrack(world, t)],
                LocationFeatureKind.Sign => [],
                LocationFeatureKind.Item => [],
            });
        return signs;
    }

    static Sign Of(World world, LocationFeature t, Carcass c) =>
        new(t, Remains.StageOf(world, t.Tile, c) == RemainsStage.Flesh ? SignKind.Carcass : SignKind.Bones, c.DiedMinute, c.Cause);

    static Sign OfBody(BodyState body, LocationFeature t) => new(t, SignKind.Body, body.DiedMinute, body.DeathCause);

    /// Ravens over a predator herd with no flesh to hold it; its cause is the predator (the lowest id, if more than one).
    static IEnumerable<Sign> OfAgent(World world, LocationFeature t)
    {
        var a = world.Agent(new AgentId(t.Index));
        if (!Fauna.IsRaven(a.Species) || Fauna.MeatHere(world, t.Tile) > 0) yield break;
        var followed = Fauna.AnimalsAt(world, t.Tile).FirstOrDefault(h => Fauna.IsPredator(h.Species) && Sim.IsAlive(h));
        if (followed != null) yield return new Sign(t, SignKind.RavensOver, null, Cause.By(world, followed.Id));
    }

    static Sign OfTrack(World world, LocationFeature t)
    {
        if (t.TrailDirection is Direction dir)
        {
            var trodden = world.Trails[new EdgeId(t.Tile, Map.Step(t.Tile, dir))];
            return new Sign(t, SignKind.Trail, trodden.Minute, null);
        }
        var prints = Tracks.Read(world, t.Tile).First(s => (int)s.Maker == t.Index);
        return new Sign(t, SignKind.Prints, world.Minute - prints.AgeMinutes, null);
    }

    /// Nobody is recorded for worked ground, so these signs carry no cause.
    static IEnumerable<Sign> OfPool(World world, LocationFeature t)
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
