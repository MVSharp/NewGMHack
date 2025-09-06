using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NewGMHack.CommunicationModel.PacketStructs.Recv
{

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct HitResponse
    {
        public       UInt32 PlayerId;
        public       UInt32 FromId;
        public fixed byte Paddings[13];
        public       UInt32 ToId;
    }
}
