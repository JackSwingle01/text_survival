namespace text_survival.Actors.NPCs
{
    /// <summary>
    /// Defines how an animal responds to player detection and interaction.
    /// Used to create distinct hunting gameplay for different animal types.
    /// </summary>
    public enum AnimalBehaviorType
    {
        /// <summary>
        /// Prey animals flee when detecting the player.
        /// Examples: Deer, Rabbit, Ptarmigan
        /// </summary>
        Prey,

        /// <summary>
        /// Predators attack when detecting the player (if hostile).
        /// Examples: Wolf, Bear, Cave Bear
        /// </summary>
        Predator,

        /// <summary>
        /// Scavengers assess the player and flee if outmatched.
        /// Examples: Fox
        /// </summary>
        Scavenger,

        /// <summary>
        /// Large prey that flee normally but fight back if cornered or wounded.
        /// Examples: Bison, Elk, Moose, Auroch (V2 feature - not in MVP)
        /// </summary>
        DangerousPrey
    }
}
