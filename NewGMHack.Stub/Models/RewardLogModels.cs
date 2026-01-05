using NewGMHack.CommunicationModel.PacketStructs.Recv;

namespace NewGMHack.Stub.Models;

/// <summary>
/// Game outcome status - transformed from WinOrLostOrDraw byte value
/// </summary>
public enum GameStatus
{
    Win = 1,
    Lost = 2,
    Draw = 3
}

/// <summary>
/// Grade rank - transformed from Grade byte value (01=A+, 02=A, 03=B+, 04=B, 05=C+, 06=C, 07=D, 08+=F)
/// </summary>
public enum GradeRank
{
    APlus = 1,  // A+
    A = 2,
    BPlus = 3,  // B+
    B = 4,
    CPlus = 5,  // C+
    C = 6,
    D = 7,
    F = 8       // 08 or else
}

public static class RewardTransformations
{
    public static GameStatus ToGameStatus(byte value) => value switch
    {
        1 => GameStatus.Win,
        2 => GameStatus.Lost,
        _ => GameStatus.Draw
    };

    public static GradeRank ToGradeRank(byte value) => value switch
    {
        1 => GradeRank.APlus,
        2 => GradeRank.A,
        3 => GradeRank.BPlus,
        4 => GradeRank.B,
        5 => GradeRank.CPlus,
        6 => GradeRank.C,
        7 => GradeRank.D,
        _ => GradeRank.F
    };

    public static string GameStatusToString(GameStatus status) => status switch
    {
        GameStatus.Win => "Win",
        GameStatus.Lost => "Lost",
        GameStatus.Draw => "Draw",
        _ => "Unknown"
    };

    public static string GradeRankToString(GradeRank grade) => grade switch
    {
        GradeRank.APlus => "A+",
        GradeRank.A => "A",
        GradeRank.BPlus => "B+",
        GradeRank.B => "B",
        GradeRank.CPlus => "C+",
        GradeRank.C => "C",
        GradeRank.D => "D",
        GradeRank.F => "F",
        _ => "F"
    };
}

public class RewardEvent
{
    public DateTime Timestamp { get; set; }
    public RewardReport? Report { get; set; }
    public SafeRewardBonus? Bonus { get; set; }
    public SafeRewardGrade? Grade { get; set; }
}

public class SafeRewardBonus
{
    public uint PlayerId { get; set; }
    public uint[] Bonuses { get; set; } = new uint[8];
}

/// <summary>
/// Safe copy of RewardGrade struct for channel transport
/// </summary>
public class SafeRewardGrade
{
    public uint PlayerId { get; set; }
    public GradeRank Grade { get; set; }
    public int DamageScore { get; set; }
    public int TeamExpectationScore { get; set; }
    public int SkillFulScore { get; set; }
}

public class MatchRewardRecord
{
    public long Id { get; set; }
    public uint PlayerId { get; set; }
    public string CreatedAtUtc { get; set; } = "";
    
    /// <summary>
    /// Links this reward to a BattleSession
    /// </summary>
    public string? SessionId { get; set; }
    
    // Report Data
    public string? GameStatus { get; set; }  // Stored as "Win", "Lost", "Draw"
    public int? Kills { get; set; }
    public int? Deaths { get; set; }
    public int? Supports { get; set; }
    public int? Points { get; set; }
    public int? ExpGain { get; set; }
    public int? GBGain { get; set; }
    public int? MachineAddedExp { get; set; }
    public int? MachineExp { get; set; }  // NEW: different from MachineAddedExp
    public int? PracticeExpAdded { get; set; }
    
    // Grade Data (from packet 1940)
    public string? GradeRank { get; set; }  // Stored as "A+", "A", "B+", etc.
    public int? DamageScore { get; set; }
    public int? TeamExpectationScore { get; set; }
    public int? SkillFulScore { get; set; }
    
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
