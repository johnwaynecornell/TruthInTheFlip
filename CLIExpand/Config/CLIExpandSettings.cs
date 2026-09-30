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
            MatchPartials = this.MatchPartials,
            SplitAfterGetByKey = this.SplitAfterGetByKey,
            SettingsPath = this.SettingsPath
        };
    }
}
