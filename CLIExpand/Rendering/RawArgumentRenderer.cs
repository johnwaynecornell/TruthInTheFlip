namespace CLIExpand.Rendering;

/// <summary>
/// Serializes command-line arguments as plain space-separated text without shell quoting or escaping.
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

        return string.Join(" ", args);
    }
}
