using System.ComponentModel.DataAnnotations;

namespace TruthInTheFlip.Format;

public class BitFactory
{
    
    public int ServeSize = 1024 * 16;
    
    
    public Func<Action<byte[]>> resetRandom = null;
    public Action<byte[]> fillArray = initRandom_Net();

    public static Action<byte[]> initRandom_Net()
    {
        Random rand = new Random();
        return (buffer) => rand.NextBytes(buffer);
    }

    private readonly object _guard = new object();

    public void Provide(Consumer consumer)
    {
        lock (_guard)
        {
            // Initialize the consumer's array once
            if (consumer.source == null || consumer.source.Length != ServeSize)
                consumer.source = new byte[ServeSize];
            
            // Just overwrite the existing array with new random bytes
            fillArray(consumer.source); 
            consumer.Index = 0;
            consumer.Index2 = 7;
        }
    }

    public void Reset()
    {
        if (resetRandom == null) throw new Exception("BitFactory.resetRandom has not been provided");
        
        lock (_guard)
        {
            fillArray = resetRandom();
        }
    }
    
    public class Consumer
    {
        public BitFactory factory;

        public byte[]? source = null;
        
        public int Index;
        public int Index2;
        
        public Consumer(BitFactory factory)
        {
            this.factory = factory;
            Index = 0;
            Index2 = 7;
        }
        
        public bool getBit()
        {
            if (source == null || Index >= source.Length) factory.Provide(this);
            if (source == null) throw new Exception("provider failure.");
            if (Index2 < 0)
            {
                Index2 = 7;
                Index++;
                if (Index >= source.Length) factory.Provide(this);
            }

            bool rc = (source[Index] & (1 << (Index2))) != 0;
            Index2--;
            return rc;

        }
    }
    
}