namespace text_survival.Bodies;

/// <summary>
/// Struct to keep the actor constructors clean
/// </summary>
public struct BodyCreationInfo
{
    public BodyTypes type;
    public double overallWeight; // KG
    public double fatPercent; // 0-1
    public double musclePercent; // 0-1
    public bool IsPlayer;
}


public enum BodyTypes
{
    Human,
    Quadruped,
    Serpentine,
    Arachnid,
    Flying
}
