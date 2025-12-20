namespace text_survival.Actors.Animals
{
    /// <summary>
    /// Represents the animal's awareness of and response to the player's presence.
    /// State transitions: Idle → Alert → Detected
    /// </summary>
    public enum AnimalState
    {
        /// <summary>
        /// Animal is unaware of player presence. Normal behavior.
        /// Detection checks are at normal difficulty.
        /// </summary>
        Idle,

        /// <summary>
        /// Animal is suspicious - heard something or noticed movement.
        /// Detection checks become harder. Further failed checks lead to Detected.
        /// </summary>
        Alert,

        /// <summary>
        /// Animal has detected the player.
        /// Response depends on BehaviorType:
        /// - Prey: Flees immediately
        /// - Predator: Attacks if hostile
        /// - Scavenger: Assesses threat then flees or attacks
        /// </summary>
        Detected
    }
}
