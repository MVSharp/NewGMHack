using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

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

            // Declare paths at method scope for use in later steps
            var newGuiPath = Path.Combine(tempDir, GuiExeName);
            var oldGuiPath = Path.Combine(appDir, GuiExeName);

            // Steps 2-4: Replace files (with rollback on error)
            try
            {
                // Step 2: Replace GUI executable

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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Updater] ERROR during update: {ex.Message}");
                RollbackUpdate(appDir, $"Update failed: {ex.Message}");
                return 1;
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
}
