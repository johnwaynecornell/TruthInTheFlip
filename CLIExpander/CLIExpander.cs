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
    /// Resolves a variable value by key from active scopes.
    /// Subclasses may override this method to provide custom or fallback variable resolution (e.g. environment variables or dynamic mappings).
    /// </summary>
    /// <param name="key">The variable identifier to look up.</param>
    /// <returns>The bound variable string value, or null if not found.</returns>
    public virtual string? GetByKey(string key)
    {
        for (int i = Scope.Count - 1; i >= 0; i--)
            if (Scope[i].Item1 == key)
                return Scope[i].Item2;
        return null;
    }

    /// <summary>
    /// When true, allows matching and substituting embedded identifiers within compound tokens (e.g. 'prefix._var.suffix').
    /// When false, only exact full-token matches are substituted. Defaults to true.
    /// </summary>
    public bool MatchPartials = true;

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

        while (index < input.Count && input[index] != ".expand." && input[index] != ".expand_end.")
        {
            string sourceText = input[index];
            
            // Expand by whitespace split on full string match
            var sub = GetByKey(sourceText);
            if (sub != null)
            {
                foreach (var item in sub.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                {
                    output.Add(item);
                }
            }
            else if (MatchPartials) // attempt inner substitution
            {
                StringBuilder completed = new StringBuilder();
                StringBuilder? identifier = null;

                for (int i = 0; i < sourceText.Length; i++)
                {
                    char c = sourceText[i];
                    bool id_char = false;

                    if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_') id_char = true;
                    if ((!id_char) && identifier != null && (c >= '0' && c <= '9')) id_char = true;

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

        if (index < input.Count && input[index] == ".expand.")
        {
            index++;
            if (index >= input.Count)
                return new CLReturn() { Status = 1, Message = "CLIExpander: Expected variable name after .expand." };

            string key = input[index];
            index++;
            if (index >= input.Count)
                return new CLReturn() { Status = 1, Message = $"CLIExpander: Missing ':' after variable '{key}'" };

            List<String> inputs = new List<String>();
            while (index < input.Count && input[index] != ":")
            {
                inputs.Add(input[index]);
                index++;
            }

            if (index >= input.Count)
                return new CLReturn()
                    { Status = 1, Message = $"CLIExpander: Missing ':' delimiter for variable '{key}'" };

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

                    if (index2 >= input.Count || input[index2] != ".expand_end.")
                        return new CLReturn() { Status = 1, Message = "CLIExpander: Expected .expand_end." };
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
                if (index >= input.Count || input[index] != ".expand_end.")
                    return new CLReturn() { Status = 1, Message = "CLIExpander: Expected .expand_end." };
            }

            index++;
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
            if (input[position] == ".expand_end.")
                return new CLReturn() { Status = 1, Message = "CLIExpander: Unexpected .expand_end." };
            return new CLReturn()
                { Status = 1, Message = $"CLIExpander: Incomplete parsing of input at '{input[position]}'" };
        }

        return new CLReturn() { Status = 0 };
    }
}