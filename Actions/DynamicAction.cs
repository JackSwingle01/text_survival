namespace text_survival.Actions;

public class DynamicAction(
    string name,
    Func<GameContext, bool>? isAvailable,
    Action<GameContext>? onExecute,
    Func<GameContext, List<IGameAction>>? getNextActions,
    string? userPrompt,
    int timeInMinutes = 1
) : GameActionBase(name)
{
    private readonly Func<GameContext, bool>? _isAvailable = isAvailable;
    private readonly Action<GameContext>? _onExecute = onExecute;
    private readonly Func<GameContext, List<IGameAction>>? _getNextActions = getNextActions;
    private readonly string? _userPrompt = userPrompt;
    public override int TimeInMinutes => timeInMinutes;

    public override bool IsAvailable(GameContext ctx)
    {
        return _isAvailable?.Invoke(ctx) ?? true;
    }

    protected override void OnExecute(GameContext ctx)
    {
        _onExecute?.Invoke(ctx);
    }

    protected override List<IGameAction> GetNextActions(GameContext ctx)
    {
        return _getNextActions?.Invoke(ctx) ?? [];
    }
    protected override string userPrompt => _userPrompt ?? base.userPrompt;
}