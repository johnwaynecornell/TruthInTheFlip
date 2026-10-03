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
    /// Explicit get token override, if supplied on CLI.
    /// </summary>
    public string? GetToken { get; set; }

    /// <summary>
    /// Explicit split_get token override, if supplied on CLI.
    /// </summary>
    public string? SplitGetToken { get; set; }

    /// <summary>
    /// Explicit value map files supplied on CLI via repeated -values options.
    /// </summary>
    public List<string> ValueFiles { get; } = new();

    /// <summary>
    /// Explicit macro map files supplied on CLI via repeated -macros options.
    /// </summary>
    public List<string> MacroFiles { get; } = new();

    /// <summary>
    /// Inline macro key-value pairs supplied on CLI via repeated -macro key=value options.
    /// </summary>
    public Dictionary<string, string> InlineMacros { get; } = new(StringComparer.Ordinal);

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
    /// Indicates whether -list-values was requested.
    /// </summary>
    public bool ListValuesRequested { get; set; }

    /// <summary>
    /// Indicates whether -list-macros was requested.
    /// </summary>
    public bool ListMacrosRequested { get; set; }

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
