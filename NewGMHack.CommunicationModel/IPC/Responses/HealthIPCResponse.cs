using MessagePack;

namespace NewGMHack.CommunicationModel.IPC.Responses;

[MessagePackObject]
public class HealthIPCResponse
{
    [Key(0)]
    public bool IsHealth { get; set; }
}