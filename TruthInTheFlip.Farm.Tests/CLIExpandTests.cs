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
        var args = new[] { "echo", "it's", "hello world", "$VAR", "" };

        string result = renderer.Render(args);

        Assert.Equal("echo it's hello world $VAR ", result);
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

    #endregion
}
