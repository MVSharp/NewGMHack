using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NewGMHack.CommunicationModel.PacketStructs.Recv
{

  //  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  //  public unsafe struct HitResponse1616
  //  {
  //      public       UInt32 MyPlayerId;
  //      public       UInt32 FromId;
  //      public fixed byte Paddings[13];
  //      public       UInt32 ToId;
  //  }
/// <summary>
/// HitResponse1616 header - use SliceAfter + CastTo&lt;Victim&gt; for victims array
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)] 
public struct HitResponse1616 
{ 
    public uint PlayerId;     // MyId - Player ID
    public uint FromId;       // AttackerId  
    public uint AttackerSP;   // Attacker SP value 
    public byte UnknownFlag;  // Hit/type flag 
    public uint WeaponId;     // Weapon identifier 
    public uint UnknownConst; // Usually FF-00-00
    public byte VictimCount;  // Number of victims (max 12)
    // After this: Victim[] - use bytes.Span.SliceAfter<HitResponse1616>().CastTo<Victim>()
}
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct HitResponse2472
    {
        public       UInt32 PlayerId;
        public       UInt32 FromId;
        public fixed byte   Paddings[14];
        public       UInt32 ToId;
        public       UInt32 Damage;
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct HitResponse1525
    {
        public       UInt32 PlayerId;
        public       UInt32 FromId;
        public fixed byte Paddings[14];
        public       UInt32 ToId;
    }
}
