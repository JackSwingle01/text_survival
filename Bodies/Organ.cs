namespace text_survival.Bodies;

public static class OrganNames
{
    public const string Brain = "Brain";
    public const string LeftEye = "Left Eye";
    public const string RightEye = "Right Eye";
    public const string LeftEar = "Left Ear";
    public const string RightEar = "Right Ear";
    public const string Heart = "Heart";
    public const string LeftLung = "Left Lung";
    public const string RightLung = "Right Lung";
    public const string Liver = "Liver";
    public const string Stomach = "Stomach";
}


public class Organ(string name, double toughness, CapacityContainer capacities, bool isExternal = false) : Tissue (name, toughness)
{
    public bool IsExternal { get; set; } = isExternal;
    public CapacityContainer _baseCapacities { get; set; } = capacities;
    public override CapacityContainer GetBaseCapacities()
    {
        return _baseCapacities;
    }
}
