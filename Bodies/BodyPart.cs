namespace text_survival.Bodies;


public static class BodyRegionNames
{
    public const string Head = "Head";
    public const string Chest = "Chest";
    public const string Abdomen = "Abdomen";
    public const string LeftArm = "Left Arm";
    public const string RightArm = "Right Arm";
    public const string LeftLeg = "Left Leg";
    public const string RightLeg = "Right Leg";
}


public class BodyRegion(string name, double coverage)
{
    // Core properties
    public string Name { get; set; } = name;
    public double Coverage { get; set; } = coverage;

    // part makeup
    public Tissue Skin { get; set; } = new Tissue("Skin");
    public Tissue Muscle { get; set; } = new Muscle();
    public Tissue Bone { get; set; } = new Bone();
    public List<Organ> Organs { get; set; } = [];

    // Parameterless constructor for JSON deserialization
    [System.Text.Json.Serialization.JsonConstructor]
    public BodyRegion() : this(string.Empty, 0)
    {
    }

    public bool IsDestroyed => Condition <= 0;
    public double Condition => AggregateCondition();

    private double AggregateCondition()
    {
        double overallCondition = 1;
        foreach (var tissue in new List<Tissue> { Skin, Muscle, Bone })
        {
            // weakest link
            overallCondition = Math.Min(overallCondition, tissue.Condition);
        }
        // Organs are excluded intentionally - they're displayed separately in the UI.
        // Condition represents tissue damage (wounds), while organ health affects Vitality.
        return overallCondition;
    }

    // public void Describe()
    // {
    //     // Calculate health percentage
    //     int healthPercent = (int)(Condition * 100); 

    //     // Determine damage severity description
    //     string damageDescription;
    //     if (healthPercent <= 0)
    //     {
    //         damageDescription = "destroyed";
    //     }
    //     else if (healthPercent < 20)
    //     {
    //         damageDescription = "critically damaged";
    //     }
    //     else if (healthPercent < 40)
    //     {
    //         damageDescription = "severely damaged";
    //     }
    //     else if (healthPercent < 60)
    //     {
    //         damageDescription = "moderately damaged";
    //     }
    //     else if (healthPercent < 80)
    //     {
    //         damageDescription = "lightly damaged";
    //     }
    //     else if (healthPercent < 100)
    //     {
    //         damageDescription = "slightly damaged";
    //     }
    //     else
    //     {
    //         damageDescription = "in perfect condition";
    //     }

    //     // Output description line
    //     GameDisplay.AddNarrative($"- {Name} is {damageDescription} ({healthPercent}%)");
    // }
    public override string ToString() => Name;
}