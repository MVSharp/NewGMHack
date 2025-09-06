using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewGMHack.CommunicationModel.PacketStructs.Recv
{
using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RoomActionOnLeave
    {
        public uint PlayerId;         
        public uint Padding;          
        public uint RepeatedPlayerId; 
        public uint Slot;             
        public uint LeaveId;
    }
}
