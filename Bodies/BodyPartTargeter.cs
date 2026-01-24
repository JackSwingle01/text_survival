using text_survival;
using text_survival.Bodies;

public static class BodyTargetHelper
{

    public static BodyRegion? GetPartByName(Body body, string name)
    {
        return GetAllMajorParts(body).FirstOrDefault(p => p.Name == name);
    }

    public static BodyRegion GetRandomMajorPartByCoverage(Body body)
    {
        var parts = GetAllMajorParts(body);
        var availableParts = parts.Where(p => !p.IsDestroyed).ToList();

        // Defensive: If all parts destroyed, entity should be dead
        // Return first part as fallback to prevent crash
        if (availableParts.Count == 0)
        {
            Console.WriteLine($"[DAMAGE WARNING] No viable body parts remaining - all parts destroyed");
            return parts.First(); // Return first part even if destroyed, to prevent crash
        }

        var partChances = availableParts.ToDictionary(p => p, p => p.Coverage);
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