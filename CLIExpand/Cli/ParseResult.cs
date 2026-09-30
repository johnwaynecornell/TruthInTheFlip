using CLIExpand.Rendering;

namespace CLIExpand.Cli;

/// <summary>
/// Contains the result of parsing command-line options and payload tokens for CLIExpand.
/// </summary>
public class ParseResult
{
    /// <summary>
    /// Explicit shell mode override, if supplied on CLI.
    /// </summary>
    public ShellMode? Mode { get; set; }

    /// <summary>
    /// Explicit expansion start token override, if supplied on CLI.
    /// </summary>
    public string? ExpandStartToken { get; set; }

    /// <summary>
    /// Explicit expansion end token override, if supplied on CLI.
    /// </summary>
    public string? ExpandEndToken { get; set; }

    /// <summary>
    /// Explicit delimiter token override, if supplied on CLI.
    /// </summary>
    public string? DelimiterToken { get; set; }

    /// <summary>
    /// Explicit join start token override, if supplied on CLI.
    /// </summary>
    public string? JoinStartToken { get; set; }

    /// <summary>
    /// Explicit join end token override, if supplied on CLI.
    /// </summary>
    public string? JoinEndToken { get; set; }

    /// <summary>
    /// Explicit partial matching override, if supplied on CLI.
    /// </summary>
    public bool? MatchPartials { get; set; }

    /// <summary>
    /// Explicit token splitting override, if supplied on CLI.
    /// </summary>
    public bool? SplitAfterGetByKey { get; set; }

    /// <summary>
    /// Explicit path to a settings JSON file, if supplied on CLI.
    /// </summary>
    public string? SettingsPath { get; set; }

    /// <summary>
    /// Indicates whether a help flag (-h, --help) was requested.
    /// </summary>
    public bool HelpRequested { get; set; }

    /// <summary>
    /// The raw payload tokens following the '--' boundary.
    /// </summary>
    public List<string> Payload { get; } = new();

    /// <summary>
    /// True if an error occurred during CLI argument parsing.
    /// </summary>
    public bool HasErrors { get; set; }

    /// <summary>
    /// Diagnostic message if <see cref="HasErrors"/> is true.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
