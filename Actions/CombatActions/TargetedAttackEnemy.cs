using text_survival.Actors;
using text_survival.Bodies;
using text_survival.IO;

namespace text_survival.Actions;

public class TargetedAttackEnemy(Npc enemy) : GameActionBase($"Targeted Attack {enemy.Name}")
{
    public override bool IsAvailable(GameContext ctx)
    {
        return ctx.player.Skills.Fighting.Level > 1;
    }
    protected override List<IGameAction> GetNextActions(GameContext ctx) => [new EnemyCombatTurn(enemy), new EndCombatAction(enemy)];

    protected override void OnExecute(GameContext ctx)
    {
        int fightingSkill = ctx.player.Skills.Fighting.Level;
        string? targetPart = SelectTargetPart(enemy, fightingSkill);
        if (targetPart != null)
        {
            ctx.player.Attack(enemy, targetPart);
        }
        else
        {
            NextActionOverride = new PlayerCombatTurnAction(enemy);
        }
    }
    private static string? SelectTargetPart(Actor enemy, int depth)
    {
        if (depth <= 0)
        {
            Output.WriteWarning("You don't have enough skill to target an attack");
            return null;
        }
        Output.WriteLine($"Where do you want to target your attack on the {enemy.Name}?");

        // Group body parts by region for better organization
        var allParts = enemy.Body.GetPartsToNDepth(depth)!;

        BodyPart? choice = Input.GetSelectionFromList(allParts, true);
        if (choice == null)
            return null;

        // todo return part itself
        return choice.Name;
    }
    private readonly Npc enemy = enemy;
}
