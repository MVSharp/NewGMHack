using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace Updater;

public class UpdateEngine
{
    private const string GuiExeName = "NewGMHack.GUI.exe";
    private const string StubDllName = "NewGMHack.Stub.dll";
    private const string WwwrootZipName = "wwwroot.zip";
    private readonly ILogger<UpdateEngine> _logger;

    public UpdateEngine(ILogger<UpdateEngine> logger)
    {
        _logger = logger;
    }

    public async Task<int> ExecuteUpdateAsync(int targetPid, string tempDir, string appDir)
    {
        try
        {
            _logger.LogInformation("Starting update process");
            _logger.LogInformation("Target PID: {TargetPid}", targetPid);
            _logger.LogInformation("Temp directory: {TempDir}", tempDir);
            _logger.LogInformation("App directory: {AppDir}", appDir);

            // Step 1: Wait for main app to exit
            _logger.LogInformation("Waiting for process {TargetPid} to exit...", targetPid);
            await WaitForProcessExitAsync(targetPid);
            _logger.LogInformation("Process exited - proceeding with file replacement");

            // Declare paths at method scope for use in later steps
            var newGuiPath = Path.Combine(tempDir, GuiExeName);
            var oldGuiPath = Path.Combine(appDir, GuiExeName);

            // Steps 2-4: Replace files (with rollback on error)
            try
            {
                // Step 2: Replace GUI executable

                if (File.Exists(newGuiPath))
                {
                    _logger.LogInformation("Replacing NewGMHack.GUI.exe...");
                    ReplaceFile(newGuiPath, oldGuiPath);
                }
                else
                {
                    _logger.LogWarning("{GuiExeName} not found in temp directory", GuiExeName);
                }

                // Step 3: Replace Stub DLL
                var newStubPath = Path.Combine(tempDir, StubDllName);
                var oldStubPath = Path.Combine(appDir, StubDllName);

                if (File.Exists(newStubPath))
                {
                    _logger.LogInformation("Replacing NewGMHack.Stub.dll...");
                    ReplaceFile(newStubPath, oldStubPath);
                }
                else
                {
                    _logger.LogWarning("{StubDllName} not found in temp directory", StubDllName);
                }

                // Step 4: Extract wwwroot.zip
                var wwwrootZipPath = Path.Combine(tempDir, WwwrootZipName);
                if (File.Exists(wwwrootZipPath))
                {
                    _logger.LogInformation("Extracting wwwroot.zip...");
                    var wwwrootDest = Path.Combine(appDir, "wwwroot");
                    ExtractWwwroot(wwwrootZipPath, wwwrootDest);
                }
                else
                {
                    _logger.LogInformation("Info: {WwwrootZipName} not found (frontend-only update?)", WwwrootZipName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR during update: {Message}", ex.Message);
                RollbackUpdate(appDir, $"Update failed: {ex.Message}");
                return 1;
            }

            // Step 5: Verify new version
            if (File.Exists(oldGuiPath))
            {
                var versionInfo = AssemblyName.GetAssemblyName(oldGuiPath).Version?.ToString() ?? "unknown";
                _logger.LogInformation("New version: {VersionInfo}", versionInfo);
            }

            // Step 6: Launch new version
            _logger.LogInformation("Launching new version: {OldGuiPath}", oldGuiPath);
            var startInfo = new ProcessStartInfo
            {
                FileName = oldGuiPath,
                Arguments = "--updated",
                UseShellExecute = true,
                WorkingDirectory = appDir
            };

            Process.Start(startInfo);

            // Step 7: Cleanup temp directory
            _logger.LogInformation("Cleaning up temp files...");
            try
            {
                Directory.Delete(tempDir, true);
                _logger.LogInformation("Temp directory cleaned");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete temp directory: {Message}", ex.Message);
            }

            _logger.LogInformation("Update completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ERROR: {Message}", ex.Message);
            return 1;
        }
    }

    private async Task WaitForProcessExitAsync(int pid, TimeSpan? maxWait = null)
    {
        var timeout = maxWait ?? TimeSpan.FromSeconds(30);
        var start = DateTime.UtcNow;

        _logger.LogInformation("Starting process wait (timeout: {Timeout}s)", timeout.TotalSeconds);

        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                if (process.HasExited)
                {
                    _logger.LogInformation("Process has exited");
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
                _logger.LogInformation("Process no longer exists");
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
            _logger.LogInformation("Deleting existing: {Destination}", destination);
            File.Delete(destination);
        }

        // Copy new file
        _logger.LogInformation("Copying {Source} -> {Destination}", source, destination);
        File.Copy(source, destination, true);

        // Verify
        if (!File.Exists(destination))
        {
            throw new IOException($"Failed to copy file to {destination}");
        }

        var fileSize = new FileInfo(destination).Length;
        _logger.LogInformation("File replaced successfully ({FileSize} bytes)", fileSize);
    }

    private void ExtractWwwroot(string zipPath, string destinationPath)
    {
        // Remove old wwwroot
        if (Directory.Exists(destinationPath))
        {
            _logger.LogInformation("Removing existing wwwroot...");
            Directory.Delete(destinationPath, true);
        }

        // Extract new wwwroot
        _logger.LogInformation("Extracting {ZipPath} -> {DestinationPath}", zipPath, destinationPath);
        ZipFile.ExtractToDirectory(zipPath, destinationPath);

        // Verify
        var fileCount = Directory.GetFiles(destinationPath, "*", SearchOption.AllDirectories).Length;
        _logger.LogInformation("Extracted {FileCount} files to wwwroot", fileCount);
    }

    private void RollbackUpdate(string appDir, string reason)
    {
        _logger.LogError("ROLLBACK INITIATED: {Reason}", reason);
        var backupDir = Path.Combine(appDir, ".backup");

        if (!Directory.Exists(backupDir))
        {
            _logger.LogWarning("No backup directory found - cannot rollback");
            return;
        }

        try
        {
            // Restore GUI exe
            var backupGui = Path.Combine(backupDir, GuiExeName);
            var appGui = Path.Combine(appDir, GuiExeName);
            if (File.Exists(backupGui) && File.Exists(appGui))
            {
                _logger.LogInformation("Restoring NewGMHack.GUI.exe from backup...");
                File.Delete(appGui);
                File.Copy(backupGui, appGui, true);
            }

            // Restore Stub DLL
            var backupStub = Path.Combine(backupDir, StubDllName);
            var appStub = Path.Combine(appDir, StubDllName);
            if (File.Exists(backupStub) && File.Exists(appStub))
            {
                _logger.LogInformation("Restoring NewGMHack.Stub.dll from backup...");
                File.Delete(appStub);
                File.Copy(backupStub, appStub, true);
            }

            // Restore wwwroot
            var backupWwwroot = Path.Combine(backupDir, "wwwroot");
            var appWwwroot = Path.Combine(appDir, "wwwroot");
            if (Directory.Exists(backupWwwroot) && Directory.Exists(appWwwroot))
            {
                _logger.LogInformation("Restoring wwwroot from backup...");
                Directory.Delete(appWwwroot, true);
                CopyDirectory(backupWwwroot, appWwwroot);
            }

            _logger.LogInformation("Rollback completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ROLLBACK FAILED: {Message}", ex.Message);
        }

        // Launch old version
        var oldGuiPath = Path.Combine(appDir, GuiExeName);
        if (File.Exists(oldGuiPath))
        {
            _logger.LogInformation("Launching restored version...");
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
}
