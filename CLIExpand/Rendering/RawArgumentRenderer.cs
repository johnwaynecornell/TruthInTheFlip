using System.Text;

namespace CLIExpand.Rendering;

/// <summary>
/// Serializes command-line arguments as plain text without shell quoting or escaping.
/// Inserts a single space between adjacent tokens only when the boundary between them does not already contain whitespace.
/// Note: Provides shell-neutral textual rendering, but is not lossless token transport when an argument contains whitespace or is empty.
/// Shell-specific modes preserve argv boundaries through quoting; raw mode intentionally does not.
/// </summary>
public class RawArgumentRenderer : ICLIArgumentRenderer
{
    public ShellMode Mode => ShellMode.Raw;

    public string Render(IReadOnlyList<string> args)
    {
        if (args == null || args.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();

        for (int i = 0; i < args.Count; i++)
        {
            string current = args[i];

            if (i > 0)
            {
                string previous = args[i - 1];
                if (!string.IsNullOrEmpty(previous) &&
                    !string.IsNullOrEmpty(current) &&
                    !char.IsWhiteSpace(previous[^1]) &&
                    !char.IsWhiteSpace(current[0]))
                {
                    sb.Append(' ');
                }
            }

            sb.Append(current);
        }

        return sb.ToString();
    }
}
