using text_survival.Actors;
using text_survival.Actors.Animals;
using text_survival.Bodies;
using text_survival.Combat;
using text_survival.IO;
using text_survival.UI;

namespace text_survival.Actions.Handlers;

/// <summary>
/// Handles combat encounters with animals.
/// Static handler that takes GameContext and mutates state directly.
/// </summary>
public static class CombatHandler
{
    public static void StartCombat(GameContext ctx, Animal enemy)
    {
        if (!enemy.IsAlive) return;

        GameDisplay.AddNarrative(ctx, "!");
        Thread.Sleep(500);
        GameDisplay.AddNarrative(ctx, CombatNarrator.DescribeCombatStart(ctx.player, enemy));

        ctx.player.IsEngaged = true;
        enemy.IsEngaged = true;

        bool enemyFirstStrike = enemy.Speed > ctx.player.Speed;

        if (enemyFirstStrike)
        {
            GameDisplay.AddNarrative(ctx, $"The {enemy.Name} moves with surprising speed!");
            Thread.Sleep(500);
            EnemyCombatTurn(ctx, enemy);
        }
        else
        {
            GameDisplay.AddNarrative(ctx, "You're quick to react, giving you the initiative!");
            Thread.Sleep(500);
            PlayerCombatTurn(ctx, enemy);
        }
    }

    private static void PlayerCombatTurn(GameContext ctx, Animal enemy)
    {
        if (!ctx.player.IsAlive || !enemy.IsAlive || !ctx.player.IsEngaged)
        {
            EndCombat(ctx, enemy);
            return;
        }

        GameDisplay.AddNarrative(ctx, "─────────────────────────────────────");
        DisplayCombatStatus(ctx, enemy);

        var choice = new Choice<Action>();
        choice.AddOption($"Attack {enemy.Name}", () => AttackEnemy(ctx, enemy));

        if (ctx.player.Skills.Fighting.Level > 1)
            choice.AddOption($"Targeted Attack {enemy.Name}", () => TargetedAttackEnemy(ctx, enemy));

        if (ctx.player.Speed > 0.25)
            choice.AddOption("Flee", () => AttemptFlee(ctx, enemy));

        choice.GetPlayerChoice(ctx).Invoke();
    }

    private static void EnemyCombatTurn(GameContext ctx, Animal enemy)
    {
        if (!ctx.player.IsAlive || !enemy.IsAlive || !enemy.IsEngaged)
        {
            EndCombat(ctx, enemy);
            return;
        }

        Thread.Sleep(500);
        enemy.Attack(ctx.player, null, null, ctx);

        if (!ctx.player.IsAlive || !enemy.IsAlive)
            EndCombat(ctx, enemy);
        else
            PlayerCombatTurn(ctx, enemy);
    }

    private static void AttackEnemy(GameContext ctx, Animal enemy)
    {
        ctx.player.Attack(enemy, ctx.Inventory.Weapon, null, ctx);

        if (!enemy.IsAlive)
            EndCombat(ctx, enemy);
        else
            EnemyCombatTurn(ctx, enemy);
    }

    private static void TargetedAttackEnemy(GameContext ctx, Animal enemy)
    {
        int fightingSkill = ctx.player.Skills.Fighting.Level;
        var targetPart = SelectTargetPart(ctx, enemy, fightingSkill);

        if (targetPart != null)
        {
            ctx.player.Attack(enemy, ctx.Inventory.Weapon, targetPart.Name, ctx);

            if (!enemy.IsAlive)
                EndCombat(ctx, enemy);
            else
                EnemyCombatTurn(ctx, enemy);
        }
        else
        {
            PlayerCombatTurn(ctx, enemy);
        }
    }

    private static BodyRegion? SelectTargetPart(GameContext ctx, Actor enemy, int depth)
    {
        if (depth <= 0)
        {
            GameDisplay.AddWarning(ctx, "You don't have enough skill to target an attack");
            return null;
        }

        GameDisplay.AddNarrative(ctx, $"Where do you want to target your attack on the {enemy.Name}?");

        List<BodyRegion> allParts = [];
        foreach (var part in enemy.Body.Parts)
        {
            if (depth > 0)
                allParts.Add(part);
        }

        return Input.Select(ctx, "Select target:", allParts);
    }

    private static void AttemptFlee(GameContext ctx, Animal enemy)
    {
        if (CombatUtils.SpeedCheck(ctx.player, enemy))
        {
            GameDisplay.AddNarrative(ctx, "You got away!");
            enemy.IsEngaged = false;
            ctx.player.IsEngaged = false;
            ctx.player.Skills.Reflexes.GainExperience(2);
        }
        else
        {
            GameDisplay.AddNarrative(ctx, $"You weren't fast enough to get away from {enemy.Name}!");
            ctx.player.Skills.Reflexes.GainExperience(1);
            EnemyCombatTurn(ctx, enemy);
        }
    }

    private static void EndCombat(GameContext ctx, Animal enemy)
    {
        ctx.player.IsEngaged = false;
        enemy.IsEngaged = false;

        if (!ctx.player.IsAlive)
        {
            GameDisplay.AddDanger(ctx, "Your vision fades to black as you collapse... You have died!");
        }
        else if (!enemy.IsAlive)
        {
            string[] victoryMessages = [
                $"The {enemy.Name} collapses, defeated!",
                $"You stand victorious over the fallen {enemy.Name}!",
                $"With a final blow, you bring down the {enemy.Name}!"
            ];
            GameDisplay.AddNarrative(ctx, victoryMessages[Utils.RandInt(0, victoryMessages.Length - 1)]);

            int xpGain = CalculateExperienceGain(enemy);
            GameDisplay.AddNarrative(ctx, $"You've gained {xpGain} fighting experience!");
            ctx.player.Skills.Fighting.GainExperience(xpGain);
        }
    }

    public static int CalculateExperienceGain(Animal enemy)
    {
        int baseXP = 5;
        double sizeMultiplier = Math.Clamp(enemy.Body.WeightKG / 50, 0.5, 3.0);
        double damageMultiplier = Math.Clamp(enemy.AttackDamage / 8, 0.5, 2.0);
        return (int)(baseXP * sizeMultiplier * damageMultiplier);
    }

    private static void DisplayCombatStatus(GameContext ctx, Animal enemy)
    {
        double playerVitality = ctx.player.Vitality;
        string playerStatus = $"You: {Math.Round(playerVitality * 100, 0)}% Vitality";
        AddHealthMessage(ctx, playerStatus, playerVitality);

        double enemyVitality = enemy.Vitality;
        string enemyStatus = $"{enemy.Name}: {Math.Round(enemyVitality * 100, 0)}% Vitality";
        AddHealthMessage(ctx, enemyStatus, enemyVitality);
    }

    private static void AddHealthMessage(GameContext ctx, string message, double healthPercentage)
    {
        if (healthPercentage < 0.2)
            GameDisplay.AddDanger(ctx, message);
        else if (healthPercentage < 0.5)
            GameDisplay.AddWarning(ctx, message);
        else
            GameDisplay.AddSuccess(ctx, message);
    }
}
