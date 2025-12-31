using System.Runtime.InteropServices;

namespace NewGMHack.CommunicationModel.PacketStructs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct OcPoints
{
    public byte Speed;
    public byte Hp;
    public byte Attack;
    public byte Defense;
    public byte Agility;
    public byte Special;
}