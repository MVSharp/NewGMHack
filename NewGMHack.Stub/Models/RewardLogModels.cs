using NewGMHack.CommunicationModel.PacketStructs.Recv;

namespace NewGMHack.Stub.Models;

public class RewardEvent
{
    public DateTime Timestamp { get; set; }
    public RewardReport? Report { get; set; }
    public SafeRewardBonus? Bonus { get; set; }
}

public class SafeRewardBonus
{
    public uint PlayerId { get; set; }
    public uint[] Bonuses { get; set; } = new uint[8];
}

public class MatchRewardRecord
{
    public long Id { get; set; }
    public uint PlayerId { get; set; }
    public string CreatedAtUtc { get; set; } = "";
    
    // Report Data
    public int? Kills { get; set; }
    public int? Deaths { get; set; }
    public int? Supports { get; set; }
    public int? Points { get; set; }
    public int? ExpGain { get; set; }
    public int? GBGain { get; set; }
    public int? MachineAddedExp { get; set; }
    public int? PracticeExpAdded { get; set; }
    
    // Bonus Data
    public uint? Bonus1 { get; set; }
    public uint? Bonus2 { get; set; }
    public uint? Bonus3 { get; set; }
    public uint? Bonus4 { get; set; }
    public uint? Bonus5 { get; set; }
    public uint? Bonus6 { get; set; }
    public uint? Bonus7 { get; set; }
    public uint? Bonus8 { get; set; }
}
