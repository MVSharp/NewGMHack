using MessagePack;
using NewGMHack.CommunicationModel.PacketStructs;
using NewGMHack.Stub.PacketStructs.Recv;

namespace NewGMHack.CommunicationModel.Models;

/// <summary>
/// Processed machine model for frontend display.
/// Maps from raw Machine struct with proper type conversions.
/// </summary>
[MessagePackObject]
public class MachineModel
{
    [Key(0)] public uint MachineId { get; set; }
    [Key(1)] public uint Slot { get; set; }
    [Key(2)] public byte Level { get; set; }
    
    /// <summary>
    /// 6 color values converted to web hex format (e.g., "#FF5500")
    /// </summary>
    [Key(3)] public string[] Colors { get; set; } = new string[6];
    
    [Key(4)] public ushort BrushPolish { get; set; }
    
    /// <summary>
    /// Battery value (max 2000), expressed as percentage 0-100
    /// </summary>
    [Key(5)] public float BatteryPercent { get; set; }
    
    /// <summary>
    /// Raw battery value (max 2000)
    /// </summary>
    [Key(6)] public float BatteryRaw { get; set; }
    
    [Key(7)] public uint BattleCount { get; set; }
    [Key(8)] public uint ExtraSkillParts { get; set; }
    
    /// <summary>
    /// Converted from GameTimestamp to C# DateTime
    /// </summary>
    [Key(9)] public DateTime BuyInTime { get; set; }
    
    [Key(10)] public uint CurrentExp { get; set; }
    [Key(11)] public byte OcMaxLevel { get; set; }
    [Key(12)] public bool IsLocked { get; set; }
    
    [Key(13)] public OcPartsModel Oc1Parts { get; set; } = new();
    [Key(14)] public OcPartsModel Oc2Parts { get; set; } = new();
    [Key(15)] public OcPointsModel OcBaseBonusPoints { get; set; } = new();
    [Key(16)] public OcPointsModel OcBonusExtraPoints { get; set; } = new();
    
    /// <summary>
    /// Maps a raw Machine struct to this model
    /// </summary>
    public static unsafe MachineModel FromRaw(Machine raw)
    {
        var model = new MachineModel
        {
            MachineId = raw.MachineId,
            Slot = raw.Slot,
            Level = raw.Level,
            BrushPolish = raw.BrushPolish,
            BatteryRaw = raw.Battery,
            BatteryPercent = (raw.Battery / 2000f) * 100f,
            BattleCount = raw.BattleCount,
            ExtraSkillParts = raw.ExtraSkillParts,
            BuyInTime = GameTimestampToDateTime(raw.BuyInTime),
            CurrentExp = raw.CurrentExp,
            OcMaxLevel = raw.OcMaxLevel,
            IsLocked = raw.Lock != 0,
            Oc1Parts = OcPartsModel.FromRaw(raw.Oc1Parts),
            Oc2Parts = OcPartsModel.FromRaw(raw.Oc2Parts),
            OcBaseBonusPoints = OcPointsModel.FromRaw(raw.OcBasePoints),
            OcBonusExtraPoints = OcPointsModel.FromRaw(raw.OcBonusPoints)
        };
        
        // Convert colors to hex strings
        for (int i = 0; i < 6; i++)
        {
            model.Colors[i] = UShortToHexColor(raw.Color[i]);
        }
        
        return model;
    }
    
    /// <summary>
    /// Converts a ushort color value to web hex color format.
    /// Assumes RGB565 format (5 bits R, 6 bits G, 5 bits B).
    /// </summary>
    private static string UShortToHexColor(ushort color)
    {
        // RGB565 format
        int r = ((color >> 11) & 0x1F) << 3; // 5 bits -> 8 bits
        int g = ((color >> 5) & 0x3F) << 2;  // 6 bits -> 8 bits
        int b = (color & 0x1F) << 3;         // 5 bits -> 8 bits
        
        return $"#{r:X2}{g:X2}{b:X2}";
    }
    
    /// <summary>
    /// Converts GameTimestamp to C# DateTime
    /// </summary>
    private static DateTime GameTimestampToDateTime(GameTimestamp ts)
    {
        try
        {
            return new DateTime(ts.Year, ts.Month, ts.Day, ts.Hour, ts.Minute, ts.Second);
        }
        catch
        {
            // Return epoch if parsing fails
            return DateTime.MinValue;
        }
    }
}

[MessagePackObject]
public class OcPartsModel
{
    [Key(0)] public ushort Part1 { get; set; }
    [Key(1)] public ushort Part2 { get; set; }
    [Key(2)] public ushort Part3 { get; set; }
    [Key(3)] public ushort Part4 { get; set; }
    
    public static OcPartsModel FromRaw(OcParts raw) => new()
    {
        Part1 = raw.Part1,
        Part2 = raw.Part2,
        Part3 = raw.Part3,
        Part4 = raw.Part4
    };
}

[MessagePackObject]
public class OcPointsModel
{
    [Key(0)] public byte Speed { get; set; }
    [Key(1)] public byte Hp { get; set; }
    [Key(2)] public byte Attack { get; set; }
    [Key(3)] public byte Defense { get; set; }
    [Key(4)] public byte Agility { get; set; }
    [Key(5)] public byte Special { get; set; }
    
    /// <summary>
    /// Total of all OC points
    /// </summary>
    [IgnoreMember]
    public int Total => Speed + Hp + Attack + Defense + Agility + Special;
    
    public static OcPointsModel FromRaw(OcPoints raw) => new()
    {
        Speed = raw.Speed,
        Hp = raw.Hp,
        Attack = raw.Attack,
        Defense = raw.Defense,
        Agility = raw.Agility,
        Special = raw.Special
    };
}
