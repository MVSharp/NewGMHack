using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewGMHack.Stub.PacketStructs.Recv
{
    //[StructLayout(LayoutKind.Sequential, Pack = 1)]

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct PlayerStateStruct
{
    public UInt32 PlayerId;          // b[6..9]   - Player unique ID
    public UInt32 SpawnId;        // b[10..11] - Spawn index (starts from 1?)
    public UInt32 CurrentHP;        // b[13..16] - Current HP
    public UInt32 CurrentSP;        // b[17..20] - Current SP
    public UInt32 FieldsHP;     // b[21..24] - State/fields HP
}
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