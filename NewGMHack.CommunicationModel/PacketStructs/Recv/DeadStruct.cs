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
    public unsafe struct DeadStruct
    {
        public UInt32 PersonId;
        public UInt32 KillerId;
        public byte   padding;
        public byte   Count; //max 14
    }

    public unsafe struct Deads
    {
        public       UInt32 Id;
        public fixed byte   Padding[7];
    }
}
