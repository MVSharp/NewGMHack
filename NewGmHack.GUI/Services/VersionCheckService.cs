using System.Net.Http;
using System.Reflection;
using System.Windows;

namespace NewGmHack.GUI.Services;

/// <summary>
/// Checks application version against remote GitHub version.txt
/// In Debug mode, this check is skipped.
/// </summary>
public class VersionCheckService
{
    // TODO: Update this URL after pushing to GitHub
    // Format: https://raw.githubusercontent.com/{owner}/{repo}/{branch}/version.txt
    private const string VersionUrl = "https://raw.githubusercontent.com/YOUR_USERNAME/NewGMHack/main/version.txt";
    
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    /// <summary>
    /// Check version on startup. Returns true if version matches or check is skipped.
    /// Returns false if version mismatch detected.
    /// </summary>
    public static async Task<(bool Success, string? Message)> CheckVersionAsync()
    {
#if DEBUG
        // Skip version check in Debug mode
        return (true, null);
#else
        try
        {
            var localVersion = GetLocalVersion();
            var remoteVersion = await GetRemoteVersionAsync();
            
            if (string.IsNullOrEmpty(remoteVersion))
            {
                // Can't reach GitHub, allow app to continue
                return (true, "Unable to verify version (offline mode)");
            }
            
            // Trim and compare versions
            localVersion = localVersion.Trim();
            remoteVersion = remoteVersion.Trim();
            
            if (localVersion != remoteVersion)
            {
                return (false, $"Version mismatch!\n\nYour version: {localVersion}\nLatest version: {remoteVersion}\n\nPlease download the latest version.");
            }
            
            return (true, null);
        }
        catch (Exception ex)
        {
            // On error, allow app to continue but log
            System.Diagnostics.Debug.WriteLine($"Version check error: {ex.Message}");
            return (true, null);
        }
#endif
    }

    /// <summary>
    /// Get the local Stub DLL version
    /// </summary>
    private static string GetLocalVersion()
    {
        try
        {
            var stubPath = "NewGMHack.Stub.dll";
            if (System.IO.File.Exists(stubPath))
            {
                var assemblyName = AssemblyName.GetAssemblyName(stubPath);
                return assemblyName.Version?.ToString() ?? "0.0.0.0";
            }
            
            // Fallback to GUI assembly version
            var guiVersion = Assembly.GetExecutingAssembly().GetName().Version;
            return guiVersion?.ToString() ?? "0.0.0.0";
        }
        catch
        {
            return "0.0.0.0";
        }
    }

    /// <summary>
    /// Fetch version from GitHub raw URL
    /// </summary>
    private static async Task<string?> GetRemoteVersionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(VersionUrl);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}
