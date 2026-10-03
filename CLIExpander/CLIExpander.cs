using System.Text;

namespace CLIExpanderNs;

/// <summary>
/// Expands combinatorial template blocks (.expand. ... .expand_end.) and variables in command-line arguments.
/// Supports extension and custom resolution via subclassing (e.g. overriding <see cref="GetByKey(string)"/>
/// or <see cref="Process(List{string}, ref int, List{string})"/>).
/// </summary>
public class CLIExpander
{
    /// <summary>
    /// Represents the status and diagnostic message resulting from an expansion operation.
    /// </summary>
    public struct CLReturn
    {
        /// <summary>
        /// The result status code (0 for success, non-zero for error).
        /// </summary>
        public int Status;

        /// <summary>
        /// A descriptive error message if an error occurred; otherwise null.
        /// </summary>
        public string? Message;
    }

    /// <summary>
    /// Stack tracking the scope high-water marks for unwinding nested block variables.
    /// </summary>
    public Stack<int> ScopeWaterMarks = new Stack<int>();

    /// <summary>
    /// Active scoped variable bindings as (key, value) pairs.
    /// </summary>
    public List<(string, string)> Scope = new();

    /// <summary>
    /// Explicit known values dictionary consulted by the default <see cref="GetKnownValue(string)"/> implementation.
    /// </summary>
    public IDictionary<string, string> ValueStore { get; set; }

    /// <summary>
    /// Baseline macro bindings dictionary consulted by <see cref="GetByKey(string)"/>
    /// when a variable is not found in the active lexical <see cref="Scope"/>.
    /// </summary>
    public IDictionary<string, string> MacroStore { get; set; }

    /// <summary>
    /// Captures the creation timestamp of this <see cref="CLIExpander"/> instance, used for stable timestamp generation.
    /// </summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>
    /// Token that marks the beginning of an expansion block. Defaults to <c>".expand."</c>.
    /// </summary>
    public string ExpandStartToken = ".expand.";

    /// <summary>
    /// Token that marks the end of an expansion block. Defaults to <c>".expand_end."</c>.
    /// </summary>
    public string ExpandEndToken = ".expand_end.";

    /// <summary>
    /// Token delimiter that separates the variable alternatives list from the block body template. Defaults to <c>":"</c>.
    /// </summary>
    public string DelimiterToken = ":";

    /// <summary>
    /// Token that marks the beginning of a token concatenation block. Defaults to <c>".join."</c>.
    /// </summary>
    public string JoinStartToken = ".join.";

    /// <summary>
    /// Token that marks the end of a token concatenation block. Defaults to <c>".join_end."</c>.
    /// </summary>
    public string JoinEndToken = ".join_end.";

    /// <summary>
    /// Token that retrieves an explicit known value by key as a single exact token. Defaults to <c>".get."</c>.
    /// </summary>
    public string GetToken = ".get.";

    /// <summary>
    /// Token that retrieves an explicit known value by key and splits it by whitespace into individual tokens. Defaults to <c>".split_get."</c>.
    /// </summary>
    public string SplitGetToken = ".split_get.";

    /// <summary>
    /// Initializes a new instance of <see cref="CLIExpander"/> capturing the current local timestamp.
    /// </summary>
    public CLIExpander() : this(DateTimeOffset.Now)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CLIExpander"/> with an explicit captured timestamp.
    /// </summary>
    /// <param name="startedAt">The timestamp instance to anchor time-based built-in values.</param>
    public CLIExpander(DateTimeOffset startedAt)
    {
        StartedAt = startedAt;
        ValueStore = new Dictionary<string, string>(StringComparer.Ordinal);
        MacroStore = new Dictionary<string, string>(StringComparer.Ordinal);
        InitializeValueStore();
    }

    /// <summary>
    /// Seeds the built-in known values into <see cref="ValueStore"/>.
    /// Subclasses may override this method to customize or extend the initial known values set.
    /// </summary>
    protected virtual void InitializeValueStore()
    {
        ValueStore["newline"] = Environment.NewLine;
        ValueStore["space"] = " ";
        ValueStore["tab"] = "\t";
        ValueStore["empty"] = "";
        ValueStore["now"] = StartedAt.ToString("O");
        ValueStore["utc_now"] = StartedAt.ToUniversalTime().ToString("O");
        ValueStore["timestamp"] = StartedAt.ToString("_yyyyMMdd_HHmmss_ff");
        ValueStore["utimestamp"] = StartedAt.ToUniversalTime().ToString("_yyyyMMdd_HHmmss_ff");
        ValueStore["dir_sep"] = Path.DirectorySeparatorChar.ToString();
        ValueStore["path_sep"] = Path.PathSeparator.ToString();
    }

    /// <summary>
    /// Resolves an explicit known value by key.
    /// Subclasses may override this method to provide authoritative or dynamic known-value resolution (e.g. system properties, external lookups).
    /// </summary>
    /// <param name="key">The known-value identifier to look up.</param>
    /// <returns>The resolved string value, or null if the key is unknown.</returns>
    public virtual string? GetKnownValue(string key)
    {
        if (ValueStore != null && ValueStore.TryGetValue(key, out var val))
            return val;
        return null;
    }

    /// <summary>
    /// Resolves a variable value by key from active scopes or fallback <see cref="MacroStore"/>.
    /// Subclasses may override this method to provide custom or fallback variable resolution (e.g. environment variables or dynamic mappings).
    /// </summary>
    /// <param name="key">The variable identifier to look up.</param>
    /// <returns>The bound variable string value, or null if not found.</returns>
    public virtual string? GetByKey(string key)
    {
        for (int i = Scope.Count - 1; i >= 0; i--)
            if (Scope[i].Item1 == key)
                return Scope[i].Item2;
        if (MacroStore != null && MacroStore.TryGetValue(key, out var val))
            return val;
        return null;
    }

    /// <summary>
    /// When true, allows matching and substituting embedded identifiers within compound tokens (e.g. 'prefix._var.suffix').
    /// When false, only exact full-token matches are substituted. Defaults to true.
    /// </summary>
    public bool MatchPartials = true;

    /// <summary>
    /// When true, splits whitespace-separated values from full-token variable matches into individual tokens.
    /// When false, the resolved variable string is emitted directly as a single token. Defaults to true.
    /// </summary>
    public bool SplitAfterGetByKey = true;

    /// <summary>
    /// Splits a string value into individual tokens.
    /// Subclasses may override this method to customize token splitting behavior (e.g. custom delimiters, quote-aware tokenization, or regex splitting).
    /// </summary>
    /// <param name="text">The string to split into tokens.</param>
    /// <returns>An enumerable collection of string tokens.</returns>
    public virtual IEnumerable<string> Split(string text)
    {
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Determines whether the specified character is valid as the start of an identifier for inner-token substitution.
    /// Subclasses may override this method to customize valid identifier start characters.
    /// </summary>
    /// <param name="c">The character to test.</param>
    /// <returns>True if the character can start an identifier; otherwise false.</returns>
    public virtual bool IsIdentifierStart(char c)
    {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
    }

    /// <summary>
    /// Determines whether the specified character is valid as a subsequent character in an identifier for inner-token substitution.
    /// Subclasses may override this method to customize valid identifier continuation characters.
    /// </summary>
    /// <param name="c">The character to test.</param>
    /// <returns>True if the character can be part of an identifier; otherwise false.</returns>
    public virtual bool IsIdentifierPart(char c)
    {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
    }

    /// <summary>
    /// Recursively processes input tokens starting at <paramref name="index"/>, evaluating expansion blocks and variable substitutions.
    /// Subclasses may override this method to customize or intercept token evaluation.
    /// </summary>
    /// <param name="input">The tokenized input argument list.</param>
    /// <param name="index">The current reading index in <paramref name="input"/>.</param>
    /// <param name="output">The list receiving expanded output tokens.</param>
    /// <returns>A <see cref="CLReturn"/> indicating success or error details.</returns>
    public virtual CLReturn Process(List<string> input, ref int index, List<String> output)
    {
        if (index >= input.Count)
            return new CLReturn() { Status = 1, Message = "CLIExpander: Unexpected end of input" }; 
        
        again:
        CLReturn Return = new CLReturn();

        while (index < input.Count 
            && input[index] != ExpandStartToken 
            && input[index] != ExpandEndToken 
            && input[index] != JoinStartToken 
            && input[index] != JoinEndToken
            && input[index] != GetToken
            && input[index] != SplitGetToken)
        {
            string sourceText = input[index];
            
            // Expand by whitespace split on full string match
            var sub = GetByKey(sourceText);
            if (sub != null)
            {
                if (SplitAfterGetByKey)
                {
                    foreach (var item in Split(sub))
                    {
                        output.Add(item);
                    }
                }
                else output.Add(sub);
            }
            else if (MatchPartials) // attempt inner substitution
            {
                StringBuilder completed = new StringBuilder();
                StringBuilder? identifier = null;

                for (int i = 0; i < sourceText.Length; i++)
                {
                    char c = sourceText[i];
                    bool id_char = identifier == null ? IsIdentifierStart(c) : IsIdentifierPart(c);

                    if (id_char)
                    {
                        if (identifier == null) identifier = new StringBuilder();
                        identifier.Append(c);
                    }
                    else
                    {
                        if (identifier != null)
                        {
                            string id = identifier.ToString();
                            identifier = null;
                            
                            var s = GetByKey(id);
                            if (s != null)
                            {
                                if (s.Any(char.IsWhiteSpace))
                                    return new CLReturn() { Status = 1, Message = $"CLIExpander: Variable '{id}' contains whitespace" };
                                completed.Append(s);
                            }
                            else completed.Append(id);
                        }

                        completed.Append(c);
                    }

                }
                
                if (identifier != null)
                {
                    string id = identifier.ToString();
                    identifier = null;
                            
                    var s = GetByKey(id);
                    if (s != null)
                    {
                        if (s.Any(char.IsWhiteSpace))
                            return new CLReturn() { Status = 1, Message = $"CLIExpander: Variable '{id}' contains whitespace" };
                        completed.Append(s);
                    }
                    else completed.Append(id);
                }

                output.Add(completed.ToString());
            } else output.Add(sourceText);
            
            index++;
        }

        if (index < input.Count && input[index] == ExpandStartToken)
        {
            index++;
            if (index >= input.Count)
                return new CLReturn() { Status = 1, Message = $"CLIExpander: Expected variable name after {ExpandStartToken}" };

            string key = input[index];
            index++;
            if (index >= input.Count)
                return new CLReturn() { Status = 1, Message = $"CLIExpander: Missing '{DelimiterToken}' after variable '{key}'" };

            List<String> inputs = new List<String>();
            while (index < input.Count && input[index] != DelimiterToken)
            {
                inputs.Add(input[index]);
                index++;
            }

            if (index >= input.Count)
                return new CLReturn()
                    { Status = 1, Message = $"CLIExpander: Missing '{DelimiterToken}' delimiter for variable '{key}'" };

            index++;

            int end_position = -1;

            foreach (String inp in inputs)
            {
                ScopeWaterMarks.Push(Scope.Count);
                Scope.Add((key, inp));

                try
                {
                    int index2 = index;
                    Return = Process(input, ref index2, output);
                    if (Return.Status != 0)
                    {
                        return Return;
                    }

                    if (index2 >= input.Count || input[index2] != ExpandEndToken)
                        return new CLReturn() { Status = 1, Message = $"CLIExpander: Expected {ExpandEndToken}" };
                    if (end_position == -1) end_position = index2;
                }
                finally
                {
                    int water = ScopeWaterMarks.Pop();
                    while (Scope.Count > water) Scope.RemoveAt(Scope.Count - 1);
                }
            }

            if (end_position != -1)
            {
                index = end_position;
            }
            else
            {
                if (index >= input.Count || input[index] != ExpandEndToken)
                    return new CLReturn() { Status = 1, Message = $"CLIExpander: Expected {ExpandEndToken}" };
            }

            index++;
            if (index < input.Count)
                goto again;
        }
        else if (index < input.Count && input[index] == JoinStartToken)
        {
            index++;
            List<string> tempTokens = new();

            while (index < input.Count && input[index] != JoinEndToken)
            {
                if (input[index] == ExpandEndToken)
                    return new CLReturn() { Status = 1, Message = $"CLIExpander: Unexpected {ExpandEndToken}" };

                Return = Process(input, ref index, tempTokens);
                if (Return.Status != 0)
                    return Return;
            }

            if (index >= input.Count || input[index] != JoinEndToken)
                return new CLReturn() { Status = 1, Message = $"CLIExpander: Expected {JoinEndToken}" };

            index++;
            output.Add(string.Concat(tempTokens));

            if (index < input.Count)
                goto again;
        }
        else if (index < input.Count && input[index] == GetToken)
        {
            index++;
            if (index >= input.Count)
                return new CLReturn() { Status = 1, Message = $"CLIExpander: Expected key after {GetToken}" };

            string key = input[index];
            index++;

            string? val = GetKnownValue(key);
            if (val == null)
                return new CLReturn() { Status = 1, Message = $"CLIExpander: Unknown value '{key}'" };

            output.Add(val);

            if (index < input.Count)
                goto again;
        }
        else if (index < input.Count && input[index] == SplitGetToken)
        {
            index++;
            if (index >= input.Count)
                return new CLReturn() { Status = 1, Message = $"CLIExpander: Expected key after {SplitGetToken}" };

            string key = input[index];
            index++;

            string? val = GetKnownValue(key);
            if (val == null)
                return new CLReturn() { Status = 1, Message = $"CLIExpander: Unknown value '{key}'" };

            foreach (var item in Split(val))
            {
                output.Add(item);
            }

            if (index < input.Count)
                goto again;
        }

        return new();
    }

    /// <summary>
    /// Processes a full tokenized input list, expanding all blocks and variables.
    /// </summary>
    /// <param name="input">The tokenized input command line.</param>
    /// <param name="output">The resulting expanded list of string tokens.</param>
    /// <param name="newExpander">Optional factory delegate to create a custom <see cref="CLIExpander"/> instance/subclass.</param>
    /// <returns>A <see cref="CLReturn"/> indicating success or error details.</returns>
    public static CLReturn Process(List<string> input, out List<String> output, Func<CLIExpander>? newExpander = null)
    {
        CLIExpander expand = (newExpander != null) ? newExpander() : new CLIExpander();
        int position = 0;
        output = new();

        CLReturn me = expand.Process(input, ref position, output);
        if (me.Status != 0) return me;
        if (position < input.Count)
        {
            if (input[position] == expand.ExpandEndToken)
                return new CLReturn() { Status = 1, Message = $"CLIExpander: Unexpected {expand.ExpandEndToken}" };
            if (input[position] == expand.JoinEndToken)
                return new CLReturn() { Status = 1, Message = $"CLIExpander: Unexpected {expand.JoinEndToken}" };
            return new CLReturn()
                { Status = 1, Message = $"CLIExpander: Incomplete parsing of input at '{input[position]}'" };
        }

        return new CLReturn() { Status = 0 };
    }
}