# Custom Updater Stub Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace AutoUpdater.NET with a custom updater stub to solve EXE file locking and implement reliable multi-component auto-updates.

**Architecture:** Main app downloads update to temp directory → Launches embedded updater stub → Main app exits (releasing file lock) → Updater stub replaces files → Updater launches new version.

**Tech Stack:** .NET 10.0, HttpClient, System.IO.Compression, Process management, Embedded resources

---

## Background

### Current Problems
1. **AutoUpdater.NET hangs** - Wrong XML URL (`/tag/` instead of `/download/`)
2. **Async without waiting** - `Synchronous = false` runs in background with no synchronization
3. **EXE file locking** - Cannot replace running executable
4. **Poor error visibility** - No detailed logging during update process

### Why This Approach Works
- **Separate process** handles file replacement (no lock conflict)
- **Main app exits completely** before replacement (file lock released)
- **Process ID synchronization** ensures safe replacement timing
- **Full control** over download, verification, and rollback
- **Industry standard** - Used by Chrome, VS Code, Discord

---

## Implementation Plan

### Task 1: Create Updater Stub Project

**Files:**
- Create: `Updater/Program.cs`
- Create: `Updater/Updater.csproj`
- Create: `Updater/UpdateEngine.cs`

**Step 1: Create Updater.csproj**

Run: `mkdir Updater`

Create file: `Updater/Updater.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x86</RuntimeIdentifier>
    <PublishTrimmed>true</PublishTrimmed>
    <PublishReadyToRun>true</PublishReadyToRun>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
  </ItemGroup>

</Project>
```

**Step 2: Create Program.cs entry point**

Create file: `Updater/Program.cs`

```csharp
using System.CommandLine;
using System.Diagnostics;

namespace Updater;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("NewGMHack Updater Stub v1.0");
            Console.WriteLine("Usage: Updater.exe --pid <process-id> --temp <temp-dir> [--app-dir <app-dir>]");
            return 1;
        }

        // Parse arguments
        var pidOption = new Option<int>("--pid", "Process ID of application to wait for");
        var tempOption = new Option<string>("--temp", "Temporary directory containing update files");
        var appDirOption = new Option<string>("--app-dir", () => Environment.CurrentDirectory, "Application directory");

        var rootCommand = new RootCommand
        {
            pidOption,
            tempOption,
            appDirOption
        };

        rootCommand.SetHandler(async (pid, tempDir, appDir) =>
        {
            var engine = new UpdateEngine();
            return await engine.ExecuteUpdateAsync(pid, tempDir, appDir);
        }, pidOption, tempOption, appDirOption);

        return await rootCommand.InvokeAsync(args);
    }
}
```

**Step 3: Create UpdateEngine.cs with core logic**

Create file: `Updater/UpdateEngine.cs`

```csharp
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Updater;

public class UpdateEngine
{
    private const string GuiExeName = "NewGMHack.GUI.exe";
    private const string StubDllName = "NewGMHack.Stub.dll";
    private const string WwwrootZipName = "wwwroot.zip";

    public async Task<int> ExecuteUpdateAsync(int targetPid, string tempDir, string appDir)
    {
        try
        {
            Console.WriteLine($"[Updater] Starting update process");
            Console.WriteLine($"[Updater] Target PID: {targetPid}");
            Console.WriteLine($"[Updater] Temp directory: {tempDir}");
            Console.WriteLine($"[Updater] App directory: {appDir}");

            // Step 1: Wait for main app to exit
            Console.WriteLine($"[Updater] Waiting for process {targetPid} to exit...");
            await WaitForProcessExitAsync(targetPid);
            Console.WriteLine("[Updater] Process exited - proceeding with file replacement");

            // Step 2: Replace GUI executable
            var newGuiPath = Path.Combine(tempDir, GuiExeName);
            var oldGuiPath = Path.Combine(appDir, GuiExeName);

            if (File.Exists(newGuiPath))
            {
                Console.WriteLine("[Updater] Replacing NewGMHack.GUI.exe...");
                ReplaceFile(newGuiPath, oldGuiPath);
            }
            else
            {
                Console.WriteLine($"[Updater] Warning: {GuiExeName} not found in temp directory");
            }

            // Step 3: Replace Stub DLL
            var newStubPath = Path.Combine(tempDir, StubDllName);
            var oldStubPath = Path.Combine(appDir, StubDllName);

            if (File.Exists(newStubPath))
            {
                Console.WriteLine("[Updater] Replacing NewGMHack.Stub.dll...");
                ReplaceFile(newStubPath, oldStubPath);
            }
            else
            {
                Console.WriteLine($"[Updater] Warning: {StubDllName} not found in temp directory");
            }

            // Step 4: Extract wwwroot.zip
            var wwwrootZipPath = Path.Combine(tempDir, WwwrootZipName);
            if (File.Exists(wwwrootZipPath))
            {
                Console.WriteLine("[Updater] Extracting wwwroot.zip...");
                var wwwrootDest = Path.Combine(appDir, "wwwroot");
                ExtractWwwroot(wwwrootZipPath, wwwrootDest);
            }
            else
            {
                Console.WriteLine($"[Updater] Info: {WwwrootZipName} not found (frontend-only update?)");
            }

            // Step 5: Verify new version
            if (File.Exists(oldGuiPath))
            {
                var versionInfo = AssemblyName.GetAssemblyName(oldGuiPath).Version?.ToString() ?? "unknown";
                Console.WriteLine($"[Updater] New version: {versionInfo}");
            }

            // Step 6: Launch new version
            Console.WriteLine($"[Updater] Launching new version: {oldGuiPath}");
            var startInfo = new ProcessStartInfo
            {
                FileName = oldGuiPath,
                Arguments = "--updated",
                UseShellExecute = true,
                WorkingDirectory = appDir
            };

            Process.Start(startInfo);

            // Step 7: Cleanup temp directory
            Console.WriteLine("[Updater] Cleaning up temp files...");
            try
            {
                Directory.Delete(tempDir, true);
                Console.WriteLine("[Updater] Temp directory cleaned");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Updater] Warning: Could not delete temp directory: {ex.Message}");
            }

            Console.WriteLine("[Updater] Update completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Updater] ERROR: {ex.Message}");
            Console.WriteLine($"[Updater] Stack: {ex.StackTrace}");
            return 1;
        }
    }

    private async Task WaitForProcessExitAsync(int pid, TimeSpan? maxWait = null)
    {
        var timeout = maxWait ?? TimeSpan.FromSeconds(30);
        var start = DateTime.UtcNow;

        Console.WriteLine($"[Updater] Starting process wait (timeout: {timeout.TotalSeconds}s)");

        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                if (process.HasExited)
                {
                    Console.WriteLine("[Updater] Process has exited");
                    // Give it a moment to fully release file handles
                    await Task.Delay(500);
                    return;
                }

                // Still running, wait
                await Task.Delay(250);
            }
            catch (ArgumentException)
            {
                // Process doesn't exist - it has exited
                Console.WriteLine("[Updater] Process no longer exists");
                await Task.Delay(500);
                return;
            }
        }

        throw new TimeoutException($"Process {pid} did not exit within {timeout.TotalSeconds}s");
    }

    private void ReplaceFile(string source, string destination)
    {
        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"Source file not found: {source}");
        }

        // Delete destination if it exists
        if (File.Exists(destination))
        {
            Console.WriteLine($"[Updater] Deleting existing: {destination}");
            File.Delete(destination);
        }

        // Copy new file
        Console.WriteLine($"[Updater] Copying {source} -> {destination}");
        File.Copy(source, destination, true);

        // Verify
        if (!File.Exists(destination))
        {
            throw new IOException($"Failed to copy file to {destination}");
        }

        var fileSize = new FileInfo(destination).Length;
        Console.WriteLine($"[Updater] File replaced successfully ({fileSize} bytes)");
    }

    private void ExtractWwwroot(string zipPath, string destinationPath)
    {
        // Remove old wwwroot
        if (Directory.Exists(destinationPath))
        {
            Console.WriteLine($"[Updater] Removing existing wwwroot...");
            Directory.Delete(destinationPath, true);
        }

        // Extract new wwwroot
        Console.WriteLine($"[Updater] Extracting {zipPath} -> {destinationPath}");
        ZipFile.ExtractToDirectory(zipPath, destinationPath);

        // Verify
        var fileCount = Directory.GetFiles(destinationPath, "*", SearchOption.AllDirectories).Length;
        Console.WriteLine($"[Updater] Extracted {fileCount} files to wwwroot");
    }
}
```

**Step 4: Verify Updater builds**

Run: `dotnet build Updater/Updater.csproj`

Expected output: Build succeeds with no errors

**Step 5: Test Updater argument parsing**

Run: `dotnet run --project Updater/Updater.csproj -- --help`

Expected output: Shows usage information with --pid, --temp, --app-dir options

**Step 6: Commit Updater stub project**

Run:
```bash
git add Updater/
git commit -m "feat: add updater stub project structure"
```

---

### Task 2: Modify AutoUpdateService to Use Custom Updater

**Files:**
- Modify: `NewGmHack.GUI/Services/AutoUpdateService.cs:227-263`
- Modify: `NewGmHack.GUI/NewGmHack.GUI.csproj` (add embedded resource)
- Remove: AutoUpdater.NET dependencies

**Step 1: Remove AutoUpdater.NET package reference**

Edit file: `NewGmHack.GUI/NewGmHack.GUI.csproj:16`

Remove line:
```xml
<PackageReference Include="Autoupdater.NET.Official" Version="1.9.2" />
```

**Step 2: Remove AutoUpdater.NET using statement**

Edit file: `NewGmHack.GUI/Services/AutoUpdateService.cs:9`

Remove line:
```csharp
using AutoUpdaterDotNET;
```

**Step 3: Replace ApplyForceUpdateAsync method**

Edit file: `NewGmHack.GUI/Services/AutoUpdateService.cs:227-263`

Replace entire method with:

```csharp
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
            Arguments = $"--pid {currentPid} --temp \"{tempDir}\" --app-dir \"{appDir}\"",
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
```

**Step 4: Add helper methods to AutoUpdateService**

Add after line 407 (after VerifyChecksumAsync):

```csharp
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
```

**Step 5: Update method signature (ApplyForceUpdateAsync is now async)**

Edit file: `NewGmHack.GUI/Services/AutoUpdateService.cs:92`

Change from:
```csharp
ApplyForceUpdateAsync(releaseInfo);
```

To:
```csharp
await ApplyForceUpdateAsync(releaseInfo);
```

**Step 6: Build and verify compilation**

Run: `dotnet build NewGmHack.GUI/NewGmHack.GUI.csproj -c Release -p:Platform=x86`

Expected: Build succeeds with errors about missing Updater.exe resource (we'll fix next)

**Step 7: Commit AutoUpdateService changes**

Run:
```bash
git add NewGmHack.GUI/
git commit -m "feat: replace AutoUpdater.NET with custom updater stub logic"
```

---

### Task 3: Embed Updater Stub in GUI Project

**Files:**
- Modify: `NewGmHack.GUI/NewGmHack.GUI.csproj`
- Create: `build-release.ps1` modifications (if needed)

**Step 1: Add embedded resource to GUI project**

Edit file: `NewGmHack.GUI/NewGmHack.GUI.csproj`

Add before closing `</Project>` tag (line 45):

```xml
<ItemGroup>
  <EmbeddedResource Include="..\Updater\bin\Release\net10.0\win-x86\publish\Updater.exe"
                    Link="Resources\Updater.exe"
                    Condition="Exists('..\Updater\bin\Release\net10.0\win-x86\publish\Updater.exe')" />
</ItemGroup>
```

**Step 2: Build Updater for publishing**

Run:
```powershell
dotnet publish Updater/Updater.csproj -c Release -r win-x86 --self-contained -o Updater/bin/Release/net10.0/win-x86/publish
```

Expected: Creates `Updater.exe` in publish directory

**Step 3: Verify embedded resource**

Run:
```powershell
dotnet build NewGmHack.GUI/NewGmHack.GUI.csproj -c Release -p:Platform=x86
```

Expected: Build succeeds, Updater.exe is embedded as resource

**Step 4: Verify resource is accessible**

Create temporary test file: `NewGmHack.GUI/Services/ResourceTest.cs`

```csharp
using System.Reflection;
using System.Text;

var assembly = Assembly.GetExecutingAssembly();
var resources = assembly.GetManifestResourceNames();
Console.WriteLine("Embedded resources:");
foreach (var resource in resources)
{
    Console.WriteLine($"  - {resource}");
}
```

Run: `dotnet run --project NewGmHack.GUI/NewGmHack.GUI.csproj -c Release`

Expected output includes: `NewGmHack.GUI.Resources.Updater.exe`

Delete test file after verification.

**Step 5: Commit embedded resource configuration**

Run:
```bash
git add NewGmHack.GUI/NewGmHack.GUI.csproj
git commit -m "feat: embed updater stub as resource in GUI"
```

---

### Task 4: Update Release Workflow to Build Updater

**Files:**
- Modify: `.github/workflows/release.yml`

**Step 1: Add Updater build step**

Edit file: `.github/workflows/release.yml:90-93`

Add after "Build Stub" step:

```yaml
      - name: Build Updater Stub
        run: |
          dotnet publish Updater/Updater.csproj -c Release -r win-x86 --self-contained -o temp_updater

      - name: Verify Updater Stub
        run: |
          if (!(Test-Path "temp_updater/Updater.exe")) {
            throw "Updater.exe not found"
          }
          Write-Host "Updater stub built successfully"
```

**Step 2: Update GUI build to depend on Updater**

Edit file: `.github/workflows/release.yml:85-88`

Change step name to "Build GUI (with embedded updater)" and add dependency:

```yaml
      - name: Build GUI (with embedded Updater)
        run: |
          dotnet publish Updater/Updater.csproj -c Release -r win-x86 --self-contained -o temp_updater
          dotnet build "NewGmHack.GUI/NewGmHack.GUI.csproj" -c Release -p:Platform=x86
```

**Step 3: Remove update.xml generation (no longer needed)**

Edit file: `.github/workflows/release.yml:146-164`

Remove entire "Generate update.xml for AutoUpdater.NET" step

**Step 4: Remove update.xml from release assets**

Edit file: `.github/workflows/release.yml:212-217`

Remove line: `release/update.xml`

**Step 5: Verify workflow syntax**

Run: `git show HEAD:.github/workflows/release.yml` (or use GitHub Actions lint tool)

Expected: No syntax errors

**Step 6: Commit workflow changes**

Run:
```bash
git add .github/workflows/release.yml
git commit -m "feat: build updater stub in release workflow, remove update.xml"
```

---

### Task 5: Add Rollback Support to Updater

**Files:**
- Modify: `Updater/UpdateEngine.cs`
- Modify: `NewGmHack.GUI/Services/AutoUpdateService.cs`

**Step 1: Add rollback detection to UpdateEngine**

Edit file: `Updater/UpdateEngine.cs`

Add method after `ExtractWwwroot`:

```csharp
private void RollbackUpdate(string appDir, string reason)
{
    Console.WriteLine($"[Updater] ROLLBACK INITIATED: {reason}");
    var backupDir = Path.Combine(appDir, ".backup");

    if (!Directory.Exists(backupDir))
    {
        Console.WriteLine("[Updater] No backup directory found - cannot rollback");
        return;
    }

    try
    {
        // Restore GUI exe
        var backupGui = Path.Combine(backupDir, GuiExeName);
        var appGui = Path.Combine(appDir, GuiExeName);
        if (File.Exists(backupGui) && File.Exists(appGui))
        {
            Console.WriteLine("[Updater] Restoring NewGMHack.GUI.exe from backup...");
            File.Delete(appGui);
            File.Copy(backupGui, appGui, true);
        }

        // Restore Stub DLL
        var backupStub = Path.Combine(backupDir, StubDllName);
        var appStub = Path.Combine(appDir, StubDllName);
        if (File.Exists(backupStub) && File.Exists(appStub))
        {
            Console.WriteLine("[Updater] Restoring NewGMHack.Stub.dll from backup...");
            File.Delete(appStub);
            File.Copy(backupStub, appStub, true);
        }

        // Restore wwwroot
        var backupWwwroot = Path.Combine(backupDir, "wwwroot");
        var appWwwroot = Path.Combine(appDir, "wwwroot");
        if (Directory.Exists(backupWwwroot) && Directory.Exists(appWwwroot))
        {
            Console.WriteLine("[Updater] Restoring wwwroot from backup...");
            Directory.Delete(appWwwroot, true);
            CopyDirectory(backupWwwroot, appWwwroot);
        }

        Console.WriteLine("[Updater] Rollback completed");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Updater] ROLLBACK FAILED: {ex.Message}");
    }

    // Launch old version
    var oldGuiPath = Path.Combine(appDir, GuiExeName);
    if (File.Exists(oldGuiPath))
    {
        Console.WriteLine("[Updater] Launching restored version...");
        Process.Start(new ProcessStartInfo
        {
            FileName = oldGuiPath,
            UseShellExecute = true,
            WorkingDirectory = appDir
        });
    }
}

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
```

**Step 2: Add error handling with rollback**

Edit file: `Updater/UpdateEngine.cs` in `ExecuteUpdateAsync` method

Wrap the replacement logic in try-catch:

```csharp
try
{
    // ... existing replacement code ...
}
catch (Exception ex)
{
    Console.WriteLine($"[Updater] ERROR during update: {ex.Message}");
    RollbackUpdate(appDir, $"Update failed: {ex.Message}");
    return 1;
}
```

**Step 3: Commit rollback support**

Run:
```bash
git add Updater/
git commit -m "feat: add rollback support to updater stub"
```

---

### Task 6: Integration Testing

**Files:**
- Create: `tests/UpdateTests.cs` (optional)
- Manual testing procedures

**Step 1: Test Updater in isolation**

Run:
```powershell
# Build updater
dotnet publish Updater/Updater.csproj -c Release -r win-x86 --self-contained -o test_temp

# Test help
.\test_temp\Updater.exe --help

Expected: Shows usage information
```

**Step 2: Test with mock update files**

Create test directory structure:
```powershell
mkdir test_update
mkdir test_update\app
mkdir test_update\temp

# Create dummy files
echo "test" > test_update\temp\NewGMHack.GUI.exe
echo "test" > test_update\temp\NewGMHack.Stub.dll

# Run updater (with non-existent PID - should timeout)
.\test_temp\Updater.exe --pid 99999 --temp test_update\temp --app-dir test_update\app
```

Expected: Timeout error waiting for process

**Step 3: End-to-end test with actual application**

Create a test release:
```powershell
# Build entire solution
.\build-release.ps1

# Manually create GitHub release with test tag
# Or test auto-update by:
# 1. Running current version
# 2. Creating new release
# 3. Checking if update downloads and applies
```

**Step 4: Verify checksum verification**

Create test checksums.txt with wrong hash:
```
0000000000000000000000000000000000000000000000000000000000000000  NewGMHack.GUI.exe
```

Expected: Updater fails with checksum mismatch error

**Step 5: Verify rollback on failure**

Simulate failure by:
- Corrupting downloaded file
- Deleting backup after update starts
- Using invalid wwwroot.zip

Expected: Updater restores from backup and launches old version

**Step 6: Commit any test fixes or documentation**

Run:
```bash
git add .
git commit -m "test: add update testing procedures and fixes"
```

---

### Task 7: Documentation

**Files:**
- Create: `docs/update-architecture.md`
- Modify: `CLAUDE.md` (update auto-update section)

**Step 1: Create update architecture documentation**

Create file: `docs/update-architecture.md`

```markdown
# Auto-Update Architecture

## Overview

NewGMHack uses a custom updater stub pattern to handle application updates without file locking issues.

## Components

### Main Application (NewGMHack.GUI.exe)
- Checks GitHub Releases API for updates on startup
- Downloads update files to temp directory
- Verifies SHA256 checksums
- Extracts embedded Updater.exe
- Launches updater with current process ID
- Exits immediately to release file locks

### Updater Stub (Updater.exe)
- Waits for main app to exit (polls process ID)
- Replaces NewGMHack.GUI.exe
- Replaces NewGMHack.Stub.dll
- Extracts wwwroot.zip to wwwroot/
- Launches new version
- Cleans up temp files
- Implements rollback on error

## Update Flow

```
┌─────────────────────────────────────────────────────────────┐
│  1. Main App: Check for updates (GitHub API)                 │
├─────────────────────────────────────────────────────────────┤
│  - Fetch latest release tag                                 │
│  - Compare with current version                            │
│  - Detect which components changed                          │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  2. Main App: Download update assets                        │
├─────────────────────────────────────────────────────────────┤
│  - Create temp directory                                    │
│  - Download NewGMHack.GUI.exe                               │
│  - Download NewGMHack.Stub.dll                              │
│  - Download wwwroot.zip                                      │
│  - Verify SHA256 checksums                                  │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  3. Main App: Prepare for update                            │
├─────────────────────────────────────────────────────────────┤
│  - Create backup of current files                          │
│  - Extract embedded Updater.exe                            │
│  - Launch: Updater.exe --pid <self> --temp <dir>           │
│  - Exit (releases file lock)                                │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  4. Updater: Wait for main app to exit                     │
├─────────────────────────────────────────────────────────────┤
│  - Poll process ID every 250ms                             │
│  - Timeout after 30 seconds                                 │
│  - Once exited, wait 500ms for file handles to release     │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  5. Updater: Replace files                                  │
├─────────────────────────────────────────────────────────────┤
│  - Delete old NewGMHack.GUI.exe                            │
│  - Copy new NewGMHack.GUI.exe                              │
│  - Delete old NewGMHack.Stub.dll                           │
│  - Copy new NewGMHack.Stub.dll                             │
│  - Delete old wwwroot/                                      │
│  - Extract wwwroot.zip to wwwroot/                          │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  6. Updater: Launch new version                            │
├─────────────────────────────────────────────────────────────┤
│  - Start NewGMHack.GUI.exe with --updated flag             │
│  - Clean up temp directory                                  │
│  - Exit                                                     │
└─────────────────────────────────────────────────────────────┘
```

## Frontend Hot-Reload

If only frontend changed (wwwroot.zip):
- Main app downloads and extracts wwwroot.zip
- No restart required
- WebView2 reload event triggered
- User sees updated UI immediately

## Rollback

If update fails:
- Updater restores files from `.backup` directory
- Launches previous version
- Logs error details
- Preserves error logs for troubleshooting

## Security

- All downloads verified with SHA256 checksums
- Checksums file signed by GitHub Releases
- Updater runs in separate process (isolated)
- Backup always created before replacement

## Building

The updater is embedded in the main GUI as a resource:

```bash
# Build updater stub
dotnet publish Updater/Updater.csproj -c Release -r win-x86 --self-contained

# Build GUI (embeds updater)
dotnet build NewGmHack.GUI/NewGmHack.GUI.csproj -c Release -p:Platform=x86
```

## Release Process

GitHub Actions workflow:
1. Builds Frontend → wwwroot.zip
2. Builds GUI → NewGMHack.GUI.exe
3. Builds Stub → NewGMHack.Stub.dll
4. Builds Updater → embedded in GUI
5. Generates SHA256 checksums
6. Creates GitHub Release with all assets
```

**Step 2: Update CLAUDE.md with new update info**

Edit file: `CLAUDE.md`

Replace "Planned Features" section (around line referencing AUTO_UPDATE_PLAN.md) with:

```markdown
### Auto-Update System

The application uses a custom updater stub pattern for reliable updates:

- **Custom Updater Stub** - Separate process handles file replacement
- **Checksum Verification** - SHA256 verification for all downloads
- **Rollback Support** - Automatic restoration on failure
- **Frontend Hot-Reload** - No restart required for frontend-only updates

See `docs/update-architecture.md` for detailed architecture.

Update flow:
1. Main app downloads update to temp directory
2. Main app launches embedded updater stub
3. Main app exits (releasing file lock)
4. Updater replaces files
5. Updater launches new version

**Key Files:**
- `Updater/` - Updater stub project
- `NewGmHack.GUI/Services/AutoUpdateService.cs` - Update orchestration
- `docs/update-architecture.md` - Architecture documentation
```

**Step 3: Remove obsolete AUTO_UPDATE_PLAN.md reference**

If `AUTO_UPDATE_PLAN.md` exists, it's now superseded by `docs/update-architecture.md`

Optional: Delete old plan file or add deprecation notice

**Step 4: Commit documentation**

Run:
```bash
git add docs/
git add CLAUDE.md
git commit -m "docs: add update architecture documentation"
```

---

### Task 8: Final Cleanup and Testing

**Files:**
- Remove: AutoUpdater.NET references from anywhere else
- Test: Full update cycle

**Step 1: Search for remaining AutoUpdater references**

Run:
```bash
grep -r "AutoUpdater" --include="*.cs" .
grep -r "AutoUpdater" --include="*.csproj" .
```

Expected: No results (all references removed)

**Step 2: Verify solution builds**

Run:
```powershell
dotnet build NewGMHack.sln -c Release -p:Platform=x86
```

Expected: Entire solution builds without errors

**Step 3: Run full update cycle test**

1. Start current version
2. Create new release with version bump
3. Run application
4. Verify update downloads
5. Verify application exits
6. Verify updater replaces files
7. Verify new version launches

**Step 4: Test rollback scenarios**

1. Corrupt download during update
2. Invalid checksum
3. Missing files in update
4. Updater crashes during replacement

Expected: Each scenario rolls back and launches old version

**Step 5: Test frontend hot-reload**

1. Create release with only wwwroot.zip changed
2. Run application
3. Verify update applies without restart

**Step 6: Clean up test files and artifacts**

Run:
```powershell
# Remove temp directories
Remove-Item -Recurse -Force test_temp, test_update -ErrorAction SilentlyContinue

# Remove test checksums
Remove-Item checksums.txt -ErrorAction SilentlyContinue
```

**Step 7: Final commit**

Run:
```bash
git add .
git commit -m "feat: complete custom updater stub implementation

- Replace AutoUpdater.NET with custom updater stub
- Solve EXE file locking issue
- Add rollback support
- Add comprehensive documentation
- Add integration tests

Updater stub runs in separate process, waits for main app to exit,
replaces files, then launches new version. Industry-standard pattern
used by Chrome, VS Code, Discord."
```

---

## Testing Checklist

Before considering this feature complete:

- [ ] Updater stub builds and runs independently
- [ ] Updater shows help with `--help`
- [ ] Updater waits for process exit correctly
- [ ] Updater replaces GUI executable
- [ ] Updater replaces Stub DLL
- [ ] Updater extracts wwwroot.zip
- [ ] Updater launches new version
- [ ] Updater cleans up temp files
- [ ] Updater rolls back on error
- [ ] AutoUpdateService downloads assets
- [ ] AutoUpdateService verifies checksums
- [ ] AutoUpdateService extracts embedded updater
- [ ] AutoUpdateService launches updater and exits
- [ ] Main app embeds updater as resource
- [ ] Release workflow builds updater
- [ ] Release workflow removes update.xml
- [ ] Frontend hot-reload works (no restart)
- [ ] Full update cycle works end-to-end
- [ ] Rollback works on various failure scenarios
- [ ] Documentation is complete and accurate
- [ ] All tests pass
- [ ] Solution builds without warnings

---

## Rollback Plan

If implementation fails:
1. Revert commits: `git revert <commit-range>`
2. Restore AutoUpdater.NET integration
3. Fix XML URL issue as quick patch:
   - Change line 240 in AutoUpdateService.cs to use correct GitHub download URL
   - Change `Synchronous = false` to `true`
4. Re-evaluate custom updater approach

---

## Success Criteria

- Application updates reliably without hanging
- No file locking errors
- Rollback works on failure
- Frontend updates don't require restart
- Clean separation of concerns (main app vs updater)
- Comprehensive error logging
- Documentation is clear and complete
