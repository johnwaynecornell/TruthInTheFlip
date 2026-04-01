using System.Reflection;

namespace TruthInTheFlip.Format;

public class UtilT
{
    public class LinkNode<T>
    {
        public LinkNode<T>? Next;
        public LinkNode<T>? Prev;
        public T? Value;

        public LinkNode(T? Value)
        {
            this.Value = Value;
        }
    }
    
    public static Tracker Subtract(TrackerStore store, int[] ver, Tracker A, Tracker B)
    {
        Tracker C = (Tracker)store.NewTracker();
        C.Source = A;

        C.total = A.total - B.total;
        C.heads = A.heads - B.heads;
        C.tails = A.tails - B.tails;
        C.anticipated = A.anticipated - B.anticipated;
        C.baseAnticipated = A.baseAnticipated - B.baseAnticipated;
        C.anticipatedHeads = A.anticipatedHeads - B.anticipatedHeads;
        C.anticipatedTails = A.anticipatedTails - B.anticipatedTails;
        C.cumulativeTicks = A.cumulativeTicks - B.cumulativeTicks;

        if (TrackerStore.VersionCompare(ver, 1, 1, 0) >= 0)
        {
            C.batchTotal = A.batchTotal;

            // Subtract wallclock time so the window reflects its actual compute duration
            C.wallclockTimeNs = A.wallclockTimeNs - B.wallclockTimeNs;

            // The start of the new window is exactly when the baseline ended
            C.utcBeginTimeMs = B.utcEndTimeMs;

            // The end of the window is when the current state ended
            C.utcEndTimeMs = A.utcEndTimeMs;

            C.betHeads = A.betHeads - B.betHeads;
            C.betSame = A.betSame - B.betSame;
            C.anticipatedSame = A.anticipatedSame - B.anticipatedSame;
        }

        return C;
    }

    public static IEnumerable<(ITracker window, ITracker absolute)> Enumerate(TrackerStore store, long window)
    {
        return Enumerate(store, (A, B) => A.total - B.total <= window);
    }

    public static IEnumerable<(ITracker window, ITracker absolute)> Enumerate(TrackerStore store,
        Func<Tracker, Tracker, bool> bound)
    {
        LinkNode<Tracker>? head = null;
        LinkNode<Tracker>? tail = null;

        if (store.Version == null) throw new NullReferenceException();
        
        int[]? ver = TrackerStore.ReadVersion("TruthInTheFlip.v", store.Version);
        if (ver == null) throw new NullReferenceException();
        

        head = tail = new LinkNode<Tracker>((Tracker)store.NewTracker());
        if (head == null || tail == null) throw new NullReferenceException();
        Tracker ?t = null;
        foreach (var t1 in store.Enumerate())
        {
            t = (Tracker)t1;
            LinkNode<Tracker> node = new(t);
            tail.Next = node;
            tail = node;

            while (head != null && head.Next != null && !bound(t, UtilT.ThrowIfNull(head.Value, "head.Value"))) head = head.Next;

            // Yield both the isolated window AND the absolute lifetime state
            yield return (Subtract(store, ver, t, UtilT.ThrowIfNull(head?.Value, "head.Value")), t);
        }
    }
    
    public static Type GetDelegateType(ParameterInfo[] parameters, Type returnType)
    {
        var paramTypes = parameters.Select(p => p.ParameterType).Concat(new[] { returnType }).ToArray();
        return System.Linq.Expressions.Expression.GetFuncType(paramTypes);
    }

    public static string PadRight(string input, int L=22)
    {
        if (input.Length >=1 && input[input.Length - 1] != ' ') input +=" ";
        return input + new string(' ', int.Max(0, L - input.Length));
    }
    
    public static T ThrowIfNull<T>(T? value, string message) where T : class
    {
        if (value == null) throw new ArgumentNullException(message);
        return value;
    }
    
    public static T ThrowIfNull<T>(T? value, Func<string> message) where T : class
    {
        if (value == null) throw new ArgumentNullException(message());
        return value;
    }
    
    
    public static SOut nillMessage => (s, nl) => { };
    public static SOut nillErrorMessage => (s, nl) => { };
    
    public static SOut consoleMessage => (s, nl ) => Console.Write(s +(nl ? "\n" : ""));
    public static SOut consoleErrorMessage => (s, nl) => Console.Error.Write(s +(nl ? "\n" : ""));
}