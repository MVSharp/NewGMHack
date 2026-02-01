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
            Environment.ExitCode = await engine.ExecuteUpdateAsync(pid, tempDir, appDir);
        }, pidOption, tempOption, appDirOption);

        return await rootCommand.InvokeAsync(args);
    }
}
