using System.Runtime.InteropServices;

namespace NewGMHack.Stub.PacketStructs.Send;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Attack1335
{
    public ushort Length;

    public ushort Split;

    public ushort Method;

    public uint Unknown1;

    public uint PlayerId;

    public uint WeaponId;

    //  public UInt32 WeaponSplit;
    public ushort WeaponSlot;
    public uint   PlayerId2;
    public byte   Unknown2;
    public byte   TargetCount;

    // [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
    // public TargetData1335[] TargetData1335;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Attack1486
{
        public ushort Length;       // 2 bytes
        public ushort Split;         // 2 bytes
        public ushort Method;        // 2 bytes
        public UInt32 Unknown1;
        public UInt32 PlayerId;
        public UInt32 ItemId;
        public UInt32 PlayerId2;
        public byte TargetCount;
}