namespace NewGMHack.Stub.PacketStructs.Recv;

public struct GetChangedMachine(uint unknown1, ushort unknown2, uint machineId)
{
    public uint   Unknown1  = unknown1;
    public ushort Unknown2  = unknown2;
    public uint   MachineId = machineId;
}