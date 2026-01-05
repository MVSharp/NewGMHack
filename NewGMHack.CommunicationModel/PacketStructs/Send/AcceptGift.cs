using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NewGMHack.CommunicationModel.PacketStructs.Send
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AcceptGiftPacket
    {
        public ushort Length;
        public ushort Splitter;
        public ushort Method;
        public uint Reserved;
        public uint GiftId;           // dynamic
        public uint unknow;
    }
}
