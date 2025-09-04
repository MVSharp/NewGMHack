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
}