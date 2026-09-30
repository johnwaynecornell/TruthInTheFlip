using System.Text;

namespace CLIExpanderNs;

public class CLIExpander
{
    public struct CLReturn
    {
        public int Status;
        public string? Message;
    }

    public Stack<int> ScopeWaterMarks = new Stack<int>();
    public List<(string, string)> Scope = new();

    public string? GetByKey(string key)
    {
        for (int i = Scope.Count - 1; i >= 0; i--)
            if (Scope[i].Item1 == key)
                return Scope[i].Item2;
        return null;
    }


    public CLReturn Process(List<string> input, ref int index, List<String> output)
    {
        if (index >= input.Count)
            return new CLReturn() { Status = 1, Message = "CLIExpander: Unexpected end of input" }; 
        
        again:
        CLReturn Return = new CLReturn();

        while (index < input.Count && input[index] != ".expand." && input[index] != ".expand_end.")
        {
            string sourceText = input[index];
            
            //epand by whitespace split on full string match
            var sub = GetByKey(sourceText);
            if (sub != null)
            {
                foreach (var item in sub.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                {
                    output.Add(item);
                }
            }
            else // attempt inner substitution
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
            }
            
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

    public static CLReturn Process(List<string> input, out List<String> output)
    {
        CLIExpander expand = new CLIExpander();
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