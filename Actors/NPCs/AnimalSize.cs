namespace text_survival.Actors.NPCs
{
    /// <summary>
    /// Size category for animals, affecting weapon effectiveness.
    /// Small game is appropriate for stones, large game for spears.
    /// </summary>
    public enum AnimalSize
    {
        /// <summary>
        /// Small animals like rabbits and birds.
        /// Can be killed with thrown stones. Harder to hit with spears.
        /// </summary>
        Small,

        /// <summary>
        /// Large animals like deer, wolves, and bears.
        /// Require spears to kill. Stones are ineffective.
        /// </summary>
        Large
    }
}
