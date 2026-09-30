using System.Text;

namespace CLIExpand.Rendering;

/// <summary>
/// Serializes command-line arguments using PowerShell single-quote literal escaping rules.
/// </summary>
public class PowerShellArgumentRenderer : ICLIArgumentRenderer
{
    public ShellMode Mode => ShellMode.PowerShell;

    public string Render(IReadOnlyList<string> args)
    {
        if (args == null || args.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < args.Count; i++)
        {
            if (i > 0)
                sb.Append(' ');

            sb.Append(RenderArgument(args[i]));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Renders a single argument token for PowerShell.
    /// </summary>
    public static string RenderArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return "''";

        if (IsSafeToken(arg))
            return arg;

        // In PowerShell single-quote strings, single quotes are escaped by doubling: ''
        return "'" + arg.Replace("'", "''") + "'";
    }

    private static bool IsSafeToken(string token)
    {
        foreach (char c in token)
        {
            if (!IsSafeChar(c))
                return false;
        }

        return true;
    }

    private static bool IsSafeChar(char c)
    {
        return (c >= 'a' && c <= 'z') ||
               (c >= 'A' && c <= 'Z') ||
               (c >= '0' && c <= '9') ||
               c == '_' || c == '.' || c == '/' || c == '\\' ||
               c == '-' || c == '+' || c == ':' || c == '=';
    }
}
