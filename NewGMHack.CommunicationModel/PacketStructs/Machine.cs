using System.Runtime.InteropServices;

namespace NewGMHack.CommunicationModel.PacketStructs;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct Machine
{
    public UInt32 MachineId;
    public ushort Pos;
    public byte   Active;
    public byte   Reserved1;

    public UInt32 Slot;

    public fixed byte Reserved2[4]; // for Slot we use uint32 instead of UUID thats why

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
    public fixed byte          RawC[3];// later on fix, because oc parts missed one byte
    //TODO fuck their mother , i dont know why they split the oc1 parts , fuck
    //public OcParts Oc1Parts;
    public ushort Oc1Part1;
    public ushort Oc1Part2;
    public ushort Oc1Part3;
    // public fixed byte          Pad3[2];
    // public       byte          VaginaLevel;//  merge more bot become stronger Vagina 
    // public fixed byte          VaginaLv[4];
    public OcParts Oc2Parts;
    public ushort  Oc1Part4;
    public fixed byte     Pad4[5];
    public       OcPoints OcBasePoints;
    public       byte     Pad5;
    public       OcPoints OcBonusPoints;
}