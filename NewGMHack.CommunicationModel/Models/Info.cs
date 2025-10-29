using MessagePack;

namespace NewGMHack.Stub;

[MessagePackObject]
public class Info
{
    [Key(0)]
    public uint PersonId { get; set; }
    [Key(1)]
    public uint GundamId { get; set; }
    [Key(2)]
    public uint Weapon1  { get; set; }
    [Key(3)]
    public uint Weapon2  { get; set; }
    [Key(4)]
    public uint Weapon3  { get; set; }

    [Key(5)]
    public float X { get;      set; }

    [Key(6)]
    public float Y { get;      set; }

    [Key(7)]
    public float Z { get;      set; }

    [Key(8)]
    public string GundamName { get; set; } = "";

    [Key(9)]
    public uint Slot { get; set; } = 0;

    [Key(10)]
    public int CurrentHp { get; set; }

    [Key(11)]
    public int MaxHp { get; set; }
}