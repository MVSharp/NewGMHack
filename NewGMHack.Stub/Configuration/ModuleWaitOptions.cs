namespace NewGMHack.Stub.Configuration;

/// <summary>
/// Configuration options for module wait service
/// </summary>
public class ModuleWaitOptions
{
    /// <summary>
    /// Maximum time to wait for all critical modules to load (default: 30 seconds)
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Polling interval for checking module load status (default: 500ms)
    /// </summary>
    public int CheckIntervalMs { get; set; } = 500;

    /// <summary>
    /// Critical module names that must be loaded before hooks initialize
    /// </summary>
    public string[] RequiredModules { get; set; } =
    {
        "dinput8.dll",    // DirectInput
        "ws2_32.dll",     // Winsock
        "d3d9.dll"        // DirectX 9
    };
}
