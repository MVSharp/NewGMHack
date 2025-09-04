using MessagePack;

namespace NewGMHack.Stub;

public class ClientConfig
{
    public List<HackFeatures> Features = new()
    {
        new HackFeatures() { Name = FeatureName.IsMissionBomb, IsEnabled    = true },
        new HackFeatures() { Name = FeatureName.IsAutoReady, IsEnabled      = false },
        new HackFeatures() { Name = FeatureName.IsRandomLocation, IsEnabled = false },
        new HackFeatures() { Name = FeatureName.IsPlayerBomb, IsEnabled     = false },
    };
}

[MessagePackObject]
public class HackFeatures
{
    [Key(0)]
    public FeatureName Name      { get; set; }
    [Key(1)]
    public bool        IsEnabled { get; set; }
}