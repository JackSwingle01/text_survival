using text_survival.Actors;
using Xunit;

namespace text_survival.Tests.Companions;

public class StrangerDispositionTests
{
    private static NPC Make(int groupId, double disposition) =>
        new("X", new Personality(), null!, null!) { GroupId = groupId, StrangerDisposition = disposition };

    [Fact]
    public void SameGroupStartsFriendly()
    {
        var a = Make(1, -0.8);
        Assert.Equal(0.5, a.GetRelationship(Make(1, -0.2)), 3);
    }

    [Fact]
    public void OtherGroupAndPlayerBothStartAtStrangerDisposition()
    {
        var a = Make(1, -0.4);
        Assert.Equal(-0.4, a.GetRelationship(Make(2, 0)), 3);
        Assert.Equal(-0.4, a.GetRelationship(new text_survival.Actors.Player.Player()), 3);
    }

    [Fact]
    public void MemoryStacksOnTopOfBaseline()
    {
        var a = Make(1, -0.4);
        var stranger = Make(2, 0);
        a.Relationships.AddMemory(MemoryType.SavedMe, stranger);
        Assert.Equal(-0.1, a.GetRelationship(stranger), 3);
    }

    [Fact]
    public void UngroupedNpcsAreNotFamily()
    {
        var a = Make(0, -0.3);
        Assert.Equal(-0.3, a.GetRelationship(Make(0, -0.3)), 3);
    }
}
