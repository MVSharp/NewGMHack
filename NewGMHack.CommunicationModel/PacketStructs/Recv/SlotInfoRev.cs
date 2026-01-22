using System;
using System.Collections.Generic;
using System.Text;

namespace NewGMHack.CommunicationModel.PacketStructs.Recv
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    public unsafe struct SlotInfoRev
    {
        public        UInt32  PlayerId;
        public        UInt32  SlotId;
        private fixed byte    unknown[4];
        public        Machine Machine; // it this correct in memorymarshal ?
    }
}
