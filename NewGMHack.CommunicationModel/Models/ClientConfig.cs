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
        new HackFeatures() { Name = FeatureName.IsMissionBomb, IsEnabled    = false },
        new HackFeatures() { Name = FeatureName.IsAutoReady, IsEnabled      = false },
        new HackFeatures() { Name = FeatureName.IsAutoCharge, IsEnabled = false },
        new HackFeatures() { Name = FeatureName.IsPlayerBomb, IsEnabled     = false },
        new HackFeatures() { Name = FeatureName.IsRebound, IsEnabled     = false },
        new HackFeatures() { Name = FeatureName.IsIllusion, IsEnabled     = false },
        new HackFeatures() { Name = FeatureName.SuckStarOverChina, IsEnabled     = false },
        new HackFeatures(){Name = FeatureName.FreezeEnemy , IsEnabled = false},
        new HackFeatures(){Name = FeatureName.WideVision , IsEnabled = false},
        //new HackFeatures() { Name = FeatureName.IsAimSupport, IsEnabled     = false },
        new HackFeatures() { Name = FeatureName.IsAutoFunnel, IsEnabled     = false },
        new HackFeatures() { Name = FeatureName.CollectGift, IsEnabled     = false },
        //new HackFeatures() { Name = FeatureName.IsAutoFunnel, IsEnabled     = false },
        new HackFeatures() { Name = FeatureName.EnableAutoAim, IsEnabled     = true },
        new HackFeatures() { Name = FeatureName.EnableOverlay, IsEnabled     = true },
        new HackFeatures() { Name = FeatureName.Debug, IsEnabled     = false },
        new HackFeatures() { Name = FeatureName.FreeMove, IsEnabled     = false },
        new HackFeatures() { Name = FeatureName.BackGroundMode, IsEnabled     = false },
    };
    public bool IsInGame { get; set; } = false;
}

[MessagePackObject]
public class HackFeatures
{
    [Key(0)]
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public FeatureName Name      { get; set; }
    [Key(1)]
    public bool        IsEnabled { get; set; }
}