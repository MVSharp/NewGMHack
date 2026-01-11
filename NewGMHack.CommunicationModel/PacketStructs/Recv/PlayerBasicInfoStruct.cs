using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NewGMHack.CommunicationModel.PacketStructs.Recv
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct PlayerBasicInfoStruct
    {
        public       UInt32 PlayerId;
        private      byte   pad1;
        private      byte   pad2;
        public       byte   NameLength;
        public fixed byte   Name[30]; // GB2312 encoded bytes
        public fixed byte   Unknown[8];
        public       UInt32 Experiences;
        public       UInt32 Unknown1;
        public       UInt32 GB;
        public       UInt32 Proficiency;
        public fixed byte   Unknown2[18];
        //bot struct
    }
}
