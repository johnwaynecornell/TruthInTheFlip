using System.Text;

namespace JWCFarm.Metrics;

public sealed class MetricBindError
{
    public string Expression { get; }
    public int Offset { get; }
    public int Length { get; }
    public string Message { get; }

    public MetricBindError(string expression, int offset, string message)
        : this(expression, offset, 1, message) { }

    public MetricBindError(string expression, int offset, int length, string message)
    {
        Expression = expression;
        Offset = Math.Max(0, offset);
        Length = Math.Max(1, Math.Min(length, Math.Max(1, expression.Length - Math.Max(0, offset))));
        Message = message;
    }

    public string FormatDiagnostic()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Invalid metric expression:");
        sb.AppendLine($"  {Expression}");
        sb.Append("  ");
        sb.Append(' ', Offset);
        sb.AppendLine(new string('^', Length));
        sb.Append(Message);
        return sb.ToString();
    }
}
