namespace JWCFarm;

/// <summary>
/// Helper for executing a child <see cref="FarmProcess"/> while observing its emitted items,
/// preserving existing <see cref="ProcessActions"/>, and guaranteeing action restoration on completion or error.
/// </summary>
public static class ChildProcessObserver
{
    /// <summary>
    /// Executes the specified child process with an observer delegate that receives each emitted item
    /// before any existing child <see cref="ProcessActions.Process"/> action is invoked.
    /// The original <see cref="FarmProcess.Actions"/> reference is guaranteed to be restored upon return or exception.
    /// </summary>
    /// <param name="child">The child process to execute.</param>
    /// <param name="context">The FarmContext for execution.</param>
    /// <param name="observe">Callback invoked for each emitted item.</param>
    public static void Execute(
        FarmProcess child,
        FarmContext context,
        Action<FarmContext, object> observe)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(observe);

        var originalActions = child.Actions;

        try
        {
            child.Actions = new ProcessActions(
                begin: originalActions?.Begin,
                process: (ctx, item) =>
                {
                    observe(ctx, item);
                    originalActions?.Process?.Invoke(ctx, item);
                },
                end: originalActions?.End,
                abort: originalActions?.Abort
            );

            child.Execute(context);
        }
        finally
        {
            child.Actions = originalActions;
        }
    }
}
