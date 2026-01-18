using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NewGMHack.CommunicationModel.PacketStructs.Recv
{
    [StructLayout(LayoutKind.Sequential,Pack = 1)]
    public struct MachineGridHeader
    {
        public UInt32 PlayerId;
        public byte   TotalCount;
    }

    [StructLayout(LayoutKind.Sequential,Pack = 1)]// length 26
    public struct MachineGrid
    {
        public              UInt32 MachineId;
        public              UInt32 Slot;
        public unsafe fixed byte   Unknown[4];//in fact maybe uuid 
        public              ushort Pos;
        public              byte   Level;
        public unsafe fixed byte   Unknown1[6];
        public              float  Battery;
        public byte Unknown2;
    }
}
