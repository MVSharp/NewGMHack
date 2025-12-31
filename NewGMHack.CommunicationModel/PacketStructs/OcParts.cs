using System.Runtime.InteropServices;

namespace NewGMHack.CommunicationModel.PacketStructs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct OcParts
{
    public ushort Part1;
    public ushort Part2;
    public ushort Part3;
    public ushort Part4;
}