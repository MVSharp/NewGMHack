using System.Runtime.InteropServices;
using System.Text;

namespace NewGMHack.Stub.MemoryScanner;

/// <summary>
/// Raw struct read directly from memory. Maps to CE offsets.
/// Total size: 0x290 (656 bytes) - covers mdrsFilePath[15] at 0x264
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 0x290, CharSet = CharSet.Unicode)]
public unsafe struct MachineBaseInfoStruct
{
    [FieldOffset(0x000)] public uint MachineId;
    
    // 0x005 to 0x068 = 99 bytes, but unicode = ~49 chars
    [FieldOffset(0x005)] public fixed char ChineseName[50];
    
    // 0x069 to 0x0CC = 99 bytes, ~49 chars
    [FieldOffset(0x069)] public fixed char EnglishName[50];
    
    [FieldOffset(0x131)] public byte IsTransformable;
    [FieldOffset(0x134)] public byte Rank;
    [FieldOffset(0x135)] public byte Quality;
    [FieldOffset(0x136)] public byte CombatType;
    [FieldOffset(0x138)] public byte AttackSpeedLevel;
    [FieldOffset(0x139)] public byte Rarity;
    [FieldOffset(0x13A)] public byte RespawnTime;
    [FieldOffset(0x13C)] public uint TransformId;
    [FieldOffset(0x141)] public uint HP;
    [FieldOffset(0x145)] public uint ShieldHP;
    [FieldOffset(0x149)] public byte ShieldType;
    [FieldOffset(0x14A)] public byte ShieldDeductionPercentage;
    
    // Shield direction: all 5 bytes should have same value
    // 6=Front, 5=ALL, 4=Back, 3=LeftRight, 1=Left, 2=Right
    [FieldOffset(0x14E)] public byte ShieldDirection14E;
    [FieldOffset(0x14F)] public byte ShieldDirection14F;
    [FieldOffset(0x150)] public byte ShieldDirection150;
    [FieldOffset(0x151)] public byte ShieldDirection151;
    [FieldOffset(0x152)] public byte ShieldDirection152;
    
    [FieldOffset(0x153)] public byte BzdSpeed;
    [FieldOffset(0x21C)] public byte MoveSpeed;
    [FieldOffset(0x21E)] public byte ForwardSpeed;
    [FieldOffset(0x21F)] public byte TrackSpeed;
    [FieldOffset(0x221)] public byte Agility;
    [FieldOffset(0x222)] public byte BoostRecoverySpeed;
    [FieldOffset(0x224)] public byte BoostConsumption;
    [FieldOffset(0x226)] public byte BoostCapacity;
    [FieldOffset(0x228)] public float TrackAcceleration;
    
    [FieldOffset(0x22C)] public short Attack;    // 2 bytes signed
    [FieldOffset(0x22E)] public short Defense;   // 2 bytes signed
    
    [FieldOffset(0x23D)] public uint RadarRange;
    [FieldOffset(0x23F)] public uint SkillID1;
    [FieldOffset(0x243)] public uint SkillID2;
    [FieldOffset(0x249)] public uint Weapon1Code;
    [FieldOffset(0x24D)] public uint Weapon2Code;
    [FieldOffset(0x251)] public uint Weapon3Code;
    [FieldOffset(0x255)] public uint SpecialAttackCode;
    [FieldOffset(0x25F)] public byte Endurance;
    
    // 0x264 to 0x27E = 26 bytes = 13-15 unicode chars
    [FieldOffset(0x264)] public fixed char MdrsFilePath[15];
}

/// <summary>
/// Transformed/friendly class for frontend display
/// </summary>
public class MachineBaseInfo
{
    public uint MachineId { get; set; }
    public string ChineseName { get; set; } = "";
    public string EnglishName { get; set; } = "";
    
    public bool IsTransformable { get; set; }
    public string Rank { get; set; } = "";  // C, B, A, S, ALL
    public int Quality { get; set; }
    public string CombatType { get; set; } = "";  // Near, Middle, Far
    public int AttackSpeedLevel { get; set; }
    public int Rarity { get; set; }
    public int RespawnTimeSeconds { get; set; }
    
    public uint TransformId { get; set; }
    public bool HasTransform => TransformId != 0;
    
    public uint HP { get; set; }
    public uint ShieldHP { get; set; }
    public string ShieldType { get; set; } = "";  // ALL, Near, BZD, Ray
    public int ShieldDeductionPercentage { get; set; }
    
    /// <summary>
    /// Shield direction: Front, ALL, Back, LeftRight, Left, Right, None
    /// </summary>
    public string ShieldDirection { get; set; } = "";
    
    public int BzdSpeed { get; set; }
    public int MoveSpeed { get; set; }
    public int ForwardSpeed { get; set; }
    public int TrackSpeed { get; set; }
    public int Agility { get; set; }
    public int BoostRecoverySpeed { get; set; }
    public int BoostConsumption { get; set; }
    public int BoostCapacity { get; set; }
    public float TrackAcceleration { get; set; }
    
    /// <summary>Base attack value from 0x22C</summary>
    public int Attack { get; set; }
    
    /// <summary>Base defense value from 0x22E</summary>
    public int Defense { get; set; }
    
    public uint RadarRange { get; set; }
    public uint SkillID1 { get; set; }
    public uint SkillID2 { get; set; }
    public uint Weapon1Code { get; set; }
    public uint Weapon2Code { get; set; }
    public uint Weapon3Code { get; set; }
    public uint SpecialAttackCode { get; set; }
    
    public bool HasEndurance { get; set; }
    public string MdrsFilePath { get; set; } = "";
    
    // Resolved skill and weapon info (populated after scanning)
    public SkillBaseInfo? Skill1Info { get; set; }
    public SkillBaseInfo? Skill2Info { get; set; }
    public WeaponBaseInfo? Weapon1Info { get; set; }
    public WeaponBaseInfo? Weapon2Info { get; set; }
    public WeaponBaseInfo? Weapon3Info { get; set; }
    public WeaponBaseInfo? SpecialAttack { get; set; }
    
    /// <summary>
    /// Transformed machine info (if HasTransform and TransformId != 0)
    /// </summary>
    public MachineBaseInfo? TransformedMachine { get; set; }
    
    /// <summary>
    /// Transform raw struct to friendly class
    /// </summary>
    public static unsafe MachineBaseInfo FromRaw(MachineBaseInfoStruct raw)
    {
        return new MachineBaseInfo
        {
            MachineId = raw.MachineId,
            ChineseName = GetFixedString(raw.ChineseName, 50),
            EnglishName = GetFixedString(raw.EnglishName, 50),
            
            IsTransformable = raw.IsTransformable != 0,
            Rank = raw.Rank switch
            {
                1 => "C",
                2 => "B",
                3 => "A",
                4 => "S",
                5 => "ALL",
                _ => "?"
            },
            Quality = raw.Quality,
            CombatType = raw.CombatType switch
            {
                0 => "Near",
                1 => "Middle",
                2 => "Far",
                _ => "?"
            },
            AttackSpeedLevel = raw.AttackSpeedLevel,
            Rarity = raw.Rarity,
            RespawnTimeSeconds = raw.RespawnTime,
            
            TransformId = raw.TransformId,
            HP = raw.HP,
            ShieldHP = raw.ShieldHP,
            ShieldType = raw.ShieldType switch
            {
                0 => "ALL",
                1 => "Near",
                2 => "BZD",
                3 => "Ray",
                _ => "?"
            },
            ShieldDeductionPercentage = raw.ShieldDeductionPercentage,
            
            // Shield direction: use any of the 5 bytes (they should all be same)
            ShieldDirection = raw.ShieldDirection14E switch
            {
                6 => "Front",
                5 => "ALL",
                4 => "Back",
                3 => "LeftRight",
                1 => "Left",
                2 => "Right",
                _ => "None"
            },
            
            BzdSpeed = raw.BzdSpeed,
            MoveSpeed = raw.MoveSpeed,
            ForwardSpeed = raw.ForwardSpeed,
            TrackSpeed = raw.TrackSpeed,
            Agility = raw.Agility,
            BoostRecoverySpeed = raw.BoostRecoverySpeed,
            BoostConsumption = raw.BoostConsumption,
            BoostCapacity = raw.BoostCapacity,
            TrackAcceleration = raw.TrackAcceleration,
            
            Attack = raw.Attack,
            Defense = raw.Defense,
            
            RadarRange = raw.RadarRange,
            SkillID1 = raw.SkillID1,
            SkillID2 = raw.SkillID2,
            Weapon1Code = raw.Weapon1Code,
            Weapon2Code = raw.Weapon2Code,
            Weapon3Code = raw.Weapon3Code,
            SpecialAttackCode = raw.SpecialAttackCode,
            
            HasEndurance = raw.Endurance == 2,
            MdrsFilePath = GetFixedString(raw.MdrsFilePath, 15)
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
