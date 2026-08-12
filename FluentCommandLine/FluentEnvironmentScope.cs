namespace FluentCommandLine;

public static class FluentEnvironmentScope
{
    [ThreadStatic]
    private static Stack<FluentEnvironment>? environments;

    public static FluentEnvironment Current =>
        environments is { Count: > 0 }
            ? environments.Peek()
            : throw new InvalidOperationException(
                "No FluentEnvironment is active on this thread.");

    public static IDisposable Enter(FluentEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        environments ??= new Stack<FluentEnvironment>();
        environments.Push(environment);

        return new Scope(environment);
    }

    private sealed class Scope : IDisposable
    {
        private readonly FluentEnvironment environment;
        private bool disposed;

        public Scope(FluentEnvironment environment)
        {
            this.environment = environment;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            if (environments is null || environments.Count == 0)
            {
                throw new InvalidOperationException(
                    "The FluentEnvironment scope stack is empty.");
            }

            FluentEnvironment current = environments.Pop();

            if (!ReferenceEquals(current, environment))
            {
                throw new InvalidOperationException(
                    "FluentEnvironment scopes were disposed out of order.");
            }
        }
    }
}