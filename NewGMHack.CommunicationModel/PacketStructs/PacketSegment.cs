namespace NewGMHack.Stub.PacketStructs;

public class PacketSegment(int length, int method, byte[] methodBody)
{
    public int     Length     { get; set; } = length;
    public int     Method     { get; set; } = method;
    public byte[]  MethodBody { get; set; } = methodBody;
}