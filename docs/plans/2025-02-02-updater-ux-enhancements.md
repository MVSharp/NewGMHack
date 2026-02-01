# Updater UX Enhancements Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Enhance the updater system with Spectre.Console progress visualization, ZLogger structured logging, fix path argument handling bugs, and implement a source generator for consistent logger creation across all projects.

**Architecture:** Integrate Spectre.Console for rich progress bars/spinners in Updater.exe, add ZLogger for structured logging matching the main app, fix argument escaping bugs causing stray quotes in file paths, and create a Roslyn source generator to auto-generate strongly-typed logger boilerplate.

**Tech Stack:** Spectre.Console 0.49.1, ZLogger 2.5.1, .NET 10.0, Roslyn Source Generators (Microsoft.CodeAnalysis.CSharp), System.CommandLine 2.0.0-beta4

---

## Background

### Current Problems

1. **Poor progress visibility** - Console.WriteLine doesn't show progress percentages or download speeds
2. **No structured logging in Updater** - Makes troubleshooting difficult compared to main app's ZLogger
3. **Path escaping bug** - Stray quotes in arguments: `Release"\NewGMHack.GUI.exe`
4. **Inconsistent logger patterns** - Every service manually declares ILogger<T>, boilerplate repetition
5. **No source generators** - Missing modern .NET performance optimization opportunities

### Why This Approach Works

- **Spectre.Console** - Industry-standard for beautiful CLI progress bars, spinners, tables
- **ZLogger in Updater** - Consistent structured logging format across all processes
- **Source generators** - Zero-allocation logger calls, compile-time safety, reduced boilerplate
- **Fixed argument handling** - Proper escaping prevents file path corruption

---

## Implementation Plan

### Task 1: Add Dependencies to Updater Project

**Files:**
- Modify: `Updater/Updater.csproj`

**Step 1: Add Spectre.Console and ZLogger packages**

```bash
cd Updater
dotnet add package Spectre.Console --version 0.49.1
dotnet add package ZLogger --version 2.5.1
dotnet add package Microsoft.Extensions.Logging --version 9.0.0
dotnet add package Microsoft.Extensions.Logging.Abstractions --version 9.0.0
```

Expected: NuGet packages restored successfully

**Step 2: Update Updater.csproj with new package references**

Edit: `Updater/Updater.csproj`

Replace file contents with:

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
    <PackageReference Include="Spectre.Console" Version="0.49.1" />
    <PackageReference Include="ZLogger" Version="2.5.1" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
  </ItemGroup>

</Project>
```

**Step 3: Verify build**

Run: `dotnet build Updater/Updater.csproj -c Release`

Expected: BUILD SUCCESS with no warnings

**Step 4: Commit**

```bash
git add Updater/Updater.csproj
git commit -m "feat(updater): add Spectre.Console and ZLogger dependencies"
```

---

### Task 2: Fix Argument Escaping Bug in AutoUpdateService

**Files:**
- Modify: `NewGmHack.GUI/Services/AutoUpdateService.cs:272-283`

**Step 1: Add ArgumentHelper utility class**

Edit: `NewGmHack.GUI/Services/AutoUpdateService.cs`

Add before the `AutoUpdateService` class (after line 11):

```csharp
/// <summary>
/// Helper for properly escaping command-line arguments on Windows
/// </summary>
internal static class ArgumentHelper
{
    /// <summary>
    /// Properly escape a command-line argument to handle spaces, quotes, and special characters
    /// </summary>
    public static string EscapeArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return "\"\"";

        // If the argument doesn't contain spaces, tabs, quotes, or backslashes, return as-is
        if (!arg.Any(c => c == ' ' || c == '\t' || c == '"' || c == '\\'))
            return arg;

        // Escape backslashes followed by quotes, then escape all quotes
        var escaped = arg.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }
}
```

**Step 2: Fix argument construction in ApplyForceUpdateAsync**

Edit: `NewGmHack.GUI/Services/AutoUpdateService.cs:272-283`

Replace the updater launch code (lines 272-283) with:

```csharp
            // Step 6: Launch updater and exit
            var currentPid = Process.GetCurrentProcess().Id;
            var appDir = AppDomain.CurrentDomain.BaseDirectory;

            _logger.LogInformation("Launching updater stub - PID: {Pid}, AppDir: {AppDir}", currentPid, appDir);

            var updaterStartInfo = new ProcessStartInfo
            {
                FileName = updaterPath,
                // Use proper argument escaping to fix path bug
                Arguments = $"--pid {currentPid} --temp {ArgumentHelper.EscapeArgument(tempDir)} --app-dir {ArgumentHelper.EscapeArgument(appDir)}",
                UseShellExecute = true
            };

            Process.Start(updaterStartInfo);
```

**Step 3: Test argument escaping manually**

Run: `dotnet run --project NewGmHack.GUI/NewGmHack.GUI.csproj`

Expected: Application launches successfully, no argument parsing errors in logs

**Step 4: Commit**

```bash
git add NewGmHack.GUI/Services/AutoUpdateService.cs
git commit -m "fix(updater): properly escape command-line arguments to fix path bug"
```

---

### Task 3: Add ZLogger Setup to Updater

**Files:**
- Create: `Updater/LoggingSetup.cs`
- Modify: `Updater/Program.cs`

**Step 1: Create LoggingSetup.cs**

Create file: `Updater/LoggingSetup.cs`

```csharp
using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Providers;

namespace Updater;

/// <summary>
/// ZLogger setup for Updater.exe - consistent logging format with main app
/// </summary>
internal static class LoggingSetup
{
    /// <summary>
    /// Configure ZLogger for the updater with file and console output
    /// </summary>
    public static ILoggerFactory CreateLoggerFactory()
    {
        var loggerFactory = LoggerFactory.Create(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Information);

            // Console output with Spectre.Console integration
            logging.AddZLoggerConsole(options =>
            {
                options.UsePlainTextFormatter(formatter =>
                {
                    formatter.SetPrefixFormatter($"{0}|{1}|",
                        (in MessageTemplate template, in LogInfo info) =>
                            template.Format(info.Timestamp, info.LogLevel));
                });
            });

            // File output for troubleshooting
            logging.AddZLoggerRollingFile(options =>
            {
                options.FilePathSelector = (timestamp, sequenceNumber) =>
                    $"logs/updater_{timestamp.ToLocalTime():yyyy-MM-dd}_{sequenceNumber:000}.log";
                options.RollingInterval = RollingInterval.Day;
                options.RollingSizeKB = 10240; // 10MB per file

                options.UsePlainTextFormatter(formatter =>
                {
                    formatter.SetPrefixFormatter($"{0}|{1}|",
                        (in MessageTemplate template, in LogInfo info) =>
                            template.Format(info.Timestamp, info.LogLevel));
                    formatter.SetExceptionFormatter((writer, ex) =>
                        Utf8StringInterpolation.Utf8String.Format(writer, $"{ex.Message}"));
                });
            });
        });

        return loggerFactory;
    }
}
```

**Step 2: Integrate ZLogger into Program.cs**

Edit: `Updater/Program.cs`

Replace entire file contents with:

```csharp
using System.CommandLine;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Updater;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Setup ZLogger
        using var loggerFactory = LoggingSetup.CreateLoggerFactory();
        var logger = loggerFactory.CreateLogger<Program>();

        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[bold yellow]NewGMHack Updater Stub v2.0[/]");
            AnsiConsole.MarkupLine("Usage: [cyan]Updater.exe --pid <process-id> --temp <temp-dir> [--app-dir <app-dir>][/]");
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
            var engine = new UpdateEngine(loggerFactory.CreateLogger<UpdateEngine>());
            Environment.ExitCode = await engine.ExecuteUpdateAsync(pid, tempDir, appDir);
        }, pidOption, tempOption, appDirOption);

        return await rootCommand.InvokeAsync(args);
    }
}
```

**Step 3: Build and verify**

Run: `dotnet build Updater/Updater.csproj -c Release`

Expected: BUILD SUCCESS

**Step 4: Commit**

```bash
git add Updater/LoggingSetup.cs Updater/Program.cs
git commit -m "feat(updater): add ZLogger structured logging"
```

---

### Task 4: Integrate Spectre.Console Progress Bars into UpdateEngine

**Files:**
- Modify: `Updater/UpdateEngine.cs`

**Step 1: Add Spectre.Console using and update constructor**

Edit: `Updater/UpdateEngine.cs`

Replace entire file contents with:

```csharp
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

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
            _logger.LogInformation("Starting update process: PID={Pid}, Temp={Temp}, AppDir={AppDir}",
                targetPid, tempDir, appDir);

            AnsiConsole.MarkupLine($"[bold green]NewGMHack Updater v2.0[/]");
            AnsiConsole.WriteLine();

            // Create progress display
            await AnsiConsole.Progress()
                .StartAsync(async ctx =>
                {
                    // Step 1: Wait for main app to exit
                    var waitTask = ctx.AddTask("[green]Waiting for application to exit[/]");
                    waitTask.MaxValue = 100;
                    waitTask.Value = 10;

                    await WaitForProcessExitAsync(targetPid);
                    waitTask.Value = 100;
                    _logger.LogInformation("Process {Pid} exited successfully", targetPid);

                    // Step 2: Replace files with sub-progress
                    var replaceTask = ctx.AddTask("[green]Replacing files[/]");
                    await ReplaceFilesAsync(replaceTask, tempDir, appDir);

                    // Step 3: Verify and launch
                    var verifyTask = ctx.AddTask("[green]Verifying and launching[/]");
                    await VerifyAndLaunchAsync(verifyTask, tempDir, appDir);
                });

            // Cleanup temp directory
            _logger.LogInformation("Cleaning up temp directory: {TempDir}", tempDir);
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete temp directory");
            }

            AnsiConsole.MarkupLine($"[bold green]✓[/] Update completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update failed");
            AnsiConsole.MarkupLine($"[bold red]✗[/] Update failed: {ex.Message}");
            RollbackUpdate(appDir, $"Update failed: {ex.Message}");
            return 1;
        }
    }

    private async Task ReplaceFilesAsync(ProgressTask task, string tempDir, string appDir)
    {
        task.MaxValue = 100;

        // Declare paths
        var newGuiPath = Path.Combine(tempDir, GuiExeName);
        var oldGuiPath = Path.Combine(appDir, GuiExeName);

        try
        {
            // Replace GUI executable (33%)
            if (File.Exists(newGuiPath))
            {
                _logger.LogInformation("Replacing {GuiExeName}", GuiExeName);
                AnsiConsole.MarkupLine($"  [dim]→[/] Replacing {GuiExeName}...");
                ReplaceFile(newGuiPath, oldGuiPath);
            }
            task.Value = 33;

            // Replace Stub DLL (66%)
            var newStubPath = Path.Combine(tempDir, StubDllName);
            var oldStubPath = Path.Combine(appDir, StubDllName);

            if (File.Exists(newStubPath))
            {
                _logger.LogInformation("Replacing {StubDllName}", StubDllName);
                AnsiConsole.MarkupLine($"  [dim]→[/] Replacing {StubDllName}...");
                ReplaceFile(newStubPath, oldStubPath);
            }
            task.Value = 66;

            // Extract wwwroot.zip (100%)
            var wwwrootZipPath = Path.Combine(tempDir, WwwrootZipName);
            if (File.Exists(wwwrootZipPath))
            {
                _logger.LogInformation("Extracting {WwwrootZipName}", WwwrootZipName);
                AnsiConsole.MarkupLine($"  [dim]→[/] Extracting {WwwrootZipName}...");
                var wwwrootDest = Path.Combine(appDir, "wwwroot");
                ExtractWwwroot(wwwrootZipPath, wwwrootDest);
            }
            else
            {
                _logger.LogInformation("{WwwrootZipName} not found (frontend-only update?)", WwwrootZipName);
            }
            task.Value = 100;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during file replacement");
            throw;
        }
    }

    private async Task VerifyAndLaunchAsync(ProgressTask task, string appDir)
    {
        task.MaxValue = 100;
        task.Value = 0;

        var guiPath = Path.Combine(appDir, GuiExeName);

        // Verify version
        if (File.Exists(guiPath))
        {
            var versionInfo = AssemblyName.GetAssemblyName(guiPath).Version?.ToString() ?? "unknown";
            _logger.LogInformation("New version: {Version}", versionInfo);
            AnsiConsole.MarkupLine($"  [dim]→[/] Version: [cyan]{versionInfo}[/]");
        }
        task.Value = 50;

        // Launch new version
        _logger.LogInformation("Launching new version: {GuiPath}", guiPath);
        AnsiConsole.MarkupLine($"  [dim]→[/] Launching new version...");

        var startInfo = new ProcessStartInfo
        {
            FileName = guiPath,
            Arguments = "--updated",
            UseShellExecute = true,
            WorkingDirectory = appDir
        };

        Process.Start(startInfo);
        task.Value = 100;

        await Task.Delay(100); // Small delay to ensure launch completes
    }

    private async Task WaitForProcessExitAsync(int pid, TimeSpan? maxWait = null)
    {
        var timeout = maxWait ?? TimeSpan.FromSeconds(30);
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                if (process.HasExited)
                {
                    await Task.Delay(500); // Release file handles
                    return;
                }
                await Task.Delay(250);
            }
            catch (ArgumentException)
            {
                await Task.Delay(500);
                return;
            }
        }

        throw new TimeoutException($"Process {pid} did not exit within {timeout.TotalSeconds}s");
    }

    private void ReplaceFile(string source, string destination)
    {
        if (!File.Exists(source))
            throw new FileNotFoundException($"Source file not found: {source}");

        if (File.Exists(destination))
        {
            File.Delete(destination);
        }

        File.Copy(source, destination, true);

        if (!File.Exists(destination))
            throw new IOException($"Failed to copy file to {destination}");

        _logger.LogDebug("Replaced file: {Destination} ({Size} bytes)",
            destination, new FileInfo(destination).Length);
    }

    private void ExtractWwwroot(string zipPath, string destinationPath)
    {
        if (Directory.Exists(destinationPath))
        {
            Directory.Delete(destinationPath, true);
        }

        ZipFile.ExtractToDirectory(zipPath, destinationPath);

        var fileCount = Directory.GetFiles(destinationPath, "*", SearchOption.AllDirectories).Length;
        _logger.LogInformation("Extracted {FileCount} files to wwwroot", fileCount);
    }

    private void RollbackUpdate(string appDir, string reason)
    {
        _logger.LogWarning("Rollback initiated: {Reason}", reason);
        AnsiConsole.MarkupLine($"[yellow]Rolling back...[/]");

        var backupDir = Path.Combine(appDir, ".backup");
        if (!Directory.Exists(backupDir))
        {
            _logger.LogError("No backup directory found");
            return;
        }

        try
        {
            // Restore GUI exe
            var backupGui = Path.Combine(backupDir, GuiExeName);
            var appGui = Path.Combine(appDir, GuiExeName);
            if (File.Exists(backupGui) && File.Exists(appGui))
            {
                File.Delete(appGui);
                File.Copy(backupGui, appGui, true);
                _logger.LogInformation("Restored {GuiExeName}", GuiExeName);
            }

            // Restore Stub DLL
            var backupStub = Path.Combine(backupDir, StubDllName);
            var appStub = Path.Combine(appDir, StubDllName);
            if (File.Exists(backupStub) && File.Exists(appStub))
            {
                File.Delete(appStub);
                File.Copy(backupStub, appStub, true);
                _logger.LogInformation("Restored {StubDllName}", StubDllName);
            }

            // Restore wwwroot
            var backupWwwroot = Path.Combine(backupDir, "wwwroot");
            var appWwwroot = Path.Combine(appDir, "wwwroot");
            if (Directory.Exists(backupWwwroot) && Directory.Exists(appWwwroot))
            {
                Directory.Delete(appWwwroot, true);
                CopyDirectory(backupWwwroot, appWwwroot);
                _logger.LogInformation("Restored wwwroot");
            }

            AnsiConsole.MarkupLine("[bold green]✓[/] Rollback completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback failed");
            AnsiConsole.MarkupLine("[bold red]✗[/] Rollback failed");
        }

        // Launch old version
        var oldGuiPath = Path.Combine(appDir, GuiExeName);
        if (File.Exists(oldGuiPath))
        {
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
```

**Step 2: Build and verify**

Run: `dotnet build Updater/Updater.csproj -c Release`

Expected: BUILD SUCCESS

**Step 3: Commit**

```bash
git add Updater/UpdateEngine.cs
git commit -m "feat(updater): integrate Spectre.Console progress visualization"
```

---

### Task 5: Add Download Progress to AutoUpdateService

**Files:**
- Modify: `NewGmHack.GUI/Services/AutoUpdateService.cs`

**Step 1: Add download progress tracking**

Edit: `NewGmHack.GUI/Services/AutoUpdateService.cs:387-395`

Replace the `DownloadFileAsync` method with:

```csharp
    /// <summary>
    /// Download file from URL with progress logging
    /// </summary>
    private async Task DownloadFileAsync(string url, string destinationPath)
    {
        _logger.LogInformation("Downloading {Url} to {Path}", url, destinationPath);

        var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        _logger.LogDebug("Content length: {TotalBytes} bytes", totalBytes);

        await using var fileStream = File.Create(destinationPath);
        await using var contentStream = await response.Content.ReadAsStreamAsync();

        var buffer = new byte[81920]; // 80KB buffer
        long bytesRead = 0;
        int lastReportedPercent = -1;

        int read;
        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, read);
            bytesRead += read;

            if (totalBytes > 0)
            {
                var percent = (int)(bytesRead * 100 / totalBytes);
                if (percent != lastReportedPercent && percent % 10 == 0) // Log every 10%
                {
                    _logger.LogInformation("Download progress: {Percent}% ({Bytes} / {TotalBytes}",
                        percent, bytesRead, totalBytes);
                    lastReportedPercent = percent;
                }
            }
        }

        _logger.LogInformation("Download completed: {FilePath} ({Size} bytes)",
            destinationPath, new FileInfo(destinationPath).Length);
    }
```

**Step 2: Test download progress**

Run: `dotnet build NewGmHack.GUI/NewGmHack.GUI.csproj -c Release`

Expected: BUILD SUCCESS

**Step 3: Commit**

```bash
git add NewGmHack.GUI/Services/AutoUpdateService.cs
git commit -m "feat(updater): add download progress logging"
```

---

### Task 6: Create Source Generator Project for Logger Boilerplate

**Files:**
- Create: `NewGMHack.LoggerGenerator/LoggerSourceGenerator.cs`
- Create: `NewGMHack.LoggerGenerator/LoggerSourceGenerator.csproj`

**Step 1: Create source generator project**

Run: `mkdir NewGMHack.LoggerGenerator`

Create file: `NewGMHack.LoggerGenerator/LoggerSourceGenerator.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <DevelopmentDependency>true</DevelopmentDependency>
    <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
    <NoPackageAnalysis>true</NoPackageAnalysis>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.11.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.4" PrivateAssets="all" />
  </ItemGroup>

</Project>
```

**Step 2: Create the source generator**

Create file: `NewGMHack.LoggerGenerator/LoggerSourceGenerator.cs`

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace NewGMHack.LoggerGenerator;

[Generator]
public class LoggerSourceGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        // Register a syntax receiver that finds class declarations
        context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (!(context.SyntaxContextReceiver is SyntaxReceiver receiver))
            return;

        var sourceBuilder = new StringBuilder();

        foreach (var candidateClass in receiver.CandidateClasses)
        {
            var classSymbol = context.Compilation.GetSemanticModel(candidateClass.SyntaxTree)
                .GetDeclaredSymbol(candidateClass);

            if (classSymbol is null)
                continue;

            var namespaceName = classSymbol.ContainingNamespace?.ToString() ?? "GlobalNamespace";
            var className = classSymbol.Name;
            var loggerFieldName = $"_logger{className}";

            // Generate partial class extension with ILogger field
            sourceBuilder.AppendLine($@"
// <auto-generated/>
namespace {namespaceName}
{{
    public partial class {className}
    {{
        /// <summary>
        /// Logger instance for {className} (auto-generated by LoggerSourceGenerator)
        /// </summary>
        private readonly Microsoft.Extensions.Logging.ILogger<{className}> {loggerFieldName};
    }}
}}
");
        }

        // Add the generated source to the compilation
        context.AddSource($"LoggerExtensions.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }

    /// <summary>
    /// Syntax receiver to collect class declarations marked for logger generation
    /// </summary>
    private class SyntaxReceiver : ISyntaxContextReceiver
    {
        public List<ClassDeclarationSyntax> CandidateClasses { get; } = new();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            // Look for classes that are partial and have a specific attribute or pattern
            if (context.Node is ClassDeclarationSyntax classDeclaration
                && classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                CandidateClasses.Add(classDeclaration);
            }
        }
    }
}
```

**Step 3: Build source generator**

Run: `dotnet build NewGMHack.LoggerGenerator/LoggerSourceGenerator.csproj`

Expected: BUILD SUCCESS

**Step 4: Commit**

```bash
git add NewGMHack.LoggerGenerator/
git commit -m "feat(logger): add source generator for logger boilerplate"
```

---

### Task 7: Integrate Source Generator into GUI and Updater

**Files:**
- Modify: `NewGMHack.LoggerGenerator/LoggerSourceGenerator.csproj`
- Modify: `NewGmHack.GUI/NewGmHack.GUI.csproj`
- Modify: `Updater/Updater.csproj`

**Step 1: Add package metadata to source generator**

Edit: `NewGMHack.LoggerGenerator/LoggerSourceGenerator.csproj`

Add after `<ItemGroup>` section:

```xml
  <ItemGroup>
    <None Include="$(OutputPath)\$(AssemblyName).dll" Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
  </ItemGroup>

</Project>
```

**Step 2: Reference source generator in GUI project**

Edit: `NewGmHack.GUI/NewGmHack.GUI.csproj`

Add to `<ItemGroup>` with PackageReferences:

```xml
    <ProjectReference Include="..\NewGMHack.LoggerGenerator\LoggerSourceGenerator.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
```

**Step 3: Reference source generator in Updater project**

Edit: `Updater/Updater.csproj`

Add to `<ItemGroup>` with PackageReferences:

```xml
    <ProjectReference Include="..\NewGMHack.LoggerGenerator\LoggerSourceGenerator.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
```

**Step 4: Build all projects**

Run: `dotnet build NewGMHack.sln -c Release`

Expected: BUILD SUCCESS for all projects

**Step 5: Commit**

```bash
git add NewGMHack.LoggerGenerator/LoggerSourceGenerator.csproj NewGmHack.GUI/NewGmHack.GUI.csproj Updater/Updater.csproj
git commit -m "feat(logger): integrate source generator into GUI and Updater"
```

---

### Task 8: Add Changelog Display to Updater

**Files:**
- Create: `Updater/ChangelogFormatter.cs`
- Modify: `Updater/Program.cs`
- Modify: `NewGmHack.GUI/Services/AutoUpdateService.cs`

**Step 1: Create changelog formatter**

Create file: `Updater/ChangelogFormatter.cs`

```csharp
using Spectre.Console;

namespace Updater;

/// <summary>
/// Format and display changelog information with Spectre.Console
/// </summary>
internal static class ChangelogFormatter
{
    /// <summary>
    /// Display brief changelog during update
    /// </summary>
    public static void DisplayBriefChangelog(string version, string releaseNotes)
    {
        var panel = new Panel($"[cyan]Version {version}[/]")
        {
            Border = BoxBorder.Rounded,
            BorderStyle = Style.Parse("blue"),
            Padding = new Padding(1, 1)
        };

        // Extract key changes (first 5 lines or less)
        var lines = releaseNotes.Split('\n', '\r')
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"))
            .Take(5)
            .ToList();

        if (lines.Any())
        {
            panel.Header = new PanelHeader("[bold yellow]What's New[/]");
            var content = string.Join("\n", lines.Select(l => $"  • {l.Trim()}"));
            panel.Content = Markup.Escape(content);
        }
        else
        {
            panel.Content = "[dim]See GitHub release notes for details[/]";
        }

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Display full changelog after update completion
    /// </summary>
    public static void DisplayFullChangelog(string version, string releaseNotes)
    {
        AnsiConsole.WriteLine();
        var rule = new Rule($"[bold green]Update Complete: Version {version}[/]")
        {
            Justification = Justify.Center
        };
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();

        // Format markdown-style release notes
        var formatted = FormatMarkdown(releaseNotes);
        AnsiConsole.MarkupLine(formatted);
        AnsiConsole.WriteLine();
    }

    private static string FormatMarkdown(string markdown)
    {
        var lines = markdown.Split('\n');
        var formatted = new System.Text.StringBuilder();

        foreach (var line in lines)
        {
            if (line.StartsWith("## "))
            {
                // Headers
                formatted.AppendLine($"[bold yellow]{line.TrimStart('#').Trim()}[/]");
            }
            else if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                // Bullet points
                formatted.AppendLine($"  [dim]•[/] {Markup.Escape(line.TrimStart('-', '*').Trim())}");
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                // Regular text
                formatted.AppendLine(Markup.Escape(line));
            }
        }

        return formatted.ToString();
    }
}
```

**Step 2: Pass changelog to updater in AutoUpdateService**

Edit: `NewGmHack.GUI/Services/AutoUpdateService.cs:268-288`

Modify the ApplyForceUpdateAsync method to pass changelog info:

```csharp
            // Step 5: Extract embedded updater stub
            var updaterPath = Path.Combine(tempDir, "Updater.exe");
            await ExtractUpdaterStubAsync(updaterPath);
            _logger.LogInformation("Updater stub extracted to: {UpdaterPath}", updaterPath);

            // Step 5.5: Save changelog to temp file for updater to display
            var changelogPath = Path.Combine(tempDir, "CHANGELOG.md");
            var changelogText = $"# {latestVersion}\n\n{releaseInfo.Body}";
            await File.WriteAllTextAsync(changelogPath, changelogText);
            _logger.LogInformation("Changelog saved to {Path}", changelogPath);

            // Step 6: Launch updater and exit
            var currentPid = Process.GetCurrentProcess().Id;
            var appDir = AppDomain.CurrentDomain.BaseDirectory;

            _logger.LogInformation("Launching updater stub - PID: {Pid}, AppDir: {AppDir}", currentPid, appDir);

            var updaterStartInfo = new ProcessStartInfo
            {
                FileName = updaterPath,
                // Use proper argument escaping to fix path bug
                Arguments = $"--pid {currentPid} --temp {ArgumentHelper.EscapeArgument(tempDir)} --app-dir {ArgumentHelper.EscapeArgument(appDir)}",
                UseShellExecute = true
            };

            Process.Start(updaterStartInfo);
```

**Step 3: Display changelog in Updater**

Edit: `Updater/Program.cs`

Add changelog parameter and display logic:

```csharp
using System.CommandLine;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Updater;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Setup ZLogger
        using var loggerFactory = LoggingSetup.CreateLoggerFactory();
        var logger = loggerFactory.CreateLogger<Program>();

        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[bold yellow]NewGMHack Updater Stub v2.0[/]");
            AnsiConsole.MarkupLine("Usage: [cyan]Updater.exe --pid <process-id> --temp <temp-dir> [--app-dir <app-dir>][/]");
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
            var engine = new UpdateEngine(loggerFactory.CreateLogger<UpdateEngine>());

            // Load and display changelog if available
            var changelogPath = Path.Combine(tempDir, "CHANGELOG.md");
            if (File.Exists(changelogPath))
            {
                try
                {
                    var changelog = await File.ReadAllTextAsync(changelogPath);
                    var version = ParseVersionFromChangelog(changelog);
                    ChangelogFormatter.DisplayBriefChangelog(version, changelog);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to display changelog");
                }
            }

            Environment.ExitCode = await engine.ExecuteUpdateAsync(pid, tempDir, appDir);
        }, pidOption, tempOption, appDirOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static string ParseVersionFromChangelog(string changelog)
    {
        // Extract version from "# v1.0.750" format
        var firstLine = changelog.Split('\n').FirstOrDefault();
        if (firstLine != null && firstLine.StartsWith("#"))
        {
            var version = firstLine.TrimStart('#').Trim().TrimStart('v').Trim();
            return version;
        }
        return "unknown";
    }
}
```

**Step 4: Add full changelog display in UpdateEngine**

Edit: `Updater/UpdateEngine.cs` in the `VerifyAndLaunchAsync` method before launching:

```csharp
        // Launch new version
        _logger.LogInformation("Launching new version: {GuiPath}", guiPath);
        AnsiConsole.MarkupLine($"  [dim]→[/] Launching new version...");

        var startInfo = new ProcessStartInfo
        {
            FileName = guiPath,
            Arguments = "--updated",
            UseShellExecute = true,
            WorkingDirectory = appDir
        };

        Process.Start(startInfo);
        task.Value = 100;

        await Task.Delay(100); // Small delay to ensure launch completes

        // Display full changelog after launch
        var changelogPath = Path.Combine(appDir, ".update_changelog.md");
        if (File.Exists(changelogPath))
        {
            try
            {
                var changelog = await File.ReadAllTextAsync(changelogPath);
                var version = Path.GetFileNameWithoutExtension(GuiExeName);
                ChangelogFormatter.DisplayFullChangelog(version, changelog);
                File.Delete(changelogPath); // Cleanup
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to display full changelog");
            }
        }
```

**Step 5: Build and verify**

Run: `dotnet build NewGMHack.sln -c Release`

Expected: BUILD SUCCESS

**Step 6: Commit**

```bash
git add Updater/ChangelogFormatter.cs Updater/Program.cs Updater/UpdateEngine.cs NewGmHack.GUI/Services/AutoUpdateService.cs
git commit -m "feat(updater): add changelog display with Spectre.Console"
```

---

### Task 9: Update Documentation

**Files:**
- Modify: `docs/update-architecture.md`
- Create: `docs/source-generator-guide.md`

**Step 1: Update update architecture documentation**

Edit: `docs/update-architecture.md`

Add new section after "Security Considerations":

```markdown
## User Experience Enhancements (v2.0)

### Spectre.Console Integration

The Updater now uses Spectre.Console for rich CLI visualization:

```csharp
// Progress bars with percentage completion
await AnsiConsole.Progress()
    .StartAsync(async ctx =>
    {
        var task = ctx.AddTask("[green]Downloading update[/]");
        task.Increment(bytesDownloaded);
    });

// Styled status messages
AnsiConsole.MarkupLine("[bold green]✓[/] Update completed successfully!");
AnsiConsole.MarkupLine("[bold red]✗[/] Update failed: {error}");
```

### ZLogger Structured Logging

Both GUI and Updater use ZLogger for consistent log format:

```
2025-02-02 12:34:56|Information| Starting update process: PID=1234
2025-02-02 12:34:57|Debug| Replaced file: NewGMHack.GUI.exe
2025-02-02 12:34:58|Warning| Could not delete temp directory
```

Logs are saved to:
- **GUI**: `logs/{date}_{sequence}.log`
- **Updater**: `logs/updater_{date}_{sequence}.log`

### Changelog Display

Updates now show formatted release notes:

1. **Brief changelog** - Displayed during update (top 5 changes)
2. **Full changelog** - Displayed after update completion
3. **Markdown formatting** - Headers, bullet points, and code blocks rendered

### Download Progress

Download operations report progress every 10%:

```
Download progress: 10% (1024000 / 10240000)
Download progress: 20% (2048000 / 10240000)
...
Download completed: NewGMHack.GUI.exe (10240000 bytes)
```
```

**Step 2: Create source generator guide**

Create file: `docs/source-generator-guide.md`

```markdown
# Logger Source Generator Guide

## Overview

The `NewGMHack.LoggerGenerator` is a Roslyn source generator that automatically adds `ILogger<T>` fields to partial classes, reducing boilerplate and ensuring consistent logging patterns across all projects.

## How It Works

The source generator:
1. Scans for `partial class` declarations
2. Automatically generates a `ILogger<ClassName>` field
3. Adds XML documentation for the logger field

## Usage

### Step 1: Mark class as partial

```csharp
namespace MyProject.Services;

public partial class MyService
{
    // No manual ILogger field needed - auto-generated!
    public void DoWork()
    {
        // Use the auto-generated logger
        _loggerMyService.LogInformation("Doing work...");
    }
}
```

### Step 2: Reference the generator

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\NewGMHack.LoggerGenerator\LoggerSourceGenerator.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>
</Project>
```

### Step 3: Build and use

Build the project - the source generator will create:

```csharp
// <auto-generated/>
namespace MyProject.Services
{
    public partial class MyService
    {
        /// <summary>
        /// Logger instance for MyService (auto-generated by LoggerSourceGenerator)
        /// </summary>
        private readonly Microsoft.Extensions.Logging.ILogger<MyService> _loggerMyService;
    }
}
```

## Benefits

- **Zero boilerplate** - No manual logger field declarations
- **Consistent naming** - `_logger{ClassName}` pattern enforced
- **Compile-time safety** - Errors caught at build time
- **Zero runtime overhead** - Source generation happens at compile time
- **IDE support** - IntelliSense works with generated code

## Implementation Details

Target Framework: `netstandard2.0` (compatible with all .NET versions)
Language Version: `latest` (C# 12+)
Analyzer Output: Embedded in consuming projects as analyzer reference

## Troubleshooting

**Logger field not found:**
- Ensure class is marked as `partial`
- Clean and rebuild the solution
- Check Visual Studio Error List for generator errors

**IntelliSense not working:**
- Restart Visual Studio
- Build the solution first
- Check that analyzer is loaded (Extensions → Source Generators)
```

**Step 3: Commit**

```bash
git add docs/update-architecture.md docs/source-generator-guide.md
git commit -m "docs: update architecture documentation for v2.0 enhancements"
```

---

### Task 10: Build and Test Full Update Flow

**Files:**
- Test: Manual integration testing

**Step 1: Build entire solution in Release mode**

Run: `dotnet build NewGMHack.sln -c Release`

Expected: BUILD SUCCESS with no warnings

**Step 2: Run GUI to trigger update check**

Run: `.\bin\x86\Release\net10.0-windows7.0\NewGmHack.GUI.exe`

Expected:
- Application launches successfully
- Update check runs on startup
- If update available, downloads with progress logging
- Spectre.Console progress bars show in updater window
- Changelog displays correctly

**Step 3: Verify log files**

Run: `ls -la logs/`

Expected:
- `logs/{date}_000.log` - Main application logs
- `logs/updater_{date}_000.log` - Updater logs with structured format

**Step 4: Test rollback scenario**

Simulate update failure:
1. Create a corrupted dummy file in temp directory
2. Trigger update
3. Verify rollback activates and restores from `.backup/`

**Step 5: Create release build**

Run: `.\build-release.ps1`

Expected:
- Frontend builds with Vite
- GUI and Stub compile to Release/x86
- Updater stub embedded in GUI
- All files in `bin/x86/Release/`

**Step 6: Final commit**

```bash
git add -A
git commit -m "test: verify full update flow with Spectre.Console and ZLogger"
```

---

## Summary

This plan implements:

1. **Spectre.Console integration** - Rich progress bars, spinners, and formatted output
2. **ZLogger structured logging** - Consistent log format across GUI and Updater
3. **Fixed argument escaping** - No more stray quotes in file paths
4. **Source generator for loggers** - Zero boilerplate, compile-time safety
5. **Changelog display** - Beautiful formatted release notes during and after updates
6. **Download progress** - 10% increment logging with byte counts
7. **Comprehensive documentation** - Updated architecture and source generator guide

**Total estimated implementation time:** 2-3 hours (following the plan task-by-task)

**Branch:** `feat/updater-enhancements`

**Merge target:** `dev` → `master` via PR
