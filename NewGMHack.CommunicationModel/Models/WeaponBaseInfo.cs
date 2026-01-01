using System.Runtime.InteropServices;
using MessagePack;

namespace NewGMHack.CommunicationModel.Models;

/// <summary>
/// Raw struct for Weapon data from memory scan.
/// AOB pattern: [WeaponID 2-byte LE] 00 00 00 ?? ??
/// Example: 28751 (0x704F) -> 4F 70 00 00 00 ?? ??
/// Total size: 0xB0 (176 bytes) to cover all fields
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 0x140, CharSet = CharSet.Unicode)]
public unsafe struct WeaponBaseInfoStruct
{
    [FieldOffset(0x000)] public uint WeaponId;
    
    // 0x05 unicode string, 50 chars (100 bytes)
    [FieldOffset(0x005)] public fixed char WeaponName[50];
    
    [FieldOffset(0x06A)] public byte WeaponType;       // 1=Near, 2=Mid, 3=Far
    [FieldOffset(0x06C)] public uint WeaponDamage;
    [FieldOffset(0x070)] public byte MissileSpeed;
    [FieldOffset(0x072)] public uint WeaponRange;
    [FieldOffset(0x078)] public byte AimSpeed;         // 抬枪速度
    [FieldOffset(0x07C)] public byte AmmoCount;        // 弹药数量
    [FieldOffset(0x07E)] public ushort AmmoRecoverySpeed; // 弹药恢复速度
    [FieldOffset(0x080)] public ushort KnockbackEffect;   // 击退效果
    [FieldOffset(0x082)] public byte CoolTime;
    [FieldOffset(0x089)] public ushort KnockdownPerHit;   // 每下倒地
    [FieldOffset(0x08B)] public ushort KnockdownThreshold; // 达到此值倒地
    [FieldOffset(0x08D)] public byte Pierce;
    [FieldOffset(0x08E)] public byte PierceValue;
    [FieldOffset(0x08F)] public byte AllowUseWhenMove;
    
    [FieldOffset(0x096)] public float CollisionWidth;
    [FieldOffset(0x09A)] public float CollisionHeight;
    [FieldOffset(0x09E)] public float SplashRadius;
    [FieldOffset(0x0A2)] public float SplashCoreRadius;
    
    [FieldOffset(0x0A4)] public fixed char TraceEffect[16];
    [FieldOffset(0x0E0)] public fixed char AttackEffect[16];
    [FieldOffset(0x120)] public fixed char AttackSound[16];
}

/// <summary>
/// Transformed weapon info for frontend display
/// </summary>
[MessagePackObject]
public class WeaponBaseInfo
{
    [Key(0)] public uint WeaponId { get; set; }
    [Key(1)] public string WeaponName { get; set; } = "";
    
    /// <summary>
    /// Weapon type: Near, Mid, Far
    /// </summary>
    [Key(2)] public string WeaponType { get; set; } = "";
    
    [Key(3)] public uint WeaponDamage { get; set; }
    [Key(4)] public int MissileSpeed { get; set; }
    [Key(5)] public uint WeaponRange { get; set; }
    [Key(6)] public int AimSpeed { get; set; }
    [Key(7)] public int AmmoCount { get; set; }
    [Key(8)] public int AmmoRecoverySpeed { get; set; }
    [Key(9)] public int KnockbackEffect { get; set; }
    [Key(10)] public int CoolTime { get; set; }
    [Key(11)] public int KnockdownPerHit { get; set; }
    [Key(12)] public int KnockdownThreshold { get; set; }
    [Key(13)] public bool HasPierce { get; set; }
    [Key(14)] public int PierceValue { get; set; }
    [Key(15)] public bool AllowUseWhenMove { get; set; }
    
    [Key(16)] public float CollisionWidth { get; set; }
    [Key(17)] public float CollisionHeight { get; set; }
    [Key(18)] public float SplashRadius { get; set; }
    [Key(19)] public float SplashCoreRadius { get; set; }
    
    [Key(20)] public string TraceEffect { get; set; } = "";
    [Key(21)] public string AttackEffect { get; set; } = "";
    [Key(22)] public string AttackSound { get; set; } = "";
    
    /// <summary>
    /// Transform raw struct to friendly class
    /// </summary>
    public static unsafe WeaponBaseInfo FromRaw(WeaponBaseInfoStruct raw)
    {
        return new WeaponBaseInfo
        {
            WeaponId = raw.WeaponId,
            WeaponName = GetFixedString(raw.WeaponName, 50),
            
            WeaponType = raw.WeaponType switch
            {
                1 => "Near",
                2 => "Mid",
                3 => "Far",
                _ => $"Unknown({raw.WeaponType})"
            },
            
            WeaponDamage = raw.WeaponDamage,
            MissileSpeed = raw.MissileSpeed,
            WeaponRange = raw.WeaponRange,
            AimSpeed = raw.AimSpeed,
            AmmoCount = raw.AmmoCount,
            AmmoRecoverySpeed = raw.AmmoRecoverySpeed,
            KnockbackEffect = raw.KnockbackEffect,
            CoolTime = raw.CoolTime,
            KnockdownPerHit = raw.KnockdownPerHit,
            KnockdownThreshold = raw.KnockdownThreshold,
            HasPierce = raw.Pierce != 0,
            PierceValue = raw.PierceValue,
            AllowUseWhenMove = raw.AllowUseWhenMove != 0,
            
            CollisionWidth = raw.CollisionWidth,
            CollisionHeight = raw.CollisionHeight,
            SplashRadius = raw.SplashRadius,
            SplashCoreRadius = raw.SplashCoreRadius,
            
            TraceEffect = GetFixedString(raw.TraceEffect, 16),
            AttackEffect = GetFixedString(raw.AttackEffect, 16),
            AttackSound = GetFixedString(raw.AttackSound, 16)
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
