using System.Runtime.InteropServices;

namespace NewGMHack.CommunicationModel.PacketStructs;
    
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct GameTimestamp
{
    public ushort Year;   // 2 bytes
    public ushort Month;  // 2 bytes
    public ushort Day;    // 2 bytes
    public ushort Hour;   // 2 bytes
    public ushort Minute; // 2 bytes
    public ushort Second; // 2 bytes
}