using CLIExpand.Rendering;

namespace CLIExpand.Config;

/// <summary>
/// Configuration settings for CLIExpand host and CLIExpander pipeline.
/// </summary>
public class CLIExpandSettings
{
    /// <summary>
    /// The target shell mode for command serialization. Defaults to <see cref="ShellMode.Raw"/>.
    /// </summary>
    public ShellMode Mode { get; set; } = ShellMode.Raw;

    /// <summary>
    /// Token identifying the start of an expansion block. Defaults to <c>".expand."</c>.
    /// </summary>
    public string ExpandStartToken { get; set; } = ".expand.";

    /// <summary>
    /// Token identifying the termination of an expansion block. Defaults to <c>".expand_end."</c>.
    /// </summary>
    public string ExpandEndToken { get; set; } = ".expand_end.";

    /// <summary>
    /// Token separating expansion variables and alternatives from the block body template. Defaults to <c>":"</c>.
    /// </summary>
    public string DelimiterToken { get; set; } = ":";

    /// <summary>
    /// Token identifying the start of a token concatenation block. Defaults to <c>".join."</c>.
    /// </summary>
    public string JoinStartToken { get; set; } = ".join.";

    /// <summary>
    /// Token identifying the termination of a token concatenation block. Defaults to <c>".join_end."</c>.
    /// </summary>
    public string JoinEndToken { get; set; } = ".join_end.";

    /// <summary>
    /// Token identifying an explicit known-value retrieval operation. Defaults to <c>".get."</c>.
    /// </summary>
    public string GetToken { get; set; } = ".get.";

    /// <summary>
    /// Token identifying an explicit known-value retrieval with whitespace splitting. Defaults to <c>".split_get."</c>.
    /// </summary>
    public string SplitGetToken { get; set; } = ".split_get.";

    /// <summary>
    /// When true, allows matching and substituting embedded identifiers within compound tokens (e.g. 'prefix._var.suffix'). Defaults to true.
    /// </summary>
    public bool MatchPartials { get; set; } = true;

    /// <summary>
    /// When true, splits whitespace-separated values from full-token variable matches into individual tokens. Defaults to true.
    /// </summary>
    public bool SplitAfterGetByKey { get; set; } = true;

    /// <summary>
    /// Optional explicit path to a settings JSON file.
    /// </summary>
    public string? SettingsPath { get; set; }

    /// <summary>
    /// List of file paths to JSON value maps to be loaded into the known-values store.
    /// </summary>
    public List<string> ValueFiles { get; set; } = new();

    /// <summary>
    /// Inline known-values dictionary loaded directly from settings.
    /// </summary>
    public Dictionary<string, string> ValueStore { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// List of file paths to JSON macro maps to be loaded into the baseline macro store.
    /// </summary>
    public List<string> MacroFiles { get; set; } = new();

    /// <summary>
    /// Inline baseline macro dictionary loaded directly from settings.
    /// </summary>
    public Dictionary<string, string> MacroStore { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a deep clone of the current settings.
    /// </summary>
    public CLIExpandSettings Clone()
    {
        return new CLIExpandSettings
        {
            Mode = this.Mode,
            ExpandStartToken = this.ExpandStartToken,
            ExpandEndToken = this.ExpandEndToken,
            DelimiterToken = this.DelimiterToken,
            JoinStartToken = this.JoinStartToken,
            JoinEndToken = this.JoinEndToken,
            GetToken = this.GetToken,
            SplitGetToken = this.SplitGetToken,
            MatchPartials = this.MatchPartials,
            SplitAfterGetByKey = this.SplitAfterGetByKey,
            SettingsPath = this.SettingsPath,
            ValueFiles = new List<string>(this.ValueFiles),
            ValueStore = new Dictionary<string, string>(this.ValueStore, StringComparer.Ordinal),
            MacroFiles = new List<string>(this.MacroFiles),
            MacroStore = new Dictionary<string, string>(this.MacroStore, StringComparer.Ordinal)
        };
    }
}
