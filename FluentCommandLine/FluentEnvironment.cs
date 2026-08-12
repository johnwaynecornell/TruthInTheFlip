using System.ComponentModel;
using System.Reflection;
using System.Text;
using JWCEssentials.Metadata;

namespace FluentCommandLine;

public class FluentEnvironment
{
    public Dictionary<Type, FluentMethodRegistry> Registries { get; set; } =
        new Dictionary<Type, FluentMethodRegistry>();
    
    public Type[] ServeTypes { get; set; } = new Type[0];
    
    public static FluentEnvironment Current =>
        FluentEnvironmentScope.Current;
    
    public FluentContextData Context { get; } = new();

    internal IDisposable EnterScope()
        => FluentEnvironmentScope.Enter(this);

    public FluentEnvironment AddModule<T>()
    {
        return AddModule(typeof(T));
    }

    private FluentMethodRegistry.SOut TextOut { get; set; } = (message, nl) =>
    {
        if (nl) Console.WriteLine(message);
        else Console.Write(message);
    };
    
    FluentMethodRegistry.SOut TextErrorOut { get; set; } = (message, nl) =>
    {
        if (nl) Console.Error.WriteLine(message);
        else Console.Error.Write(message);
    };
    
    public static string? GetAttrValue(
        MemberInfo member,
        FluentAttribute key)
    {
        var attr = member
            .GetCustomAttributes<KVAttribute<FluentAttribute>>(false)
            .SingleOrDefault(x => x.Key.Equals(key));

        if (attr != null || key != FluentAttribute.Help) return attr?.Value;

        var attr2 = member
            .GetCustomAttributes<DescriptionAttribute>(false)
            .SingleOrDefault();

        if (attr2 == null) return null;
        
        return attr2.Description;
    }
    
    public static string? GetAttrValue(
        ParameterInfo parameter,
        FluentAttribute key)
    {
        var attr = parameter
            .GetCustomAttributes<KVAttribute<FluentAttribute>>(false)
            .SingleOrDefault(x => x.Key.Equals(key));

        if (attr != null || key != FluentAttribute.Help) return attr?.Value;

        var attr2 = parameter
            .GetCustomAttributes<DescriptionAttribute>(false)
            .SingleOrDefault();

        if (attr2 == null) return null;
        
        return attr2.Description;
    }
    
    public bool WantExit { get; set; } = false;
    protected int status = 0;
    public int Status { get => status; set => status = value; }
    
    /// <summary>
    /// Scans host type for static methods with the correct attributes and loads them into the registry.
    /// </summary>
    public FluentEnvironment AddModule(Type host)
    {
        var methods = host.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        foreach (var method in methods)
        {
            var FMA = method.GetCustomAttributes(typeof(FluentMethodAttribute), false).FirstOrDefault()
                as FluentMethodAttribute;

            if (FMA == null)
                continue;

            var help = GetAttrValue(method, FluentAttribute.Help);

            var parameters = method.GetParameters();
            string[] paramNames = new string[parameters.Length];
            string[] defValues = new string[parameters.Length];
            string?[] paramHelp = new string?[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                paramNames[i] = parameters[i].Name ?? $"arg{i}";
                var def = GetAttrValue(parameters[i], FluentAttribute.Def);
                defValues[i] = def ?? "";
                paramHelp[i] = GetAttrValue(parameters[i], FluentAttribute.Help);
            }

            AddSource(
                method,
                FMA.Name ?? method.Name,
                help ?? "No description provided.",
                paramNames,
                defValues,
                paramHelp
            );
        }

        var moduleInit = host.GetMethod("FluentModuleInitialize", BindingFlags.Public | BindingFlags.Static);
        moduleInit?.Invoke(null, new object[] { this });
        
        return this;
    }

    protected List<Type>? References = null; 
    
    public void ListReference(Type type)
    {
        if (References == null) return;
        if (!References.Contains(type) && !ServeTypes.Contains(type))
            References.Add(type);
    }

    public string ListCapture(Func<string> body)
    {
        References = new List<Type>();
        
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Available Commands");
        sb.AppendLine();

        sb.AppendLine(body().TrimEnd());

        string indent = new string(' ', 4);

        if (References.Count != 0)
        {
            sb.AppendLine();
            sb.AppendLine("Argument Types");
            sb.AppendLine();

            List<Type> work = new List<Type>(References);
            int known = References.Count;
            int i = 0;
            
            while (i < work.Count)
            {
                FluentMethodRegistry registry;
                    
                if (Registries.TryGetValue(work[i], out registry))  
                {
                    sb.AppendLine($"<{registry.RegistryType.Name}> — {registry.ElementDescription}");
                    sb.AppendLine();

                    sb.AppendLine(indent + registry.List(this).Replace("\n", "\n" + indent).TrimEnd());
                    sb.AppendLine();
                } 
                else if (work[i].IsEnum)
                {
                    sb.AppendLine($"<{work[i].Name}> — \n    {string.Join(", ", work[i].GetEnumNames())}");
                    sb.AppendLine();
                }
                i++;

                int ii = i;
                while (known < References.Count)
                {
                    work.Insert(ii++, References[known++]);
                }
            }
        }

        References = null;
        return sb.ToString();
    }
    
    public string List()
    {
        StringBuilder sb = new StringBuilder();

        sb.Append(ListCapture(() =>
        {
            StringBuilder sb = new StringBuilder();
            
            foreach (var T in ServeTypes)
            {
                sb.AppendLine(Registries[T].List(this));
            }
            
            return sb.ToString();
        }));
        
        WantExit = true;
        return sb.ToString();
    }

    public void EnsureRegistry(Type type, string? typeDescription = null)
    {
        if (!Registries.ContainsKey(type))
        {
            if (typeDescription == null)
            {
                var help = GetAttrValue(type, FluentAttribute.Help);

                if (help != null)
                    typeDescription = help;
                else
                    typeDescription = type.Name;
            }

            Registries[type] = new FluentMethodRegistry(type, typeDescription);
        }
    }

    public FluentMethod AddSource(Delegate func, string name, string help, string[] parameterNames,
        string[] defaultValues, string?[] parameterHelp)
    {
        Type returnType = func.Method.ReturnType;
        EnsureRegistry(returnType);
        var ret = Registries[returnType].AddSource(func, name, help, parameterNames, defaultValues, parameterHelp);
        return ret;
    }

    public FluentMethod AddSource(MethodInfo func, string name, string help, string[] parameterNames,
        string[] defaultValues, string?[] parameterHelp)
    {
        Type returnType = func.ReturnType;
        EnsureRegistry(returnType);
        var ret = Registries[returnType].AddSource(func, name, help, parameterNames, defaultValues, parameterHelp);
        return ret;
    }
    
    public FluentMethodRegistry.RegistryParseResult ParseOne(IReadOnlyList<string> cl, ref int cursor)
    {
        using var scope = EnterScope();
        
        if (cl[cursor] == "list")
        {
            TextOut(List().TrimEnd());
            cursor++;
            
            return null;
        }
        
        
        foreach (Type T in ServeTypes)
        {
            if (!Registries.TryGetValue(T, out var registry))
            {
                throw new ArgumentException($"FluentEnvironment '{T.Name}' not in Registries");
            }

            FluentMethodRegistry.RegistryParseResult result;
            int startCursor = cursor;
            if (!registry.TryParse(this, cl, ref cursor, ref status, TextOut, TextErrorOut, out result))
            {
                if (Status != 0)
                {
                    WantExit = true;
                    return null;
                }
                
                cursor = startCursor;
                continue;
            }
            
            return result;
        }
        
        return null;
    }
    
    [ThreadStatic] public static Func<string, int, string> _PadRight;

    public static string PadRight(string input, int L = 22)
    {
        if (_PadRight == null)
            _PadRight = (string input, int L = 22) =>
            {
                if (input.Length >= 1 && input[input.Length - 1] != ' ') input += " ";
                return input + new string(' ', int.Max(0, L - input.Length));
            };

        return _PadRight(input, L);
    }
    
    public void Unique<T>(ref T token, T value, Func<string> error)
    {
        if (token != null)
        {
            TextErrorOut(error());
            WantExit = true;
            Status = -1;
            return;
        }
                
        token = value;
    }
    
    public virtual string Help()
    {
        StringBuilder sbTypes = new StringBuilder();

        bool first = true;

        foreach (Type T in ServeTypes)
        {
            if (!first) sbTypes.Append(" or ");
            else first = false;
            
            var v = Registries[T];
            //sbTypes.Append($"<{v.RegistryType.Name}>");
            //sbTypes.Append($"({v.ElementDescription})");

            sbTypes.Append(v.ElementDescription);
        }
        
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.AppendLine(PadRight($"  list") +
                                 $"List available commands to {sbTypes}");
        stringBuilder.AppendLine(PadRight($"  <string> [params...]") +
                                 $"Configure specific command");
        stringBuilder.AppendLine(PadRight($"  <string> def") +
                                 $"Use supported default arguments");

        stringBuilder.AppendLine();

        stringBuilder.AppendLine(List());
            
        return stringBuilder.ToString();
    }

    public delegate bool TypeParseDelegate(Type T, IReadOnlyList<string> commandArgs, ref int cursor, ref int status,
        FluentMethodRegistry.SOut message,
        FluentMethodRegistry.SOut errorMessage, out object? result);
    
    public Dictionary<Type, TypeParseDelegate> TypeParseHandlers = new();
    
    public virtual bool TryParseParameter(Type T, IReadOnlyList<string> commandArgs, ref int cursor, ref int status,
        FluentMethodRegistry.SOut message,
        FluentMethodRegistry.SOut errorMessage, out object? result)
    {
        result = null;
        int startCursor = cursor;
        
        if (Registries.TryGetValue(T, out var handler))
        {
            if (!handler.TryParse(this, commandArgs, ref cursor, ref status, message, errorMessage, out var res) ||
                status != 0)
            {
                cursor = startCursor;
                return false;
            }

            /*
            if (res == null)
            {
                cursor = startCursor;
                return false;
            }
            */
            
            result = res;
            return true;
        }  
        
        if (TypeParseHandlers.TryGetValue(T, out var typeParseHandler))
        {
            return typeParseHandler(T, commandArgs, ref cursor, ref status, message, errorMessage, out result);
        }

        if (T.IsArray)
        {
            Type E = T.GetElementType();
            List<object> list = new();
            
            while (cursor < commandArgs.Count)
            {
                if (commandArgs[cursor] == ".END.")
                {
                    cursor++;
                    break;
                }

                object o;
                if (TryParseParameter(E, commandArgs, ref cursor, ref status, message, errorMessage, out o))
                {
                    list.Add(o);
                }
                else
                {
                    cursor = startCursor;
                    return false;
                }
            }

            //result = Array.CreateInstance(E, list.Count);
            result = list.ToArray();
            int i;
            for (i = 0; i < list.Count; i++)
            {
                ((object[])result)[i] = list[i];
            }

            return true;

        }

        if (cursor >= commandArgs.Count) return false;

        if (T == typeof(string))
        {
            result = commandArgs[cursor];
            cursor++;
            return true;
        }
        
        var parseMethod = T.GetMethod("Parse",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(string) },
            null);

        if (parseMethod == null)
        {
            return false;
        }

        try
        {
            result = parseMethod.Invoke(null, new object[] { commandArgs[cursor] })!;
            cursor++;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage(
                $"Error parsing '{commandArgs[cursor]}' as {T.Name}: {ex.InnerException?.Message ?? ex.Message}");
            status = -1;
            cursor = startCursor;
        }
        
        return false;
    }

}
