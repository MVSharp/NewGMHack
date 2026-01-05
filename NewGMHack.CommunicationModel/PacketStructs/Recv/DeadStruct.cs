using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewGMHack.CommunicationModel.PacketStructs.Recv
{
    public unsafe struct Dead1506
    {
        public UInt32 PersonId;
        public UInt32 DeadId;
    }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public unsafe struct DeadStruct
    {
        public UInt32 PersonId;
        public UInt32 KillerId;
        public byte   padding;
        public byte   Count; //max 14
    }

    /// <summary>
    /// Dead entry in death notification packet.
    /// Size: 9 bytes (128 bytes / 14 max entries ≈ 9 bytes each)
    /// </summary>
    public unsafe struct Deads
    {
        public       UInt32 Id;        // 4 bytes
        public fixed byte   Padding[7]; // 5 bytes (total 9 bytes per entry)
    }
}
