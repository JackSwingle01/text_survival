namespace text_survival.Bodies;

/// <summary>
/// Struct to keep the actor constructors clean
/// </summary>
public struct BodyCreationInfo
{
    public BodyPartFactory.BodyTypes type;
    public double overallWeight;
    public double fatPercent;
    public double musclePercent;
    public bool IsPlayer;
}
