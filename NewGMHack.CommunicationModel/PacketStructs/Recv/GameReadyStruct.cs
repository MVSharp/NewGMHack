using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NewGMHack.CommunicationModel.PacketStructs.Recv
{
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct GameReadyStruct
    {
public uint PlayerId;

	public fixed byte Pad8[5];

	public byte MapId;

	public byte Pad10;

	public byte Pad11;
	
	public byte Pad12;
	
	public byte Pad13;
	// 14 : IsTeam (2 = yes, 1 = random)
	public byte IsTeam; 

	public byte GameType; // 3 = dead match , 1 = usual 
 
	private fixed byte Pad16_29[14]; 

	public byte PlayerCount;
    }
}
