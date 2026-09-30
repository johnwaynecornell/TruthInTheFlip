namespace CLIExpand.Rendering;

/// <summary>
/// Factory helper for creating shell-specific argument renderers.
/// </summary>
public static class CLIArgumentRendererFactory
{
    /// <summary>
    /// Creates an argument renderer instance for the given <paramref name="mode"/>.
    /// </summary>
    /// <param name="mode">The desired shell mode.</param>
    /// <returns>An instance of <see cref="ICLIArgumentRenderer"/>.</returns>
    public static ICLIArgumentRenderer Create(ShellMode mode) => mode switch
    {
        ShellMode.Bash => new BashArgumentRenderer(),
        ShellMode.PowerShell => new PowerShellArgumentRenderer(),
        ShellMode.Cmd => new CmdArgumentRenderer(),
        ShellMode.Raw => new RawArgumentRenderer(),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, $"Unsupported shell mode: {mode}")
    };
}
