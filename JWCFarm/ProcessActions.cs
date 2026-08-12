namespace JWCFarm;

public class ProcessActions
{
    public Action<FarmContext>? Begin { get; set; }
    public Action<FarmContext, object>? Process{ get; set; }
    public Action<FarmContext>? End{ get; set; }
    public Action<FarmContext, Exception>? Abort{ get; set; }
    
    public ProcessActions(
        Action<FarmContext, object>? process = null,
        Action<FarmContext>? begin = null,
        Action<FarmContext>? end = null,
        Action<FarmContext, Exception>? abort = null)
    {
        Begin = begin;
        Process = process;
        End = end;
        Abort = abort;
    }
}
