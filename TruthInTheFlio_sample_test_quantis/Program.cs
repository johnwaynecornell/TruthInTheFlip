using System.Text;
using TruthInTheFlip.Format;

try
{
    if (QuantisInterop.CountDevices(QuantisInterop.DeviceType.PCI)<1)
    {
        Console.WriteLine("No Quantis PCI device found");
        return;
    }
} catch (Exception ex)
{
    Console.WriteLine("Quantis hardware/drivers not detected. Skipping hardware entropy test.");
    Console.WriteLine("Quantis.dll for Windows and libquantis.so for Linux should be resolvable or in binary directory.");
    return;
}

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