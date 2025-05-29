using text_survival.Actors;
using text_survival.IO;

namespace text_survival.Actions;

public class PlayerCombatTurnAction(Npc enemy) : GameActionBase("Player Turn")
{
    public override bool IsAvailable(GameContext ctx)
    {
        return ctx.player.IsAlive && enemy.IsAlive && ctx.player.IsEngaged;
    }
    protected override void OnExecute(GameContext ctx)
    {
        Output.WriteLine("─────────────────────────────────────");
        DisplayCombatStatus(ctx.player, enemy);
    }
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [new AttackEnemy(enemy), new TargetedAttackEnemy(enemy), new CastSpell(enemy), new FleeCombat(enemy)];
    }
    private static void DisplayCombatStatus(Player player, Actor enemy)
    {
        ConsoleColor oldColor = Console.ForegroundColor;

        // Player status
        Console.ForegroundColor = GetHealthColor(player.Body.Health / player.Body.MaxHealth);
        Output.WriteLine($"You: {Math.Round(player.Body.Health, 0)}/{Math.Round(player.Body.MaxHealth, 0)} HP");

        // Enemy status
        Console.ForegroundColor = GetHealthColor(enemy.Body.Health / enemy.Body.MaxHealth);
        Output.WriteLine($"{enemy.Name}: {Math.Round(enemy.Body.Health, 0)}/{Math.Round(enemy.Body.MaxHealth, 0)} HP");

        Console.ForegroundColor = oldColor;
    }

    private static ConsoleColor GetHealthColor(double healthPercentage)
    {
        if (healthPercentage < 0.2) return ConsoleColor.Red;
        if (healthPercentage < 0.5) return ConsoleColor.Yellow;
        return ConsoleColor.Green;
    }
    private readonly Npc enemy = enemy;
}
