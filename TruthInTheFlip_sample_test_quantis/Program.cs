using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using TruthInTheFlip.Format;

if (!QuantisInterop.EnsureSDK()) Environment.Exit(1);

Random r = new Random();
Func<Action<byte[]>>? resetRandom = QuantisInterop.QuantisFactory(true, 0, QuantisInterop.DeviceType.PCI);
Func<Action<byte[]>>? resetRandom2 = QuantisInterop.QuantisFactory(false, 0, QuantisInterop.DeviceType.PCI);

var fill = resetRandom();
byte[] buffer = new byte[1024];
fill(buffer);
Dump("\nentropy random:", buffer);

resetRandom2()(buffer);
Dump("\nwhite random:", buffer);

fill(buffer);
Dump("\nentropy random:", buffer);

for (int pow2 = 0; pow2 <= 10; pow2++)
{
    int chunk = (1 << pow2);
    int blockSize = 1024 * chunk;
    int iterations = (1024>>pow2) * 5;

    buffer = new byte[blockSize];

    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    long totalBytes = 0;

    for (int i = 0; i < iterations; i++)
    {
        fill(buffer);
        totalBytes += buffer.Length;
    }

    stopwatch.Stop();

    double mibPerSecond =
        totalBytes / 1024.0 / 1024.0 / stopwatch.Elapsed.TotalSeconds;

    Console.WriteLine(
        $"2^{pow2}({chunk})KB buffer size, {totalBytes} bytes "+
        $"{totalBytes:N0} bytes in {stopwatch.Elapsed}: " +
        $"{mibPerSecond:F2} MiB/s");
}

void Dump(string message, byte[] buffer)
{
    Console.WriteLine(message);
    
    StringBuilder sb = new StringBuilder();
    int c = 0;
    
    for (int position = 0; position < buffer.Length; position += 8)
    {
        sb.Append("[ ");
        
        int length = Math.Min(8, buffer.Length - position);
        sb.Append(BitConverter.ToString(buffer, position, length));
        sb.Append(" ] ");

        c++;
        if (c == 16)
        {
            Console.WriteLine(sb.ToString());
            sb.Clear(); // Reset for the next line
            c = 0;
        }
    }
    
    if (sb.Length > 0)
    {
        Console.WriteLine(sb.ToString());
    }
}