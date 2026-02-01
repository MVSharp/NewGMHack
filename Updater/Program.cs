using System.CommandLine;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Updater;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Setup ZLogger
        using var loggerFactory = LoggingSetup.BuildLoggerFactory();
        var logger = loggerFactory.CreateLogger<Program>();

        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[bold yellow]NewGMHack Updater Stub v1.0[/]");
            AnsiConsole.MarkupLine("[dim]Usage: Updater.exe --pid <process-id> --temp <temp-dir> [--app-dir <app-dir>][/]");
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
            // Display brief changelog if available
            var changelogPath = Path.Combine(tempDir, "CHANGELOG.md");
            if (File.Exists(changelogPath))
            {
                try
                {
                    var changelogContent = await File.ReadAllTextAsync(changelogPath);
                    var lines = changelogContent.Split('\n', 2);
                    var version = lines.Length > 0 ? lines[0].TrimStart('#', ' ') : "Unknown Version";
                    var markdown = lines.Length > 1 ? lines[1] : "No release notes available.";

                    AnsiConsole.WriteLine();
                    ChangelogFormatter.DisplayBriefChangelog(version, markdown);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to read changelog: {Message}", ex.Message);
                }
            }

            var engine = new UpdateEngine(loggerFactory.CreateLogger<UpdateEngine>());
            Environment.ExitCode = await engine.ExecuteUpdateAsync(pid, tempDir, appDir);
        }, pidOption, tempOption, appDirOption);

        return await rootCommand.InvokeAsync(args);
    }
}
