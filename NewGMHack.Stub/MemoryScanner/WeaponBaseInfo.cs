using System.Runtime.InteropServices;

namespace NewGMHack.Stub.MemoryScanner;

/// <summary>
/// Raw struct for Weapon data from memory scan.
/// AOB pattern: [WeaponID 2-byte LE] 00 00 00 ?? ??
/// Example: 28751 (0x704F) -> 4F 70 00 00 00 ?? ??
/// Total size: 0xB0 (176 bytes) to cover all fields
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 0xB0, CharSet = CharSet.Unicode)]
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
}

/// <summary>
/// Transformed weapon info for frontend display
/// </summary>
public class WeaponBaseInfo
{
    public uint WeaponId { get; set; }
    public string WeaponName { get; set; } = "";
    
    /// <summary>
    /// Weapon type: Near, Mid, Far
    /// </summary>
    public string WeaponType { get; set; } = "";
    
    public uint WeaponDamage { get; set; }
    public int MissileSpeed { get; set; }
    public uint WeaponRange { get; set; }
    public int AimSpeed { get; set; }
    public int AmmoCount { get; set; }
    public int AmmoRecoverySpeed { get; set; }
    public int KnockbackEffect { get; set; }
    public int CoolTime { get; set; }
    public int KnockdownPerHit { get; set; }
    public int KnockdownThreshold { get; set; }
    public bool HasPierce { get; set; }
    public int PierceValue { get; set; }
    public bool AllowUseWhenMove { get; set; }
    
    public float CollisionWidth { get; set; }
    public float CollisionHeight { get; set; }
    public float SplashRadius { get; set; }
    public float SplashCoreRadius { get; set; }
    
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
            SplashCoreRadius = raw.SplashCoreRadius
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
