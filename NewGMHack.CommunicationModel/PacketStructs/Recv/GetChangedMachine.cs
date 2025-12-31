using System.Runtime.InteropServices;

namespace NewGMHack.Stub.PacketStructs.Recv;

//public struct GetChangedMachine(uint unknown1, ushort unknown2, uint machineId,uint unknown3,uint slot)
//{
//    public uint   Unknown1  = unknown1; //00-00-00-00
//    public ushort Unknown2  = unknown2; //-00-00

//    //public Machine bot; // refine below to bot struct
//    public uint MachineId = machineId; //C9-3A-00-00
//    public uint Unknown3 = unknown3;// in fact ,there is a active bool in one byte here
//    public uint Slot = slot;// UUID 1C-C8-97-00-00-00-00-00
//                            //complete etc....
//}
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct GetChangedMachine
{
    public uint    Unknown1; // always 0x00000000
    public ushort  Unknown2; // always 0x0000
    public Machine Machine;  // embedded robot struct
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct Machine
{
    public UInt32 MachineId;
    public ushort Pos;
    public byte   Active;
    public byte   Reserved1;

    public UInt32 Slot;

    public fixed byte Reserved2[2]; // for Slot we use uint32 instead of UUID thats why

    //public       ulong         Slot;
    public       byte          Level;
    public fixed byte          Pad0[29];
    public fixed ushort        Color[6];
    public       ushort        BrushPolish;
    public fixed uint          Coat[3];
    public fixed byte          Pad1[2];
    public       float         Battery; //max is 2000 as always
    public       uint          BattleCount;
    public       uint          ExtraSkillParts;
    public       GameTimestamp BuyInTime; //Server time
    public fixed byte          Pad2[34];
    public       uint          CurrentExp;
    public       byte          OcMaxLevel;
    public       byte          Lock; //Is Locked
    public fixed byte          RawC[4];

    public OcParts Oc1Parts;

    // public fixed byte          Pad3[2];
    // public       byte          VaginaLevel;//  merge more bot become stronger Vagina 
    // public fixed byte          VaginaLv[4];
    public OcParts Oc2Parts;

    public fixed byte     Pad4[5];
    public       OcPoints OcBasePoints;
    public       byte     Pad5;
    public       OcPoints OcBonusPoints;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct GameTimestamp
{
    public ushort Year;   // 2 bytes
    public ushort Month;  // 2 bytes
    public ushort Day;    // 2 bytes
    public ushort Hour;   // 2 bytes
    public ushort Minute; // 2 bytes
    public ushort Second; // 2 bytes
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct OcParts
{
    public ushort Part1;
    public ushort Part2;
    public ushort Part3;
    public ushort Part4;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct OcPoints
{
    public byte Speed;
    public byte Hp;
    public byte Attack;
    public byte Defense;
    public byte Agility;
    public byte Special;
}