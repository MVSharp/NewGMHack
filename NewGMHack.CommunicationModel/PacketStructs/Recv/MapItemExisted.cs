namespace NewGMHack.Stub.PacketStructs.Recv;

public class MapItemExisted
{
    public uint   PersonId { get; set; }
    public uint   Unknown1 { get; set; }
    public byte   Count    { get; set; }
    public uint[] Targets  { get; set; }
}