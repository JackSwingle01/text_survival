using text_survival.Actors;
using text_survival.Magic;

namespace text_survival.Actions;

public class SelectSpell(Npc enemy) : GameActionBase("Cast Spell")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        List<IGameAction> options = [];
        foreach (Spell spell in ctx.player._spells)
        {
            options.Add(new SelectSpellTarget(spell, enemy));
        }
        var back = new PlayerCombatTurnAction(enemy);
        back.Name = "Choose a different action.";
        options.Add(back);
        return options;
    }

    protected override void OnExecute(GameContext ctx) { }
    protected override string userPrompt => "Which spell would you like to cast?";
    private readonly Npc enemy = enemy;
}

public class SelectSpellTarget(Spell spell, Npc enemy) : GameActionBase($"Cast {spell.ToString()}")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx) => [new CastSpellAtTarget(spell, enemy, ctx.player), new CastSpellAtTarget(spell, enemy, enemy)];

    protected override void OnExecute(GameContext ctx) { }

    private readonly Npc enemy = enemy;
    private readonly Spell spell = spell;
    protected override string userPrompt => $"Which target would you like to cast {spell} on?";

}

public class CastSpellAtTarget(Spell spell, Npc enemy, Actor target) : GameActionBase($"Cast {spell} on {target}")
{
    protected override List<IGameAction> GetNextActions(GameContext ctx) => [new EnemyCombatTurn(enemy), new EndCombatAction(enemy)];
    protected override void OnExecute(GameContext ctx)
    {
        castSpell(spell, target);
    }
    private void castSpell(Spell spell, Actor target)
    {
        // if (spell.NeedsTargetPart)
        // {
        //     Output.WriteLine("Select a part to target:");
        //     var parts = target.Body.GetAllParts();
        //     var part = Input.GetSelectionFromList(parts)!;
        //     spell.Cast(target, part);
        // }
        // else
        // {
        // }
        spell.Cast(target);
        // Player._skills.AddExperience("Shamanism", 2);
    }
}
