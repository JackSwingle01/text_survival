namespace text_survival.Actors;

public class RelationshipMemory
{
    public readonly List<MemoryEvent> MemoryEvents = [];
    public void AddMemory(MemoryType memory, Actor subject)
    {
        var existing = MemoryEvents.FirstOrDefault(x => x.Subject == subject && x.Type == memory);
        if (existing != null)
        {
            existing.Increment();
            return;
        }
        MemoryEvents.Add(new MemoryEvent(memory, subject));
    }
    public double GetOpinion(Actor actor)
    {
        double sum = 0;
        for (int i = 0; i < MemoryEvents.Count; i++)
        {
            if (MemoryEvents[i].Subject == actor)
                sum += GetMemoryImpact(MemoryEvents[i].Type) * MemoryEvents[i].Count;
        }
        return sum;
    }
    
    private static double GetMemoryImpact(MemoryType memoryType)
    {
        return memoryType switch
        {
            MemoryType.SavedMe => .3,
            MemoryType.AbandonedMe => -.5,
            MemoryType.SharedFood => .05,
            MemoryType.FoughtTogether => .1,
            MemoryType.TimeTogether => .001,
            _ => throw new NotImplementedException(),
        };
    }
}

public class MemoryEvent
{
    public MemoryType Type;
    public Actor Subject;
    public int Count;
    public MemoryEvent(MemoryType memoryType, Actor subject)
    {
        Type = memoryType;
        Subject = subject;
        Count = 1;
    }
    public MemoryEvent() { }
    public void Increment()
    {
        Count++;
    }
}
public enum MemoryType
{
    SavedMe,
    AbandonedMe,
    SharedFood,
    // StoleFrom,
    FoughtTogether,
    // BuiltTogether,
    // WitnessedTheft,
    // WitnessedGenerosity,
    TimeTogether,
}