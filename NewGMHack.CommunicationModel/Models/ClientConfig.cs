using MessagePack;

namespace NewGMHack.Stub;

public static class FeatureExtensions

{
    public static HackFeatures? GetFeature(this IEnumerable<HackFeatures?> features,FeatureName name)
    {
        return features.FirstOrDefault(x => x.Name == name);
    }

    public static bool IsFeatureEnable(this IEnumerable<HackFeatures> features, FeatureName name)
    {
        var f = features.GetFeature(name);
        if (f == null) return false;
        return f.IsEnabled;
    }
}
public class ClientConfig
{
    public List<HackFeatures> Features = new()
    {
        new HackFeatures() { Name = FeatureName.IsMissionBomb, IsEnabled    = true },
        new HackFeatures() { Name = FeatureName.IsAutoReady, IsEnabled      = false },
        new HackFeatures() { Name = FeatureName.IsRandomLocation, IsEnabled = false },
        new HackFeatures() { Name = FeatureName.IsPlayerBomb, IsEnabled     = false },
        new HackFeatures() { Name = FeatureName.IsRebound, IsEnabled     = true },
        new HackFeatures() { Name = FeatureName.IsIllusion, IsEnabled     = true },
        new HackFeatures() { Name = FeatureName.IsAimSupport, IsEnabled     = false },
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