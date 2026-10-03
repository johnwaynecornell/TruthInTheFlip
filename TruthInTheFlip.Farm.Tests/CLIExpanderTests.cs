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

    #region Join Block (.join. ... .join_end.) Tests

    [Fact]
    public void Join_BasicTwoTokens_ConcatenatesWithNoSeparator()
    {
        var input = new List<string> { ".join.", "hello", "world", ".join_end." };
        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "helloworld" }, output);
    }

    [Fact]
    public void Join_FilenameAndPathConstruction_ConcatenatesAllTokens()
    {
        var input1 = new List<string> { ".join.", "Quant", ".", "tkr", ".join_end." };
        var status1 = CLIExpander.Process(input1, out var output1);

        Assert.Equal(0, status1.Status);
        Assert.Equal(new[] { "Quant.tkr" }, output1);

        var input2 = new List<string> { ".join.", "/tmp/", "report", ".", "csv", ".join_end." };
        var status2 = CLIExpander.Process(input2, out var output2);

        Assert.Equal(0, status2.Status);
        Assert.Equal(new[] { "/tmp/report.csv" }, output2);
    }

    [Fact]
    public void Join_PreservesWhitespaceInsideTokens()
    {
        var input = new List<string> { ".join.", "/home/jwc/", "my file", ".join_end." };
        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(0, status.Status);
        Assert.Single(output);
        Assert.Equal("/home/jwc/my file", output[0]);
    }

    [Fact]
    public void Join_EmptyBlock_EmitsSingleEmptyStringToken()
    {
        var input = new List<string> { ".join.", ".join_end." };
        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(0, status.Status);
        Assert.Single(output);
        Assert.Equal("", output[0]);
    }

    [Fact]
    public void Join_InsideExpandBlock_EvaluatesPerIteration()
    {
        var input = new List<string>
        {
            ".expand.", "_name", "Quant", "Quant2", ":",
            ".join.", "/data/", "_name", ".tkr", ".join_end.",
            ".expand_end."
        };

        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "/data/Quant.tkr", "/data/Quant2.tkr" }, output);
    }

    [Fact]
    public void Join_CartesianExpansionWithJoin_GeneratesAllCombinations()
    {
        var input = new List<string>
        {
            ".expand.", "_base", "Quant", "Quant2", ":",
            ".expand.", "_ext", "tkr", "csv", ":",
            ".join.", "_base", ".", "_ext", ".join_end.",
            ".expand_end.",
            ".expand_end."
        };

        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "Quant.tkr", "Quant.csv", "Quant2.tkr", "Quant2.csv" }, output);
    }

    [Fact]
    public void Join_ExpandInsideJoin_ConcatenatesAllBranchEmissionsIntoOneToken()
    {
        var input = new List<string>
        {
            ".join.",
            "prefix-",
            ".expand.", "_x", "a", "b", ":",
            "_x",
            ".expand_end.",
            "suffix",
            ".join_end."
        };

        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(0, status.Status);
        Assert.Single(output);
        Assert.Equal("prefix-absuffix", output[0]);
    }

    [Fact]
    public void Join_NestedJoins_ConcatenatesRecursively()
    {
        var input = new List<string>
        {
            ".join.",
            "root/",
            ".join.", "child", "/", "leaf", ".join_end.",
            ".txt",
            ".join_end."
        };

        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(0, status.Status);
        Assert.Single(output);
        Assert.Equal("root/child/leaf.txt", output[0]);
    }

    [Fact]
    public void Join_Substitutions_ExactAndInnerSubstitutionsInsideJoin()
    {
        var input = new List<string>
        {
            ".expand.", "_item", "item0", "item1", ":",
            ".expand.", "_metric", "mean", "stddev", ":",
            ".join.", "stat_", "_item._metric", ".dat", ".join_end.",
            ".expand_end.",
            ".expand_end."
        };

        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[]
        {
            "stat_item0.mean.dat",
            "stat_item0.stddev.dat",
            "stat_item1.mean.dat",
            "stat_item1.stddev.dat"
        }, output);
    }

    [Fact]
    public void Join_CustomSyntaxTokens_ExpandsConfiguredTokens()
    {
        var input = new List<string>
        {
            "@join", "path/", "file", ".ext", "@endjoin"
        };

        var status = CLIExpander.Process(input, out var output, () => new CLIExpander
        {
            JoinStartToken = "@join",
            JoinEndToken = "@endjoin"
        });

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "path/file.ext" }, output);
    }

    [Fact]
    public void Join_MissingEndToken_ReturnsDescriptiveError()
    {
        var input = new List<string> { ".join.", "hello", "world" };
        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Expected .join_end.", status.Message);
    }

    [Fact]
    public void Join_UnexpectedEndTokenAtRoot_ReturnsDescriptiveError()
    {
        var input = new List<string> { "hello", ".join_end." };
        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Unexpected .join_end.", status.Message);
    }

    [Fact]
    public void Join_UnexpectedExpandEndInsideJoin_ReturnsDescriptiveError()
    {
        var input = new List<string> { ".join.", "hello", ".expand_end.", ".join_end." };
        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Unexpected .expand_end.", status.Message);
    }

    #endregion

    #region Known Values (.get. and .split_get.) Tests

    [Fact]
    public void Get_BuiltInWhitespaceTokens_EmittedAsExactTokens()
    {
        var input = new List<string> { "a", ".get.", "space", "b", ".get.", "tab", "c", ".get.", "newline", "d", ".get.", "empty", "e" };
        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "a", " ", "b", "\t", "c", Environment.NewLine, "d", "", "e" }, output);
    }

    [Fact]
    public void Get_BuiltInPlatformSeparators_EmitsNativeSeparators()
    {
        var input = new List<string> { ".get.", "dir_sep", ".get.", "path_sep" };
        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { Path.DirectorySeparatorChar.ToString(), Path.PathSeparator.ToString() }, output);
    }

    [Fact]
    public void Get_BuiltInTimestamps_DeriveFromCapturedStartedAt()
    {
        var fixedTime = new DateTimeOffset(2026, 10, 3, 9, 9, 29, 120, TimeSpan.FromHours(-4));
        var expander = new CLIExpander(fixedTime);

        Assert.Equal(fixedTime, expander.StartedAt);
        Assert.Equal(fixedTime.ToString("O"), expander.GetKnownValue("now"));
        Assert.Equal(fixedTime.ToUniversalTime().ToString("O"), expander.GetKnownValue("utc_now"));
        Assert.Equal(fixedTime.ToString("_yyyyMMdd_HHmmss_ff"), expander.GetKnownValue("timestamp"));
        Assert.Equal(fixedTime.ToUniversalTime().ToString("_yyyyMMdd_HHmmss_ff"), expander.GetKnownValue("utimestamp"));
    }

    [Fact]
    public void Get_TimestampStability_RepeatedLookupsYieldIdenticalInstant()
    {
        var expander = new CLIExpander();
        var input = new List<string>
        {
            ".expand.", "x", "1", "2", "3", ":",
            ".get.", "now",
            ".expand_end."
        };

        var status = CLIExpander.Process(input, out var output, () => expander);

        Assert.Equal(0, status.Status);
        Assert.Equal(3, output.Count);
        Assert.Equal(output[0], output[1]);
        Assert.Equal(output[1], output[2]);
        Assert.Equal(expander.GetKnownValue("now"), output[0]);
    }

    [Fact]
    public void Get_ScopeVsValueStoreSeparation_BareWordNotSubstituted()
    {
        var expander = new CLIExpander();
        expander.ValueStore["project"] = "TruthInTheFlip";

        // Plain token 'project' should not be substituted because it is in ValueStore, not Scope
        var input = new List<string> { "project", ".get.", "project" };
        var status = CLIExpander.Process(input, out var output, () => expander);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "project", "TruthInTheFlip" }, output);
    }

    [Fact]
    public void Get_ScopeAndValueStoreCoexistWithSameKeyName()
    {
        var fixedTime = new DateTimeOffset(2026, 10, 3, 9, 9, 29, TimeSpan.Zero);
        var expander = new CLIExpander(fixedTime);

        // Binding 'now' in .expand. scope shadows the lexical variable, but .get. now still retrieves ValueStore["now"]
        var input = new List<string>
        {
            ".expand.", "now", "yesterday", "tomorrow", ":",
            "now", ".get.", "now",
            ".expand_end."
        };

        var status = CLIExpander.Process(input, out var output, () => expander);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[]
        {
            "yesterday", fixedTime.ToString("O"),
            "tomorrow", fixedTime.ToString("O")
        }, output);
    }

    [Fact]
    public void Get_ExactTokenSemantics_DoesNotSplitWhitespace()
    {
        var expander = new CLIExpander();
        expander.ValueStore["title"] = "My Project File";
        expander.SplitAfterGetByKey = true; // Ensure SplitAfterGetByKey does not affect .get.

        var input = new List<string> { "name", ".get.", "title" };
        var status = CLIExpander.Process(input, out var output, () => expander);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "name", "My Project File" }, output);
    }

    [Fact]
    public void SplitGet_SplitsValueOnWhitespace()
    {
        var expander = new CLIExpander();
        expander.ValueStore["flags"] = "--verbose --debug --all";

        var input = new List<string> { "run", ".split_get.", "flags", "target" };
        var status = CLIExpander.Process(input, out var output, () => expander);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "run", "--verbose", "--debug", "--all", "target" }, output);
    }

    [Fact]
    public void Get_InsideJoinBlock_ComposesIntoSingleToken()
    {
        var expander = new CLIExpander();
        expander.ValueStore["project"] = "TruthInTheFlip";

        var input = new List<string>
        {
            ".join.", "/tmp/", ".get.", "project", ".txt", ".join_end."
        };

        var status = CLIExpander.Process(input, out var output, () => expander);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "/tmp/TruthInTheFlip.txt" }, output);
    }

    [Fact]
    public void Get_InsideJoinWithExpand_GeneratesSeparatedTokens()
    {
        var input = new List<string>
        {
            ".expand.", "Fruit", "apple", "orange", ":",
            ".join.", "Fruit", ".png", ".join_end.",
            ".get.", "newline",
            ".expand_end."
        };

        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "apple.png", Environment.NewLine, "orange.png", Environment.NewLine }, output);
    }

    private class CustomResolverExpander : CLIExpander
    {
        public override string? GetKnownValue(string key)
        {
            if (key == "dynamic_host")
                return "server42.internal";
            return base.GetKnownValue(key);
        }
    }

    [Fact]
    public void Get_VirtualGetKnownValueOverride_AuthoritativeResolution()
    {
        var input = new List<string> { "connect", ".get.", "dynamic_host", ".get.", "space" };
        var status = CLIExpander.Process(input, out var output, () => new CustomResolverExpander());

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "connect", "server42.internal", " " }, output);
    }

    [Fact]
    public void Get_CustomGetAndSplitTokens_Configurable()
    {
        var expander = new CLIExpander
        {
            GetToken = "@get",
            SplitGetToken = "@split_get"
        };
        expander.ValueStore["msg"] = "hello world";

        var input = new List<string> { "@get", "msg", "@split_get", "msg" };
        var status = CLIExpander.Process(input, out var output, () => expander);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "hello world", "hello", "world" }, output);
    }

    [Fact]
    public void Get_MissingKey_ReturnsDescriptiveError()
    {
        var input = new List<string> { "echo", ".get." };
        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Expected key after .get.", status.Message);
    }

    [Fact]
    public void SplitGet_MissingKey_ReturnsDescriptiveError()
    {
        var input = new List<string> { "echo", ".split_get." };
        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Expected key after .split_get.", status.Message);
    }

    [Fact]
    public void Get_UnknownKey_ReturnsDescriptiveError()
    {
        var input = new List<string> { "echo", ".get.", "undefined_variable" };
        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Unknown value 'undefined_variable'", status.Message);
    }

    [Fact]
    public void SplitGet_UnknownKey_ReturnsDescriptiveError()
    {
        var input = new List<string> { "echo", ".split_get.", "undefined_variable" };
        var status = CLIExpander.Process(input, out var output);

        Assert.Equal(1, status.Status);
        Assert.Equal("CLIExpander: Unknown value 'undefined_variable'", status.Message);
    }

    #endregion

    #region MacroStore Baseline Tests

    [Fact]
    public void MacroStore_FullTokenSubstitution_SubstitutesAndSplits()
    {
        var expander = new CLIExpander();
        expander.MacroStore["CONFIG"] = "Release";
        expander.MacroStore["FLAGS"] = "-O3 --strip";

        var input = new List<string> { "build", "CONFIG", "FLAGS" };
        var status = CLIExpander.Process(input, out var output, () => expander);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "build", "Release", "-O3", "--strip" }, output);
    }

    [Fact]
    public void MacroStore_DisabledSplitAfterGetByKey_EmitsSingleToken()
    {
        var expander = new CLIExpander
        {
            SplitAfterGetByKey = false
        };
        expander.MacroStore["FLAGS"] = "-O3 --strip";

        var input = new List<string> { "build", "FLAGS" };
        var status = CLIExpander.Process(input, out var output, () => expander);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "build", "-O3 --strip" }, output);
    }

    [Fact]
    public void MacroStore_PartialSubstitution_EmbedsCorrectly()
    {
        var expander = new CLIExpander();
        expander.MacroStore["ARCH"] = "x64";

        var input = new List<string> { "app-ARCH-binary", "target" };
        var status = CLIExpander.Process(input, out var output, () => expander);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "app-x64-binary", "target" }, output);
    }

    [Fact]
    public void MacroStore_ScopeShadowing_BlockScopeShadowsMacroStoreAndRestoresAfterBlock()
    {
        var expander = new CLIExpander();
        expander.MacroStore["TARGET"] = "default_target";

        var input = new List<string>
        {
            "before", "TARGET",
            ".expand.", "TARGET", "alpha", "beta", ":",
                "in", "TARGET",
            ".expand_end.",
            "after", "TARGET"
        };
        var status = CLIExpander.Process(input, out var output, () => expander);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[]
        {
            "before", "default_target",
            "in", "alpha",
            "in", "beta",
            "after", "default_target"
        }, output);
    }

    [Fact]
    public void MacroStore_UnknownKey_RemainsLiteralText()
    {
        var expander = new CLIExpander();
        expander.MacroStore["DEFINED_VAR"] = "val";

        var input = new List<string> { "DEFINED_VAR", "UNDEFINED_VAR" };
        var status = CLIExpander.Process(input, out var output, () => expander);

        Assert.Equal(0, status.Status);
        Assert.Equal(new[] { "val", "UNDEFINED_VAR" }, output);
    }

    #endregion
}