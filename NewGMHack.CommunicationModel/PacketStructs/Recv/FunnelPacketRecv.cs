using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewGMHack.CommunicationModel.PacketStructs.Recv
{
    public struct FunnelPacketRecv
    {
        public ushort Version;         // 10 00
        public ushort Split;           // F0 03
        public ushort Method;          // 54 08

        public UInt16 PlayerId;          // EB 02 00 00
        public UInt32 FromId;            // EB 02 00 00

        public byte Count;             // 01

        public UInt32 TargetId; // 46 EF 00 00 00
        public byte Unknown2;
    }
}
