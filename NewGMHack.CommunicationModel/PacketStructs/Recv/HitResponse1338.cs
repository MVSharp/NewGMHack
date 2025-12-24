using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NewGMHack.CommunicationModel.PacketStructs.Recv
{

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct HitResponse1616
    {
        public       UInt32 PlayerId;
        public       UInt32 FromId;
        public fixed byte Paddings[13];
        public       UInt32 ToId;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct HitResponse2472
    {
        public       UInt32 PlayerId;
        public       UInt32 FromId;
        public fixed byte   Paddings[14];
        public       UInt32 ToId;
        public       UInt32 Damage;
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct HitResponse1525
    {
        public       UInt32 PlayerId;
        public       UInt32 FromId;
        public fixed byte Paddings[14];
        public       UInt32 ToId;
    }
}
