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

        [JsonPropertyName("join")]
        public string? Join { get; set; }

        [JsonPropertyName("joinStart")]
        public string? JoinStart { get; set; }

        [JsonPropertyName("joinStartToken")]
        public string? JoinStartToken { get; set; }

        [JsonPropertyName("joinEnd")]
        public string? JoinEnd { get; set; }

        [JsonPropertyName("joinEndToken")]
        public string? JoinEndToken { get; set; }

        [JsonPropertyName("get")]
        public string? Get { get; set; }

        [JsonPropertyName("getToken")]
        public string? GetToken { get; set; }

        [JsonPropertyName("splitGet")]
        public string? SplitGet { get; set; }

        [JsonPropertyName("splitGetToken")]
        public string? SplitGetToken { get; set; }

        [JsonPropertyName("matchPartials")]
        public bool? MatchPartials { get; set; }

        [JsonPropertyName("splitAfterGetByKey")]
        public bool? SplitAfterGetByKey { get; set; }

        [JsonPropertyName("values")]
        public JsonElement? Values { get; set; }

        [JsonPropertyName("valueFiles")]
        public JsonElement? ValueFiles { get; set; }

        [JsonPropertyName("valueStore")]
        public Dictionary<string, string>? ValueStore { get; set; }

        [JsonPropertyName("macros")]
        public JsonElement? Macros { get; set; }

        [JsonPropertyName("macroFiles")]
        public JsonElement? MacroFiles { get; set; }

        [JsonPropertyName("macroStore")]
        public Dictionary<string, string>? MacroStore { get; set; }
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
            string baseDir = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? Directory.GetCurrentDirectory();
            return TryLoadFromJson(json, target, out errorMessage, filePath, baseDir);
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
    public static bool TryLoadFromJson(string json, CLIExpandSettings target, out string? errorMessage, string? sourceIdentifier = null, string? baseDirectory = null)
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

        string? joinStart = dto.Join ?? dto.JoinStart ?? dto.JoinStartToken;
        if (joinStart != null)
            target.JoinStartToken = joinStart;

        string? joinEnd = dto.JoinEnd ?? dto.JoinEndToken;
        if (joinEnd != null)
            target.JoinEndToken = joinEnd;

        string? get = dto.Get ?? dto.GetToken;
        if (get != null)
            target.GetToken = get;

        string? splitGet = dto.SplitGet ?? dto.SplitGetToken;
        if (splitGet != null)
            target.SplitGetToken = splitGet;

        if (dto.MatchPartials.HasValue)
            target.MatchPartials = dto.MatchPartials.Value;

        if (dto.SplitAfterGetByKey.HasValue)
            target.SplitAfterGetByKey = dto.SplitAfterGetByKey.Value;

        void ProcessValuesElement(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        string path = item.GetString()!;
                        string resolved = !string.IsNullOrEmpty(baseDirectory) && !Path.IsPathRooted(path)
                            ? Path.GetFullPath(Path.Combine(baseDirectory, path))
                            : Path.GetFullPath(path);
                        target.ValueFiles.Add(resolved);
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                string path = element.GetString()!;
                string resolved = !string.IsNullOrEmpty(baseDirectory) && !Path.IsPathRooted(path)
                    ? Path.GetFullPath(Path.Combine(baseDirectory, path))
                    : Path.GetFullPath(path);
                target.ValueFiles.Add(resolved);
            }
        }

        if (dto.Values.HasValue)
            ProcessValuesElement(dto.Values.Value);

        if (dto.ValueFiles.HasValue)
            ProcessValuesElement(dto.ValueFiles.Value);

        if (dto.ValueStore != null)
        {
            foreach (var kvp in dto.ValueStore)
            {
                target.ValueStore[kvp.Key] = kvp.Value;
            }
        }

        void ProcessMacroFilesElement(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        string path = item.GetString()!;
                        string resolved = !string.IsNullOrEmpty(baseDirectory) && !Path.IsPathRooted(path)
                            ? Path.GetFullPath(Path.Combine(baseDirectory, path))
                            : Path.GetFullPath(path);
                        target.MacroFiles.Add(resolved);
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                string path = element.GetString()!;
                string resolved = !string.IsNullOrEmpty(baseDirectory) && !Path.IsPathRooted(path)
                    ? Path.GetFullPath(Path.Combine(baseDirectory, path))
                    : Path.GetFullPath(path);
                target.MacroFiles.Add(resolved);
            }
        }

        if (dto.Macros.HasValue)
            ProcessMacroFilesElement(dto.Macros.Value);

        if (dto.MacroFiles.HasValue)
            ProcessMacroFilesElement(dto.MacroFiles.Value);

        if (dto.MacroStore != null)
        {
            foreach (var kvp in dto.MacroStore)
            {
                target.MacroStore[kvp.Key] = kvp.Value;
            }
        }

        return true;
    }

    /// <summary>
    /// Loads a flat JSON macro-map file into <paramref name="targetStore"/>.
    /// </summary>
    /// <param name="filePath">The path to the JSON macro-map file.</param>
    /// <param name="targetStore">The target dictionary receiving the key-value mappings.</param>
    /// <param name="errorMessage">Error diagnostic message if loading failed.</param>
    /// <returns>True if loaded successfully; otherwise false.</returns>
    public static bool TryLoadMacroMapFile(string filePath, IDictionary<string, string> targetStore, out string? errorMessage)
    {
        errorMessage = null;

        if (!File.Exists(filePath))
        {
            errorMessage = $"Macro map file not found: {filePath}";
            return false;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            return TryLoadMacroMapFromJson(json, targetStore, out errorMessage, filePath);
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to read macro map file '{filePath}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Deserializes a flat JSON macro-map string into <paramref name="targetStore"/>.
    /// Validates that all values are strings.
    /// </summary>
    /// <param name="json">The JSON text to deserialize.</param>
    /// <param name="targetStore">The target dictionary receiving the key-value mappings.</param>
    /// <param name="errorMessage">Error diagnostic message if parsing or validation failed.</param>
    /// <param name="sourceIdentifier">Optional source file name/identifier for diagnostic messages.</param>
    /// <returns>True if deserialized successfully; otherwise false.</returns>
    public static bool TryLoadMacroMapFromJson(string json, IDictionary<string, string> targetStore, out string? errorMessage, string? sourceIdentifier = null)
    {
        errorMessage = null;
        string sourceDesc = sourceIdentifier != null ? $" in '{sourceIdentifier}'" : string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                errorMessage = $"Invalid macro map JSON{sourceDesc}: Root element must be a JSON object.";
                return false;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.String)
                {
                    errorMessage = $"Invalid macro map JSON{sourceDesc}: Property '{prop.Name}' must have a string value, but found {prop.Value.ValueKind.ToString().ToLowerInvariant()}.";
                    return false;
                }

                targetStore[prop.Name] = prop.Value.GetString()!;
            }

            return true;
        }
        catch (JsonException ex)
        {
            errorMessage = $"Failed to parse macro map JSON{sourceDesc}: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Loads a flat JSON value-map file into <paramref name="targetStore"/>.
    /// </summary>
    /// <param name="filePath">The path to the JSON value-map file.</param>
    /// <param name="targetStore">The target dictionary receiving the key-value mappings.</param>
    /// <param name="errorMessage">Error diagnostic message if loading failed.</param>
    /// <returns>True if loaded successfully; otherwise false.</returns>
    public static bool TryLoadValueMapFile(string filePath, IDictionary<string, string> targetStore, out string? errorMessage)
    {
        errorMessage = null;

        if (!File.Exists(filePath))
        {
            errorMessage = $"Value map file not found: {filePath}";
            return false;
        }

        try
        {
            string json = File.ReadAllText(filePath);
            return TryLoadValueMapFromJson(json, targetStore, out errorMessage, filePath);
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to read value map file '{filePath}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Deserializes a flat JSON value-map string into <paramref name="targetStore"/>.
    /// Validates that all values are strings.
    /// </summary>
    /// <param name="json">The JSON text to deserialize.</param>
    /// <param name="targetStore">The target dictionary receiving the key-value mappings.</param>
    /// <param name="errorMessage">Error diagnostic message if parsing or validation failed.</param>
    /// <param name="sourceIdentifier">Optional source file name/identifier for diagnostic messages.</param>
    /// <returns>True if deserialized successfully; otherwise false.</returns>
    public static bool TryLoadValueMapFromJson(string json, IDictionary<string, string> targetStore, out string? errorMessage, string? sourceIdentifier = null)
    {
        errorMessage = null;
        string sourceDesc = sourceIdentifier != null ? $" in '{sourceIdentifier}'" : string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                errorMessage = $"Invalid value map JSON{sourceDesc}: Root element must be a JSON object.";
                return false;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.String)
                {
                    errorMessage = $"Invalid value map JSON{sourceDesc}: Property '{prop.Name}' must have a string value, but found {prop.Value.ValueKind.ToString().ToLowerInvariant()}.";
                    return false;
                }

                targetStore[prop.Name] = prop.Value.GetString()!;
            }

            return true;
        }
        catch (JsonException ex)
        {
            errorMessage = $"Failed to parse value map JSON{sourceDesc}: {ex.Message}";
            return false;
        }
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
