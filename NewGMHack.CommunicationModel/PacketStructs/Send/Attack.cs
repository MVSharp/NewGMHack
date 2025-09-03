using System.Runtime.InteropServices;

namespace NewGMHack.Stub.PacketStructs.Send;

public struct Attack
{
    public ushort Version;

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

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
    public TargetData[] TargetData;
}