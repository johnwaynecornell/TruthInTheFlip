namespace CLIExpand.Rendering;

/// <summary>
/// Specifies the target shell or format for command-line argument rendering.
/// </summary>
public enum ShellMode
{
    Bash,
    PowerShell,
    Cmd,
    Raw
}

/// <summary>
/// Defines the contract for serializing expanded argument tokens into shell-safe command line text.
/// </summary>
public interface ICLIArgumentRenderer
{
    /// <summary>
    /// Gets the shell mode handled by this renderer.
    /// </summary>
    ShellMode Mode { get; }

    /// <summary>
    /// Serializes an argument list into a command-line string.
    /// </summary>
    /// <param name="args">The list of arguments to render.</param>
    /// <returns>A formatted command-line string suitable for the target shell.</returns>
    string Render(IReadOnlyList<string> args);
}
