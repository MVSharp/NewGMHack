using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NewGMHack.CommunicationModel.PacketStructs.Send
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct BuildingDamageFrame
    {
        public ushort Length;      // fixed: 0x004F
        public ushort Split;       // fixed: 0x03F0
        public ushort Method;      // fixed: 0x08E6
        public uint   Padding1;    // 0x00000000
        public uint   AttackerUID; // variable  my person id alwyas as input 
        public uint   Padding2;    // 0x00000000
        public byte   Count;       // number of building IDs at max count is 8 

        public fixed UInt32 Buildings[8];
        public fixed UInt32 Damages[8];
        //public Uint32[] Buildings   list of uint32 BuildingsId at max 8 not enough 8 pad 0
        //public Uint32[] Damages    list of uint32 Damages at max 8 , not enough 8 pad 0  default input will be Uint32 max for each
    }
}