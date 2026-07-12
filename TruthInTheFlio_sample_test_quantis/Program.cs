using System.Text;
using TruthInTheFlip.Format;

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
    StringBuilder sb = new StringBuilder();
    sb.AppendLine(message);
    
    int c = 0;
    for (int position = 0; position < buffer.Length; position += 8)
    {
        sb.Append("[ ");
        sb.Append(BitConverter.ToString(buffer, position, 8));
        sb.Append(" ]");

        c++;
        if (c == 16)
        {
            sb.AppendLine();
            c = 0;
        }
        
        Console.WriteLine(sb.ToString());
    }
}
    