using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NewGMHack.CommunicationModel.PacketStructs.Recv
{

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ReadyStruct
    {
        public UInt32 MyPlayerId;
        public byte   Slot;
        public byte   IsReady;
        public byte   Unknown2;
    }
}
