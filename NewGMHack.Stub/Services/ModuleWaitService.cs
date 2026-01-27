using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;
using NewGMHack.Stub.Configuration;

namespace NewGMHack.Stub.Services;

public class ModuleWaitService
{
    private const uint TH32CS_SNAPMODULE = 0x00000008;
    private const uint TH32CS_SNAPMODULE32 = 0x00000010;

    private readonly ILogger<ModuleWaitService> _logger;
    private readonly int _targetPid;
    private readonly ModuleWaitOptions _options;

    public ModuleWaitService(ILogger<ModuleWaitService> logger, IOptions<ModuleWaitOptions> options)
    {
        _logger = logger;
        _targetPid = Process.GetCurrentProcess().Id;
        _options = options.Value;
    }

    public HashSet<string> GetLoadedModules()
    {
        var modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        IntPtr hSnapshot = IntPtr.Zero;
        try
        {
            hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, (uint)_targetPid);
            if (hSnapshot == IntPtr.Zero || hSnapshot == new IntPtr(-1))
            {
                int error = Marshal.GetLastWin32Error();
                _logger.ZLogWarning($"[ModuleWait] CreateToolhelp32Snapshot failed for PID {_targetPid}. Error: {error}");
                return modules;
            }

            var me32 = new MODULEENTRY32
            {
                dwSize = (uint)Marshal.SizeOf(typeof(MODULEENTRY32))
            };

            if (Module32First(hSnapshot, ref me32))
            {
                do
                {
                    modules.Add(me32.szModule);
                } while (Module32Next(hSnapshot, ref me32));
            }
        }
        finally
        {
            if (hSnapshot != IntPtr.Zero && hSnapshot != new IntPtr(-1))
            {
                CloseHandle(hSnapshot);
            }
        }

        return modules;
    }

    public void WaitForModules()
    {
        _logger.ZLogInformation($"[ModuleWait] Waiting for {_options.RequiredModules.Length} modules to load: [{string.Join(", ", _options.RequiredModules)}]");

        var startTime = DateTime.UtcNow;
        while (true)
        {
            var loadedModules = GetLoadedModules();

            var missingModules = _options.RequiredModules.Where(m => !loadedModules.Contains(m)).ToArray();

            if (missingModules.Length == 0)
            {
                _logger.ZLogInformation($"[ModuleWait] All required modules loaded!");
                return;
            }

            _logger.ZLogDebug($"[ModuleWait] Missing modules: {string.Join(", ", missingModules)}, waiting {_options.CheckIntervalMs}ms...");

            var elapsed = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            if (elapsed > _options.TimeoutMs)
            {
                throw new TimeoutException(
                    $"[ModuleWait] Timeout waiting for modules after {_options.TimeoutMs}ms. " +
                    $"Missing: [{string.Join(", ", missingModules)}]");
            }

            Thread.Sleep(_options.CheckIntervalMs);
        }
    }

    #region P/Invoke Declarations

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool Module32First(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool Module32Next(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Auto)]
    private struct MODULEENTRY32
    {
        public uint dwSize;
        public uint th32ModuleID;
        public uint th32ProcessID;
        public uint GlblcntUsage;
        public uint ProccntUsage;
        public IntPtr modBaseAddr;
        public uint modBaseSize;
        public IntPtr hModule;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szModule;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExePath;
    }

    #endregion
}
