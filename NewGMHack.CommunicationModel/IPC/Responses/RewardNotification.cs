using MessagePack;

namespace NewGMHack.CommunicationModel.IPC.Responses
{
    [MessagePackObject]
    public class RewardNotification
    {
        [Key(0)] public long RecordId { get; set; }
        [Key(1)] public uint PlayerId { get; set; }
        [Key(2)] public int Points { get; set; }
        [Key(3)] public int Kills { get; set; }
        [Key(4)] public int Deaths { get; set; }
        [Key(5)] public int Supports { get; set; }
        [Key(6)] public bool HasBonus { get; set; }
        [Key(7)] public string Timestamp { get; set; } = "";
    }
}
