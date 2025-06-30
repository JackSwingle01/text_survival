using text_survival;
using text_survival.Bodies;

public static class BodyTargetHelper
{

    public static BodyRegion? GetPartByName(Body body, string name)
    {
        return GetAllMajorParts(body).FirstOrDefault(p => p.Name == name);
    }

    public static Tissue? GetTissueByName(BodyRegion part, string name)
    {
        return GetTissues(part).FirstOrDefault(p => p.Name == name);
    }

    public static BodyRegion GetRandomMajorPartByCoverage(Body body)
    {
        var parts = GetAllMajorParts(body);
        var partChances = parts.Where(p => !p.IsDestroyed).ToDictionary(p => p, p => p.Coverage);
        return Utils.GetRandomWeighted(partChances);
    }

    public static List<BodyRegion> GetAllMajorParts(Body body)
    {
        return body.Parts;
    }

    public static List<Organ> GetAllOrgans(Body body)
    {
        return GetAllMajorParts(body).SelectMany(p => p.Organs).ToList();
    }

    public static List<Tissue> GetAllTissues(Body body)
    {
        List<Tissue> tissues = [];
        GetAllMajorParts(body).ForEach(p => tissues.AddRange(GetTissues(p)));
        GetAllOrgans(body).ForEach(tissues.Add);
        return tissues;
    }

    public static List<Tissue> GetTissues(BodyRegion part)
    {
        return [part.Skin, part.Bone, part.Muscle];
    }

    public static List<Organ> GetOrgans(BodyRegion part)
    {
        return part.Organs;
    }

    public static Organ? SelectRandomOrganToHit(BodyRegion part, double damageAmount)
    {
        // External organs can be hit even with light damage
        var externalOrgans = part.Organs.Where(o => o.IsExternal).ToList();
        if (externalOrgans.Count > 0 && damageAmount > 0)
        {
            return externalOrgans[Random.Shared.Next(externalOrgans.Count)];
        }

        // Internal organs need significant penetrating damage
        var internalOrgans = part.Organs.Where(o => !o.IsExternal).ToList();
        if (internalOrgans.Count > 0 && damageAmount > 5) // Threshold for internal damage
        {
            return internalOrgans[Random.Shared.Next(internalOrgans.Count)];
        }

        return null; // No organ hit
    }
}