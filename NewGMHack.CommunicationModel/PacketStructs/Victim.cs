using System.Runtime.InteropServices;

namespace NewGMHack.CommunicationModel.PacketStructs;

[StructLayout(LayoutKind.Sequential, Pack = 1)] 
public struct Victim 
{ 
    public uint VictimId; // 4 bytes 
    public uint AfterHitHP; // 4 bytes (final HP after damage) 
    public uint AfterHitSP; // 4 bytes (raw damage/SP snapshot) 
    public uint AfterHitShieldHP; // 4 bytes (shield/absorb value) 
    public uint IsDown; // 4 bytes (downed/cooldown flag) // Total: 20 bytes 
}