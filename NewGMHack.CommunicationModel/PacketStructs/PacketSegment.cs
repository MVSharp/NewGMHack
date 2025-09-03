namespace NewGMHack.Stub.PacketStructs;

public class PacketSegment(int version, int method, byte[] methodBody)
{
    public int     Version    { get; set; } = version;
    public int     Method     { get; set; } = method;
    public byte[]  MethodBody { get; set; } = methodBody;
    //public byte[]  Raw        { get; set; } = raw;
}