namespace JWCEssentials.Metadata;

[AttributeUsage(
    AttributeTargets.All,
    AllowMultiple = true,
    Inherited = false)]
public class KVAttribute<TKey> : Attribute
    where TKey : struct, Enum
{
    public TKey Key { get; }
    public string Value { get; }

    public KVAttribute(TKey key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        Key = key;
        Value = value;
    }
}
