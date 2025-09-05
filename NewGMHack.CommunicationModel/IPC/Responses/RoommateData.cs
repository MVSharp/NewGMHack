using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NewGMHack.CommunicationModel.IPC.Responses
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct RoommateHeader
    {
        public fixed byte   Unknown[12]; // First 12 bytes
        public       byte   NameSize;    // Byte 12
        public fixed byte   Name[16];    // Bytes 13–30
        public fixed byte   Unknown2[282];
        public       UInt32 ItemId;
        public fixed byte   Unknown3[14];
        public       UInt32 PlayerId;

        public Span<byte> GetNameSpan()
        {
            fixed (byte* ptr = Name)
            {
                return new Span<byte>(ptr, 16);
            }
        }
    }
}