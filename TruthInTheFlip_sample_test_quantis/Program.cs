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