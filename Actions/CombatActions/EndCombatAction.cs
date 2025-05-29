using text_survival.Actors;
using text_survival.IO;

namespace text_survival.Actions;

public class EndCombatAction(Npc enemy) : GameActionBase("End Combat")
{
    public override bool IsAvailable(GameContext ctx)
    {
        return !enemy.IsEngaged || !ctx.player.IsEngaged || !ctx.player.IsAlive || !enemy.IsAlive;
    }

    protected override void OnExecute(GameContext ctx)
    {
        ctx.player.IsEngaged = false;
        enemy.IsEngaged = false;

        // Combat end
        if (!ctx.player.IsAlive)
        {
            Output.WriteDanger("Your vision fades to black as you collapse... You have died!");
        }
        else if (!enemy.IsAlive)
        {
            string[] victoryMessages = {
                    $"The {enemy.Name} collapses, defeated!",
                    $"You stand victorious over the fallen {enemy.Name}!",
                    $"With a final blow, you bring down the {enemy.Name}!"
                };
            Output.WriteLine(victoryMessages[Utils.RandInt(0, victoryMessages.Length - 1)]);

            // Calculate experience based on enemy difficulty
            int xpGain = CalculateExperienceGain();
            Output.WriteLine($"You've gained {xpGain} fighting experience!");
            ctx.player._skillRegistry.AddExperience("Fighting", xpGain);
        }
    }
    protected override List<IGameAction> GetNextActions(GameContext ctx) => [];

    private int CalculateExperienceGain()
    {
        // Base XP
        int baseXP = 5;

        // Adjust based on enemy weight/size
        double sizeMultiplier = Math.Clamp(enemy.Body.Weight / 50, 0.5, 3.0);

        // Adjust based on enemy weapon damage
        double weaponMultiplier = Math.Clamp(enemy.ActiveWeapon.Damage / 8, 0.5, 2.0);

        return (int)(baseXP * sizeMultiplier * weaponMultiplier);
    }
    private readonly Npc enemy = enemy;
}