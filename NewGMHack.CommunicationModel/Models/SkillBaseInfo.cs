using System.Runtime.InteropServices;
using MessagePack;

namespace NewGMHack.CommunicationModel.Models;

/// <summary>
/// Raw struct for Skill data from memory scan.
/// AOB pattern: [SkillID as 3-byte little-endian] 00 00 ??
/// Example: 70121 (0x011189) -> E9 11 01 00 00 ??
/// Total size: 0x300 (768 bytes) to cover description at 0x1E1
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 0x300, CharSet = CharSet.Unicode)]
public unsafe struct SkillBaseInfoStruct
{
    [FieldOffset(0x000)] public uint SkillId;
    
    // 0x05 to 0x64 = ~47 unicode chars
    [FieldOffset(0x005)] public fixed char SkillName[48];
    
    [FieldOffset(0x069)] public byte HpActivate;          // 0=NoNeed, 2=HpMeet, 4=LastLife, 6=LastLife
    [FieldOffset(0x06A)] public byte ExactHpActivatePercent; // 0=NoNeed, else %
    
    [FieldOffset(0x0D0)] public byte Movement;
    [FieldOffset(0x0D2)] public byte ForwardSpeedPercent;
    [FieldOffset(0x0D4)] public byte UrgentEscape;
    [FieldOffset(0x0D6)] public byte AgilityPercent;
    
    // 0xDA - Boost recovery: 3F80=50%, 4000=100%, 3F00=25%, C0=75%
    [FieldOffset(0x0DA)] public uint BoostRecoveryRaw;
    
    [FieldOffset(0x0DC)] public ushort BoostCapacityIncrease;
    [FieldOffset(0x0DE)] public ushort AttackIncrease;
    [FieldOffset(0x0E0)] public ushort DefenseIncrease;  // Can be negative: 65526 = -10
    [FieldOffset(0x0E2)] public ushort RadarRangeIncrease;
    
    // Additional fields
    [FieldOffset(0x0E4)] public ushort SpIncreaseSpeed;   // SP积累加快 150=15%
    [FieldOffset(0x0E8)] public ushort ApplyToRaw;        // 3F80=Self, else Team
    [FieldOffset(0x0EA)] public byte WeaponReloadIncrease; // CD装填上升
    
    [FieldOffset(0x10A)] public ushort NearDamageReduction;  // 近距离减伤
    [FieldOffset(0x110)] public ushort MidDamageReductionRaw; // 3E80=25.6%, 3F00=50%, 4C=12.5%
    [FieldOffset(0x122)] public ushort MeleeDamageIncrease;   // 近战伤害增加
    
    // 0x1E1 - Description unicode string, 60 chars (fits within 0x300 buffer)
    [FieldOffset(0x1E1)] public fixed char Description[60];
}

/// <summary>
/// Transformed skill info for frontend display
/// </summary>
[MessagePackObject]
public class SkillBaseInfo
{
    [Key(0)] public uint SkillId { get; set; }
    [Key(1)] public string SkillName { get; set; } = "";
    
    /// <summary>
    /// HP activation condition: None, HpMeet, LastLife
    /// </summary>
    [Key(2)] public string HpActivateCondition { get; set; } = "";
    
    /// <summary>
    /// Exact HP % to activate (0 = no requirement)
    /// </summary>
    [Key(3)] public int ExactHpActivatePercent { get; set; }
    
    [Key(4)] public int Movement { get; set; }
    [Key(5)] public int ForwardSpeedPercent { get; set; }
    [Key(6)] public int UrgentEscape { get; set; }
    [Key(7)] public int AgilityPercent { get; set; }
    
    /// <summary>
    /// Boost recovery % bonus
    /// </summary>
    [Key(8)] public int BoostRecoveryPercent { get; set; }
    
    [Key(9)] public int BoostCapacityIncrease { get; set; }
    [Key(10)] public int AttackIncrease { get; set; }
    
    /// <summary>
    /// Defense change (can be negative)
    /// </summary>
    [Key(11)] public int DefenseIncrease { get; set; }
    
    [Key(12)] public int RadarRangeIncrease { get; set; }
    
    /// <summary>
    /// SP accumulation speed increase (150 = 15%)
    /// </summary>
    [Key(13)] public float SpIncreaseSpeedPercent { get; set; }
    
    /// <summary>
    /// True = applies to self, False = applies to team
    /// </summary>
    [Key(14)] public bool AppliesToSelf { get; set; }
    
    [Key(15)] public int WeaponReloadIncrease { get; set; }
    
    /// <summary>
    /// Near-range damage reduction %
    /// </summary>
    [Key(16)] public float NearDamageReductionPercent { get; set; }
    
    /// <summary>
    /// Mid-range damage reduction %
    /// </summary>
    [Key(17)] public float MidDamageReductionPercent { get; set; }
    
    /// <summary>
    /// Melee damage increase
    /// </summary>
    [Key(18)] public int MeleeDamageIncrease { get; set; }
    
    [Key(19)] public string Description { get; set; } = "";
    
    /// <summary>
    /// Transform raw struct to friendly class
    /// </summary>
    public static unsafe SkillBaseInfo FromRaw(SkillBaseInfoStruct raw)
    {
        return new SkillBaseInfo
        {
            SkillId = raw.SkillId,
            SkillName = GetFixedString(raw.SkillName, 48),
            
            HpActivateCondition = raw.HpActivate switch
            {
                0 => "None",
                2 => "HpMeet",
                4 or 6 => "LastLife",
                _ => $"Unknown({raw.HpActivate})"
            },
            ExactHpActivatePercent = raw.ExactHpActivatePercent,
            
            Movement = raw.Movement,
            ForwardSpeedPercent = raw.ForwardSpeedPercent,
            UrgentEscape = raw.UrgentEscape,
            AgilityPercent = raw.AgilityPercent,
            
            BoostRecoveryPercent = DecodePercentValue(raw.BoostRecoveryRaw),
            
            BoostCapacityIncrease = raw.BoostCapacityIncrease,
            AttackIncrease = raw.AttackIncrease,
            
            // Defense can be negative: ushort 65526 = -10 as signed
            DefenseIncrease = raw.DefenseIncrease > 32767 
                ? raw.DefenseIncrease - 65536 
                : raw.DefenseIncrease,
                
            RadarRangeIncrease = raw.RadarRangeIncrease,
            
            // SP increase: 150 = 15%
            SpIncreaseSpeedPercent = raw.SpIncreaseSpeed / 10f,
            
            // 3F80 = Self, else Team
            AppliesToSelf = raw.ApplyToRaw == 0x3F80 || raw.ApplyToRaw == 0x803F,
            
            WeaponReloadIncrease = raw.WeaponReloadIncrease,
            
            NearDamageReductionPercent = DecodePercentValue16(raw.NearDamageReduction),
            MidDamageReductionPercent = DecodePercentValue16(raw.MidDamageReductionRaw),
            MeleeDamageIncrease = raw.MeleeDamageIncrease,
            
            Description = GetFixedString(raw.Description, 60)
        };
    }
    
    /// <summary>
    /// Decode special percent encoding: 3F80=50%, 4000=100%, 3F00=25%, C0=75%
    /// </summary>
    private static int DecodePercentValue(uint raw)
    {
        return raw switch
        {
            0x3F80 or 0x803F => 50,
            0x4000 or 0x0040 => 100,
            0x3F00 or 0x003F => 25,
            0x00C0 or 0xC000 => 75,
            0 => 0,
            _ => (int)raw
        };
    }
    
    /// <summary>
    /// Decode 16-bit percent: 3E80=25.6%, 3F00=50%, 4C=12.5%
    /// </summary>
    private static float DecodePercentValue16(ushort raw)
    {
        return raw switch
        {
            0x3E80 or 0x803E => 25.6f,
            0x3F00 or 0x003F => 50f,
            0x4C or 0x4C00 => 12.5f,
            0 => 0,
            _ => raw / 100f  // Fallback: treat as direct percentage
        };
    }
    
    private static unsafe string GetFixedString(char* ptr, int maxLength)
    {
        int length = 0;
        for (int i = 0; i < maxLength; i++)
        {
            if (ptr[i] == '\0') break;
            length++;
        }
        return new string(ptr, 0, length);
    }
}
