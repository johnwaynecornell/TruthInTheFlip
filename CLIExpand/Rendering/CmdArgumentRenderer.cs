using System.Text;

namespace CLIExpand.Rendering;

/// <summary>
/// Serializes command-line arguments using Windows cmd.exe / CommandLineToArgvW double-quote conventions.
/// </summary>
public class CmdArgumentRenderer : ICLIArgumentRenderer
{
    public ShellMode Mode => ShellMode.Cmd;

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
    /// Renders a single argument token for Windows cmd.exe / CommandLineToArgvW.
    /// </summary>
    public static string RenderArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return "\"\"";

        if (IsSafeToken(arg))
            return arg;

        var sb = new StringBuilder();
        sb.Append('"');

        int backslashCount = 0;
        for (int i = 0; i < arg.Length; i++)
        {
            char c = arg[i];
            if (c == '\\')
            {
                backslashCount++;
            }
            else if (c == '"')
            {
                // Escape backslashes and the quote: 2 * backslashCount + 1 backslashes followed by quote
                sb.Append('\\', backslashCount * 2 + 1);
                sb.Append('"');
                backslashCount = 0;
            }
            else
            {
                if (backslashCount > 0)
                {
                    sb.Append('\\', backslashCount);
                    backslashCount = 0;
                }
                sb.Append(c);
            }
        }

        if (backslashCount > 0)
        {
            // Backslashes at end of token must be doubled before closing quote
            sb.Append('\\', backslashCount * 2);
        }

        sb.Append('"');
        return sb.ToString();
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
