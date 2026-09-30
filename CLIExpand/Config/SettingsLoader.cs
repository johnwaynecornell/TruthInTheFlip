using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using CLIExpand.Rendering;

namespace CLIExpand.Config;

/// <summary>
/// Loads and resolves CLIExpand configuration settings across defaults, user settings files, and explicit overrides.
/// </summary>
public static class SettingsLoader
{
    private class JsonSettingsDto
    {
        [JsonPropertyName("mode")]
        public string? Mode { get; set; }

        [JsonPropertyName("begin")]
        public string? Begin { get; set; }

        [JsonPropertyName("expandStartToken")]
        public string? ExpandStartToken { get; set; }

        [JsonPropertyName("end")]
        public string? End { get; set; }

        [JsonPropertyName("expandEndToken")]
        public string? ExpandEndToken { get; set; }

        [JsonPropertyName("delim")]
        public string? Delim { get; set; }

        [JsonPropertyName("delimiter")]
        public string? Delimiter { get; set; }

        [JsonPropertyName("delimiterToken")]
        public string? DelimiterToken { get; set; }

        [JsonPropertyName("matchPartials")]
        public bool? MatchPartials { get; set; }

        [JsonPropertyName("splitAfterGetByKey")]
        public bool? SplitAfterGetByKey { get; set; }
    }

    /// <summary>
    /// Gets the standard platform-specific user settings file path if found, or null if no standard settings file exists.
    /// </summary>
    public static string? GetDefaultUserSettingsPath()
    {
        var candidates = GetDefaultUserSettingsCandidatePaths();
        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    /// <summary>
    /// Returns the candidate standard user settings file paths for the current operating system.
    /// </summary>
    public static IReadOnlyList<string> GetDefaultUserSettingsCandidatePaths()
    {
        var paths = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
            {
                paths.Add(Path.Combine(appData, "CLIExpand", "settings.json"));
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                paths.Add(Path.Combine(userProfile, "Library", "Application Support", "CLIExpand", "settings.json"));
                paths.Add(Path.Combine(userProfile, ".config", "CLIExpand", "settings.json"));
            }
        }
        else // Linux & others
        {
            string? xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (!string.IsNullOrEmpty(xdgConfig))
            {
                paths.Add(Path.Combine(xdgConfig, "CLIExpand", "settings.json"));
            }

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                paths.Add(Path.Combine(userProfile, ".config", "CLIExpand", "settings.json"));
            }
        }

        return paths;
    }

    /// <summary>
    /// Loads settings from a JSON file, applying values onto <paramref name="target"/>.
    /// </summary>
    /// <param name="filePath">The path to the settings JSON file.</param>
    /// <param name="target">The settings object to populate.</param>
    /// <param name="errorMessage">Error diagnostic message if loading failed.</param>
    /// <returns>True if loaded successfully; otherwise false.</returns>
    public static bool TryLoadFromFile(string filePath, CLIExpandSettings target, out string? errorMessage)
    {
        errorMessage = null;

        if (!File.Exists(filePath))
        {
            errorMessage = $"Settings file not found: {filePath}";
            return false;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            return TryLoadFromJson(json, target, out errorMessage, filePath);
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to read settings file '{filePath}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Deserializes settings from a JSON string and updates <paramref name="target"/>.
    /// </summary>
    public static bool TryLoadFromJson(string json, CLIExpandSettings target, out string? errorMessage, string? sourceIdentifier = null)
    {
        errorMessage = null;
        string sourceDesc = sourceIdentifier != null ? $" in '{sourceIdentifier}'" : string.Empty;

        JsonSettingsDto? dto;
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            dto = JsonSerializer.Deserialize<JsonSettingsDto>(json, options);
        }
        catch (JsonException ex)
        {
            errorMessage = $"Failed to parse settings JSON{sourceDesc}: {ex.Message}";
            return false;
        }

        if (dto == null)
            return true;

        if (!string.IsNullOrWhiteSpace(dto.Mode))
        {
            if (TryParseShellMode(dto.Mode, out var mode))
            {
                target.Mode = mode;
            }
            else
            {
                errorMessage = $"Invalid shell mode '{dto.Mode}'{sourceDesc}. Supported modes: bash, ps, cmd, raw.";
                return false;
            }
        }

        string? begin = dto.Begin ?? dto.ExpandStartToken;
        if (begin != null)
            target.ExpandStartToken = begin;

        string? end = dto.End ?? dto.ExpandEndToken;
        if (end != null)
            target.ExpandEndToken = end;

        string? delim = dto.Delim ?? dto.Delimiter ?? dto.DelimiterToken;
        if (delim != null)
            target.DelimiterToken = delim;

        if (dto.MatchPartials.HasValue)
            target.MatchPartials = dto.MatchPartials.Value;

        if (dto.SplitAfterGetByKey.HasValue)
            target.SplitAfterGetByKey = dto.SplitAfterGetByKey.Value;

        return true;
    }

    /// <summary>
    /// Parses a shell mode name in a case-insensitive manner, accepting aliases (e.g. 'ps' or 'powershell').
    /// </summary>
    public static bool TryParseShellMode(string modeStr, out ShellMode mode)
    {
        switch (modeStr.Trim().ToLowerInvariant())
        {
            case "bash":
            case "sh":
            case "posix":
                mode = ShellMode.Bash;
                return true;
            case "ps":
            case "powershell":
            case "pwsh":
                mode = ShellMode.PowerShell;
                return true;
            case "cmd":
            case "cmd.exe":
            case "bat":
            case "batch":
                mode = ShellMode.Cmd;
                return true;
            case "raw":
            case "none":
            case "plain":
                mode = ShellMode.Raw;
                return true;
            default:
                mode = default;
                return false;
        }
    }
}
