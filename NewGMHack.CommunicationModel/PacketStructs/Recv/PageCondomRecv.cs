using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NewGMHack.CommunicationModel.PacketStructs.Recv
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PageCondomRecv
    {
        public UInt32 MyPlayerId;
        public byte   Size;
    }
}
