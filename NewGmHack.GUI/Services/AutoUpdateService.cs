using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace NewGmHack.GUI.Services;

/// <summary>
/// Auto-update service with force update, frontend hot-reload, and rollback support
/// </summary>
public class AutoUpdateService
{
    private const string GitHubOwner = "MVSharp";
    private const string GitHubRepo = "NewGMHack";
    private const string ReleasesUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders = { { "User-Agent", "NewGMHack" } }
    };

    private readonly ILogger<AutoUpdateService> _logger;
    private readonly string _appDirectory;
    private readonly string _backupDirectory;
    private readonly string _wwwrootPath;

    public AutoUpdateService(ILogger<AutoUpdateService> logger)
    {
        _logger = logger;
        _appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _backupDirectory = Path.Combine(_appDirectory, ".backup");
        _wwwrootPath = Path.Combine(_appDirectory, "wwwroot");
    }

    /// <summary>
    /// Check for and apply updates synchronously (blocking call for startup)
    /// Returns true if a force update was triggered (app should exit)
    /// </summary>
    public async Task<bool> CheckForUpdatesAsync()
    {
#if DEBUG
        _logger.LogInformation("Skipping update check in DEBUG mode");
        return false;
#endif

        try
        {
            _logger.LogInformation("Checking for updates...");

            // Fetch latest release info from GitHub
            var releaseInfo = await FetchLatestReleaseAsync();
            if (releaseInfo == null)
            {
                _logger.LogWarning("Unable to fetch release info (offline mode)");
                return false;
            }

            // Compare versions
            var currentVersion = GetCurrentVersion();
            var latestVersion = ParseVersion(releaseInfo.TagName);

            if (latestVersion <= currentVersion)
            {
                _logger.LogInformation("Already up to date: {Version}", currentVersion);
                return false;
            }

            _logger.LogInformation("Update available: {Current} -> {Latest}", currentVersion, latestVersion);

            // Detect which components changed
            var changeDetection = await DetectChangedComponentsAsync(releaseInfo);

            // Scenario A: Only frontend changed - hot-reload WebView2
            if (changeDetection.FrontendChanged && !changeDetection.GuiChanged && !changeDetection.StubChanged)
            {
                _logger.LogInformation("Only frontend changed - applying hot-reload");
                await ApplyFrontendUpdateAsync(releaseInfo);
                return false;
            }

            // Scenario B: GUI or Stub changed - force update and restart
            if (changeDetection.GuiChanged || changeDetection.StubChanged)
            {
                _logger.LogInformation("GUI or Stub changed - initiating force update");
                await ApplyForceUpdateAsync(releaseInfo);
                return true; // Signal caller to exit - don't continue with old DLL
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
            // Don't block startup on error
            return false;
        }
    }

    /// <summary>
    /// Fetch latest release info from GitHub API
    /// </summary>
    private async Task<GitHubRelease?> FetchLatestReleaseAsync()
    {
        try
        {
            _logger.LogInformation("Fetching latest release from {Url}", ReleasesUrl);
            var response = await _httpClient.GetAsync(ReleasesUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub API returned {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("GitHub API response: {Json}", json);

            var release = JsonSerializer.Deserialize<GitHubRelease>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (release == null)
            {
                _logger.LogWarning("Failed to deserialize GitHub release response");
                return null;
            }

            _logger.LogInformation("Successfully fetched release: {TagName}, {AssetCount} assets",
                release.TagName, release.Assets?.Length ?? 0);

            return release;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching release info");
            return null;
        }
    }

    /// <summary>
    /// Detect which components changed by checking assets
    /// </summary>
    private async Task<ComponentChangeDetection> DetectChangedComponentsAsync(GitHubRelease release)
    {
        var detection = new ComponentChangeDetection();

        // Check for wwwroot.zip (frontend)
        detection.FrontendChanged = release.Assets.Any(a => a.Name.Equals("wwwroot.zip", StringComparison.OrdinalIgnoreCase));

        // Check for GUI executable
        detection.GuiChanged = release.Assets.Any(a => a.Name.Equals("NewGMHack.GUI.exe", StringComparison.OrdinalIgnoreCase));

        // Check for Stub DLL
        detection.StubChanged = release.Assets.Any(a => a.Name.Equals("NewGMHack.Stub.dll", StringComparison.OrdinalIgnoreCase));

        _logger.LogInformation("Component changes: Frontend={Frontend}, GUI={GUI}, Stub={Stub}",
            detection.FrontendChanged, detection.GuiChanged, detection.StubChanged);

        return detection;
    }

    /// <summary>
    /// Apply frontend-only update with hot-reload
    /// </summary>
    private async Task ApplyFrontendUpdateAsync(GitHubRelease release)
    {
        try
        {
            // Create backup
            CreateBackup(new[] { _wwwrootPath });

            // Download wwwroot.zip
            var wwwrootAsset = release.Assets.First(a => a.Name.Equals("wwwroot.zip", StringComparison.OrdinalIgnoreCase));
            var tempZipPath = Path.Combine(Path.GetTempPath(), "wwwroot.zip");

            _logger.LogInformation("Downloading wwwroot.zip...");
            await DownloadFileAsync(wwwrootAsset.BrowserDownloadUrl, tempZipPath);

            // Verify checksum if available
            var checksumsAsset = release.Assets.FirstOrDefault(a => a.Name.Equals("checksums.txt", StringComparison.OrdinalIgnoreCase));
            if (checksumsAsset != null)
            {
                _logger.LogInformation("Verifying checksums...");
                await VerifyChecksumAsync(checksumsAsset.BrowserDownloadUrl, tempZipPath);
            }

            // Extract to temp directory
            var tempExtractPath = Path.Combine(Path.GetTempPath(), "wwwroot_new");
            if (Directory.Exists(tempExtractPath)) Directory.Delete(tempExtractPath, true);
            System.IO.Compression.ZipFile.ExtractToDirectory(tempZipPath, tempExtractPath);

            // Replace wwwroot contents
            _logger.LogInformation("Updating wwwroot...");
            if (Directory.Exists(_wwwrootPath))
            {
                Directory.Delete(_wwwrootPath, true);
            }
            Directory.Move(tempExtractPath, _wwwrootPath);

            // Cleanup
            File.Delete(tempZipPath);
            if (Directory.Exists(tempExtractPath)) Directory.Delete(tempExtractPath, true);

            _logger.LogInformation("Frontend update applied successfully");

            // Hot-reload will be handled by WebView2 reload event
            FrontendUpdateRequired?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying frontend update");
            RestoreBackup();
            throw;
        }
    }

    /// <summary>
    /// Apply force update (GUI or Stub changed) using custom updater stub
    /// </summary>
    private async Task ApplyForceUpdateAsync(GitHubRelease release)
    {
        try
        {
            _logger.LogInformation("Starting custom force update flow");

            // Step 1: Create backup
            CreateBackup(new[]
            {
                "NewGMHack.GUI.exe",
                "NewGMHack.Stub.dll",
                _wwwrootPath
            });

            // Step 2: Create temp directory for update files
            var tempDir = Path.Combine(Path.GetTempPath(), $"NewGMHack_Update_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
            _logger.LogInformation("Created temp directory: {TempDir}", tempDir);

            // Step 3: Download all assets to temp directory
            var downloadTasks = new List<Task>
            {
                DownloadAssetAsync(release, "NewGMHack.GUI.exe", tempDir),
                DownloadAssetAsync(release, "NewGMHack.Stub.dll", tempDir),
                DownloadAssetAsync(release, "wwwroot.zip", tempDir)
            };

            await Task.WhenAll(downloadTasks);
            _logger.LogInformation("All assets downloaded to temp directory");

            // Step 4: Verify checksums
            var checksumsAsset = release.Assets.FirstOrDefault(a =>
                a.Name.Equals("checksums.txt", StringComparison.OrdinalIgnoreCase));

            if (checksumsAsset != null)
            {
                _logger.LogInformation("Verifying checksums...");
                await VerifyAllChecksumsAsync(checksumsAsset.BrowserDownloadUrl, tempDir);
            }

            // Step 5: Extract embedded updater stub
            var updaterPath = Path.Combine(tempDir, "Updater.exe");
            await ExtractUpdaterStubAsync(updaterPath);
            _logger.LogInformation("Updater stub extracted to: {UpdaterPath}", updaterPath);

            // Step 6: Launch updater and exit
            var currentPid = Process.GetCurrentProcess().Id;
            var appDir = AppDomain.CurrentDomain.BaseDirectory;

            _logger.LogInformation("Launching updater stub - PID: {Pid}, AppDir: {AppDir}", currentPid, appDir);

            var updaterStartInfo = new ProcessStartInfo
            {
                FileName = updaterPath,
                ArgumentList =
                {
                    "--pid", currentPid.ToString(),
                    "--temp", tempDir,
                    "--app-dir", appDir
                },
                UseShellExecute = true
            };

            Process.Start(updaterStartInfo);

            // Step 7: Exit immediately (releases file lock)
            _logger.LogInformation("Exiting to allow updater to replace files");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying force update");
            RestoreBackup();
            throw;
        }
    }

    /// <summary>
    /// Create backup of specified files/directories
    /// </summary>
    private void CreateBackup(string[] paths)
    {
        try
        {
            _logger.LogInformation("Creating backup...");

            if (Directory.Exists(_backupDirectory))
            {
                Directory.Delete(_backupDirectory, true);
            }
            Directory.CreateDirectory(_backupDirectory);

            foreach (var path in paths)
            {
                var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(_appDirectory, path);

                if (File.Exists(fullPath))
                {
                    var destPath = Path.Combine(_backupDirectory, Path.GetFileName(path));
                    File.Copy(fullPath, destPath, true);
                    _logger.LogDebug("Backed up: {Path}", path);
                }
                else if (Directory.Exists(fullPath))
                {
                    var destPath = Path.Combine(_backupDirectory, Path.GetFileName(path));
                    CopyDirectory(fullPath, destPath);
                    _logger.LogDebug("Backed up directory: {Path}", path);
                }
            }

            _logger.LogInformation("Backup created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating backup");
        }
    }

    /// <summary>
    /// Restore from backup
    /// </summary>
    private void RestoreBackup()
    {
        try
        {
            if (!Directory.Exists(_backupDirectory))
            {
                _logger.LogWarning("No backup found to restore");
                return;
            }

            _logger.LogWarning("Restoring from backup...");

            foreach (var file in Directory.GetFiles(_backupDirectory))
            {
                var fileName = Path.GetFileName(file);
                var destPath = Path.Combine(_appDirectory, fileName);

                // Remove existing file/directory
                if (File.Exists(destPath)) File.Delete(destPath);
                if (Directory.Exists(destPath)) Directory.Delete(destPath, true);

                // Restore from backup
                if (File.GetAttributes(file).HasFlag(FileAttributes.Directory))
                {
                    CopyDirectory(file, destPath);
                }
                else
                {
                    File.Copy(file, destPath, true);
                }

                _logger.LogDebug("Restored: {FileName}", fileName);
            }

            _logger.LogInformation("Backup restored successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring backup");
        }
    }

    /// <summary>
    /// Download file from URL
    /// </summary>
    private async Task DownloadFileAsync(string url, string destinationPath)
    {
        var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var fileStream = File.Create(destinationPath);
        await using var contentStream = await response.Content.ReadAsStreamAsync();
        await contentStream.CopyToAsync(fileStream);
    }

    /// <summary>
    /// Verify SHA256 checksum
    /// </summary>
    private async Task VerifyChecksumAsync(string checksumsUrl, string filePath)
    {
        var response = await _httpClient.GetAsync(checksumsUrl);
        response.EnsureSuccessStatusCode();

        var checksumsContent = await response.Content.ReadAsStringAsync();
        var fileName = Path.GetFileName(filePath);

        // Parse checksums.txt (format: HASH  FILENAME)
        var expectedHash = checksumsContent.Split('\n')
            .FirstOrDefault(line => line.Contains(fileName))?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];

        if (string.IsNullOrEmpty(expectedHash))
        {
            _logger.LogWarning("Checksum not found for {FileName}", fileName);
            return;
        }

        // Calculate file hash
        using var sha256 = SHA256.Create();
        await using var fileStream = File.OpenRead(filePath);
        var hashBytes = await sha256.ComputeHashAsync(fileStream);
        var actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

        if (actualHash != expectedHash.ToLowerInvariant())
        {
            throw new InvalidOperationException($"Checksum mismatch for {fileName}: expected {expectedHash}, got {actualHash}");
        }

        _logger.LogInformation("Checksum verified for {FileName}", fileName);
    }

    /// <summary>
    /// Download single asset to temp directory
    /// </summary>
    private async Task DownloadAssetAsync(GitHubRelease release, string assetName, string destDir)
    {
        var asset = release.Assets.FirstOrDefault(a =>
            a.Name.Equals(assetName, StringComparison.OrdinalIgnoreCase));

        if (asset == null)
        {
            _logger.LogWarning("Asset not found in release: {AssetName}", assetName);
            return;
        }

        var destPath = Path.Combine(destDir, assetName);
        _logger.LogInformation("Downloading {AssetName}...", assetName);

        try
        {
            await DownloadFileAsync(asset.BrowserDownloadUrl, destPath);
            _logger.LogInformation("Downloaded {AssetName} to {Path}", assetName, destPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download {AssetName}", assetName);
            throw;
        }
    }

    /// <summary>
    /// Verify all checksums for downloaded files
    /// </summary>
    private async Task VerifyAllChecksumsAsync(string checksumsUrl, string tempDir)
    {
        var response = await _httpClient.GetAsync(checksumsUrl);
        response.EnsureSuccessStatusCode();

        var checksumsContent = await response.Content.ReadAsStringAsync();

        foreach (var line in checksumsContent.Split('\n'))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var expectedHash = parts[0].ToLowerInvariant();
                var fileName = parts[1];
                var filePath = Path.Combine(tempDir, fileName);

                if (File.Exists(filePath))
                {
                    await VerifyFileChecksumAsync(filePath, expectedHash);
                }
            }
        }
    }

    /// <summary>
    /// Verify single file checksum
    /// </summary>
    private async Task VerifyFileChecksumAsync(string filePath, string expectedHash)
    {
        using var sha256 = SHA256.Create();
        await using var fileStream = File.OpenRead(filePath);
        var hashBytes = await sha256.ComputeHashAsync(fileStream);
        var actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

        if (actualHash != expectedHash)
        {
            throw new InvalidOperationException(
                $"Checksum mismatch for {Path.GetFileName(filePath)}: expected {expectedHash}, got {actualHash}");
        }

        _logger.LogInformation("Checksum verified: {FileName}", Path.GetFileName(filePath));
    }

    /// <summary>
    /// Extract embedded updater stub to temp directory
    /// </summary>
    private async Task ExtractUpdaterStubAsync(string destPath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "NewGmHack.GUI.Resources.Updater.exe";

        await using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException("Updater stub not found as embedded resource. Check build process.");
        }

        await using var fileStream = File.Create(destPath);
        await stream.CopyToAsync(fileStream);

        _logger.LogInformation("Extracted updater stub ({Size} bytes)", new FileInfo(destPath).Length);
    }

    /// <summary>
    /// Get current application version
    /// </summary>
    private Version GetCurrentVersion()
    {
        try
        {
            // Try to get Stub DLL version first
            var stubPath = Path.Combine(_appDirectory, "NewGMHack.Stub.dll");
            if (File.Exists(stubPath))
            {
                var stubVersion = AssemblyName.GetAssemblyName(stubPath).Version;
                if (stubVersion != null) return stubVersion;
            }

            // Fallback to GUI version
            return Assembly.GetExecutingAssembly().GetName().Version ?? new Version("1.0.0.0");
        }
        catch
        {
            return new Version("1.0.0.0");
        }
    }

    /// <summary>
    /// Parse version from tag name (e.g., "v1.0.747.10419")
    /// </summary>
    private Version ParseVersion(string? tagName)
    {
        if (string.IsNullOrEmpty(tagName))
        {
            _logger.LogWarning("Tag name is null or empty, returning default version");
            return new Version("1.0.0.0");
        }

        var versionString = tagName.TrimStart('v');
        if (Version.TryParse(versionString, out var version))
        {
            return version;
        }

        _logger.LogWarning("Failed to parse version from tag: {TagName}", tagName);
        return new Version("1.0.0.0");
    }

    /// <summary>
    /// Copy directory recursively
    /// </summary>
    private void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destDir = Path.Combine(targetDir, Path.GetFileName(dir));
            CopyDirectory(dir, destDir);
        }
    }

    /// <summary>
    /// Event raised when frontend is updated (for WebView2 hot-reload)
    /// </summary>
    public event EventHandler? FrontendUpdateRequired;
}

/// <summary>
/// GitHub Release API response
/// </summary>
internal record GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    [JsonPropertyName("assets")]
    public GitHubAsset[] Assets { get; init; } = Array.Empty<GitHubAsset>();
}

/// <summary>
/// GitHub Release Asset
/// </summary>
internal record GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; init; }
}

/// <summary>
/// Component change detection result
/// </summary>
internal class ComponentChangeDetection
{
    public bool FrontendChanged { get; set; }
    public bool GuiChanged { get; set; }
    public bool StubChanged { get; set; }
}
