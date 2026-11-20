using text_survival.Actions.Variants;
using text_survival.Actors.Animals;
using text_survival.Environments.Features;

namespace text_survival.Actions;

public static partial class GameEventRegistry
{
    // === PACK ARC EVENTS ===

    /// <summary>
    /// Stage 1 of Pack arc - discovering pack signs.
    /// Multiple coordinated tracks indicating a pack rather than lone predator.
    /// </summary>
    private static GameEvent PackSigns(GameContext ctx)
    {
        var predator = AuthoredPredatorScenes.Source(ctx, PredatorSceneKind.PackSigns)?.AnimalType ?? AnimalType.Wolf;
        var variant = AnimalSelector.GetVariant(predator);

        // Pack animals coordinate better - higher risk of being detected
        bool isPackAnimal = AnimalSelector.IsPredatorPackAnimal(variant);

        return new GameEvent("Pack Signs",
            $"Multiple tracks, recent. {(isPackAnimal ? "Coordinated movement patterns." : "Scattered, but recent.")} This isn't a lone hunter — it's a pack of {predator.DisplayName()}s.", 0.8)
            .ForPredatorScene(PredatorSceneKind.PackSigns)
            .Requires(EventCondition.InAnimalTerritory, EventCondition.HasPredators)
            .Requires(EventCondition.OnExpedition)
            .RequiresSituation(AnimalPresence.PackPredatorsNear)  // Requires pack predator herd present
            .WithSituationFactor(Situations.AttractiveToPredators, 2.0)  // Meat, bleeding, food scent
            .Choice("Move Carefully, Watch Flanks",
                "Slow down. Stay alert. Don't let them get behind you.",
                [
                    new EventResult("You proceed cautiously. No sign of them... yet.", weight: 0.70, minutes: 15),
                    new EventResult("You spot movement in your peripheral. They're paralleling you.", weight: 0.30, minutes: 10)
                        .ObservesPredator(predator)
                ])
            .Choice("Pick Up Pace Toward Camp",
                "Get back toward camp and fire. Move!",
                [new EventResult("You move fast, toward camp.")],
                action: SceneAction.RetreatTowardCamp)
            .Choice("Hold Position and Assess",
                "Stop. Watch. Know what you're dealing with.",
                [
                    new EventResult("You study the overlapping tracks. More than one animal has passed this way.", weight: 0.50, minutes: 12)
                        .ObservesPredator(predator),
                    new EventResult("The ground is churned with prints. You take your time studying them.", weight: 0.30, minutes: 12)
                        .ObservesPredator(predator),
                    new EventResult("Old tracks. They passed hours ago. You're probably fine.", weight: 0.20, minutes: 10)
                ],
                action: SceneAction.Wait);
    }

    /// <summary>
    /// Stage 2 of Pack arc - eyes in the treeline.
    /// They're paralleling the player, testing. Not attacking yet.
    /// </summary>
    private static GameEvent EyesInTreeline(GameContext ctx)
    {
        var packTension = AuthoredPredatorScenes.Source(ctx, PredatorSceneKind.EyesInTreeline);
        var predator = packTension?.AnimalType ?? AnimalType.Wolf;
        var variant = AnimalSelector.GetVariant(predator);

        // Pack coordination affects how they respond
        bool isPackAnimal = AnimalSelector.IsPredatorPackAnimal(variant);
        double noiseRetreatWeight = 0.25 + variant.NoiseEffectiveness * 0.25;  // 0.25-0.50
        double noiseProvokeWeight = 0.15 + (1 - variant.NoiseEffectiveness) * 0.15;  // 0.15-0.30

        return new GameEvent("Eyes in the Treeline",
            $"Glimpses of movement. The {predator.DisplayName()}s are paralleling you. Not attacking yet — {(isPackAnimal ? "coordinating, probing." : "watching, testing.")}", 1.5)
            .ForPredatorScene(PredatorSceneKind.EyesInTreeline)
            .Requires(EventCondition.PackFollowing, EventCondition.IsExpedition)
            .WithSituationFactor(Situations.AttractiveToPredators, 2.5)
            .WithSituationFactor(Situations.Vulnerable, 2.5)
            .WithSituationFactor(Situations.PackThreat, 1.5)
            .Choice("Keep Moving Steadily",
                "Don't run. Don't stop. Steady pace. Show no weakness.",
                [new EventResult("You keep walking. Steady. No running.")],
                action: SceneAction.Retreat)
            .Choice("Make Yourself Large, Shout",
                $"Posturing. Show them you're not prey. {(variant.NoiseEffectiveness > 0.6 ? "Should work." : "Risky.")}",
                [
                    new EventResult("They hesitate. Some back off. Posturing works.", weight: noiseRetreatWeight, minutes: 8)
                        .PredatorWithdraws(),
                    new EventResult("No reaction. They've seen this before.", weight: 0.60 - noiseRetreatWeight - noiseProvokeWeight, minutes: 10),
                    new EventResult("Your aggression provokes them. They're committed now.", weight: noiseProvokeWeight, minutes: 5)
                        .PredatorFollows()
                        .Unsettling()
                ])
            .Choice("Light a Torch",
                "Fire. Most animals keep their distance from it.",
                [new EventResult("You reach for your torch.")],
                action: SceneAction.LightTorch);
    }

    /// <summary>
    /// Stage 3 of Pack arc - circling.
    /// They're closing in. Need defensible ground or fire NOW.
    /// </summary>
    private static GameEvent Circling(GameContext ctx)
    {
        var packTension = AuthoredPredatorScenes.Source(ctx, PredatorSceneKind.Circling);
        var predator = packTension?.AnimalType ?? AnimalType.Wolf;
        var variant = AnimalSelector.GetVariant(predator);


        return new GameEvent("Circling",
            $"{variant.CirclingDescription} You need defensible ground — NOW.", 2.0)
            .ForPredatorScene(PredatorSceneKind.Circling)
            .Requires(EventCondition.PredatorWithinReach, EventCondition.IsExpedition)
            .WithSituationFactor(Situations.PackThreat, 2.0)
            .Choice("Back Against Tree or Cliff",
                "Limit attack angles. Nothing behind you.",
                [
                    new EventResult("Solid tree at your back. They can only come from the front.", weight: 0.60, minutes: 8)
                        .PredatorWithdraws(),
                    new EventResult("Cliff face. Safe from behind, but nowhere to run.", weight: 0.30, minutes: 10),
                    new EventResult("You're cornered. But so are they, in a way.", weight: 0.10, minutes: 8)
                        .PredatorFollows()
                ])
            .Choice("Start Fire Here",
                "Right here. Right now. Fire is worth a try.",
                [new EventResult("You drop to your knees and work your fire kit.")],
                action: SceneAction.StartFire)
            .Choice("Break and Run",
                "All-out sprint. Succeed or fail.",
                [new EventResult("You run.")],
                action: SceneAction.Retreat);
    }

    /// <summary>
    /// Stage 4 of Pack arc - the pack commits.
    /// They've decided to attack. Multiple attackers.
    /// </summary>
    private static GameEvent ThePackCommits(GameContext ctx)
    {
        var packTension = AuthoredPredatorScenes.Source(ctx, PredatorSceneKind.ThePackCommits);
        var predator = packTension?.AnimalType ?? AnimalType.Wolf;
        var variant = AnimalSelector.GetVariant(predator);

        bool isPackAnimal = AnimalSelector.IsPredatorPackAnimal(variant);

        string commitDesc = isPackAnimal
            ? $"They've decided. This is happening. The {predator.DisplayName()}s move as one."
            : $"It's decided. This is happening. The {predator.DisplayName()} attacks.";

        return new GameEvent("The Pack Commits", commitDesc, 3.0)
            .ForPredatorScene(PredatorSceneKind.ThePackCommits)
            .Requires(EventCondition.PredatorWithinReach, EventCondition.IsExpedition)
            .WithSituationFactor(Situations.PackThreat, 2.5)
            .Choice("Stand and Fight",
                "Face them. Take as many as you can.",
                [
                    new EventResult($"The first {predator.DisplayName()} lunges. The fight is on.", weight: 1.0, minutes: 5)
                        .ConfrontPredator(predator, 5, 0.85)
                ])
            .Choice("Feed the Fire",
                "Everything on the flames. Make it roar.",
                [new EventResult("You pile fuel on the flames.").FeedsFire(4)],
                requires: [EventCondition.NearFire, EventCondition.HasFuel],
                action: SceneAction.BuildUpFire)
            .Choice("Drop All Meat and Flee",
                "Give them what they want. Food. Not you.",
                [new EventResult("You throw down your meat and go.")],
                requires: [EventCondition.HasMeat],
                action: SceneAction.DropMeatAndRun);
    }
}
