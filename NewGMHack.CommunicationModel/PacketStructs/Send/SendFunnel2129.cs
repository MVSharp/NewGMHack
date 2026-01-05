using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewGMHack.CommunicationModel.PacketStructs.Send
{
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct SendFunnel2129
{
    public UInt16 Length;        // 14 00 (packet length)
    public UInt16 Split;         // F0 03
    public UInt16 Method;        // 51 08

    public fixed byte Unknown[4];   // 00 00 00 00
    public UInt32 PlayerId;          // EB 02 00 00
    public UInt32 WeaponId;        // 58 A4 00 00
    public byte Count;           // 01

    public UInt32 TargetId;  // 46 EF 00 00 00
    public byte Unknown2;
}
}
