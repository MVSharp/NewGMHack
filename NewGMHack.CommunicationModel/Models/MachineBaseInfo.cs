using System.Runtime.InteropServices;
using MessagePack;

namespace NewGMHack.CommunicationModel.Models;

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
    
    [FieldOffset(0x23D)] public ushort RadarRange;
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
[MessagePackObject]
public class MachineBaseInfo
{
    [Key(0)] public uint MachineId { get; set; }
    [Key(1)] public string ChineseName { get; set; } = "";
    [Key(2)] public string EnglishName { get; set; } = "";
    
    [Key(3)] public bool IsTransformable { get; set; }
    [Key(4)] public string Rank { get; set; } = "";  // C, B, A, S, ALL
    [Key(5)] public int Quality { get; set; }
    [Key(6)] public string CombatType { get; set; } = "";  // Near, Middle, Far
    [Key(7)] public int AttackSpeedLevel { get; set; }
    [Key(8)] public int Rarity { get; set; }
    [Key(9)] public int RespawnTimeSeconds { get; set; }
    
    [Key(10)] public uint TransformId { get; set; }
    [Key(11)] public bool HasTransform { get; set; }
    
    [Key(12)] public uint HP { get; set; }
    [Key(13)] public uint ShieldHP { get; set; }
    [Key(14)] public string ShieldType { get; set; } = "";  // ALL, Near, BZD, Ray
    [Key(15)] public int ShieldDeductionPercentage { get; set; }
    
    /// <summary>
    /// Shield direction: Front, ALL, Back, LeftRight, Left, Right, None
    /// </summary>
    [Key(16)] public string ShieldDirection { get; set; } = "";
    
    [Key(17)] public int BzdSpeed { get; set; }
    [Key(18)] public int MoveSpeed { get; set; }
    [Key(19)] public int ForwardSpeed { get; set; }
    [Key(20)] public int TrackSpeed { get; set; }
    [Key(21)] public int Agility { get; set; }
    [Key(22)] public int BoostRecoverySpeed { get; set; }
    [Key(23)] public int BoostConsumption { get; set; }
    [Key(24)] public int BoostCapacity { get; set; }
    [Key(25)] public float TrackAcceleration { get; set; }
    
    /// <summary>Base attack value from 0x22C</summary>
    [Key(26)] public int Attack { get; set; }
    
    /// <summary>Base defense value from 0x22E</summary>
    [Key(27)] public int Defense { get; set; }
    
    [Key(28)] public uint RadarRange { get; set; }
    [Key(29)] public uint SkillID1 { get; set; }
    [Key(30)] public uint SkillID2 { get; set; }
    [Key(31)] public uint Weapon1Code { get; set; }
    [Key(32)] public uint Weapon2Code { get; set; }
    [Key(33)] public uint Weapon3Code { get; set; }
    [Key(34)] public uint SpecialAttackCode { get; set; }
    
    [Key(35)] public bool HasEndurance { get; set; }
    [Key(36)] public string MdrsFilePath { get; set; } = "";
    
    // Resolved skill and weapon info (populated after scanning)
    [Key(37)] public SkillBaseInfo? Skill1Info { get; set; }
    [Key(38)] public SkillBaseInfo? Skill2Info { get; set; }
    [Key(39)] public WeaponBaseInfo? Weapon1Info { get; set; }
    [Key(40)] public WeaponBaseInfo? Weapon2Info { get; set; }
    [Key(41)] public WeaponBaseInfo? Weapon3Info { get; set; }
    [Key(42)] public WeaponBaseInfo? SpecialAttack { get; set; }
    
    /// <summary>
    /// Transformed machine info (if HasTransform and TransformId != 0)
    /// </summary>
    [Key(43)] public MachineBaseInfo? TransformedMachine { get; set; }
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
            HasTransform = raw.TransformId != 0,
            
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
