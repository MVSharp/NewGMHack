using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NewGMHack.CommunicationModel.PacketStructs.Recv
{
    /// <summary>
    /// HitResponse2472 header
    /// Structure:
    /// - MyId (4 bytes)
    /// - AttackerId/FromId (4 bytes)
    /// - Unknown1 (1 byte, e.g. F9)
    /// - AttackerSP (4 bytes)
    /// - WeaponId (4 bytes)
    /// - Unknown2 (4 bytes)
    /// - VictimCount (1 byte)
    /// - Victim[] (use SliceAfter + CastTo)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct HitResponse2472
    {
        public       uint MyPlayerId; // My ID
        public       uint FromId;     // Attacker ID
        private      byte Unknown1;
        public       uint AttackerSP;  // Attacker SP value
        public       uint WeaponId;    // Weapon identifier
        public fixed byte Unknown[4];  // Always FF 00 00
        public       byte VictimCount; // Number of victims
        // After this: Victim[] - use bytes.Span.SliceAfter<HitResponse2472>().CastTo<Victim>()
    }
    /// <summary>
    /// HitResponse1616 header
    /// Structure:
    /// - MyId (4 bytes)
    /// - AttackerId (4 bytes)
    /// - AttackerSP (4 bytes)
    /// - WeaponId (4 bytes)
    /// - Unknown (3 bytes, always FF 00 00)
    /// - VictimCount (1 byte)
    /// - Victim[] (use SliceAfter + CastTo)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct HitResponse1616
    {
        public       uint MyPlayerId; // My ID
        public       uint FromId;     // Attacker ID
        public       uint AttackerSP; // Attacker SP value
        private      byte Unknown1;
        public       uint WeaponId;    // Weapon identifier
        private fixed byte Unknown2[3];
        public       byte VictimCount; // Number of victims
        // After this: Victim[] - use bytes.Span.SliceAfter<HitResponse1616>().CastTo<Victim>()
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct HitResponse1525
    {
        public UInt32 PlayerId;
        public UInt32 FromId;
        public fixed byte Paddings[14];
        public UInt32 ToId;
    }
}
