using CLIExpanderNs;
using CLIExpand.Cli;
using CLIExpand.Config;
using CLIExpand.Rendering;

namespace CLIExpand;

/// <summary>
/// CLIExpand entry point and host execution pipeline.
/// </summary>
public static class Program
{
    /// <summary>
    /// Process entry point.
    /// </summary>
    public static int Main(string[] args)
    {
        return Run(args, Console.Out, Console.Error);
    }

    /// <summary>
    /// Executes the CLIExpand pipeline with custom IO streams and optional settings path override.
    /// </summary>
    /// <param name="args">Command-line argument tokens.</param>
    /// <param name="stdout">Output text writer for rendered command lines.</param>
    /// <param name="stderr">Diagnostic text writer for error messages.</param>
    /// <param name="defaultSettingsOverridePath">Optional override for the default user settings path (used in testing).</param>
    /// <returns>Exit code (0 for success, non-zero for failure).</returns>
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, string? defaultSettingsOverridePath = null)
    {
        var parseResult = CLIOptionParser.Parse(args);

        if (parseResult.HelpRequested)
        {
            PrintUsage(stderr);
            return 0;
        }

        if (parseResult.HasErrors)
        {
            stderr.WriteLine(parseResult.ErrorMessage);
            return 1;
        }

        // Precedence Tier 1: Built-in defaults
        var settings = new CLIExpandSettings();

        // Precedence Tier 2: Default user settings file
        string? userSettingsPath = defaultSettingsOverridePath ?? SettingsLoader.GetDefaultUserSettingsPath();
        if (userSettingsPath != null && File.Exists(userSettingsPath))
        {
            if (!SettingsLoader.TryLoadFromFile(userSettingsPath, settings, out var userErr))
            {
                stderr.WriteLine(userErr);
                return 1;
            }
        }

        // Precedence Tier 3: Explicit -settings <path>
        if (!string.IsNullOrWhiteSpace(parseResult.SettingsPath))
        {
            if (!SettingsLoader.TryLoadFromFile(parseResult.SettingsPath, settings, out var explicitErr))
            {
                stderr.WriteLine(explicitErr);
                return 1;
            }
        }

        // Precedence Tier 4: Explicit CLI option overrides
        if (parseResult.Mode.HasValue)
            settings.Mode = parseResult.Mode.Value;

        if (parseResult.ExpandStartToken != null)
            settings.ExpandStartToken = parseResult.ExpandStartToken;

        if (parseResult.ExpandEndToken != null)
            settings.ExpandEndToken = parseResult.ExpandEndToken;

        if (parseResult.DelimiterToken != null)
            settings.DelimiterToken = parseResult.DelimiterToken;

        if (parseResult.MatchPartials.HasValue)
            settings.MatchPartials = parseResult.MatchPartials.Value;

        if (parseResult.SplitAfterGetByKey.HasValue)
            settings.SplitAfterGetByKey = parseResult.SplitAfterGetByKey.Value;

        // Instantiate and configure CLIExpander
        var expander = new CLIExpander
        {
            ExpandStartToken = settings.ExpandStartToken,
            ExpandEndToken = settings.ExpandEndToken,
            DelimiterToken = settings.DelimiterToken,
            MatchPartials = settings.MatchPartials,
            SplitAfterGetByKey = settings.SplitAfterGetByKey
        };

        var expansionStatus = CLIExpander.Process(parseResult.Payload, out var outputTokens, () => expander);
        if (expansionStatus.Status != 0)
        {
            stderr.WriteLine(expansionStatus.Message ?? "CLIExpand: Expansion failed");
            return expansionStatus.Status;
        }

        var renderer = CLIArgumentRendererFactory.Create(settings.Mode);
        string rendered = renderer.Render(outputTokens);
        stdout.WriteLine(rendered);

        return 0;
    }

    private static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: CLIExpand [options] -- <payload tokens...>");
        writer.WriteLine();
        writer.WriteLine("Options:");
        writer.WriteLine("  -mode <bash|ps|cmd|raw>   Target shell rendering mode (default: raw)");
        writer.WriteLine("  -begin <token>            Block expansion start token (default: .expand.)");
        writer.WriteLine("  -end <token>              Block expansion end token (default: .expand_end.)");
        writer.WriteLine("  -delim <token>            Alternatives delimiter token (default: :)");
        writer.WriteLine("  -no-partials              Disable partial inner-token substitutions");
        writer.WriteLine("  -partials                 Enable partial inner-token substitutions (default)");
        writer.WriteLine("  -no-split                 Disable whitespace splitting on full token match");
        writer.WriteLine("  -split                    Enable whitespace splitting on full token match (default)");
        writer.WriteLine("  -settings <path>          Path to JSON configuration file");
        writer.WriteLine("  -h, -help, --help, -?     Show this help information");
    }
}
