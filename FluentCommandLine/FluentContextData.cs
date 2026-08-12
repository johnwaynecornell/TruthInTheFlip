namespace FluentCommandLine;

public class FluentContextData
{
    private readonly Dictionary<Type, object> values = new();

    public void Set<T>(T value) where T : notnull
        => values[typeof(T)] = value;

    public bool TryGet<T>(out T? value)
    {
        if (values.TryGetValue(typeof(T), out object? raw) &&
            raw is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public T Get<T>()
    {
        if (TryGet<T>(out T? value))
            return value!;

        throw new KeyNotFoundException(
            $"No contextual value is registered for {typeof(T).FullName}.");
    }
}