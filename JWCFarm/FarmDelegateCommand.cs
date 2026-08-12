namespace JWCFarm;

public class FarmDelegateCommand : FarmCommand
{
    public Action<FarmContext> Action;
        
    public FarmDelegateCommand(Action<FarmContext> action)
    {
        Action = action;
    }
        
    public override void Execute(FarmContext context)
    {
        Action(context);
    }
}
