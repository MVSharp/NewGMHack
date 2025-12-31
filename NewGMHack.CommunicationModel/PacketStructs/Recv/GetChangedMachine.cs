using System.Runtime.InteropServices;
using NewGMHack.CommunicationModel.PacketStructs;

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