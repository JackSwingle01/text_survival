using text_survival.Actions.Variants;
using text_survival.Bodies;
using text_survival.Effects;
using text_survival.Items;

namespace text_survival.Actions;

/// <summary>
/// Equipment wear events - create decision moments around gear degradation.
/// These events trigger when equipment/tools fall below condition thresholds,
/// rewarding players who maintain their gear.
/// </summary>
public static partial class GameEventRegistry
{
    private static GameEvent BootFailure(GameContext ctx)
    {
        var variant = EquipmentWearSelector.SelectBootWear(ctx);

        return new GameEvent("Boot Trouble", variant.Description, 0.9)
            .RequiresSituation(Situations.BootsWorn)
            .Requires(EventCondition.Traveling)
            .WithSituationFactor(Situations.RemoteAndVulnerable, 2.0)
            .WithConditionFactor(EventCondition.HazardousTerrain, 1.5)
            .WithConditionFactor(EventCondition.ExtremelyCold, 1.5)
            .WithCooldown(8) // Can happen again in same expedition if not fixed
            .Choice("Field Repair",
                variant.RepairHint,
                [
                    new EventResult("Crude but effective. Should hold for now.", 0.55, 15)
                        .Costs(ResourceType.PlantFiber, 1)
                        .FieldRepair(EquipSlot.Feet),
                    new EventResult("The repair doesn't hold. You'll need proper materials.", 0.30, 15)
                        .Costs(ResourceType.PlantFiber, 1)
                        .MinorEquipmentWear(EquipSlot.Feet),
                    new EventResult("Better than expected. Solid work.", 0.15, 20)
                        .Costs(ResourceType.PlantFiber, 1)
                        .ProperRepair(EquipSlot.Feet)
                ],
                requires: [EventCondition.HasPlantFiber])
            .Choice("Limp On",
                "It'll hold for a while longer. Maybe.",
                [
                    new EventResult("You favor the damaged boot. Slower going, but manageable.", 0.50, 0)
                        .LightChill()
                        .MinorEquipmentWear(EquipSlot.Feet),
                    new EventResult("The damage spreads. Cold bites at your toes.", 0.35, 0)
                        .ModerateCold()
                        .ModerateEquipmentWear(EquipSlot.Feet),
                    new EventResult("The boot fails completely. Each step is agony.", 0.15, 0)
                        .HarshCold()
                        .SevereEquipmentWear(EquipSlot.Feet)
                        .WithEffects(EffectFactory.SprainedAnkle(0.2))
                ])
            .Choice("Turn Back",
                "Not worth risking further damage.",
                [
                    new EventResult("Better to fix this properly at camp.", 1.0, 0)
                        .Aborts()
                ]);
    }

    private static GameEvent GlovesFraying(GameContext ctx)
    {
        var variant = EquipmentWearSelector.SelectGloveWear(ctx);

        return new GameEvent("Gloves Fraying", variant.Description, 0.8)
            .RequiresSituation(Situations.GlovesWorn)
            .Requires(EventCondition.Working)
            .WithConditionFactor(EventCondition.ExtremelyCold, 1.8)
            .WithConditionFactor(EventCondition.LowTemperature, 1.3)
            .WithCooldown(6)
            .Choice("Wrap Them",
                variant.RepairHint,
                [
                    new EventResult("A quick wrap keeps them functional.", 0.60, 10)
                        .Costs(ResourceType.PlantFiber, 1)
                        .FieldRepair(EquipSlot.Hands),
                    new EventResult("Holds for now. Won't last.", 0.30, 10)
                        .Costs(ResourceType.PlantFiber, 1),
                    new EventResult("Good improvisation. Almost as good as new.", 0.10, 12)
                        .Costs(ResourceType.PlantFiber, 1)
                        .ProperRepair(EquipSlot.Hands)
                ],
                requires: [EventCondition.HasPlantFiber])
            .Choice("Work Bare-Handed",
                "Take them off and work carefully.",
                [
                    new EventResult("Cold hands, but you finish the work.", 0.50, 0)
                        .LightChill()
                        .WithEffects(EffectFactory.Clumsy(0.2, 30)),
                    new EventResult("Your hands go numb. Fine work becomes impossible.", 0.35, 0)
                        .ModerateCold()
                        .WithEffects(EffectFactory.Clumsy(0.4, 60)),
                    new EventResult("Frostnip. Your fingertips are white and painful.", 0.15, 0)
                        .WithFrostbite(4, 0.3)
                ],
                requires: [EventCondition.LowTemperature])
            .Choice("Take a Break",
                "Warm your hands, continue later.",
                [
                    new EventResult("You take time to warm up. Hands recover.", 0.70, 15),
                    new EventResult("Break helps, but your gloves are still failing.", 0.30, 15)
                        .MinorEquipmentWear(EquipSlot.Hands)
                ]);
    }

    private static GameEvent KnifeDulling(GameContext ctx)
    {
        var variant = EquipmentWearSelector.SelectBladeWear(ctx);

        return new GameEvent("Blade Dulling", variant.Description, 0.7)
            .RequiresSituation(Situations.BladeWorn)
            .Requires(EventCondition.Working)
            .WithCooldown(12)
            .Choice("Sharpen It",
                variant.RepairHint,
                [
                    new EventResult("Steady strokes restore the edge.", 0.60, 15)
                        .RepairsTool(variant.Tool ?? ToolType.Knife, 8),
                    new EventResult("Better, but not perfect. Good enough.", 0.30, 12)
                        .RepairsTool(variant.Tool ?? ToolType.Knife, 4),
                    new EventResult("Expert work. Keener than before.", 0.10, 20)
                        .RepairsTool(variant.Tool ?? ToolType.Knife, 12)
                ])
            .Choice("Force It",
                "Push through. The blade will hold.",
                [
                    new EventResult("Extra effort compensates for the dull edge.", 0.45, 0)
                        .WithEffects(EffectFactory.Exhausted(0.15, 30))
                        .DamagesTool(variant.Tool ?? ToolType.Knife, 2),
                    new EventResult("Harder work, more fatigue. But it gets done.", 0.35, 0)
                        .WithEffects(EffectFactory.Exhausted(0.25, 45))
                        .DamagesTool(variant.Tool ?? ToolType.Knife, 3),
                    new EventResult("The blade slips. A cut across your hand.", 0.20, 0)
                        .Damage(5, DamageType.Sharp, BodyTarget.AnyArm)
                        .DamagesTool(variant.Tool ?? ToolType.Knife, 4)
                ])
            .Choice("Switch Tasks",
                "Do something that doesn't need a sharp edge.",
                [
                    new EventResult("You find other work to do.", 1.0, 0)
                        .Aborts()
                ]);
    }

    private static GameEvent ChestWrapTearing(GameContext ctx)
    {
        var variant = EquipmentWearSelector.SelectChestWear(ctx);

        return new GameEvent("Chest Wrap Failing", variant.Description, 0.6)
            .RequiresSituation(Situations.ChestWrapCritical)
            .Requires(EventCondition.Traveling)
            .WithConditionFactor(EventCondition.ExtremelyCold, 2.0)
            .WithConditionFactor(EventCondition.HighWind, 1.5)
            .WithConditionFactor(EventCondition.FarFromCamp, 1.5)
            .WithCooldown(12)
            .Choice("Emergency Patch",
                variant.RepairHint,
                [
                    new EventResult("Fiber binds the tear. Warmth returns.", 0.55, 20)
                        .Costs(ResourceType.PlantFiber, 2)
                        .FieldRepair(EquipSlot.Chest),
                    new EventResult("Partial fix. Still drafty.", 0.30, 15)
                        .Costs(ResourceType.PlantFiber, 2)
                        .LightChill(),
                    new EventResult("Solid repair. Wind can't find its way through.", 0.15, 25)
                        .Costs(ResourceType.PlantFiber, 2)
                        .ProperRepair(EquipSlot.Chest)
                ],
                requires: [EventCondition.HasPlantFiber])
            .Choice("Cinch Tighter",
                "Pull it together. Restrict movement to hold it closed.",
                [
                    new EventResult("Tight binding holds it together. Movement is awkward.", 0.60, 5)
                        .WithEffects(EffectFactory.Stiff(0.2, 60)),
                    new EventResult("Too tight. Breathing becomes difficult.", 0.30, 5)
                        .WithEffects(EffectFactory.Exhausted(0.3, 45)),
                    new EventResult("Cinching tears it further. Cold floods in.", 0.10, 3)
                        .ModerateEquipmentWear(EquipSlot.Chest)
                        .ModerateCold()
                ])
            .Choice("Push Through",
                "Ignore it. You can handle the cold.",
                [
                    new EventResult("You push on. The cold is constant.", 0.45, 0)
                        .ModerateCold(),
                    new EventResult("The tear widens. You're losing significant warmth.", 0.35, 0)
                        .SevereEquipmentWear(EquipSlot.Chest)
                        .HarshCold(),
                    new EventResult("Your core temperature drops. This is getting dangerous.", 0.20, 0)
                        .SevereEquipmentWear(EquipSlot.Chest)
                        .DangerousCold()
                        .WithEffects(EffectFactory.Shivering(0.4))
                ]);
    }

    private static GameEvent FirestarterFailing(GameContext ctx)
    {
        var variant = EquipmentWearSelector.SelectFirestarterWear(ctx);

        return new GameEvent("Firestarter Failing", variant.Description, 0.5)
            .RequiresSituation(Situations.FirestarterCritical)
            .Requires(EventCondition.AtCamp)
            .WithConditionFactor(EventCondition.ExtremelyCold, 1.8)
            .WithCooldown(24)
            .Choice("Gentle Use",
                "Take extra time. Be careful with what's left.",
                [
                    new EventResult("Slow and steady. The tool survives.", 0.65, 10)
                        .RepairsTool(variant.Tool ?? ToolType.HandDrill, 2),
                    new EventResult("Careful work, but it's still wearing down.", 0.30, 12),
                    new EventResult("Patience pays off. You learn its quirks.", 0.05, 15)
                        .RepairsTool(variant.Tool ?? ToolType.HandDrill, 5)
                ])
            .Choice("Force It",
                "You need fire now. Push harder.",
                [
                    new EventResult("It holds. Fire starts.", 0.40, 5),
                    new EventResult("Harder use, faster wear.", 0.40, 5)
                        .DamagesTool(variant.Tool ?? ToolType.HandDrill, 3),
                    new EventResult("It breaks in your hands.", 0.20, 3)
                        .DestroysTool(variant.Tool ?? ToolType.HandDrill)
                        .Frightening()
                ])
            .Choice("Prepare a Backup",
                "Focus on crafting a replacement while this one still works.",
                [
                    new EventResult("You gather materials for a new firestarter.", 0.80, 20)
                        .Rewards(RewardPool.CraftingMaterials),
                    new EventResult("Not the right materials here. Need to search elsewhere.", 0.20, 15)
                ]);
    }
}
