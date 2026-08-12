using System.Reflection;

namespace FluentCommandLine;

public class FluentMethod
{
    public MethodInfo MethodInfo { get; set; }
    
    public Delegate _Method = null;
    public Delegate Method 
    {
        get
        {
            if (_Method == null)
            {
                var parameters = MethodInfo.GetParameters();
                // Create a generic delegate targeting the static method
                Type delegateType = GetDelegateType(parameters, MethodInfo.ReturnType);
                _Method = Delegate.CreateDelegate(delegateType, MethodInfo);
            }
            return _Method;
        }

        set
        {
            MethodInfo = value.Method;
            _Method = value;
        }
    } 
    public string Help { get; set; } = "";
        
    public string Name { get; set; } = "";
    public bool IsDefault { get; set; } = false;

    public class Parameter
    {
        public string Name { get; set; } = "";
        public Type Type { get; set; } = default!;
        public string Default { get; set; } = "";
        
        public string Help { get; set; } = "";
    }

    public List<Parameter> Parameters { get; set; } = new List<Parameter>();
    
    public static Type GetDelegateType(ParameterInfo[] parameters, Type returnType)
    {
        var paramTypes = parameters.Select(p => p.ParameterType).Concat(new[] { returnType }).ToArray();
        return System.Linq.Expressions.Expression.GetFuncType(paramTypes);
    }
}