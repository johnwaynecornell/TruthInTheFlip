using System.Globalization;
using System.Text;

namespace JWCFarm.Metrics;

public class MetricPath : List<MetricDescriptor.Instance>
{
    // ── public entry points ────────────────────────────────────────────────────

    // Main entry point when evaluating within an explicit session.
    public object Get(MetricEvaluationSession session, object stats)
    {
        var ctx = new MetricEvaluationContext
        {
            Session = session,
            Stats   = stats,
            Root    = stats,
        };

        object o = stats;
        int i = 0;
        while (i < Count)
            o = GetInContext(ctx, o, stats, ref i);

        return o;
    }

    // Overload for standalone/ephemeral evaluation from a projection.
    public object Get(MetricProjection projection, object stats)
        => Get(new MetricEvaluationSession(projection), stats);

    // Overload used internally for aggregate arg evaluation within a session.
    public object Get(MetricEvaluationSession session, object stats, object root, ref int index)
    {
        var ctx = new MetricEvaluationContext
        {
            Session = session,
            Stats   = stats,
            Root    = root,
        };
        return GetInContext(ctx, stats, root, ref index);
    }

    // Overload used internally by MetricProjection.ProcessPath for aggregate arg evaluation.
    // Callers that have a richer context (e.g. the FarmProcess) may pass one directly.
    public object Get(MetricProjection projection, object stats, object root, ref int index)
        => Get(new MetricEvaluationSession(projection), stats, root, ref index);

    // Overload for callers that already hold a MetricEvaluationContext.
    public object Get(MetricEvaluationContext ctx, object stats, object root, ref int index)
        => GetInContext(ctx, stats, root, ref index);

    // ── core implementation ────────────────────────────────────────────────────

    private object GetInContext(
        MetricEvaluationContext ctx,
        object stats,
        object root,
        ref int index)
    {
        bool hasO = false;
        object o = null!;

        while (index < Count)
        {
            var p = this[index++];

            if (!hasO)
            {
                o = stats;
                hasO = true;
            }

            if (o == null)
                throw new NullReferenceException(
                    $"Field {string.Join(".", from p2 in this select p2.IsValue ? p2.Value?.ToString() : p2.InstanceDescriptor.Name)} contains null");

            if (p.IsValue)
            {
                o = p.Value!;
            }
            else if (p.InstanceDescriptor.Type == MetricDescriptor.EType.Property)
            {
                // Give the Getter a context whose Stats is the current receiver object
                // so that ctx.Get<T>("expr") evaluates against the right instance.
                var callCtx = ctx.WithStats(o);
                o = p.InstanceDescriptor.Getter!(callCtx, o)!;
            }
            else if (p.InstanceDescriptor.Type == MetricDescriptor.EType.Method)
            {
                object[] parameters = new object[p.ArgumentPaths.Count];

                for (int i = 0; i < parameters.Length; i++)
                {
                    var arg  = p.ArgumentPaths[i];
                    var desc = p.InstanceDescriptor.Parameters![i];

                    if (desc.Type == MetricParameterType.Scalar)
                    {
                        int ii = 0;
                        object? val = arg.Get(ctx.Session, root, root, ref ii);
                        if (desc.ReflectedType != null)
                        {
                            val = MetricBinder.CoerceNumericWidening(val, desc.ReflectedType);
                        }
                        parameters[i] = val!;
                    }
                    else if (desc.Type == MetricParameterType.Aggregate)
                    {
                        parameters[i] = ctx.Session.GetStatValues(stats, this, i);
                        index = Count; // no further path steps after consuming aggregate
                    }
                }

                // Give the Invoke a context whose Stats is the current receiver object.
                var callCtx = ctx.WithStats(o);
                o = p.InstanceDescriptor.Invoke!(callCtx, o, parameters)!;
            }
        }

        return o;
    }

    // ── canonical string helpers ───────────────────────────────────────────────

    public readonly record struct CommaList(object?[] Parts);

    public static CommaList Commas(params object?[] parts) => new(parts);

    public static string CannonPrint(params object?[] parts)
    {
        StringBuilder sb = new();

        void Append(object? part)
        {
            switch (part)
            {
                case CommaList list:
                    for (int i = 0; i < list.Parts.Length; i++)
                    {
                        if (i != 0) sb.Append(',');
                        Append(list.Parts[i]);
                    }
                    break;

                case double d:
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    break;

                case float f:
                    sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                    break;

                case IFormattable fmtable:
                    sb.Append(fmtable.ToString(null, CultureInfo.InvariantCulture));
                    break;

                default:
                    sb.Append(part);
                    break;
            }
        }

        foreach (object? part in parts)
            Append(part);

        return sb.ToString();
    }

    public override string ToString()
    {
        StringBuilder writer = new();
        string mark = "";

        foreach (var p in this)
        {
            writer.Append(mark);

            if (p.IsValue)
                writer.Append(CannonPrint(p.Value));
            else
            {
                writer.Append(p.InstanceDescriptor.Name);
                if (p.ArgumentPaths != null)
                    writer.Append($"#{string.Join(",", p.ArgumentPaths)}");
            }

            mark = ".";
        }

        return writer.ToString();
    }
}
