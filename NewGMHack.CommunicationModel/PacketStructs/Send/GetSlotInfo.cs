using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NewGMHack.CommunicationModel.PacketStructs.Send
{
    [StructLayout(LayoutKind.Sequential,Pack = 1)]
    public struct GetSlotInfo
    {
        public ushort Length;
        public ushort Split;//1008
        public ushort Method;//BB07
        public UInt32 Unknow;
        public UInt32 SlotId;
        public UInt32 Unknown2;
    }
}
