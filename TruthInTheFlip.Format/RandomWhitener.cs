namespace TruthInTheFlip.Format;

public class RandomWhitener
{
    protected long entropyOffset = 0;
    protected int entropyBit = 0;
    protected byte[] entropyBuffer = null;
    protected int entropyLength;

    public BitFactory Source;

    public RandomWhitener(BitFactory Source)
    {
        this.Source = Source;
    }

    public void Fill(byte[] buffer)
    {
        nint position;
        nint size = buffer.Length;
        
        if (entropyBuffer == null)
        {
            entropyBuffer = new byte[32 * 1024];
            entropyOffset = entropyBuffer.Length;
            entropyBit = 0;
        }

        position = 0;
        int position_bit = 0;

        Func<int> getRaw = () =>
        {
            if (entropyOffset >= entropyLength)
            {
                Source.fillArray(entropyBuffer);
                entropyOffset = 0;
                entropyLength = entropyBuffer.Length;
                entropyBit = 0;
            }

            int bit = ((entropyBuffer[entropyOffset]) >> entropyBit) & 1;
            entropyBit++;
            if (entropyBit == 8)
            {
                entropyOffset++;
                entropyBit = 0;
            }

            return bit;
        };

        while (position < size)
        {
            int bit1 = getRaw();
            int bit2 = getRaw();

            if (bit1 == bit2)
                continue;

            if (position_bit == 0)
                buffer[position] = 0;

            // 01 -> 0; 10 -> 1
            if (bit1 != 0)
                buffer[position] |= (byte)(1 << position_bit);

            position_bit++;

            if (position_bit == 8)
            {
                position++;
                position_bit = 0;
            }
        }
        
    }
}