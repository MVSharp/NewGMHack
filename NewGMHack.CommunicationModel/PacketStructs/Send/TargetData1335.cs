using System.Runtime.InteropServices;

namespace NewGMHack.Stub.PacketStructs.Send;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TargetData
{
    public uint   TargetId;
    public ushort Damage;
    public ushort Unknown1, Unknown2;
    public byte   Unknown3;
    public byte   Count;
}
