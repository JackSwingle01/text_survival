using text_survival.Actors.Animals;
using text_survival.Environments;
using text_survival.Environments.Grid;

namespace text_survival.Actions.Expeditions;

/// <summary>
/// Turns a queued encounter into the animal that will fight.
/// </summary>
public static class EncounterRunner
{
    /// <summary>
    /// Creates an animal from an EncounterConfig. Logs a warning and returns null for unknown animal types.
    /// </summary>
    public static Animal? CreateAnimalFromConfig(EncounterConfig config, Location location, GameMap map)
    {
        var animal = AnimalFactory.FromType(config.AnimalType, location, map);

        if (animal == null)
        {
            Console.WriteLine($"[EncounterRunner] WARNING: Unknown animal type '{config.AnimalType}', encounter skipped");
            return null;
        }

        animal.DistanceFromPlayer = config.InitialDistance;
        return animal;
    }
}
