# Auto-Update System Architecture

## Overview

NewGMHack uses a **custom updater stub pattern** for reliable, atomic updates without external dependencies. The system handles GUI updates, Stub DLL updates, and frontend hot-reloads with automatic rollback on failure.

## Architecture

### Components

```
┌─────────────────────────────────────────────────────────────┐
│                    NewGmHack.GUI                            │
│  ┌──────────────────────────────────────────────────────┐  │
│  │         AutoUpdateService                            │  │
│  │  - Checks GitHub Releases                            │  │
│  │  - Downloads updates to temp                         │  │
│  │  - Verifies SHA256 checksums                         │  │
│  │  - Launches updater stub                             │  │
│  └──────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │         UpdaterStub (Embedded)                       │  │
│  │  - Extracted to temp on update                       │  │
│  │  - Handles file replacement                          │  │
│  │  - Performs rollback on failure                      │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│                    GitHub Releases                          │
│  - NewGMHack.GUI.{version}.zip                             │
│  - NewGMHack.Stub.{version}.zip                            │
│  - frontend.{version}.zip                                  │
│  - checksums.sha256                                        │
└─────────────────────────────────────────────────────────────┘
```

### Update Flow

#### Standard Update (GUI/Stub)

```
┌──────────────────┐
│ 1. Check Update  │ AutoUpdateService queries GitHub Releases
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│ 2. Download      │ Download asset to temp directory
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│ 3. Verify SHA256 │ Validate checksum from checksums.sha256
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│ 4. Extract       │ Extract to temp/update/{version}/
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│ 5. Backup        │ Copy current files to temp/backup/
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│ 6. Launch Stub   │ Extract and launch UpdaterStub.exe
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│ 7. Main App Exit │ Release file locks
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│ 8. Replace Files │ UpdaterStub copies new files
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│ 9. Launch New    │ Start updated version
└──────────────────┘
```

#### Frontend Hot-Reload

```
┌──────────────────┐
│ 1. Detect        │ Update contains only frontend.zip
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│ 2. Download      │ Download to temp
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│ 3. Verify        │ SHA256 validation
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│ 4. Extract       │ Extract to temp/frontend/
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│ 5. Hot-Reload    │ WebView2 reload location
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│ 6. Notify User   │ Show "Frontend updated" message
└──────────────────┘
```

## UpdaterStub Implementation

### Entry Point

```csharp
// UpdaterStub/Program.cs
static async Task<int> Main(string[] args)
{
    // Args: [sourceDir] [targetDir] [backupDir] [mainExe]
    var sourceDir = args[0];
    var targetDir = args[1];
    var backupDir = args[2];
    var mainExe = args[3];

    try
    {
        // Perform replacement
        await ReplaceFiles(sourceDir, targetDir);

        // Launch new version
        Process.Start(mainExe);

        return 0; // Success
    }
    catch
    {
        // Rollback on failure
        await Rollback(backupDir, targetDir);

        return 1; // Failure
    }
}
```

### File Replacement Strategy

```csharp
static async Task ReplaceFiles(string source, string target)
{
    // Ensure target is writable
    WaitForFileRelease(target);

    // Copy files with retry
    await CopyDirectoryRecursive(source, target, maxRetries: 3);

    // Verify critical files
    VerifyInstallation(target);
}
```

### Rollback Mechanism

```csharp
static async Task Rollback(string backup, string target)
{
    if (!Directory.Exists(backup))
    {
        Log.Error("Backup directory not found");
        return;
    }

    // Restore backup
    await CopyDirectoryRecursive(backup, target);

    // Relaunch old version
    Process.Start(Path.Combine(target, "NewGmHack.GUI.exe"));
}
```

## GitHub Releases Structure

### Release Naming

```
v{major}.{minor}.{patch}

Example: v1.0.750.0
```

### Assets

Each release includes:

```
NewGMHack.GUI.{version}.zip
  ├── NewGmHack.GUI.exe
  ├── NewGmHack.GUI.dll
  ├── *.dll (dependencies)
  └── wwwroot/ (optional, for bundled updates)

NewGMHack.Stub.{version}.zip
  └── NewGMHack.Stub.dll

frontend.{version}.zip
  └── dist/
      ├── index.html
      ├── assets/
      │   ├── index-[hash].js
      │   └── index-[hash].css
      └── favicon.ico

checksums.sha256
  SHA256(NewGMHack.GUI.1.0.750.0.zip) = abc123...
  SHA256(NewGMHack.Stub.1.0.750.0.zip) = def456...
  SHA256(frontend.1.0.750.0.zip) = ghi789...
```

### Checksum Validation

```csharp
// AutoUpdateService.cs
async Task VerifyChecksum(string assetPath, string checksumPath)
{
    var expected = await GetExpectedChecksum(checksumPath, assetPath);
    var actual = ComputeSHA256(assetPath);

    if (!expected.Equals(actual, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Checksum verification failed");
    }
}
```

## AutoUpdateService Details

### Configuration

```csharp
// appsettings.json
{
  "AutoUpdate": {
    "Enabled": true,
    "RepoOwner": "MVSharp",
    "RepoName": "NewGMHack",
    "CheckInterval": "01:00:00", // 1 hour
    "Prerelease": false
  }
}
```

### Update Check

```csharp
public async Task<UpdateInfo?> CheckForUpdatesAsync()
{
    var releases = await _githubClient.Repository.GetAllReleases("MVSharp", "NewGMHack");

    var latest = releases
        .Where(r => !r.Prerelease || _config.Prerelease)
        .OrderByDescending(r => r.CreatedAt)
        .FirstOrDefault();

    if (latest == null) return null;

    var currentVersion = GetCurrentVersion();
    var latestVersion = new Version(latest.TagName.TrimStart('v'));

    if (latestVersion <= currentVersion) return null;

    return new UpdateInfo
    {
        Version = latestVersion,
        ReleaseNotes = latest.Body,
        Assets = latest.Assets.Select(a => new AssetInfo
        {
            Name = a.Name,
            Url = a.BrowserDownloadUrl,
            Size = a.Size
        }).ToList()
    };
}
```

### Update Execution

```csharp
public async Task PerformUpdateAsync(UpdateInfo update)
{
    var tempDir = Path.Combine(Path.GetTempPath(), "NewGMHack-Update");

    // Download all assets
    foreach (var asset in update.Assets)
    {
        var assetPath = Path.Combine(tempDir, asset.Name);
        await DownloadAsset(asset.Url, assetPath);
        await VerifyChecksum(assetPath, update.ChecksumsUrl);
    }

    // Determine update type
    if (update.IsFrontendOnly)
    {
        await PerformFrontendUpdate(tempDir);
    }
    else
    {
        await PerformFullUpdate(tempDir);
    }
}
```

## Error Handling

### Download Failures

```csharp
try
{
    await DownloadWithRetry(asset.Url, assetPath, maxRetries: 3);
}
catch (Exception ex)
{
    _logger.Error(ex, "Failed to download {Asset}", asset.Name);
    throw new UpdateException("Download failed", ex);
}
```

### Checksum Mismatches

```csharp
if (!VerifyChecksum(assetPath, expectedChecksum))
{
    _logger.Error("Checksum mismatch for {Asset}", assetName);
    File.Delete(assetPath);
    throw new UpdateException("Checksum verification failed");
}
```

### File Lock Failures

```csharp
// UpdaterStub waits for main process to exit
await WaitForProcessExit(mainProcessId, timeout: TimeSpan.FromSeconds(10));

// Retry file operations
await RetryAsync(() => File.Copy(source, target, true), maxRetries: 5);
```

## Security Considerations

### Checksum Verification

- **Mandatory SHA256 verification** for all downloads
- Checksums file signed/must match GitHub release
- Prevents tampered/malicious updates

### UpdaterStub Isolation

- **Minimal dependencies** - No external libraries required
- **Runs as separate process** - Isolated from main app
- **No network access** - Only local file operations
- **Reads from temp only** - Never downloads from internet

### Release Signing (Future)

```csharp
// Planned: Authenticode signing
public async Task VerifySignature(string assetPath)
{
    var signature = await _certificateService.Verify(assetPath);

    if (!signature.IsValid || signature.Subject != "MVSharp")
    {
        throw new SecurityException("Invalid signature");
    }
}
```

## User Experience Enhancements (v2.0)

### Spectre.Console Integration

The updater uses [Spectre.Console](https://spectreconsole.net/) for rich terminal output with progress bars, colors, and styled messages.

#### Progress Bars

```csharp
// Download progress with live updates
await AnsiConsole.Progress()
    .StartAsync(async ctx =>
    {
        var task = ctx.AddTask("[green]Downloading GUI[/]");
        var downloadSize = update.Assets.First(a => a.Name.Contains("GUI")).Size;

        while (!ctx.IsFinished)
        {
            var progress = await GetDownloadProgress();
            task.Value(progress.PercentComplete);
            task.MaxValue = 100;
        }
    });

// Output example:
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ [---------------] 45%
```

#### Styled Messages

```csharp
// Success messages
AnsiConsole.MarkupLine("[green bold]✔[/] Update downloaded successfully");
AnsiConsole.MarkupLine("[dim]→[/] Verified checksums for 3 assets");

// Error messages
AnsiConsole.MarkupLine("[red bold]✗[/] Checksum verification failed");
AnsiConsole.MarkupLine("[yellow]⚠[/] Update will be rolled back[/]");

// Info messages
AnsiConsole.MarkupLine("[cyan]ℹ[/] Checking for updates...");
AnsiConsole.MarkupLine("[blue]→[/] Current version: [bold]{0}[/]", currentVersion);
```

#### Table Display

```csharp
// Display update info
var table = new Table();
table.AddColumn("[yellow]Asset[/]");
table.AddColumn("[yellow]Size[/]");
table.AddColumn("[yellow]Status[/]");

table.AddRow(
    "NewGMHack.GUI.1.0.750.0.zip",
    "15.2 MB",
    "[green]Ready[/]"
);
table.AddRow(
    "NewGMHack.Stub.1.0.750.0.zip",
    "2.8 MB",
    "[green]Ready[/]"
);

AnsiConsole.Write(table);
```

### ZLogger Structured Logging

The updater uses ZLogger for high-performance structured logging with file rolling.

#### Log Format

```csharp
// Structured logging with ZLogger
_logger.UpdateInfo(
    "UpdateAvailable",
    currentVersion,
    latestVersion,
    downloadSize
);

// Output:
// [2025-02-02 14:30:15] [INFO] UpdateAvailable CurrentVersion=1.0.747.10419 LatestVersion=1.0.750.0 DownloadSize=18200432
```

#### Log Paths

```
logs/updater-{date}-{sequence}.log
Example: logs/updater-20250202-1.log
```

#### Rolling Configuration

- **Daily rotation** - New log file each day
- **10MB per file** - Automatic rollover when size exceeded
- **30-day retention** - Old logs automatically cleaned up
- **Structured format** - Key-value pairs for easy parsing

#### Log Examples

```csharp
// Download start
_logger.DownloadStart(assetName, assetSize, downloadUrl);
// [2025-02-02 14:30:16] [INFO] DownloadStart Asset=NewGMHack.GUI.zip Size=18200432 Url=https://github.com/...

// Download progress (every 10%)
_logger.DownloadProgress(assetName, 30, totalBytes, speed);
// [2025-02-02 14:30:20] [INFO] DownloadProgress Asset=NewGMHack.GUI.zip Percent=30 Bytes=5460129 Speed=2.5MB/s

// Download complete
_logger.DownloadComplete(assetName, elapsed, avgSpeed);
// [2025-02-02 14:30:25] [INFO] DownloadComplete Asset=NewGMHack.GUI.zip Elapsed=00:00:09 Speed=2.0MB/s

// Checksum verification
_logger.ChecksumVerify(assetName, expectedChecksum, actualChecksum, isValid);
// [2025-02-02 14:30:26] [INFO] ChecksumVerify Asset=NewGMHack.GUI.zip Expected=abc123... Actual=abc123... Valid=True
```

### Changelog Display

The updater displays release notes during and after the update process.

#### During Update (Brief)

```csharp
// Show first 5 lines of changelog while downloading
var briefNotes = update.ReleaseNotes
    .Split('\n')
    .Take(5)
    .ToArray();

AnsiConsole.MarkupLine("[bold cyan]What's New in v{0}:[/]", update.Version);
foreach (var line in briefNotes)
{
    AnsiConsole.MarkupLine("  {0}", line);
}
```

**Example Output:**
```
What's New in v1.0.750.0:
  - Added Spectre.Console for enhanced console output
  - Improved download progress with 10% increments
  - Added structured logging with ZLogger
  - Enhanced changelog display during updates
  - Fixed rollback timeout issues
```

#### After Update (Full)

```csharp
// Display full changelog in web UI after restart
public class ChangelogViewModel
{
    public string Version { get; set; }
    public string[] ReleaseNotes { get; set; }
    public DateTime ReleaseDate { get; set; }
}

// Auto-show on first launch after update
if (_wasJustUpdated)
{
    var changelog = await GetChangelog(latestVersion);
    _windowService.ShowDialog<ChangelogWindow>(changelog);
}
```

### Download Progress Tracking

Downloads report progress at 10% increments to balance feedback and performance.

```csharp
public async Task DownloadWithProgress(string url, string outputPath)
{
    var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
    var totalBytes = response.Content.Headers.ContentLength ?? 0;
    var totalBytesRead = 0L;
    var lastReportedProgress = 0;

    using var stream = await response.Content.ReadAsStreamAsync();
    using var fileStream = File.Create(outputPath);

    var buffer = new byte[81920];
    int bytesRead;

    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
    {
        await fileStream.WriteAsync(buffer, 0, bytesRead);
        totalBytesRead += bytesRead;

        // Report progress every 10%
        var progress = (int)((totalBytesRead * 100) / totalBytes);
        if (progress >= lastReportedProgress + 10)
        {
            _logger.DownloadProgress(Path.GetFileName(outputPath), progress, totalBytesRead);
            lastReportedProgress = progress;
        }
    }
}
```

**Example Log Output:**
```
[2025-02-02 14:30:16] [INFO] DownloadProgress Asset=NewGMHack.GUI.zip Percent=10 Bytes=1820043
[2025-02-02 14:30:18] [INFO] DownloadProgress Asset=NewGMHack.GUI.zip Percent=20 Bytes=3640086
[2025-02-02 14:30:20] [INFO] DownloadProgress Asset=NewGMHack.GUI.zip Percent=30 Bytes=5460129
...
[2025-02-02 14:30:44] [INFO] DownloadProgress Asset=NewGMHack.GUI.zip Percent=100 Bytes=18200432
```

### Combined Console Output Example

```
[14:30:15 INF] CheckForUpdates Current=1.0.747.10419
[cyan]ℹ[/] Checking for updates...
[cyan]ℹ[/] Found new version: [bold green]v1.0.750.0[/]

┌────────────────────────────────────┬──────────┬──────────┐
│ Asset                              │ Size     │ Status   │
├────────────────────────────────────┼──────────┼──────────┤
│ NewGMHack.GUI.1.0.750.0.zip       │ 15.2 MB  │ Ready    │
│ NewGMHack.Stub.1.0.750.0.zip      │ 2.8 MB   │ Ready    │
│ frontend.1.0.750.0.zip            │ 1.1 MB   │ Ready    │
└────────────────────────────────────┴──────────┴──────────┘

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ [---------------] 30%
[green]✔[/] Downloaded NewGMHack.GUI.zip (15.2 MB in 9s)
[green]✔[/] Verified checksum: abc123def456...
[green]✔[/] Extracted to temp/update/1.0.750.0/

[yellow]⚠[/] Restart required to complete update
[dim]Press any key to restart...[/]
```

## Rollback Scenarios

### Automatic Rollback

UpdaterStub performs automatic rollback on:

1. **File copy failures** - Permissions, disk space, locks
2. **Checksum failures** - Post-replacement verification
3. **DLL load failures** - Main app fails to start
4. **Timeout** - Update takes longer than expected

### Manual Rollback

User can manually rollback by:

1. Restore backup from `temp/backup/{timestamp}/`
2. Run `NewGmHack.GUI.exe` from backup directory
3. Copy files to main directory

## Performance Considerations

### Download Optimization

- **Parallel downloads** - GUI and Stub downloaded concurrently
- **Resume support** - Check file size before re-downloading
- **Compression** - ZIP format reduces bandwidth

### Frontend Hot-Reload

- **No restart** - WebView2 reloads in ~2 seconds
- **Cached assets** - Browser caches unchanged files
- **Instant update** - No application downtime

### Update Frequency

- **Check on startup** - Quick API call to GitHub
- **Background checks** - Every hour (configurable)
- **Manual check** - "Check for Updates" button in settings

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public async Task VerifyChecksum_ValidFile_ReturnsTrue()
{
    var service = new AutoUpdateService();
    var result = await service.VerifyChecksum("test.zip", "abc123");
    Assert.True(result);
}

[Fact]
public void ParseVersion_ValidTag_ReturnsVersion()
{
    var version = AutoUpdateService.ParseVersion("v1.0.750.0");
    Assert.Equal(new Version(1, 0, 750, 0), version);
}
```

### Integration Tests

```csharp
[Fact]
public async Task FullUpdate_EndToEnd_Success()
{
    // Setup: Create test GitHub release
    var release = await CreateTestRelease();

    // Act: Run update
    var service = new AutoUpdateService();
    await service.PerformUpdateAsync(release);

    // Assert: Verify files updated
    Assert.True(File.Exists("NewGmHack.GUI.exe"));
    Assert.Equal(release.Version, service.GetCurrentVersion());
}
```

### Manual Testing

1. **Create test release** with pre-release tag
2. **Enable prerelease updates** in config
3. **Run update** and verify all scenarios
4. **Test rollback** by corrupting update files

## Future Enhancements

### Delta Updates (Planned)

```csharp
// Download only changed files
public async Task ApplyDeltaUpdate(DeltaInfo delta)
{
    foreach (var file in delta.ChangedFiles)
    {
        await DownloadAndApply(file);
    }

    // Apply binary patch for large files
    await ApplyBinaryPatch(delta.Patches);
}
```

### Background Updates (Planned)

```csharp
// Download in background, prompt to install
public async Task BackgroundUpdateCheck()
{
    var update = await CheckForUpdatesAsync();

    if (update != null)
    {
        await DownloadInBackground(update);
        ShowUpdateNotification();
    }
}
```

### Automatic Changelog Display

```csharp
// Show release notes on update
public void ShowChangelog(ReleaseInfo release)
{
    var window = new ChangelogWindow
    {
        Version = release.Version,
        Notes = release.Body,
        HighlightChanges = true
    };

    window.ShowDialog();
}
```

## Troubleshooting

### Common Issues

**Update fails with "file in use"**
- Ensure game is closed before updating
- Check for background processes in Task Manager
- Use UpdaterStub logs to identify locked file

**Checksum verification fails**
- Verify internet connection - download may be corrupted
- Check GitHub release for correct checksums
- Re-download asset manually and compare checksums

**Frontend update not applied**
- Clear browser cache in WebView2
- Check wwwroot folder permissions
- Verify frontend.zip contents

**UpdaterStub doesn't launch**
- Check antivirus isn't blocking temp files
- Verify UpdaterStub.exe extracted correctly
- Review temp directory logs for errors

### Debug Logging

```csharp
// Enable verbose logging
services.AddLogging(builder =>
{
    builder.AddFilter("NewGmHack.GUI.Services.AutoUpdateService", LogLevel.Trace);
    builder.AddFilter("UpdaterStub", LogLevel.Trace);
});
```

### Manual Update Fallback

If auto-update fails completely:

1. Download assets from GitHub Releases manually
2. Close NewGmHack.GUI
3. Extract GUI zip to application directory
4. Extract Stub zip to game directory (if applicable)
5. Restart application

## References

- [GitHub Releases API](https://docs.github.com/en/rest/releases)
- [SHA256 Checksums](https://en.wikipedia.org/wiki/SHA-2)
- [Updater Stub Pattern](https://martinfowler.com/articles/patterns-of-distributed-systems/#UpdaterStub)
