using System;
using System.Collections.Generic;
using System.Text;

namespace NewGMHack.CommunicationModel.PacketStructs
{
    using System.Runtime.InteropServices;

 [StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct PlayerBattleStruct
{
    public uint Player;
    public byte RoomSlot;
        public byte TeamId1; // 1 is red 2 is blue team else unknown
        public byte TeamId2; // 1 is red 2 is blue team else unknown
    public uint MachineId;
    private fixed byte _pad1[3];
    public uint MachineIdAfterTransform;
    public uint MaxHP;
    private fixed byte Unknown1[6];
    public ushort Attack;
    public ushort Defense;
    public uint Shield;
    public uint ExtraStat;
    public ushort Attack2;
    public ushort Defense2;
    public fixed byte _pad3[20];

    public ushort OcPart1; public byte padp1;
    public ushort OcPart2; public byte padp2;
    public ushort OcPart3; public byte padp3;
    public ushort OcPart4; public byte padp4;
    
    

    public OcPoints OcPoints;
    public OcPoints OcBonusPoints;
	private fixed byte Unknown[17*15 + 13+11];
}
}
