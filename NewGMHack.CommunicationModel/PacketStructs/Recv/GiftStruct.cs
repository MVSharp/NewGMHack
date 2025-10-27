using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NewGMHack.CommunicationModel.PacketStructs.Recv
{
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct GiftStruct
{
    public uint GiftId;        // 4 bytes
    public uint Unknown1;      // 4 bytes
    public uint ItemId;        // 4 bytes
    public uint Unknown2;      // 4 bytes
    public uint ItemType;      // 4 bytes
    public uint Unknown3;      // 4 bytes
    public ushort Unknown4;    // 2 bytes
    public ushort Unknown5;    // 2 bytes
}
}
