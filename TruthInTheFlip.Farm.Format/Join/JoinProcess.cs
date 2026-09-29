using FluentCommandLine;
using JWCEssentials.Metadata;
using JWCFarm;
using JWCFarm.Metrics;

namespace TruthInTheFlip.Farm.Format;

/// <summary>
/// An N-way inner equijoin higher-order Farm process that executes child processes sequentially,
/// evaluates a named metric key expression against each emitted item in its arrival sequence,
/// and aligns matching items from all children into <see cref="ProcessArrayStats"/> rows.
///
/// Operational semantics:
/// - First-child ordering: output rows preserve the emitted order of child 0.
/// - Unique-coordinate semantics: duplicate canonical keys within any child process throw a <see cref="FarmInputException"/>.
/// - Inner join: a row is emitted only when every child contains the same canonical key.
/// - Null keys: items evaluating to a null key are omitted and do not participate in the join.
/// - Independent key evaluation: key projections and evaluation sessions are owned privately without altering child output projections.
/// </summary>
public sealed class JoinProcess : FarmProcess
{
    private readonly string _keyExpression;
    private readonly IReadOnlyList<FarmProcess> _children;
    private readonly MetricCatalog _dynamicMetricCatalog;
    private MetricProjection[]? _keyProjections;
    private Type? _canonicalKeyType;

    /// <summary>
    /// Gets the metric expression used as the join coordinate key.
    /// </summary>
    public string KeyExpression => _keyExpression;

    /// <summary>
    /// Gets the ordered collection of child processes participating in the join.
    /// </summary>
    public override IReadOnlyList<FarmProcess> Children => _children;

    /// <summary>
    /// Gets the resolved canonical key type for comparison across all children, or null if not yet bound.
    /// </summary>
    public Type? CanonicalKeyType => _canonicalKeyType;

    /// <summary>
    /// Gets the private bound key projections for each child process, or null if not yet bound.
    /// </summary>
    public IReadOnlyList<MetricProjection>? KeyProjections => _keyProjections;

    public JoinProcess(string keyExpression, IReadOnlyList<FarmProcess> children)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyExpression);
        ArgumentNullException.ThrowIfNull(children);
        if (children.Count == 0)
        {
            throw new FarmInputException("Join requires at least one child process.");
        }

        var list = new FarmProcess[children.Count];
        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i] ?? throw new ArgumentNullException(nameof(children), $"Child process at index {i} cannot be null.");
            child.Projection ??= new MetricProjection();
            list[i] = child;
        }

        _keyExpression = keyExpression;
        _children = list;
        _dynamicMetricCatalog = ProcessArrayStats.CreateDynamicMetricCatalog(list);
    }

    public JoinProcess(string keyExpression, params FarmProcess[] children)
        : this(keyExpression, (IReadOnlyList<FarmProcess>)children)
    {
    }

    [FluentMethod("join")]
    [KV_FA(FluentAttribute.Help, "Join multiple child FarmProcesses on a metric key expression into combined rows. End the child process list with .END.")]
    public static FarmProcess Join(
        [KV_FA(FluentAttribute.Help, "Metric key expression to evaluate on each child.")]
        string keyExpression,
        [KV_FA(FluentAttribute.Help, "Child FarmProcesses to join. End with .END.")]
        FarmProcess[] processes)
    {
        if (string.IsNullOrWhiteSpace(keyExpression))
        {
            throw new FarmInputException("Join requires a non-empty key expression.");
        }

        if (processes == null || processes.Length == 0)
        {
            throw new FarmInputException("Join requires at least one child process.");
        }

        return new JoinProcess(keyExpression, processes);
    }

    public override Type StatType => typeof(ProcessArrayStats);
    public override Type InputType => typeof(object);
    public override FarmProcess? InputProcess => null;

    public override MetricCatalog? GetDynamicMetricCatalog(Type type)
    {
        if (type == typeof(ProcessArrayStats))
            return _dynamicMetricCatalog;

        return null;
    }

    /// <summary>
    /// Binds the join key expression independently against each child process and resolves the symmetric canonical key type.
    /// </summary>
    public bool EnsureKeyProjections(MetricCatalogs catalogs, out MetricBindError? error)
    {
        error = null;
        var keyProjs = new MetricProjection[_children.Count];
        var keyTypes = new Type[_children.Count];

        for (int i = 0; i < _children.Count; i++)
        {
            var child = _children[i];
            if (!MetricBinder.Bind(child, catalogs, child.StatType, child.InputType, out MetricProjection? keyProj, out error, _keyExpression))
            {
                return false;
            }

            if (keyProj == null || keyProj.Fields.Count == 0)
            {
                error = new MetricBindError(_keyExpression, 0, _keyExpression.Length, $"Failed to bind join key expression '{_keyExpression}' for child {i}.");
                return false;
            }

            if (keyProj.Fields.Count > 1)
            {
                error = new MetricBindError(_keyExpression, 0, _keyExpression.Length, $"Join key expression must resolve to a single metric field, but produced {keyProj.Fields.Count} fields.");
                return false;
            }

            keyProjs[i] = keyProj;
            Type fieldType = MetricBinder.GetPathReturnType(keyProj.Fields[0]);
            keyTypes[i] = fieldType;
        }

        if (!MetricKeyResolver.TryResolveCanonicalKeyType(keyTypes, out Type? canonicalType, out string? keyError))
        {
            error = new MetricBindError(_keyExpression, 0, _keyExpression.Length, keyError ?? "Incompatible join key types.");
            return false;
        }

        _keyProjections = keyProjs;
        _canonicalKeyType = canonicalType;
        return true;
    }

    public override bool BindFields(MetricCatalogs catalogs, string[] fields, out MetricBindError? error)
    {
        if (!EnsureKeyProjections(catalogs, out error))
            return false;

        return base.BindFields(catalogs, fields, out error);
    }

    protected override IEnumerable<object> EnumerateItems(FarmContext context)
    {
        if (_children.Count == 0)
            yield break;

        if (_keyProjections == null || _canonicalKeyType == null)
        {
            var catalogs = FluentEnvironment.Current?.Context.TryGet<MetricCatalogs>(out var cat) == true
                ? cat
                : new MetricCatalogs { Reflect = TruthInTheFlip_Fluent.DefaultReflect };

            if (!EnsureKeyProjections(catalogs, out var bindError))
            {
                throw new FarmInputException(bindError!.FormatDiagnostic());
            }
        }

        // Each execution must use fresh MetricEvaluationSession instances for key evaluation
        var keySessions = new MetricEvaluationSession[_children.Count];
        for (int i = 0; i < _children.Count; i++)
        {
            keySessions[i] = new MetricEvaluationSession(_keyProjections![i]);
        }

        // Child 0: capture in original emitted order and track seen keys for duplicate detection
        var child0Items = new List<(object CanonicalKey, object Item)>();
        var child0Seen = new HashSet<object>();

        ChildProcessObserver.Execute(_children[0], context, (_, item) =>
        {
            keySessions[0].Inspect(_children[0], item, item);
            object? rawKey = _keyProjections![0].Fields[0].Get(keySessions[0], item);
            object? canonicalKey = MetricKeyResolver.CanonicalizeKey(rawKey, _canonicalKeyType!);

            if (canonicalKey == null)
                return; // Null keys do not participate in the join

            if (!child0Seen.Add(canonicalKey))
            {
                throw new FarmInputException(
                    $"Duplicate join key '{canonicalKey}' encountered in child process 0 (StatType: {_children[0].StatType.Name}). Join requires unique keys per child.");
            }

            child0Items.Add((canonicalKey, item));
        });

        if (_children[0].Session != null)
        {
            Session?.AddChildSession(_children[0].Session);
        }

        if (child0Items.Count == 0)
            yield break;

        // If single child join, emit child 0 items
        if (_children.Count == 1)
        {
            foreach (var (_, item0) in child0Items)
            {
                yield return new ProcessArrayStats(new[] { item0 });
            }
            yield break;
        }

        // Children 1..N-1: map canonical keys to items and check for duplicates
        var childDicts = new List<Dictionary<object, object>>(_children.Count - 1);

        for (int c = 1; c < _children.Count; c++)
        {
            int childIndex = c;
            var dict = new Dictionary<object, object>();

            ChildProcessObserver.Execute(_children[childIndex], context, (_, item) =>
            {
                keySessions[childIndex].Inspect(_children[childIndex], item, item);
                object? rawKey = _keyProjections![childIndex].Fields[0].Get(keySessions[childIndex], item);
                object? canonicalKey = MetricKeyResolver.CanonicalizeKey(rawKey, _canonicalKeyType!);

                if (canonicalKey == null)
                    return; // Null keys do not participate in the join

                if (dict.ContainsKey(canonicalKey))
                {
                    throw new FarmInputException(
                        $"Duplicate join key '{canonicalKey}' encountered in child process {childIndex} (StatType: {_children[childIndex].StatType.Name}). Join requires unique keys per child.");
                }

                dict.Add(canonicalKey, item);
            });

            if (_children[childIndex].Session != null)
            {
                Session?.AddChildSession(_children[childIndex].Session);
            }

            if (dict.Count == 0)
                yield break; // Inner join is empty if any child has 0 matching items

            childDicts.Add(dict);
        }

        // Emit N-way intersection in child 0 order
        foreach (var (canonicalKey, item0) in child0Items)
        {
            bool match = true;
            var rowItems = new object[_children.Count];
            rowItems[0] = item0;

            for (int c = 1; c < _children.Count; c++)
            {
                if (!childDicts[c - 1].TryGetValue(canonicalKey, out var childItem))
                {
                    match = false;
                    break;
                }

                rowItems[c] = childItem;
            }

            if (match)
            {
                yield return new ProcessArrayStats(rowItems);
            }
        }
    }
}
