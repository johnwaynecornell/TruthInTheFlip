using CLIExpanderNs;

namespace TruthInTheFlip.Farm.Tests;

public class CLIExpanderTests
{
    private static (CLIExpander.CLReturn Result, List<string> Output) Expand(string command)
    {
        List<string> input = command.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
        var status = CLIExpander.Process(input, out var output);
        return (status, output);
    }

    [Fact]
    public void Process_EmptyInputList_ReturnsUnexpectedEndOfInput()
    {
        var input = new List<string>();
        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Unexpected end of input", status.Message);
        Assert.Empty(output);
    }

    [Fact]
    public void Process_NoExpansionTokens_ReturnsInputAsOutput()
    {
        string command = "tracker window by_total 10B file test.tkr";
        var (status, output) = Expand(command);

        Assert.Equal(0, status.Status);
        Assert.Null(status.Message);
        Assert.Equal(new[] { "tracker", "window", "by_total", "10B", "file", "test.tkr" }, output);
    }

    [Fact]
    public void Process_SingleExpandBlock_ExpandsAlternativesInOrder()
    {
        string command = @".expand. _file A.tkr B.tkr C.tkr :
            file _file
        .expand_end.";

        var (status, output) = Expand(command);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "file", "A.tkr", "file", "B.tkr", "file", "C.tkr" }, output);
    }

    [Fact]
    public void Process_NestedCartesianProduct_ExpandsAllCombinations()
    {
        string command = @".expand. _file Quant.tkr Quant2.tkr :
            .expand. _size 10B 100B :
                tracker window by_total _size file _file
            .expand_end.
        .expand_end.";

        var (status, output) = Expand(command);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[]
        {
            "tracker", "window", "by_total", "10B", "file", "Quant.tkr",
            "tracker", "window", "by_total", "100B", "file", "Quant.tkr",
            "tracker", "window", "by_total", "10B", "file", "Quant2.tkr",
            "tracker", "window", "by_total", "100B", "file", "Quant2.tkr"
        }, output);
    }

    [Fact]
    public void Process_FragmentSubstitution_SplitsMultiTokenVariableValues()
    {
        var expander = new CLIExpander();
        expander.Scope.Add(("_flags", "--all --verbose --debug"));
        int index = 0;
        var output = new List<string>();
        var input = new List<string> { "run", "_flags", "target" };

        var status = expander.Process(input, ref index, output);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "run", "--all", "--verbose", "--debug", "target" }, output);
        Assert.Equal(3, index);
    }

    [Fact]
    public void Process_FragmentSubstitution_InsideExpandBlock()
    {
        var expander = new CLIExpander();
        // Values in Scope with internal whitespace will be split into multiple tokens
        expander.Scope.Add(("_profile", "fast release"));
        int index = 0;
        var output = new List<string>();
        var input = new List<string>
        {
            ".expand.", "_mode", "opt", "dbg", ":",
            "build", "_profile", "_mode",
            ".expand_end."
        };

        var status = expander.Process(input, ref index, output);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[]
        {
            "build", "fast", "release", "opt",
            "build", "fast", "release", "dbg"
        }, output);
    }

    [Fact]
    public void Process_NestedShadowing_InnerScopeOverridesOuterScopeAndRestores()
    {
        string command = @".expand. _val outer1 outer2 :
            .expand. _val inner1 inner2 :
                _val
            .expand_end.
        .expand_end.";

        var (status, output) = Expand(command);

        Assert.Equal(0, status.Status);
        // For outer1: inner expands to inner1, inner2
        // For outer2: inner expands to inner1, inner2
        Assert.Equal(new[] { "inner1", "inner2", "inner1", "inner2" }, output);
    }

    [Fact]
    public void Process_NestedShadowing_RestoresOuterScopeCorrectly()
    {
        var expander = new CLIExpander();
        expander.Scope.Add(("_x", "base"));
        Assert.Equal("base", expander.GetByKey("_x"));

        int index = 0;
        var output = new List<string>();
        var input = new List<string>
        {
            ".expand.", "_x", "shadow", ":",
            "_x",
            ".expand_end."
        };

        var status = expander.Process(input, ref index, output);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "shadow" }, output);
        // Ensure outer scope remained intact after unwinding
        Assert.Single(expander.Scope);
        Assert.Equal("base", expander.GetByKey("_x"));
    }

    [Fact]
    public void Process_EmptyAlternatives_EmptyBody_SucceedsWithNoOutput()
    {
        string command = ".expand. _var : .expand_end.";
        var (status, output) = Expand(command);

        Assert.Equal(0, status.Status);
        Assert.Empty(output);
    }

    [Fact]
    public void Process_EmptyAlternatives_NonEmptyBody_ReturnsExpectedExpandEnd()
    {
        string command = ".expand. _var : something .expand_end.";
        var (status, output) = Expand(command);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Expected .expand_end.", status.Message);
        Assert.Empty(output);
    }

    [Fact]
    public void Process_MissingVariableNameAfterExpand_ReturnsMeaningfulError()
    {
        var input = new List<string> { ".expand." };
        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Expected variable name after .expand.", status.Message);
        Assert.Empty(output);
    }

    [Fact]
    public void Process_MissingColonAfterVariable_ReturnsMeaningfulError()
    {
        var input = new List<string> { ".expand.", "_var" };
        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Missing ':' after variable '_var'", status.Message);
        Assert.Empty(output);
    }

    [Fact]
    public void Process_MissingColonDelimiterAfterValues_ReturnsMeaningfulError()
    {
        var input = new List<string> { ".expand.", "_var", "val1", "val2" };
        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Missing ':' delimiter for variable '_var'", status.Message);
        Assert.Empty(output);
    }

    [Fact]
    public void Process_MissingExpandEnd_ReturnsMeaningfulError()
    {
        string command = ".expand. _file A B : file _file";
        var (status, output) = Expand(command);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Expected .expand_end.", status.Message);
    }

    [Fact]
    public void Process_NestedMissingInnerExpandEnd_ReturnsMeaningfulError()
    {
        string command = @".expand. _outer 1 2 :
            .expand. _inner A B :
                _outer _inner
        .expand_end.";

        var (status, output) = Expand(command);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Expected .expand_end.", status.Message);
    }

    [Fact]
    public void Process_UnexpectedExpandEndAtRoot_ReturnsMeaningfulError()
    {
        var input = new List<string> { ".expand_end." };
        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Unexpected .expand_end.", status.Message);
        Assert.Empty(output);
    }

    [Fact]
    public void Process_PrefixAndBodyTokens_ExpandsCorrectly()
    {
        string command = @"prefix .expand. _x A B : item _x .expand_end.";
        var (status, output) = Expand(command);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "prefix", "item", "A", "item", "B" }, output);
    }

    [Fact]
    public void GetByKey_ReturnsCorrectScopedValueAndNullWhenNotFound()
    {
        var expander = new CLIExpander();
        Assert.Null(expander.GetByKey("unknown"));

        expander.Scope.Add(("var", "first"));
        Assert.Equal("first", expander.GetByKey("var"));

        expander.Scope.Add(("var", "second"));
        Assert.Equal("second", expander.GetByKey("var"));

        expander.Scope.RemoveAt(expander.Scope.Count - 1);
        Assert.Equal("first", expander.GetByKey("var"));
    }

    [Fact]
    public void Process_WhitespaceOnlyCommand_ProducesEmptyListAndUnexpectedEndOfInput()
    {
        string command = "    \t \r\n   ";
        var (status, output) = Expand(command);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Unexpected end of input", status.Message);
        Assert.Empty(output);
    }

    [Fact]
    public void Process_InnerSubstitution_WhitespaceInVariable_ReturnsError()
    {
        var expander = new CLIExpander();
        expander.Scope.Add(("_var", "val 1"));
        int index = 0;
        var output = new List<string>();
        var input = new List<string> { "prefix._var.suffix" };

        var status = expander.Process(input, ref index, output);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Variable '_var' contains whitespace", status.Message);
        Assert.Empty(output);
    }

    [Fact]
    public void Process_InnerSubstitution_WhitespaceInVariableAtEnd_ReturnsError()
    {
        var expander = new CLIExpander();
        expander.Scope.Add(("_var", "val 1"));
        int index = 0;
        var output = new List<string>();
        var input = new List<string> { "prefix._var" };

        var status = expander.Process(input, ref index, output);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Variable '_var' contains whitespace", status.Message);
        Assert.Empty(output);
    }

    [Fact]
    public void Process_InnerSubstitution_WithoutWhitespace_Succeeds()
    {
        var expander = new CLIExpander();
        expander.Scope.Add(("_var", "value"));
        int index = 0;
        var output = new List<string>();
        var input = new List<string> { "prefix._var.suffix", "file._var" };

        var status = expander.Process(input, ref index, output);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "prefix.value.suffix", "file.value" }, output);
    }

    private class CustomFallbackExpander : CLIExpander
    {
        private readonly Dictionary<string, string> _customGlobals;

        public CustomFallbackExpander(Dictionary<string, string> customGlobals)
        {
            _customGlobals = customGlobals;
        }

        public override string? GetByKey(string key)
        {
            var scoped = base.GetByKey(key);
            if (scoped != null) return scoped;
            return _customGlobals.TryGetValue(key, out var val) ? val : null;
        }
    }

    [Fact]
    public void Subclass_GetByKeyOverride_ProvidesFallbackVariableResolution()
    {
        var globals = new Dictionary<string, string>
        {
            { "_env", "production" },
            { "_ext", "json" }
        };

        var input = new List<string>
        {
            ".expand.", "_name", "app1", "app2", ":",
            "deploy", "_name", "_env", "config._ext",
            ".expand_end."
        };

        var status = CLIExpander.Process(input, out var output, () => new CustomFallbackExpander(globals));

        Assert.Equal(0, status.Status);
        Assert.Equal(new[]
        {
            "deploy", "app1", "production", "config.json",
            "deploy", "app2", "production", "config.json"
        }, output);
    }

    [Fact]
    public void Subclass_MatchPartialsFalse_DisablesInnerSubstitutions()
    {
        var expander = new CLIExpander { MatchPartials = false };
        expander.Scope.Add(("_var", "val"));
        int index = 0;
        var output = new List<string>();
        var input = new List<string> { "prefix._var.suffix", "_var" };

        var status = expander.Process(input, ref index, output);

        Assert.Equal(0, status.Status);
        // "prefix._var.suffix" should remain unmodified since MatchPartials is false; "_var" should expand
        Assert.Equal(new[] { "prefix._var.suffix", "val" }, output);
    }

    private class UppercaseTokenExpander : CLIExpander
    {
        public override CLReturn Process(List<string> input, ref int index, List<string> output)
        {
            var tempOutput = new List<string>();
            var ret = base.Process(input, ref index, tempOutput);
            if (ret.Status != 0) return ret;

            foreach (var tok in tempOutput)
            {
                output.Add(tok.ToUpperInvariant());
            }

            return ret;
        }
    }

    [Fact]
    public void Subclass_ProcessOverride_CustomizesExpansionPipeline()
    {
        var input = new List<string>
        {
            ".expand.", "_mode", "fast", "slow", ":",
            "run", "_mode",
            ".expand_end."
        };

        var status = CLIExpander.Process(input, out var output, () => new UppercaseTokenExpander());

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "RUN", "FAST", "RUN", "SLOW" }, output);
    }

    [Fact]
    public void SplitAfterGetByKey_WhenFalse_PreservesWhitespaceInFullTokenMatch()
    {
        var expander = new CLIExpander { SplitAfterGetByKey = false };
        expander.Scope.Add(("_flags", "--all --verbose --debug"));
        int index = 0;
        var output = new List<string>();
        var input = new List<string> { "run", "_flags", "target" };

        var status = expander.Process(input, ref index, output);

        Assert.Equal(0, status.Status);
        // With SplitAfterGetByKey = false, the entire string is emitted as a single token without splitting
        Assert.Equal(new[] { "run", "--all --verbose --debug", "target" }, output);
    }

    [Fact]
    public void SplitAfterGetByKey_WhenTrue_SplitsWhitespaceInFullTokenMatch()
    {
        var expander = new CLIExpander { SplitAfterGetByKey = true };
        expander.Scope.Add(("_flags", "--all --verbose --debug"));
        int index = 0;
        var output = new List<string>();
        var input = new List<string> { "run", "_flags", "target" };

        var status = expander.Process(input, ref index, output);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "run", "--all", "--verbose", "--debug", "target" }, output);
    }

    private class CommaDelimitedExpander : CLIExpander
    {
        public override IEnumerable<string> Split(string text)
        {
            return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }

    [Fact]
    public void Subclass_SplitOverride_CustomizesTokenSplitting()
    {
        var input = new List<string>
        {
            ".expand.", "_item", "itemA", "itemB", ":",
            "process", "_item", "_metrics",
            ".expand_end."
        };

        var globals = new Dictionary<string, string>
        {
            { "_metrics", "mean, stddev, min, max" }
        };

        var status = CLIExpander.Process(input, out var output, () => new CustomFallbackCommaExpander(globals));

        Assert.Equal(0, status.Status);
        Assert.Equal(new[]
        {
            "process", "itemA", "mean", "stddev", "min", "max",
            "process", "itemB", "mean", "stddev", "min", "max"
        }, output);
    }

    private class CustomFallbackCommaExpander : CommaDelimitedExpander
    {
        private readonly Dictionary<string, string> _globals;

        public CustomFallbackCommaExpander(Dictionary<string, string> globals)
        {
            _globals = globals;
        }

        public override string? GetByKey(string key)
        {
            var scoped = base.GetByKey(key);
            if (scoped != null) return scoped;
            return _globals.TryGetValue(key, out var val) ? val : null;
        }
    }

    [Fact]
    public void CustomSyntaxTokens_AssignableProperties_ExpandsCustomBlockSyntax()
    {
        var expander = new CLIExpander
        {
            ExpandStartToken = "@expand",
            ExpandEndToken = "@end",
            DelimiterToken = "in"
        };

        int index = 0;
        var output = new List<string>();
        var input = new List<string>
        {
            "@expand", "_file", "a.txt", "b.txt", "in",
            "process", "_file",
            "@end"
        };

        var status = expander.Process(input, ref index, output);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "process", "a.txt", "process", "b.txt" }, output);
    }

    [Fact]
    public void CustomSyntaxTokens_MissingDelimiter_ReportsConfiguredDelimiterInError()
    {
        var expander = new CLIExpander
        {
            ExpandStartToken = "@expand",
            ExpandEndToken = "@end",
            DelimiterToken = "in"
        };

        int index = 0;
        var output = new List<string>();
        var input = new List<string>
        {
            "@expand", "_file", "a.txt", "b.txt"
        };

        var status = expander.Process(input, ref index, output);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Missing 'in' delimiter for variable '_file'", status.Message);
    }

    [Fact]
    public void CustomSyntaxTokens_MissingEndToken_ReportsConfiguredEndTokenInError()
    {
        var expander = new CLIExpander
        {
            ExpandStartToken = "@expand",
            ExpandEndToken = "@end",
            DelimiterToken = "in"
        };

        int index = 0;
        var output = new List<string>();
        var input = new List<string>
        {
            "@expand", "_file", "a.txt", "in",
            "process", "_file"
        };

        var status = expander.Process(input, ref index, output);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Expected @end", status.Message);
    }

    [Fact]
    public void CustomSyntaxTokens_StaticProcessWithFactoryDelegate_WorksSeamlessly()
    {
        var input = new List<string>
        {
            "[expand]", "_env", "dev", "prod", "->",
            "deploy", "_env",
            "[/expand]"
        };

        var status = CLIExpander.Process(input, out var output, () => new CLIExpander
        {
            ExpandStartToken = "[expand]",
            ExpandEndToken = "[/expand]",
            DelimiterToken = "->"
        });

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "deploy", "dev", "deploy", "prod" }, output);
    }

    private class DollarIdentifierExpander : CLIExpander
    {
        public override bool IsIdentifierStart(char c)
        {
            return base.IsIdentifierStart(c) || c == '$';
        }

        public override bool IsIdentifierPart(char c)
        {
            return base.IsIdentifierPart(c) || c == '$';
        }
    }

    [Fact]
    public void Subclass_IsIdentifierStartAndPartOverrides_AllowsCustomIdentifierCharacters()
    {
        var expander = new DollarIdentifierExpander();
        expander.Scope.Add(("$VAR", "replaced"));

        int index = 0;
        var output = new List<string>();
        var input = new List<string> { "prefix.$VAR.suffix" };

        var status = expander.Process(input, ref index, output);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "prefix.replaced.suffix" }, output);
    }
}