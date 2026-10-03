using CLIExpand;
using CLIExpand.Cli;
using CLIExpand.Config;
using CLIExpand.Rendering;
using Xunit;

namespace TruthInTheFlip.Farm.Tests;

public class CLIExpandTests
{
    #region 1. Option Parser Tests

    [Fact]
    public void OptionParser_BasicPayloadOnly_ExtractsPayloadSuccessfully()
    {
        string[] args = ["--", "echo", "hello", "world"];
        var result = CLIOptionParser.Parse(args);

        Assert.False(result.HasErrors);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(["echo", "hello", "world"], result.Payload);
    }

    [Fact]
    public void OptionParser_SwitchLikeTokensAfterBoundary_PreservedAsLiteralPayload()
    {
        string[] args = ["--", "-mode", "ps", "--begin", "foo", "-no-partials"];
        var result = CLIOptionParser.Parse(args);

        Assert.False(result.HasErrors);
        Assert.Null(result.Mode); // Mode option was not set before '--'
        Assert.Equal(["-mode", "ps", "--begin", "foo", "-no-partials"], result.Payload);
    }

    [Fact]
    public void OptionParser_MissingBoundary_ReturnsDescriptiveError()
    {
        string[] args = ["-mode", "bash", "echo", "hello"];
        var result = CLIOptionParser.Parse(args);

        Assert.True(result.HasErrors);
        Assert.Contains("Missing '--' payload separator", result.ErrorMessage);
        Assert.Empty(result.Payload);
    }

    [Fact]
    public void OptionParser_EmptyArgs_ReturnsDescriptiveError()
    {
        string[] args = [];
        var result = CLIOptionParser.Parse(args);

        Assert.True(result.HasErrors);
        Assert.Contains("Missing '--' payload separator", result.ErrorMessage);
    }

    [Fact]
    public void OptionParser_EmptyPayloadAfterBoundary_ReturnsDescriptiveError()
    {
        string[] args = ["-mode", "bash", "--"];
        var result = CLIOptionParser.Parse(args);

        Assert.True(result.HasErrors);
        Assert.Contains("No payload tokens provided after '--'", result.ErrorMessage);
    }

    [Fact]
    public void OptionParser_AllSupportedOptions_ParsesOverridesCorrectly()
    {
        string[] args =
        [
            "-mode", "ps",
            "-begin", "@expand",
            "-end", "@end",
            "-delim", "in",
            "-no-partials",
            "-no-split",
            "-settings", "custom.json",
            "--",
            "run", "payload"
        ];

        var result = CLIOptionParser.Parse(args);

        Assert.False(result.HasErrors);
        Assert.Equal(ShellMode.PowerShell, result.Mode);
        Assert.Equal("@expand", result.ExpandStartToken);
        Assert.Equal("@end", result.ExpandEndToken);
        Assert.Equal("in", result.DelimiterToken);
        Assert.False(result.MatchPartials);
        Assert.False(result.SplitAfterGetByKey);
        Assert.Equal("custom.json", result.SettingsPath);
        Assert.Equal(["run", "payload"], result.Payload);
    }

    [Fact]
    public void OptionParser_PositiveFlags_SetsFlagsToTrue()
    {
        string[] args =
        [
            "-partials",
            "-split",
            "--",
            "target"
        ];

        var result = CLIOptionParser.Parse(args);

        Assert.False(result.HasErrors);
        Assert.True(result.MatchPartials);
        Assert.True(result.SplitAfterGetByKey);
        Assert.Equal(["target"], result.Payload);
    }

    [Fact]
    public void OptionParser_AliasesAndDoubleDashes_ParsesCorrectly()
    {
        string[] args =
        [
            "--mode", "raw",
            "--start", "[expand]",
            "--delimiter", "->",
            "--settings", "my-settings.json",
            "--",
            "test"
        ];

        var result = CLIOptionParser.Parse(args);

        Assert.False(result.HasErrors);
        Assert.Equal(ShellMode.Raw, result.Mode);
        Assert.Equal("[expand]", result.ExpandStartToken);
        Assert.Equal("->", result.DelimiterToken);
        Assert.Equal("my-settings.json", result.SettingsPath);
    }

    [Fact]
    public void OptionParser_UnknownOption_ReturnsDescriptiveError()
    {
        string[] args = ["-invalidOption", "value", "--", "payload"];
        var result = CLIOptionParser.Parse(args);

        Assert.True(result.HasErrors);
        Assert.Contains("Unrecognized option '-invalidOption'", result.ErrorMessage);
    }

    [Fact]
    public void OptionParser_MissingOptionValue_ReturnsDescriptiveError()
    {
        string[] args = ["-mode", "--", "payload"];
        var result = CLIOptionParser.Parse(args);

        Assert.True(result.HasErrors);
        Assert.Contains("Missing value for option '-mode'", result.ErrorMessage);
    }

    [Fact]
    public void OptionParser_InvalidShellMode_ReturnsDescriptiveError()
    {
        string[] args = ["-mode", "unsupported_shell", "--", "payload"];
        var result = CLIOptionParser.Parse(args);

        Assert.True(result.HasErrors);
        Assert.Contains("Invalid shell mode 'unsupported_shell'", result.ErrorMessage);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("-help")]
    [InlineData("--help")]
    [InlineData("-?")]
    [InlineData("/?")]
    [InlineData("help")]
    public void OptionParser_HelpFlags_SetsHelpRequested(string flag)
    {
        string[] args = [flag];
        var result = CLIOptionParser.Parse(args);

        Assert.True(result.HelpRequested);
        Assert.False(result.HasErrors);
    }

    #endregion

    #region 2. Settings & Precedence Tests

    [Fact]
    public void Settings_Defaults_InitializesWithBuiltInDefaults()
    {
        var settings = new CLIExpandSettings();

        Assert.Equal(ShellMode.Raw, settings.Mode);
        Assert.Equal(".expand.", settings.ExpandStartToken);
        Assert.Equal(".expand_end.", settings.ExpandEndToken);
        Assert.Equal(":", settings.DelimiterToken);
        Assert.True(settings.MatchPartials);
        Assert.True(settings.SplitAfterGetByKey);
        Assert.Null(settings.SettingsPath);
    }

    [Fact]
    public void SettingsLoader_ValidJsonString_UpdatesTargetSettings()
    {
        string json = """
        {
            "mode": "ps",
            "begin": "@start",
            "end": "@end",
            "delimiter": "in",
            "matchPartials": false,
            "splitAfterGetByKey": false
        }
        """;

        var settings = new CLIExpandSettings();
        bool success = SettingsLoader.TryLoadFromJson(json, settings, out string? error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(ShellMode.PowerShell, settings.Mode);
        Assert.Equal("@start", settings.ExpandStartToken);
        Assert.Equal("@end", settings.ExpandEndToken);
        Assert.Equal("in", settings.DelimiterToken);
        Assert.False(settings.MatchPartials);
        Assert.False(settings.SplitAfterGetByKey);
    }

    [Fact]
    public void SettingsLoader_AlternatePropertyNames_UpdatesTargetSettings()
    {
        string json = """
        {
            "mode": "cmd",
            "expandStartToken": "[expand]",
            "expandEndToken": "[/expand]",
            "delimiterToken": "->"
        }
        """;

        var settings = new CLIExpandSettings();
        bool success = SettingsLoader.TryLoadFromJson(json, settings, out string? error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(ShellMode.Cmd, settings.Mode);
        Assert.Equal("[expand]", settings.ExpandStartToken);
        Assert.Equal("[/expand]", settings.ExpandEndToken);
        Assert.Equal("->", settings.DelimiterToken);
    }

    [Fact]
    public void SettingsLoader_MalformedJson_ReturnsDescriptiveError()
    {
        string json = "{ mode: 'bash', incomplete json ";

        var settings = new CLIExpandSettings();
        bool success = SettingsLoader.TryLoadFromJson(json, settings, out string? error, "bad.json");

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Contains("Failed to parse settings JSON in 'bad.json'", error);
    }

    [Fact]
    public void SettingsLoader_InvalidShellModeInJson_ReturnsDescriptiveError()
    {
        string json = """{ "mode": "invalid_mode" }""";

        var settings = new CLIExpandSettings();
        bool success = SettingsLoader.TryLoadFromJson(json, settings, out string? error);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Contains("Invalid shell mode 'invalid_mode'", error);
    }

    [Fact]
    public void SettingsLoader_MissingFile_ReturnsDescriptiveError()
    {
        var settings = new CLIExpandSettings();
        bool success = SettingsLoader.TryLoadFromFile("non_existent_path_48291.json", settings, out string? error);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Contains("Settings file not found", error);
    }

    [Fact]
    public void SettingsLoader_PrecedenceHierarchy_CLIOverridesCustomSettingsAndUserFile()
    {
        string tempUserFile = Path.GetTempFileName();
        string tempCustomFile = Path.GetTempFileName();

        try
        {
            // User settings (Tier 2) sets mode = cmd, delimiter = ":"
            File.WriteAllText(tempUserFile, """{ "mode": "cmd", "delimiter": ":" }""");

            // Custom settings (Tier 3) sets mode = ps, delimiter = "in"
            File.WriteAllText(tempCustomFile, """{ "mode": "ps", "delimiter": "in" }""");

            // CLI options (Tier 4) overrides mode = raw
            string[] args = ["-settings", tempCustomFile, "-mode", "raw", "--", "hello"];

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr, defaultSettingsOverridePath: tempUserFile);

            Assert.Equal(0, exitCode);
            // Mode should be raw (CLI wins over custom 'ps' and user 'cmd')
            Assert.Equal("hello\n", stdout.ToString().Replace("\r\n", "\n"));
        }
        finally
        {
            if (File.Exists(tempUserFile)) File.Delete(tempUserFile);
            if (File.Exists(tempCustomFile)) File.Delete(tempCustomFile);
        }
    }

    #endregion

    #region 3. Shell Argument Renderers Tests

    [Fact]
    public void BashRenderer_SafeTokens_EmittedUnquoted()
    {
        var renderer = new BashArgumentRenderer();
        var args = new[] { "echo", "hello_world", "v1.2.3", "path/to/file.txt", "foo=bar", "a+b", "user@host:8080" };

        string result = renderer.Render(args);

        Assert.Equal("echo hello_world v1.2.3 path/to/file.txt foo=bar a+b user@host:8080", result);
    }

    [Fact]
    public void BashRenderer_SpacesAndTabs_QuotedInSingleQuotes()
    {
        var renderer = new BashArgumentRenderer();
        var args = new[] { "echo", "hello world", "tab\tseparated" };

        string result = renderer.Render(args);

        Assert.Equal("echo 'hello world' 'tab\tseparated'", result);
    }

    [Fact]
    public void BashRenderer_EmbeddedSingleQuotes_EscapedCorrectly()
    {
        var renderer = new BashArgumentRenderer();
        var args = new[] { "it's", "say 'hello'", "'quoted'" };

        string result = renderer.Render(args);

        Assert.Equal(@"'it'\''s' 'say '\''hello'\''' ''\''quoted'\'''", result);
    }

    [Fact]
    public void BashRenderer_EmptyString_EmittedAsEmptyQuotes()
    {
        var renderer = new BashArgumentRenderer();
        var args = new[] { "cmd", "", "arg" };

        string result = renderer.Render(args);

        Assert.Equal("cmd '' arg", result);
    }

    [Fact]
    public void BashRenderer_ShellMetacharacters_EnclosedInSingleQuotes()
    {
        var renderer = new BashArgumentRenderer();
        var args = new[] { "$VAR", "foo;bar", "a&b", "x|y", "<in>", ">out", "(sub)", "*", "?", "`cmd`" };

        string result = renderer.Render(args);

        Assert.Equal("'$VAR' 'foo;bar' 'a&b' 'x|y' '<in>' '>out' '(sub)' '*' '?' '`cmd`'", result);
    }

    [Fact]
    public void PowerShellRenderer_SafeTokens_EmittedUnquoted()
    {
        var renderer = new PowerShellArgumentRenderer();
        var args = new[] { "git", "status", "src/file.cs", @"C:\path\to\file", "flag=val", "v1.0.0" };

        string result = renderer.Render(args);

        Assert.Equal(@"git status src/file.cs C:\path\to\file flag=val v1.0.0", result);
    }

    [Fact]
    public void PowerShellRenderer_SpecialCharactersAndQuotes_SingleQuotedWithDoubleQuoteEscape()
    {
        var renderer = new PowerShellArgumentRenderer();
        var args = new[] { "it's", "$variable", "@array", "foo;bar", "`backtick`", "hello world", "" };

        string result = renderer.Render(args);

        Assert.Equal("'it''s' '$variable' '@array' 'foo;bar' '`backtick`' 'hello world' ''", result);
    }

    [Fact]
    public void CmdRenderer_SafeTokens_EmittedUnquoted()
    {
        var renderer = new CmdArgumentRenderer();
        var args = new[] { "findstr", "main", "src/Program.cs", @"C:\Temp\log.txt" };

        string result = renderer.Render(args);

        Assert.Equal(@"findstr main src/Program.cs C:\Temp\log.txt", result);
    }

    [Fact]
    public void CmdRenderer_SpacesAndMetacharacters_DoubleQuotedWithCRTEscaping()
    {
        var renderer = new CmdArgumentRenderer();
        var args = new[] { "hello world", "foo&bar", "a|b", "<test>", "say \"hi\"", @"C:\path with spaces\" };

        string result = renderer.Render(args);

        // Windows CRT escaping: say "hi" -> "say \"hi\""; trailing backslash before quote doubled: "C:\path with spaces\\"
        Assert.Equal("\"hello world\" \"foo&bar\" \"a|b\" \"<test>\" \"say \\\"hi\\\"\" \"C:\\path with spaces\\\\\"", result);
    }

    [Fact]
    public void CmdRenderer_EmptyString_EmittedAsEmptyDoubleQuotes()
    {
        var renderer = new CmdArgumentRenderer();
        var args = new[] { "cmd", "", "arg" };

        string result = renderer.Render(args);

        Assert.Equal("cmd \"\" arg", result);
    }

    [Fact]
    public void RawRenderer_SpaceDelimitedWithoutEscaping()
    {
        var renderer = new RawArgumentRenderer();
        var args = new[] { "echo", "it's", "hello world", "$VAR" };

        string result = renderer.Render(args);

        Assert.Equal("echo it's hello world $VAR", result);
    }

    [Fact]
    public void RawRenderer_BoundaryAwareWhitespace_InsertsSpaceOnlyWhenNeeded()
    {
        var renderer = new RawArgumentRenderer();

        // Standard adjacent non-whitespace tokens
        Assert.Equal("a b", renderer.Render(["a", "b"]));

        // Newline token - no spaces inserted around it
        Assert.Equal("a\nb", renderer.Render(["a", "\n", "b"]));

        // Tab token - no spaces inserted around it
        Assert.Equal("a\tb", renderer.Render(["a", "\t", "b"]));

        // Carriage return + line feed
        Assert.Equal("a\r\nb", renderer.Render(["a", "\r\n", "b"]));

        // Token with trailing whitespace
        Assert.Equal("a b", renderer.Render(["a ", "b"]));

        // Token with leading whitespace
        Assert.Equal("a b", renderer.Render(["a", " b"]));

        // Both tokens having whitespace at boundary
        Assert.Equal("a  b", renderer.Render(["a ", " b"]));

        // Internal whitespace preserved
        Assert.Equal("my  file", renderer.Render(["my  file"]));

        // Real-world filename and newline sequence
        Assert.Equal("apple_small.jpeg\napple_medium.jpeg",
            renderer.Render(["apple_small.jpeg", "\n", "apple_medium.jpeg"]));
    }

    [Fact]
    public void RawRenderer_EmptyStringBehavior()
    {
        var renderer = new RawArgumentRenderer();

        Assert.Equal("", renderer.Render([]));
        Assert.Equal("", renderer.Render([""]));
        Assert.Equal("", renderer.Render(["", ""]));
        Assert.Equal("a", renderer.Render(["a", ""]));
        Assert.Equal("b", renderer.Render(["", "b"]));
        Assert.Equal("ab", renderer.Render(["a", "", "b"]));
    }

    [Fact]
    public void Factory_CreatesCorrectRendererOrThrowsOnInvalid()
    {
        Assert.IsType<BashArgumentRenderer>(CLIArgumentRendererFactory.Create(ShellMode.Bash));
        Assert.IsType<PowerShellArgumentRenderer>(CLIArgumentRendererFactory.Create(ShellMode.PowerShell));
        Assert.IsType<CmdArgumentRenderer>(CLIArgumentRendererFactory.Create(ShellMode.Cmd));
        Assert.IsType<RawArgumentRenderer>(CLIArgumentRendererFactory.Create(ShellMode.Raw));
    }

    #endregion

    #region 4. End-to-End Pipeline & Integration Tests

    [Fact]
    public void Pipeline_BasicExpansion_RendersRawByDefault()
    {
        string[] args = ["--", ".expand.", "_x", "a", "b", ":", "echo", "_x", ".expand_end."];
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Equal("echo a echo b\n", stdout.ToString().Replace("\r\n", "\n"));
        Assert.Empty(stderr.ToString());
    }

    [Fact]
    public void Pipeline_DefaultRawMode_PreservesUnquotedMetacharactersAndOperators()
    {
        string[] args = ["--", ".expand.", "_x", "1", "2", ":", "echo", "_x", ";", "$VAR", ".expand_end."];
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(0, exitCode);
        // In default raw mode, ; and $VAR are passed straight through unquoted
        Assert.Equal("echo 1 ; $VAR echo 2 ; $VAR\n", stdout.ToString().Replace("\r\n", "\n"));
        Assert.Empty(stderr.ToString());
    }

    [Fact]
    public void Pipeline_ExplicitBashMode_QuotesMetacharacters()
    {
        string[] args = ["-mode", "bash", "--", ".expand.", "_x", "1", "2", ":", "echo", "_x", ";", "$VAR", ".expand_end."];
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(0, exitCode);
        // In explicit bash mode, metacharacters ; and $VAR are safely single-quoted
        Assert.Equal("echo 1 ';' '$VAR' echo 2 ';' '$VAR'\n", stdout.ToString().Replace("\r\n", "\n"));
        Assert.Empty(stderr.ToString());
    }

    [Fact]
    public void Pipeline_NestedExpansion_PowerShellMode()
    {
        string[] args =
        [
            "-mode", "ps",
            "--",
            ".expand.", "_app", "web", "api", ":",
                ".expand.", "_env", "dev", "prod", ":",
                    "deploy", "_app", "to", "_env", "with", "special $var",
                ".expand_end.",
            ".expand_end."
        ];

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(0, exitCode);
        string expected = "deploy web to dev with 'special $var' deploy web to prod with 'special $var' " +
                          "deploy api to dev with 'special $var' deploy api to prod with 'special $var'\n";
        Assert.Equal(expected, stdout.ToString().Replace("\r\n", "\n"));
    }

    [Fact]
    public void Pipeline_CustomSyntaxTokens_ExpandsSuccessfully()
    {
        string[] args =
        [
            "-begin", "@expand",
            "-end", "@end",
            "-delim", "in",
            "-mode", "raw",
            "--",
            "@expand", "_x", "1", "2", "in",
            "val", "_x",
            "@end"
        ];

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Equal("val 1 val 2\n", stdout.ToString().Replace("\r\n", "\n"));
    }

    [Fact]
    public void Pipeline_NoPartialsFlag_DisablesInnerSubstitutions()
    {
        string[] args =
        [
            "-no-partials",
            "-mode", "raw",
            "--",
            ".expand.", "_var", "val", ":",
            "prefix._var.suffix", "_var",
            ".expand_end."
        ];

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(0, exitCode);
        // prefix._var.suffix remains unexpanded because MatchPartials is false
        Assert.Equal("prefix._var.suffix val\n", stdout.ToString().Replace("\r\n", "\n"));
    }

    [Fact]
    public void Pipeline_ExpansionError_WritesToStderrAndReturnsNonZero()
    {
        // Missing colon delimiter in expansion block
        string[] args = ["--", ".expand.", "_var", "a", "b"];
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.NotEqual(0, exitCode);
        Assert.Empty(stdout.ToString());
        Assert.Contains("CLIExpander: Missing ':' delimiter for variable '_var'", stderr.ToString());
    }

    [Fact]
    public void Pipeline_OptionError_WritesToStderrAndReturnsOne()
    {
        string[] args = ["-unknownOption", "--", "echo", "hello"];
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Empty(stdout.ToString());
        Assert.Contains("Unrecognized option '-unknownOption'", stderr.ToString());
    }

    [Theory]
    [InlineData("-help")]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    public void Pipeline_HelpOption_WritesUsageToStderrAndReturnsZero(string helpFlag)
    {
        string[] args = [helpFlag];
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Empty(stdout.ToString());
        Assert.Contains("Usage: CLIExpand [options] -- <payload tokens...>", stderr.ToString());
    }

    [Fact]
    public void CrossModeInvariance_ProducesDeterministicOutputRegardlessOfHostPlatform()
    {
        string[] payload = [".expand.", "_name", "app space", "plain", ":", "deploy", "_name", ".expand_end."];

        // Bash mode with -no-split to preserve multi-word variable token
        using (var stdout = new StringWriter())
        {
            int code = Program.Run(["-mode", "bash", "-no-split", "--", .. payload], stdout, TextWriter.Null);
            Assert.Equal(0, code);
            Assert.Equal("deploy 'app space' deploy plain\n", stdout.ToString().Replace("\r\n", "\n"));
        }

        // PowerShell mode with -no-split
        using (var stdout = new StringWriter())
        {
            int code = Program.Run(["-mode", "ps", "-no-split", "--", .. payload], stdout, TextWriter.Null);
            Assert.Equal(0, code);
            Assert.Equal("deploy 'app space' deploy plain\n", stdout.ToString().Replace("\r\n", "\n"));
        }

        // Cmd mode with -no-split
        using (var stdout = new StringWriter())
        {
            int code = Program.Run(["-mode", "cmd", "-no-split", "--", .. payload], stdout, TextWriter.Null);
            Assert.Equal(0, code);
            Assert.Equal("deploy \"app space\" deploy plain\n", stdout.ToString().Replace("\r\n", "\n"));
        }

        // Raw mode with -no-split
        using (var stdout = new StringWriter())
        {
            int code = Program.Run(["-mode", "raw", "-no-split", "--", .. payload], stdout, TextWriter.Null);
            Assert.Equal(0, code);
            Assert.Equal("deploy app space deploy plain\n", stdout.ToString().Replace("\r\n", "\n"));
        }
    }

    [Theory]
    [InlineData("-join", "-join-end")]
    [InlineData("--join", "--join-end")]
    [InlineData("-join-start", "-joinend")]
    [InlineData("--join-start", "--joinend")]
    public void OptionParser_JoinTokens_ParsesCorrectly(string startFlag, string endFlag)
    {
        string[] args = [startFlag, "@j", endFlag, "@je", "--", "@j", "a", "b", "@je"];
        var result = CLIOptionParser.Parse(args);

        Assert.False(result.HasErrors);
        Assert.Equal("@j", result.JoinStartToken);
        Assert.Equal("@je", result.JoinEndToken);
        Assert.Equal(new[] { "@j", "a", "b", "@je" }, result.Payload);
    }

    [Fact]
    public void SettingsLoader_JsonSettings_LoadsJoinTokens()
    {
        string json = """
        {
            "join": "[j]",
            "joinEnd": "[/j]"
        }
        """;

        var settings = new CLIExpandSettings();
        bool success = SettingsLoader.TryLoadFromJson(json, settings, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal("[j]", settings.JoinStartToken);
        Assert.Equal("[/j]", settings.JoinEndToken);
    }

    [Fact]
    public void SettingsLoader_JsonSettings_LoadsLongFormJoinTokens()
    {
        string json = """
        {
            "joinStartToken": "{join}",
            "joinEndToken": "{/join}"
        }
        """;

        var settings = new CLIExpandSettings();
        bool success = SettingsLoader.TryLoadFromJson(json, settings, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal("{join}", settings.JoinStartToken);
        Assert.Equal("{/join}", settings.JoinEndToken);
    }

    [Fact]
    public void Pipeline_JoinWithShellRenderers_PreservesSingleArgQuoting()
    {
        string[] payload = [".join.", "/home/jwc/", "my file", ".join_end."];

        // Raw mode
        using (var stdout = new StringWriter())
        {
            int code = Program.Run(["-mode", "raw", "--", .. payload], stdout, TextWriter.Null);
            Assert.Equal(0, code);
            Assert.Equal("/home/jwc/my file\n", stdout.ToString().Replace("\r\n", "\n"));
        }

        // Bash mode
        using (var stdout = new StringWriter())
        {
            int code = Program.Run(["-mode", "bash", "--", .. payload], stdout, TextWriter.Null);
            Assert.Equal(0, code);
            Assert.Equal("'/home/jwc/my file'\n", stdout.ToString().Replace("\r\n", "\n"));
        }

        // PowerShell mode
        using (var stdout = new StringWriter())
        {
            int code = Program.Run(["-mode", "ps", "--", .. payload], stdout, TextWriter.Null);
            Assert.Equal(0, code);
            Assert.Equal("'/home/jwc/my file'\n", stdout.ToString().Replace("\r\n", "\n"));
        }

        // Cmd mode
        using (var stdout = new StringWriter())
        {
            int code = Program.Run(["-mode", "cmd", "--", .. payload], stdout, TextWriter.Null);
            Assert.Equal(0, code);
            Assert.Equal("\"/home/jwc/my file\"\n", stdout.ToString().Replace("\r\n", "\n"));
        }
    }

    [Fact]
    public void Pipeline_JoinCliOption_OverridesJsonSettings()
    {
        string settingsJson = """
        {
            "join": "[j]",
            "joinEnd": "[/j]"
        }
        """;
        string tempSettingsFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempSettingsFile, settingsJson);

            string[] args =
            [
                "-settings", tempSettingsFile,
                "-join", "@j",
                "-join-end", "@je",
                "--",
                "@j", "foo", "bar", "@je"
            ];

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.Equal("foobar\n", stdout.ToString().Replace("\r\n", "\n"));
            Assert.Empty(stderr.ToString());
        }
        finally
        {
            if (File.Exists(tempSettingsFile))
                File.Delete(tempSettingsFile);
        }
    }

    [Fact]
    public void Pipeline_JoinInsideExpand_CartesianGeneration()
    {
        string[] args =
        [
            "-mode", "raw",
            "--",
            ".expand.", "_base", "Quant", "Quant2", ":",
            ".expand.", "_ext", "tkr", "csv", ":",
            ".join.", "_base", ".", "_ext", ".join_end.",
            ".expand_end.",
            ".expand_end."
        ];

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Equal("Quant.tkr Quant.csv Quant2.tkr Quant2.csv\n", stdout.ToString().Replace("\r\n", "\n"));
        Assert.Empty(stderr.ToString());
    }

    [Fact]
    public void Pipeline_JoinWithNewlineToken_RendersCleanLinesInRawMode()
    {
        string[] args =
        [
            "-mode", "raw",
            "--",
            ".expand.", "Fruit", "apple", "orange", ":",
            ".expand.", "Size", "small", "large", ":",
            ".join.", "Fruit", "_", "Size", ".jpeg", ".join_end.",
            "\n",
            ".expand_end.",
            ".expand_end."
        ];

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(0, exitCode);
        string expected = "apple_small.jpeg\napple_large.jpeg\norange_small.jpeg\norange_large.jpeg\n\n";
        Assert.Equal(expected, stdout.ToString().Replace("\r\n", "\n"));
        Assert.Empty(stderr.ToString());
    }

    #endregion

    #region 5. Known Values (.get., .split_get., -values, Settings) Tests

    [Fact]
    public void OptionParser_GetAndValuesOptions_ParsedCorrectly()
    {
        string[] args =
        [
            "-get", "@get",
            "-split-get", "@sget",
            "-values", "common.json",
            "-values", "project.json",
            "--",
            "echo", "hello"
        ];

        var result = CLIOptionParser.Parse(args);

        Assert.False(result.HasErrors);
        Assert.Equal("@get", result.GetToken);
        Assert.Equal("@sget", result.SplitGetToken);
        Assert.Equal(["common.json", "project.json"], result.ValueFiles);
        Assert.Equal(["echo", "hello"], result.Payload);
    }

    [Fact]
    public void Pipeline_MotivatingExample_PlatformNeutralNewlines()
    {
        string[] args =
        [
            "-mode", "raw",
            "--",
            ".expand.", "Fruit", "apple", "orange", "grape", ":",
                ".expand.", "Size", "small", "medium", "large", ":",
                    ".join.", "Fruit", "_", "Size", ".jpeg", ".join_end.",
                    ".get.", "newline",
                ".expand_end.",
            ".expand_end."
        ];

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(0, exitCode);
        string expected = string.Join(Environment.NewLine, new[]
        {
            "apple_small.jpeg",
            "apple_medium.jpeg",
            "apple_large.jpeg",
            "orange_small.jpeg",
            "orange_medium.jpeg",
            "orange_large.jpeg",
            "grape_small.jpeg",
            "grape_medium.jpeg",
            "grape_large.jpeg",
            "",
            ""
        });
        Assert.Equal(expected, stdout.ToString());
        Assert.Empty(stderr.ToString());
    }

    [Fact]
    public void Pipeline_SingleValueMap_LoadsKnownValues()
    {
        string tempMap = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempMap, """
            {
                "project": "TruthInTheFlip",
                "extension": ".tkr",
                "output_root": "/tmp/results"
            }
            """);

            string[] args =
            [
                "-mode", "raw",
                "-values", tempMap,
                "--",
                ".join.", ".get.", "output_root", "/", ".get.", "project", ".get.", "extension", ".join_end."
            ];

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.Equal("/tmp/results/TruthInTheFlip.tkr\n", stdout.ToString().Replace("\r\n", "\n"));
            Assert.Empty(stderr.ToString());
        }
        finally
        {
            if (File.Exists(tempMap)) File.Delete(tempMap);
        }
    }

    [Fact]
    public void Pipeline_MultipleValueMaps_LaterMapOverridesEarlierMap()
    {
        string baseMap = Path.GetTempFileName();
        string overrideMap = Path.GetTempFileName();
        try
        {
            File.WriteAllText(baseMap, """
            {
                "project": "DefaultProject",
                "env": "development",
                "format": "json"
            }
            """);

            File.WriteAllText(overrideMap, """
            {
                "project": "TruthInTheFlip",
                "env": "production"
            }
            """);

            string[] args =
            [
                "-mode", "raw",
                "-values", baseMap,
                "-values", overrideMap,
                "--",
                ".get.", "project", ".get.", "env", ".get.", "format"
            ];

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.Equal("TruthInTheFlip production json\n", stdout.ToString().Replace("\r\n", "\n"));
            Assert.Empty(stderr.ToString());
        }
        finally
        {
            if (File.Exists(baseMap)) File.Delete(baseMap);
            if (File.Exists(overrideMap)) File.Delete(overrideMap);
        }
    }

    [Fact]
    public void Pipeline_SettingsFileWithRelativeValueMaps_ResolvesRelativeToSettingsFile()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "cli_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string commonMap = Path.Combine(tempDir, "common.json");
            File.WriteAllText(commonMap, """
            {
                "framework": "dotnet10",
                "author": "JetBrains"
            }
            """);

            string settingsFile = Path.Combine(tempDir, "settings.json");
            File.WriteAllText(settingsFile, """
            {
                "mode": "raw",
                "values": [ "common.json" ]
            }
            """);

            string[] args =
            [
                "-settings", settingsFile,
                "--",
                "info", ".get.", "framework", "by", ".get.", "author"
            ];

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.Equal("info dotnet10 by JetBrains\n", stdout.ToString().Replace("\r\n", "\n"));
            Assert.Empty(stderr.ToString());
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Pipeline_Precedence_BuiltIn_Settings_CliValues_OverridesCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "cli_prec_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            // Settings map overrides built-in "dir_sep"
            string settingsMap = Path.Combine(tempDir, "settings_map.json");
            File.WriteAllText(settingsMap, """
            {
                "dir_sep": "#",
                "tier": "settings"
            }
            """);

            string settingsFile = Path.Combine(tempDir, "settings.json");
            File.WriteAllText(settingsFile, """
            {
                "mode": "raw",
                "values": [ "settings_map.json" ]
            }
            """);

            // CLI map overrides "tier"
            string cliMap = Path.Combine(tempDir, "cli_map.json");
            File.WriteAllText(cliMap, """
            {
                "tier": "cli_override"
            }
            """);

            string[] args =
            [
                "-settings", settingsFile,
                "-values", cliMap,
                "--",
                ".get.", "dir_sep", ".get.", "tier", ".get.", "space", ".get.", "tab"
            ];

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.Equal("# cli_override \t\n", stdout.ToString().Replace("\r\n", "\n"));
            Assert.Empty(stderr.ToString());
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Pipeline_CustomGetToken_FromCliOption_ResolvesProperly()
    {
        string tempMap = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempMap, """
            {
                "var1": "value1"
            }
            """);

            string[] args =
            [
                "-mode", "raw",
                "-get", "%get%",
                "-values", tempMap,
                "--",
                "output:", "%get%", "var1"
            ];

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.Equal("output: value1\n", stdout.ToString().Replace("\r\n", "\n"));
            Assert.Empty(stderr.ToString());
        }
        finally
        {
            if (File.Exists(tempMap)) File.Delete(tempMap);
        }
    }

    [Fact]
    public void Pipeline_SplitGetToken_SplitsValuesOnWhitespace()
    {
        string tempMap = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempMap, """
            {
                "tags": "release prod v1.0"
            }
            """);

            string[] args =
            [
                "-mode", "bash",
                "-values", tempMap,
                "--",
                "deploy", ".split_get.", "tags"
            ];

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.Equal("deploy release prod v1.0\n", stdout.ToString().Replace("\r\n", "\n"));
            Assert.Empty(stderr.ToString());
        }
        finally
        {
            if (File.Exists(tempMap)) File.Delete(tempMap);
        }
    }

    [Fact]
    public void Pipeline_MissingValueMapFile_ReturnsError()
    {
        string[] args =
        [
            "-values", "non_existent_map_12345.json",
            "--",
            "echo", "test"
        ];

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("Value map file not found", stderr.ToString());
    }

    [Fact]
    public void Pipeline_MalformedJsonValueMap_ReturnsError()
    {
        string tempMap = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempMap, "{ not valid json }");

            string[] args =
            [
                "-values", tempMap,
                "--",
                "echo", "test"
            ];

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(1, exitCode);
            Assert.Contains("Failed to parse value map JSON", stderr.ToString());
        }
        finally
        {
            if (File.Exists(tempMap)) File.Delete(tempMap);
        }
    }

    [Fact]
    public void Pipeline_NonStringJsonValueMap_ReturnsError()
    {
        string tempMap = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempMap, """
            {
                "number_value": 42
            }
            """);

            string[] args =
            [
                "-values", tempMap,
                "--",
                "echo", "test"
            ];

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(1, exitCode);
            Assert.Contains("must have a string value", stderr.ToString());
        }
        finally
        {
            if (File.Exists(tempMap)) File.Delete(tempMap);
        }
    }

    [Fact]
    public void Pipeline_MissingKeyAfterGet_ReturnsDescriptiveError()
    {
        string[] args =
        [
            "--",
            "echo", ".get."
        ];

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("Expected key after .get.", stderr.ToString());
    }

    [Fact]
    public void Pipeline_UnknownKnownValueKey_ReturnsDescriptiveError()
    {
        string[] args =
        [
            "--",
            "echo", ".get.", "undefined_key"
        ];

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("Unknown value 'undefined_key'", stderr.ToString());
    }

    #endregion

    #region 6. Known Values Help & List-Values Tests

    [Theory]
    [InlineData("-list-values")]
    [InlineData("--list-values")]
    [InlineData("-list")]
    [InlineData("--list")]
    public void OptionParser_ListValuesFlag_SetsListValuesRequestedWithoutPayloadBoundary(string flag)
    {
        string[] args = [flag];
        var result = CLIOptionParser.Parse(args);

        Assert.False(result.HasErrors);
        Assert.True(result.ListValuesRequested);
        Assert.Empty(result.Payload);
    }

    [Fact]
    public void OptionParser_ListValuesWithBoundaryAndEmptyPayload_Allowed()
    {
        string[] args = ["-list-values", "--"];
        var result = CLIOptionParser.Parse(args);

        Assert.False(result.HasErrors);
        Assert.True(result.ListValuesRequested);
        Assert.Empty(result.Payload);
    }

    [Fact]
    public void OptionParser_ListValuesWithOptionsAndWithoutBoundary_ParsesOptions()
    {
        string[] args = ["-values", "custom.json", "-list-values"];
        var result = CLIOptionParser.Parse(args);

        Assert.False(result.HasErrors);
        Assert.True(result.ListValuesRequested);
        Assert.Equal(["custom.json"], result.ValueFiles);
    }

    [Fact]
    public void Pipeline_Help_IncludesBuiltInKnownValuesAndListValuesOption()
    {
        string[] args = ["--help"];
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(0, exitCode);
        string helpText = stderr.ToString();

        Assert.Contains("-list-values", helpText);
        Assert.Contains("Built-in Known Values (.get. <key>):", helpText);
        Assert.Contains("newline", helpText);
        Assert.Contains("space", helpText);
        Assert.Contains("tab", helpText);
        Assert.Contains("empty", helpText);
        Assert.Contains("now", helpText);
        Assert.Contains("utc_now", helpText);
        Assert.Contains("timestamp", helpText);
        Assert.Contains("utimestamp", helpText);
        Assert.Contains("dir_sep", helpText);
        Assert.Contains("path_sep", helpText);
    }

    [Fact]
    public void Pipeline_ListValues_DefaultBuiltIns_PrintsActiveKnownValuesTable()
    {
        string[] args = ["-list-values"];
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Empty(stderr.ToString());

        string output = stdout.ToString();
        Assert.Contains("Active Known Values (10 keys):", output);
        Assert.Contains("dir_sep", output);
        Assert.Contains("empty", output);
        Assert.Contains("newline", output);
        Assert.Contains("now", output);
        Assert.Contains("path_sep", output);
        Assert.Contains("space", output);
        Assert.Contains("tab", output);
        Assert.Contains("timestamp", output);
        Assert.Contains("utc_now", output);
        Assert.Contains("utimestamp", output);
    }

    [Fact]
    public void Pipeline_ListValues_WithLoadedValueMap_PrintsLoadedEntries()
    {
        string tempMap = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempMap, """
            {
                "project": "TruthInTheFlip",
                "custom_dir": "/tmp/output"
            }
            """);

            string[] args = ["-values", tempMap, "-list-values"];
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.Empty(stderr.ToString());

            string output = stdout.ToString();
            Assert.Contains("Active Known Values (12 keys):", output);
            Assert.Contains("project", output);
            Assert.Contains("TruthInTheFlip", output);
            Assert.Contains("custom_dir", output);
            Assert.Contains("/tmp/output", output);
        }
        finally
        {
            if (File.Exists(tempMap)) File.Delete(tempMap);
        }
    }

    [Fact]
    public void Pipeline_ListValues_WithSettingsAndCLIOverride_RespectsPrecedence()
    {
        string tempSettings = Path.GetTempFileName();
        string tempMap1 = Path.GetTempFileName();
        string tempMap2 = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempMap1, """
            {
                "project": "BaseProject",
                "env": "staging"
            }
            """);

            File.WriteAllText(tempMap2, """
            {
                "project": "OverrideProject"
            }
            """);

            File.WriteAllText(tempSettings, $$"""
            {
                "values": ["{{tempMap1.Replace("\\", "\\\\")}}"]
            }
            """);

            string[] args = ["-settings", tempSettings, "-values", tempMap2, "-list-values"];
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(0, exitCode);
            string output = stdout.ToString();

            Assert.Contains("project", output);
            Assert.Contains("OverrideProject", output);
            Assert.DoesNotContain("BaseProject", output);
            Assert.Contains("env", output);
            Assert.Contains("staging", output);
        }
        finally
        {
            if (File.Exists(tempSettings)) File.Delete(tempSettings);
            if (File.Exists(tempMap1)) File.Delete(tempMap1);
            if (File.Exists(tempMap2)) File.Delete(tempMap2);
        }
    }

    [Fact]
    public void Pipeline_ListValues_MissingValueFile_ReturnsError()
    {
        string[] args = ["-values", "missing_value_file_98765.json", "-list-values"];
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("Value map file not found", stderr.ToString());
    }

    #endregion

    #region 7. Macros CLI & Settings Integration Tests

    [Fact]
    public void OptionParser_MacrosAndMacroFlags_PopulatesParseResult()
    {
        string[] args = ["-macros", "common.json", "-macros", "env.json", "-macro", "ARCH=x64", "-macro", "CONFIG=Release", "--", "echo", "ARCH"];
        var result = CLIOptionParser.Parse(args);

        Assert.False(result.HasErrors);
        Assert.Equal(["common.json", "env.json"], result.MacroFiles);
        Assert.Equal("x64", result.InlineMacros["ARCH"]);
        Assert.Equal("Release", result.InlineMacros["CONFIG"]);
        Assert.Equal(["echo", "ARCH"], result.Payload);
    }

    [Theory]
    [InlineData("invalid_no_equals")]
    [InlineData("=missing_key")]
    public void OptionParser_MacroFlag_InvalidFormat_ReturnsError(string macroDef)
    {
        string[] args = ["-macro", macroDef, "--", "echo", "test"];
        var result = CLIOptionParser.Parse(args);

        Assert.True(result.HasErrors);
        Assert.Contains("Invalid macro definition", result.ErrorMessage);
    }

    [Theory]
    [InlineData("-list-macros")]
    [InlineData("--list-macros")]
    [InlineData("-list-macro")]
    [InlineData("--list-macro")]
    public void OptionParser_ListMacrosFlag_SetsListMacrosRequestedWithoutPayloadBoundary(string flag)
    {
        string[] args = [flag];
        var result = CLIOptionParser.Parse(args);

        Assert.False(result.HasErrors);
        Assert.True(result.ListMacrosRequested);
        Assert.Empty(result.Payload);
    }

    [Fact]
    public void Pipeline_Help_IncludesMacroOptions()
    {
        string[] args = ["--help"];
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(0, exitCode);
        string helpText = stderr.ToString();

        Assert.Contains("-macros <path>", helpText);
        Assert.Contains("-macro <key>=<value>", helpText);
        Assert.Contains("-list-macros", helpText);
    }

    [Fact]
    public void Pipeline_ListMacros_DefaultEmpty_PrintsEmptyTable()
    {
        string[] args = ["-list-macros"];
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Empty(stderr.ToString());

        string output = stdout.ToString();
        Assert.Contains("Active Macros (0 keys):", output);
    }

    [Fact]
    public void Pipeline_ListMacros_WithLoadedMacroMapAndInlineMacros_PrintsActiveMacrosTable()
    {
        string tempMap = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempMap, """
            {
                "OUT_DIR": "/var/builds",
                "TARGET": "net10.0"
            }
            """);

            string[] args = ["-macros", tempMap, "-macro", "CONFIG=Release", "-list-macros"];
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.Empty(stderr.ToString());

            string output = stdout.ToString();
            Assert.Contains("Active Macros (3 keys):", output);
            Assert.Contains("CONFIG", output);
            Assert.Contains("Release", output);
            Assert.Contains("OUT_DIR", output);
            Assert.Contains("/var/builds", output);
            Assert.Contains("TARGET", output);
            Assert.Contains("net10.0", output);
        }
        finally
        {
            if (File.Exists(tempMap)) File.Delete(tempMap);
        }
    }

    [Fact]
    public void Pipeline_MacrosFile_LoadsAndSubstitutes()
    {
        string tempMap = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempMap, """
            {
                "APP": "TruthInTheFlip",
                "ENV": "production"
            }
            """);

            string[] args = ["-macros", tempMap, "--", "deploy", "APP", "--env", "ENV"];
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.Equal("deploy TruthInTheFlip --env production\n", stdout.ToString().Replace("\r\n", "\n"));
        }
        finally
        {
            if (File.Exists(tempMap)) File.Delete(tempMap);
        }
    }

    [Fact]
    public void Pipeline_MultipleMacrosFiles_LaterFileOverridesEarlier()
    {
        string tempMap1 = Path.GetTempFileName();
        string tempMap2 = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempMap1, """
            {
                "CONFIG": "Debug",
                "ARCH": "x86"
            }
            """);

            File.WriteAllText(tempMap2, """
            {
                "CONFIG": "Release"
            }
            """);

            string[] args = ["-macros", tempMap1, "-macros", tempMap2, "--", "build", "CONFIG", "ARCH"];
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.Equal("build Release x86\n", stdout.ToString().Replace("\r\n", "\n"));
        }
        finally
        {
            if (File.Exists(tempMap1)) File.Delete(tempMap1);
            if (File.Exists(tempMap2)) File.Delete(tempMap2);
        }
    }

    [Fact]
    public void Pipeline_InlineMacro_OverridesMacrosFile()
    {
        string tempMap = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempMap, """
            {
                "REGION": "us-east-1",
                "ZONE": "a"
            }
            """);

            string[] args = ["-macros", tempMap, "-macro", "REGION=eu-west-1", "--", "cluster", "REGION", "ZONE"];
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.Equal("cluster eu-west-1 a\n", stdout.ToString().Replace("\r\n", "\n"));
        }
        finally
        {
            if (File.Exists(tempMap)) File.Delete(tempMap);
        }
    }

    [Fact]
    public void Pipeline_Settings_MacrosFilesAndMacroStore_LoadsAndSubstitutes()
    {
        string tempSettings = Path.GetTempFileName();
        string tempMap = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempMap, """
            {
                "BASE_PATH": "/opt/app"
            }
            """);

            File.WriteAllText(tempSettings, $$"""
            {
                "macros": ["{{tempMap.Replace("\\", "\\\\")}}"],
                "macroStore": {
                    "PORT": "8080"
                }
            }
            """);

            string[] args = ["-settings", tempSettings, "--", "start", "BASE_PATH", "PORT"];
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.Equal("start /opt/app 8080\n", stdout.ToString().Replace("\r\n", "\n"));
        }
        finally
        {
            if (File.Exists(tempSettings)) File.Delete(tempSettings);
            if (File.Exists(tempMap)) File.Delete(tempMap);
        }
    }

    [Fact]
    public void Pipeline_Settings_Macros_RelativePathResolution()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "cli_macro_rel_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string mapPath = Path.Combine(tempDir, "shared_macros.json");
            File.WriteAllText(mapPath, """
            {
                "HOST": "127.0.0.1"
            }
            """);

            string settingsPath = Path.Combine(tempDir, "settings.json");
            File.WriteAllText(settingsPath, """
            {
                "macros": ["shared_macros.json"]
            }
            """);

            string[] args = ["-settings", settingsPath, "--", "connect", "HOST"];
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.Equal("connect 127.0.0.1\n", stdout.ToString().Replace("\r\n", "\n"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Pipeline_MacroPrecedence_UserSettings_ExplicitSettings_CliMacros_CliInlineMacro()
    {
        string userSettingsPath = Path.GetTempFileName();
        string userMap = Path.GetTempFileName();
        string explicitSettingsPath = Path.GetTempFileName();
        string explicitMap = Path.GetTempFileName();
        string cliMap = Path.GetTempFileName();

        try
        {
            File.WriteAllText(userMap, """
            {
                "V1": "user_map",
                "V2": "user_map",
                "V3": "user_map",
                "V4": "user_map"
            }
            """);
            File.WriteAllText(userSettingsPath, $$"""
            {
                "macros": ["{{userMap.Replace("\\", "\\\\")}}"]
            }
            """);

            File.WriteAllText(explicitMap, """
            {
                "V2": "explicit_map",
                "V3": "explicit_map",
                "V4": "explicit_map"
            }
            """);
            File.WriteAllText(explicitSettingsPath, $$"""
            {
                "macros": ["{{explicitMap.Replace("\\", "\\\\")}}"]
            }
            """);

            File.WriteAllText(cliMap, """
            {
                "V3": "cli_map",
                "V4": "cli_map"
            }
            """);

            string[] args =
            [
                "-settings", explicitSettingsPath,
                "-macros", cliMap,
                "-macro", "V4=cli_inline",
                "--",
                "V1", "V2", "V3", "V4"
            ];

            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr, defaultSettingsOverridePath: userSettingsPath);

            Assert.Equal(0, exitCode);
            Assert.Equal("user_map explicit_map cli_map cli_inline\n", stdout.ToString().Replace("\r\n", "\n"));
        }
        finally
        {
            if (File.Exists(userSettingsPath)) File.Delete(userSettingsPath);
            if (File.Exists(userMap)) File.Delete(userMap);
            if (File.Exists(explicitSettingsPath)) File.Delete(explicitSettingsPath);
            if (File.Exists(explicitMap)) File.Delete(explicitMap);
            if (File.Exists(cliMap)) File.Delete(cliMap);
        }
    }

    [Fact]
    public void Pipeline_Macros_MissingFile_ReturnsError()
    {
        string[] args = ["-macros", "non_existent_macro_map_12345.json", "--", "echo", "test"];
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(args, stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("Macro map file not found", stderr.ToString());
    }

    [Fact]
    public void Pipeline_Macros_InvalidJson_ReturnsError()
    {
        string tempMap = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempMap, "{ not valid json }");

            string[] args = ["-macros", tempMap, "--", "echo", "test"];
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(1, exitCode);
            Assert.Contains("Failed to parse macro map JSON", stderr.ToString());
        }
        finally
        {
            if (File.Exists(tempMap)) File.Delete(tempMap);
        }
    }

    [Fact]
    public void Pipeline_Macros_NonStringValue_ReturnsError()
    {
        string tempMap = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempMap, """
            {
                "KEY": 123
            }
            """);

            string[] args = ["-macros", tempMap, "--", "echo", "test"];
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(1, exitCode);
            Assert.Contains("must have a string value", stderr.ToString());
        }
        finally
        {
            if (File.Exists(tempMap)) File.Delete(tempMap);
        }
    }

    [Fact]
    public void Pipeline_Macros_ComposedWithExpandAndJoin()
    {
        string tempMap = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempMap, """
            {
                "EXT": ".png",
                "PREFIX": "img"
            }
            """);

            string[] args =
            [
                "-macros", tempMap,
                "-macro", "SEP=_",
                "--",
                ".expand.", "NAME", "banner", "icon", ":",
                    ".join.", "PREFIX", "SEP", "NAME", "EXT", ".join_end.",
                ".expand_end."
            ];
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            int exitCode = Program.Run(args, stdout, stderr);

            Assert.Equal(0, exitCode);
            Assert.Equal("img_banner.png img_icon.png\n", stdout.ToString().Replace("\r\n", "\n"));
        }
        finally
        {
            if (File.Exists(tempMap)) File.Delete(tempMap);
        }
    }

    #endregion
}
