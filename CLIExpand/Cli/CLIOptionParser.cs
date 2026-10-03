using CLIExpand.Config;
using CLIExpand.Rendering;

namespace CLIExpand.Cli;

/// <summary>
/// Parses command-line arguments for CLIExpand, enforcing the hard '--' boundary
/// between configuration options and raw literal payload tokens.
/// </summary>
public static class CLIOptionParser
{
    /// <summary>
    /// Parses the command-line argument array into a <see cref="ParseResult"/>.
    /// </summary>
    /// <param name="args">The argument array passed to the process.</param>
    /// <returns>A <see cref="ParseResult"/> containing options and payload.</returns>
    public static ParseResult Parse(string[] args)
    {
        var result = new ParseResult();

        if (args == null || args.Length == 0)
        {
            result.HasErrors = true;
            result.ErrorMessage = "Missing '--' payload separator. Usage: CLIExpand [options] -- <payload tokens...>";
            return result;
        }

        int separatorIndex = Array.IndexOf(args, "--");

        // Check if help was requested before '--' or as the only argument
        for (int i = 0; i < (separatorIndex >= 0 ? separatorIndex : args.Length); i++)
        {
            string arg = args[i].ToLowerInvariant();
            if (arg is "-h" or "--h" or "-help" or "--help" or "help" or "-?" or "--?" or "/?" or "/h" or "/help")
            {
                result.HelpRequested = true;
                return result;
            }
        }

        // Check if -list-values or -list-macros was requested
        for (int i = 0; i < (separatorIndex >= 0 ? separatorIndex : args.Length); i++)
        {
            string arg = args[i].ToLowerInvariant();
            if (arg is "-list-values" or "--list-values" or "-list" or "--list")
            {
                result.ListValuesRequested = true;
            }
            else if (arg is "-list-macros" or "--list-macros" or "-list-macro" or "--list-macro")
            {
                result.ListMacrosRequested = true;
            }
        }

        if (separatorIndex == -1 && !result.ListValuesRequested && !result.ListMacrosRequested)
        {
            result.HasErrors = true;
            result.ErrorMessage = "Missing '--' payload separator. Usage: CLIExpand [options] -- <payload tokens...>";
            return result;
        }

        int optionsLimit = separatorIndex >= 0 ? separatorIndex : args.Length;

        // Parse options before '--' (or all options if -list-values is active without '--')
        for (int i = 0; i < optionsLimit; i++)
        {
            string arg = args[i];

            switch (arg.ToLowerInvariant())
            {
                case "-mode" or "--mode":
                    if (i + 1 >= optionsLimit)
                    {
                        result.HasErrors = true;
                        result.ErrorMessage = $"Missing value for option '{arg}'.";
                        return result;
                    }
                    i++;
                    string modeStr = args[i];
                    if (SettingsLoader.TryParseShellMode(modeStr, out var mode))
                    {
                        result.Mode = mode;
                    }
                    else
                    {
                        result.HasErrors = true;
                        result.ErrorMessage = $"Invalid shell mode '{modeStr}'. Supported modes: bash, ps, cmd, raw.";
                        return result;
                    }
                    break;

                case "-begin" or "--begin" or "-start" or "--start":
                    if (i + 1 >= optionsLimit)
                    {
                        result.HasErrors = true;
                        result.ErrorMessage = $"Missing value for option '{arg}'.";
                        return result;
                    }
                    i++;
                    result.ExpandStartToken = args[i];
                    break;

                case "-end" or "--end":
                    if (i + 1 >= optionsLimit)
                    {
                        result.HasErrors = true;
                        result.ErrorMessage = $"Missing value for option '{arg}'.";
                        return result;
                    }
                    i++;
                    result.ExpandEndToken = args[i];
                    break;

                case "-delim" or "--delim" or "-delimiter" or "--delimiter":
                    if (i + 1 >= optionsLimit)
                    {
                        result.HasErrors = true;
                        result.ErrorMessage = $"Missing value for option '{arg}'.";
                        return result;
                    }
                    i++;
                    result.DelimiterToken = args[i];
                    break;

                case "-join" or "--join" or "-join-start" or "--join-start" or "-joinstart" or "--joinstart":
                    if (i + 1 >= optionsLimit)
                    {
                        result.HasErrors = true;
                        result.ErrorMessage = $"Missing value for option '{arg}'.";
                        return result;
                    }
                    i++;
                    result.JoinStartToken = args[i];
                    break;

                case "-join-end" or "--join-end" or "-joinend" or "--joinend":
                    if (i + 1 >= optionsLimit)
                    {
                        result.HasErrors = true;
                        result.ErrorMessage = $"Missing value for option '{arg}'.";
                        return result;
                    }
                    i++;
                    result.JoinEndToken = args[i];
                    break;

                case "-get" or "--get" or "-get-token" or "--get-token":
                    if (i + 1 >= optionsLimit)
                    {
                        result.HasErrors = true;
                        result.ErrorMessage = $"Missing value for option '{arg}'.";
                        return result;
                    }
                    i++;
                    result.GetToken = args[i];
                    break;

                case "-split-get" or "--split-get" or "-splitget" or "--splitget" or "-split-get-token" or "--split-get-token":
                    if (i + 1 >= optionsLimit)
                    {
                        result.HasErrors = true;
                        result.ErrorMessage = $"Missing value for option '{arg}'.";
                        return result;
                    }
                    i++;
                    result.SplitGetToken = args[i];
                    break;

                case "-values" or "--values" or "-value" or "--value":
                    if (i + 1 >= optionsLimit)
                    {
                        result.HasErrors = true;
                        result.ErrorMessage = $"Missing value for option '{arg}'.";
                        return result;
                    }
                    i++;
                    result.ValueFiles.Add(args[i]);
                    break;

                case "-macros" or "--macros":
                    if (i + 1 >= optionsLimit)
                    {
                        result.HasErrors = true;
                        result.ErrorMessage = $"Missing value for option '{arg}'.";
                        return result;
                    }
                    i++;
                    result.MacroFiles.Add(args[i]);
                    break;

                case "-macro" or "--macro":
                    if (i + 1 >= optionsLimit)
                    {
                        result.HasErrors = true;
                        result.ErrorMessage = $"Missing value for option '{arg}'.";
                        return result;
                    }
                    i++;
                    string macroDef = args[i];
                    int eqIdx = macroDef.IndexOf('=');
                    if (eqIdx <= 0)
                    {
                        result.HasErrors = true;
                        result.ErrorMessage = $"Invalid macro definition '{macroDef}'. Expected format: -macro <key>=<value>";
                        return result;
                    }
                    string macroKey = macroDef.Substring(0, eqIdx);
                    string macroVal = macroDef.Substring(eqIdx + 1);
                    result.InlineMacros[macroKey] = macroVal;
                    break;

                case "-list-values" or "--list-values" or "-list" or "--list":
                    result.ListValuesRequested = true;
                    break;

                case "-list-macros" or "--list-macros" or "-list-macro" or "--list-macro":
                    result.ListMacrosRequested = true;
                    break;

                case "-no-partials" or "--no-partials":
                    result.MatchPartials = false;
                    break;

                case "-partials" or "--partials":
                    result.MatchPartials = true;
                    break;

                case "-no-split" or "--no-split":
                    result.SplitAfterGetByKey = false;
                    break;

                case "-split" or "--split":
                    result.SplitAfterGetByKey = true;
                    break;

                case "-h" or "--h" or "-help" or "--help" or "help" or "-?" or "--?" or "/?" or "/h" or "/help":
                    result.HelpRequested = true;
                    return result;

                case "-settings" or "--settings":
                    if (i + 1 >= optionsLimit)
                    {
                        result.HasErrors = true;
                        result.ErrorMessage = $"Missing value for option '{arg}'.";
                        return result;
                    }
                    i++;
                    result.SettingsPath = args[i];
                    break;

                default:
                    result.HasErrors = true;
                    result.ErrorMessage = $"Unrecognized option '{arg}'. Usage: CLIExpand [options] -- <payload tokens...>";
                    return result;
            }
        }

        if (separatorIndex >= 0)
        {
            // Add everything after '--' as literal payload tokens
            for (int i = separatorIndex + 1; i < args.Length; i++)
            {
                result.Payload.Add(args[i]);
            }

            if (result.Payload.Count == 0 && !result.ListValuesRequested && !result.ListMacrosRequested)
            {
                result.HasErrors = true;
                result.ErrorMessage = "No payload tokens provided after '--'.";
                return result;
            }
        }

        return result;
    }
}
