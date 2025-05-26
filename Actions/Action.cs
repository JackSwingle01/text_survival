using text_survival.IO;
namespace text_survival.Actions;

public interface IGameAction
{
    public string Name { get; set; }
    public void Execute(GameContext ctx);
    public bool IsAvailable(GameContext ctx);
}

public abstract class GameActionBase(string name) : IGameAction
{
    public virtual string Name { get; set; } = name;
    public virtual bool IsAvailable(GameContext ctx) => true;
    public void Execute(GameContext ctx)
    {
        OnExecute(ctx);
        World.Update(1);
        SelectNextAction(ctx);
    }

    protected abstract void OnExecute(GameContext ctx);
    protected abstract List<IGameAction> GetNextActions(GameContext ctx);
    protected IGameAction? NextActionOverride = null;

    private void SelectNextAction(GameContext ctx)
    {
        if (NextActionOverride != null)
        {
            NextActionOverride.Execute(ctx);
            return;
        }

        var actions = GetNextActions(ctx).Where(a => a.IsAvailable(ctx)).ToList();
        if (actions.Count == 0)
        {
            return; // back to main game loop   
        }
        else if (actions.Count == 1)
        {
            actions[0].Execute(ctx);
            return;
        }
        Output.WriteLine("\n" + userPrompt);
        IGameAction action = Input.GetSelectionFromList(actions)!;
        action.Execute(ctx);
    }
    protected virtual string userPrompt => "What would you like to do?";
    public override string ToString() => Name;
}

public class ReturnAction(string name = "Back") : GameActionBase(name)
{
    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return [];
    }

    protected override void OnExecute(GameContext ctx)
    {
        return;
    }
}
