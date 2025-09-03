using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewGMHack.Stub.PacketStructs.Recv
{
    //[StructLayout(LayoutKind.Sequential, Pack = 1)]

    public class Reborn
    {
        public uint PersionId;
        public uint TargetId;
        public ushort Location;

        public Reborn(uint persionId, uint targetId, ushort location)
        {
            PersionId = persionId;
            TargetId  = targetId;
            Location  = location;
        }
    }
}