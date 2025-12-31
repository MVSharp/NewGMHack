using MessagePack;
using NewGMHack.CommunicationModel.Models;

namespace NewGMHack.CommunicationModel.IPC.Responses
{
    /// <summary>
    /// Response containing machine + base info for Machine Info tab
    /// </summary>
    [MessagePackObject]
    public class MachineInfoResponse
    {
        [Key(0)] public MachineModel? MachineModel { get; set; }
        [Key(1)] public MachineBaseInfo? MachineBaseInfo { get; set; }
    }
}
