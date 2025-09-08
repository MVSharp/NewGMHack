using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NewGMHack.CommunicationModel.PacketStructs.Send
{

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public  struct ChargeRequest
    {
        public UInt16 Version;
        public UInt16 Split;
        public UInt16 Method; // 84 06
        public UInt32 Unknown1; // 00 
        public UInt32 Slot;
        public UInt32 Unknown2; //00
    }
}
