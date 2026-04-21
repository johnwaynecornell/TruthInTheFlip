using System.Collections;
using System.Reflection;

namespace TruthInTheFlip.Format;

public class UtilT
{
    public class LinkNode<T>
    {
        public LinkNode<T>? Next;
        public LinkNode<T>? Prev;
        public T? Value;
        public object? Tag;
        
        public LinkNode(T? Value)
        {
            this.Value = Value;
        }
    }
    
    /// <summary>
        /// A high-performance doubly linked list designed for sliding window telemetry.
        /// Exposes raw node access for O(1) insertions and removals.
        /// </summary>
        public class LinkedList<T> : ICollection<T>
        {
            public LinkNode<T>? Head { get; private set; }
            public LinkNode<T>? Tail { get; private set; }
            
            public int Count { get; private set; }
            public bool IsReadOnly => false;
    
            // --- Raw Node Operations (O(1) Performance) ---
    
            /// <summary>
            /// Inserts a new node into the list relative to an existing node.
            /// </summary>
            /// <param name="at">The reference node currently in the list.</param>
            /// <param name="node">The new node to insert.</param>
            /// <param name="after">If true, inserts after the reference node. If false, inserts before.</param>
            public void Insert(LinkNode<T> at, LinkNode<T> node, bool after = false)
            {
                if (at == null) throw new ArgumentNullException(nameof(at));
                if (node == null) throw new ArgumentNullException(nameof(node));
    
                if (after)
                {
                    node.Prev = at;
                    node.Next = at.Next;
                    
                    if (at.Next != null) at.Next.Prev = node;
                    else Tail = node; // We inserted after the tail
                    
                    at.Next = node;
                }
                else // Before
                {
                    node.Next = at;
                    node.Prev = at.Prev;
                    
                    if (at.Prev != null) at.Prev.Next = node;
                    else Head = node; // We inserted before the head
                    
                    at.Prev = node;
                }
    
                Count++;
            }
    
            public void AddTail(LinkNode<T> node)
            {
                if (Head == null)
                {
                    Head = Tail = node;
                    node.Prev = node.Next = null;
                }
                else
                {
                    Tail!.Next = node;
                    node.Prev = Tail;
                    node.Next = null;
                    Tail = node;
                }
                Count++;
            }
    
            public void AddHead(LinkNode<T> node)
            {
                if (Head == null)
                {
                    Head = Tail = node;
                    node.Prev = node.Next = null;
                }
                else
                {
                    node.Next = Head;
                    node.Prev = null;
                    Head.Prev = node;
                    Head = node;
                }
                Count++;
            }
    
            public LinkNode<T>? PopHead()
            {
                if (Head == null) return null;
                return UnlinkNode(Head);
            }
            
            public LinkNode<T>? PopTail()
            {
                if (Tail == null) return null;
                return UnlinkNode(Tail);
            }
                        
            
            /// <summary>
            /// Unlinks a specific node from the list in O(1) time.
            /// </summary>
            public LinkNode<T> UnlinkNode(LinkNode<T> node)
            {
                if (node == null) throw new ArgumentNullException(nameof(node));
    
                if (node.Prev != null) node.Prev.Next = node.Next;
                else Head = node.Next;
    
                if (node.Next != null) node.Next.Prev = node.Prev;
                else Tail = node.Prev;
    
                node.Next = null;
                node.Prev = null;
                Count--;
                return node;
            }
    
            public bool Empty => Head == null;
            
            // --- ICollection<T> Implementation ---
    
            public void Add(T item) => AddTail(new LinkNode<T>(item));
    
            public void Clear()
            {
                // Optional: iterate and clear refs to help GC if the list is massive, 
                // but for simple value-types or small objects, this is fine.
                Head = Tail = null;
                Count = 0;
            }
    
            public bool Contains(T item)
            {
                var current = Head;
                var comparer = EqualityComparer<T>.Default;
                while (current != null)
                {
                    if (comparer.Equals(current.Value, item)) return true;
                    current = current.Next;
                }
                return false;
            }
    
            public void CopyTo(T[] array, int arrayIndex)
            {
                if (array == null) throw new ArgumentNullException(nameof(array));
                if (arrayIndex < 0 || arrayIndex > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
                if (array.Length - arrayIndex < Count) throw new ArgumentException("Destination array is not long enough.");
    
                var current = Head;
                while (current != null)
                {
                    array[arrayIndex++] = current.Value!;
                    current = current.Next;
                }
            }
    
            public bool Remove(T item)
            {
                var current = Head;
                var comparer = EqualityComparer<T>.Default;
                
                while (current != null)
                {
                    if (comparer.Equals(current.Value, item))
                    {
                        UnlinkNode(current);
                        return true;
                    }
                    current = current.Next;
                }
                return false;
            }
    
            // --- Enumerators ---
    
            public IEnumerator<T> GetEnumerator()
            {
                var current = Head;
                while (current != null)
                {
                    if (current.Value != null) yield return current.Value;
                    current = current.Next;
                }
            }
            
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    
    public static Tracker Subtract(TrackerStore store, int[] ver, Tracker A, Tracker B)
    {
        Tracker C = (Tracker)store.NewTracker();
        C.Source = A;
        C.From = B;

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
    
    public static T ThrowIfNull<T>(T? value, string message) 
    {
        if (value == null) throw new ArgumentNullException(message);
        return value;
    }
    
    public static T ThrowIfNull<T>(T? value, Func<string> message)
    {
        if (value == null) throw new ArgumentNullException(message());
        return value;
    }
    
    
    public static SOut nillMessage => (s, nl) => { };
    public static SOut nillErrorMessage => (s, nl) => { };
    
    public static SOut consoleMessage => (s, nl ) => Console.Write(s +(nl ? "\n" : ""));
    public static SOut consoleErrorMessage => (s, nl) => Console.Error.Write(s +(nl ? "\n" : ""));
}