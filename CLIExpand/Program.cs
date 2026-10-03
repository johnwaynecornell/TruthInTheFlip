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
        var expander = new CLIExpander();

        // Precedence Tier 2: Default user settings file
        string? userSettingsPath = defaultSettingsOverridePath ?? SettingsLoader.GetDefaultUserSettingsPath();
        if (userSettingsPath != null && File.Exists(userSettingsPath))
        {
            var userSettings = new CLIExpandSettings();
            if (!SettingsLoader.TryLoadFromFile(userSettingsPath, userSettings, out var userErr))
            {
                stderr.WriteLine(userErr);
                return 1;
            }

            foreach (var valFile in userSettings.ValueFiles)
            {
                if (!SettingsLoader.TryLoadValueMapFile(valFile, expander.ValueStore, out var valErr))
                {
                    stderr.WriteLine(valErr);
                    return 1;
                }
            }
            foreach (var kvp in userSettings.ValueStore)
            {
                expander.ValueStore[kvp.Key] = kvp.Value;
            }

            foreach (var macroFile in userSettings.MacroFiles)
            {
                if (!SettingsLoader.TryLoadMacroMapFile(macroFile, expander.MacroStore, out var macroErr))
                {
                    stderr.WriteLine(macroErr);
                    return 1;
                }
            }
            foreach (var kvp in userSettings.MacroStore)
            {
                expander.MacroStore[kvp.Key] = kvp.Value;
            }

            settings = userSettings;
        }

        // Precedence Tier 3: Explicit -settings <path>
        if (!string.IsNullOrWhiteSpace(parseResult.SettingsPath))
        {
            var explicitSettings = settings.Clone();
            explicitSettings.ValueFiles.Clear();
            explicitSettings.ValueStore.Clear();
            explicitSettings.MacroFiles.Clear();
            explicitSettings.MacroStore.Clear();
            if (!SettingsLoader.TryLoadFromFile(parseResult.SettingsPath, explicitSettings, out var explicitErr))
            {
                stderr.WriteLine(explicitErr);
                return 1;
            }

            foreach (var valFile in explicitSettings.ValueFiles)
            {
                if (!SettingsLoader.TryLoadValueMapFile(valFile, expander.ValueStore, out var valErr))
                {
                    stderr.WriteLine(valErr);
                    return 1;
                }
            }
            foreach (var kvp in explicitSettings.ValueStore)
            {
                expander.ValueStore[kvp.Key] = kvp.Value;
            }

            foreach (var macroFile in explicitSettings.MacroFiles)
            {
                if (!SettingsLoader.TryLoadMacroMapFile(macroFile, expander.MacroStore, out var macroErr))
                {
                    stderr.WriteLine(macroErr);
                    return 1;
                }
            }
            foreach (var kvp in explicitSettings.MacroStore)
            {
                expander.MacroStore[kvp.Key] = kvp.Value;
            }

            settings = explicitSettings;
        }

        // Precedence Tier 4: CLI -values and -macros files in command-line order
        foreach (var cliValFile in parseResult.ValueFiles)
        {
            string resolvedCliValFile = Path.GetFullPath(cliValFile);
            if (!SettingsLoader.TryLoadValueMapFile(resolvedCliValFile, expander.ValueStore, out var valErr))
            {
                stderr.WriteLine(valErr);
                return 1;
            }
        }

        foreach (var cliMacroFile in parseResult.MacroFiles)
        {
            string resolvedCliMacroFile = Path.GetFullPath(cliMacroFile);
            if (!SettingsLoader.TryLoadMacroMapFile(resolvedCliMacroFile, expander.MacroStore, out var macroErr))
            {
                stderr.WriteLine(macroErr);
                return 1;
            }
        }

        foreach (var kvp in parseResult.InlineMacros)
        {
            expander.MacroStore[kvp.Key] = kvp.Value;
        }

        if (parseResult.ListValuesRequested)
        {
            PrintActiveValues(stdout, expander.ValueStore);
            return 0;
        }

        if (parseResult.ListMacrosRequested)
        {
            PrintActiveMacros(stdout, expander.MacroStore);
            return 0;
        }

        // Precedence Tier 4 (cont.): Explicit CLI option overrides
        if (parseResult.Mode.HasValue)
            settings.Mode = parseResult.Mode.Value;

        if (parseResult.ExpandStartToken != null)
            settings.ExpandStartToken = parseResult.ExpandStartToken;

        if (parseResult.ExpandEndToken != null)
            settings.ExpandEndToken = parseResult.ExpandEndToken;

        if (parseResult.DelimiterToken != null)
            settings.DelimiterToken = parseResult.DelimiterToken;

        if (parseResult.JoinStartToken != null)
            settings.JoinStartToken = parseResult.JoinStartToken;

        if (parseResult.JoinEndToken != null)
            settings.JoinEndToken = parseResult.JoinEndToken;

        if (parseResult.GetToken != null)
            settings.GetToken = parseResult.GetToken;

        if (parseResult.SplitGetToken != null)
            settings.SplitGetToken = parseResult.SplitGetToken;

        if (parseResult.MatchPartials.HasValue)
            settings.MatchPartials = parseResult.MatchPartials.Value;

        if (parseResult.SplitAfterGetByKey.HasValue)
            settings.SplitAfterGetByKey = parseResult.SplitAfterGetByKey.Value;

        // Configure CLIExpander instance
        expander.ExpandStartToken = settings.ExpandStartToken;
        expander.ExpandEndToken = settings.ExpandEndToken;
        expander.DelimiterToken = settings.DelimiterToken;
        expander.JoinStartToken = settings.JoinStartToken;
        expander.JoinEndToken = settings.JoinEndToken;
        expander.GetToken = settings.GetToken;
        expander.SplitGetToken = settings.SplitGetToken;
        expander.MatchPartials = settings.MatchPartials;
        expander.SplitAfterGetByKey = settings.SplitAfterGetByKey;

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

    private static void PrintActiveValues(TextWriter writer, IDictionary<string, string> valueStore)
    {
        writer.WriteLine($"Active Known Values ({valueStore.Count} keys):");
        int maxKeyLen = valueStore.Keys.Count > 0 ? valueStore.Keys.Max(k => k.Length) : 0;
        maxKeyLen = Math.Max(maxKeyLen, 10);
        foreach (var kvp in valueStore.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            string displayVal = kvp.Value
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
            writer.WriteLine($"  {kvp.Key.PadRight(maxKeyLen)} = {displayVal}");
        }
    }

    private static void PrintActiveMacros(TextWriter writer, IDictionary<string, string> macroStore)
    {
        writer.WriteLine($"Active Macros ({macroStore.Count} keys):");
        int maxKeyLen = macroStore.Keys.Count > 0 ? macroStore.Keys.Max(k => k.Length) : 0;
        maxKeyLen = Math.Max(maxKeyLen, 10);
        foreach (var kvp in macroStore.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            string displayVal = kvp.Value
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
            writer.WriteLine($"  {kvp.Key.PadRight(maxKeyLen)} = {displayVal}");
        }
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
        writer.WriteLine("  -join <token>             Block join start token (default: .join.)");
        writer.WriteLine("  -join-end <token>         Block join end token (default: .join_end.)");
        writer.WriteLine("  -get <token>              Known value retrieval token (default: .get.)");
        writer.WriteLine("  -split-get <token>        Known value whitespace-split retrieval token (default: .split_get.)");
        writer.WriteLine("  -values <path>            Path to JSON value map file (can be repeated)");
        writer.WriteLine("  -list-values              List all active known values (built-ins + loaded maps) and exit");
        writer.WriteLine("  -macros <path>            Path to JSON macro map file (can be repeated)");
        writer.WriteLine("  -macro <key>=<value>      Inline baseline macro definition (can be repeated)");
        writer.WriteLine("  -list-macros              List all active baseline macros and exit");
        writer.WriteLine("  -no-partials              Disable partial inner-token substitutions");
        writer.WriteLine("  -partials                 Enable partial inner-token substitutions (default)");
        writer.WriteLine("  -no-split                 Disable whitespace splitting on full token match");
        writer.WriteLine("  -split                    Enable whitespace splitting on full token match (default)");
        writer.WriteLine("  -settings <path>          Path to JSON configuration file");
        writer.WriteLine("  -h, -help, --help, -?     Show this help information");
        writer.WriteLine();
        writer.WriteLine("Built-in Known Values (.get. <key>):");
        writer.WriteLine("  newline       Platform-native line break (Environment.NewLine)");
        writer.WriteLine("  space         Single space character (\" \")");
        writer.WriteLine("  tab           Horizontal tab (\"\\t\")");
        writer.WriteLine("  empty         Empty string (\"\")");
        writer.WriteLine("  now           Instance local timestamp in ISO-8601 round-trip format");
        writer.WriteLine("  utc_now       Instance UTC timestamp in ISO-8601 round-trip format");
        writer.WriteLine("  timestamp     Filename-safe local timestamp (_yyyyMMdd_HHmmss_ff)");
        writer.WriteLine("  utimestamp    Filename-safe UTC timestamp (_yyyyMMdd_HHmmss_ff)");
        writer.WriteLine("  dir_sep       Platform directory separator (/ or \\)");
        writer.WriteLine("  path_sep      Platform path list separator (: or ;)");
    }
}
