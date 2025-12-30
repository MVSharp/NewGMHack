using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NewGMHack.CommunicationModel.PacketStructs.Recv
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RewardGrade
    {
        public uint PlayerId;
        public byte Grade; //01 = A+ ,02 =A , 03=B+,04=B ..... etc to  ,08 or else =F
        public byte DamageScore;//傷害評分
        public byte TeamExpectationScore;//團隊合作評分
        public byte SkillFulScore;// 技術評分
    }
[StructLayout(    LayoutKind.Explicit, Pack = 1)]
public struct RewardReport
{
	// 00..03
	[FieldOffset(0)] public uint PlayerId;                 // EB 02 00 00 = 747

	// 04..07: 00 00 00 00
	// 08..11: 02 00 00 00 (unknown uint)
//	[FieldOffset(8)] public uint UnknownA;                 // 2

	// 12..15: 00 00 00 00
	// 16..19: 01 01 01 01 flags
	//[FieldOffset(16)] public uint Flags;                    // 0x01010101

	[FieldOffset(16)] public byte WinOrLostOrDraw;                //  02 is lost , 01 is won else draw?
	// 20..21: 87 00
	[FieldOffset(20)] public ushort ExpGain;                // 135

	// 22..23: 30 00
	[FieldOffset(22)] public ushort GBGain;                 // 48

	// 24..25: FC 00
	[FieldOffset(24)] public ushort MachineAddedExp;        // 252

	// 26..27: 87 00 (unknown / duplicate)
	//[FieldOffset(26)] public ushort UnknownB;               // 135

	// 28..29: 28 00
	//[FieldOffset(28)] public ushort UnknownC;               // 40

	// 30..31: D2 00
	//[FieldOffset(30)] public ushort UnknownD;               // 210

	// 32..41: 10 bytes of 00
	// 42..43: 08 00
	//[FieldOffset(42)] public ushort UnknownE;               // 8

	// 44..45: 2A 00
	//[FieldOffset(44)] public ushort UnknownF;               // 42

	// 46..59: 14 bytes of 00
	// 60..61: 1A 01
	[FieldOffset(56)] public ushort Points;                 // 282

	// 62..63: 00 00
	// 64..65: 82 00
	[FieldOffset(60)] public ushort Kills;                  // 130
    [FieldOffset(64)] public ushort Deaths;   
    [FieldOffset(68)] public ushort Supports;   
	// 66..75: 10 bytes of 00
	// 76: 02 (alignment byte)
	// 77..78: 24 04
	[FieldOffset(73)] public ushort MachineExp;             // 1060

	// 79..82: 00 00 00 00
	// 83..84: 0C 00
	[FieldOffset(79)] public ushort PracticeExpAdded;       // 12
}
 
// Explicit layout: field offsets match the payload bytes exactly 
[StructLayout(LayoutKind.Explicit, Pack = 1)] 
public unsafe struct RewardBonus { 
// 00..03 
[FieldOffset(0)]
public uint PlayerId; // EB 02 00 00 = 747 
[FieldOffset(42)] public fixed uint Bonuses[8]; // Bonus1..Bonus8
}
}
