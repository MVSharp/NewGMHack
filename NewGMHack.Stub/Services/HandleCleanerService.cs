using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace NewGMHack.Stub.Services
{
    public class HandleCleanerService : BackgroundService
    {
        private readonly ILogger<HandleCleanerService> _logger;
        private readonly int _currentPid;

        public HandleCleanerService(ILogger<HandleCleanerService> logger)
        {
            _logger = logger;
            _currentPid = Process.GetCurrentProcess().Id;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.ZLogInformation($"[HandleCleaner] Started. Waiting for initialization...");
            // Wait a bit for the game to create its initial locks
            await Task.Delay(5000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    CleanHandles();
                }
                catch (Exception ex)
                {
                    _logger.ZLogError(ex, $"[HandleCleaner] Error scrubbing handles.");
                }

                // Run periodically
                await Task.Delay(10000, stoppingToken);
            }
        }

        private void CleanHandles()
        {
            var handles = GetSystemHandles(_currentPid);
            
            foreach (var hInfo in handles)
            {
                if (IsRestrictedHandle(hInfo.HandleValue))
                {
                    continue; 
                }

                string type = GetHandleType(hInfo.HandleValue);
                if (type == "Mutant" || type == "Event" || type == "Section" || type == "Semaphore")
                {
                    string name = GetHandleName(hInfo.HandleValue);
                    
                    if (!string.IsNullOrEmpty(name))
                    {
                        // --- WHITELIST: Do NOT close these ---
                        if (name.Contains("SdHook", StringComparison.OrdinalIgnoreCase) || 
                            name.Contains("NewGMHack", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Visual Studio", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("MsFteWds", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Reloaded", StringComparison.OrdinalIgnoreCase) ||    // Reloaded Libs
                            name.Contains("WilStaging", StringComparison.OrdinalIgnoreCase) ||  // Windows Internal
                            name.Contains("WilError", StringComparison.OrdinalIgnoreCase) ||    // Windows Internal
                            name.Contains("DwmDx", StringComparison.OrdinalIgnoreCase) ||       // Desktop Window Manager
                            name.Contains("DirectSound", StringComparison.OrdinalIgnoreCase) || // Audio
                            name.Contains("DirectInput", StringComparison.OrdinalIgnoreCase) || // Input
                            name.Contains("windows_shell", StringComparison.OrdinalIgnoreCase)) // Shell
                        {
                            continue;
                        }

                        // --- TARGETS: Close these ---
                        // The suspicious one seen in logs: "CDdf212806D6EmB31yE0c"
                        // Or any other random named object in BaseNamedObjects
                        if (name.Contains("BaseNamedObjects") || name.Contains("Global\\"))
                        {
                            _logger.ZLogWarning($"[HandleCleaner] Closing named {type}: {name}");
                            CloseHandle(hInfo.HandleValue);
                        }
                    }
                }
            }
        }

        // --- Native Interop ---

        [DllImport("ntdll.dll")]
        public static extern NTSTATUS NtQuerySystemInformation(
            int SystemInformationClass,
            IntPtr SystemInformation,
            int SystemInformationLength,
            out int ReturnLength);

        [DllImport("ntdll.dll")]
        public static extern NTSTATUS NtQueryObject(
            IntPtr Handle,
            int ObjectInformationClass,
            IntPtr ObjectInformation,
            int ObjectInformationLength,
            out int ReturnLength);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hObject);

        private const int SystemHandleInformation = 16;
        private const int ObjectNameInformation = 1;
        private const int ObjectTypeInformation = 2;
        private const int STATUS_INFO_LENGTH_MISMATCH = unchecked((int)0xC0000004);
        private const int STATUS_SUCCESS = 0x00000000;

        public enum NTSTATUS : uint
        {
            Success = 0x00000000
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_HANDLE_INFORMATION
        {
            public int NumberOfHandles;
            // SYSTEM_HANDLE_TABLE_ENTRY_INFO Handles[1];
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_HANDLE_TABLE_ENTRY_INFO
        {
            public ushort UniqueProcessId;
            public ushort CreatorBackTraceIndex;
            public byte ObjectTypeIndex;
            public byte HandleAttributes;
            public ushort HandleValue;
            public IntPtr Object;
            public int GrantedAccess;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct OBJECT_NAME_INFORMATION
        {
            public UNICODE_STRING Name;
        }

        private List<(IntPtr HandleValue, int Access)> GetSystemHandles(int pid)
        {
            List<(IntPtr, int)> result = new();
            int size = 0x10000;
            IntPtr buffer = Marshal.AllocHGlobal(size);

            try
            {
                int needed;
                int status = (int)NtQuerySystemInformation(SystemHandleInformation, buffer, size, out needed);

                while (status == STATUS_INFO_LENGTH_MISMATCH)
                {
                    Marshal.FreeHGlobal(buffer);
                    size = needed == 0 ? size * 2 : needed; 
                    buffer = Marshal.AllocHGlobal(size);
                    status = (int)NtQuerySystemInformation(SystemHandleInformation, buffer, size, out needed);
                }

                if (status == STATUS_SUCCESS)
                {
                    int handleCount = Marshal.ReadInt32(buffer);
                    IntPtr ptr = buffer + (IntPtr.Size == 8 ? 8 : 4); 

                    bool is64 = IntPtr.Size == 8; 
                    
                    for (int i = 0; i < handleCount; i++)
                    {
                        ushort entryPid = (ushort)Marshal.ReadInt16(ptr);
                        
                        if (entryPid == pid)
                        {
                            ushort hVal = (ushort)Marshal.ReadInt16(ptr + 6);
                            int access = Marshal.ReadInt32(ptr + (is64 ? 16 : 12));
                            result.Add(((IntPtr)hVal, access));
                        }
                        ptr += (is64 ? 24 : 16); 
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
            return result;
        }

        private string GetHandleType(IntPtr handle)
        {
            int size = 0x2000;
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                int status = (int)NtQueryObject(handle, ObjectTypeInformation, buffer, size, out int _);
                if (status == STATUS_SUCCESS)
                {
                    return ReadUnicodeString(buffer);
                }
            }
            catch {}
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
            return "";
        }

        private string GetHandleName(IntPtr handle)
        {
            int size = 0x2000;
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                int status = (int)NtQueryObject(handle, ObjectNameInformation, buffer, size, out int _);
                if (status == STATUS_SUCCESS)
                {
                    return ReadUnicodeString(buffer);
                }
            }
            catch {}
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
            return "";
        }
        
        private string ReadUnicodeString(IntPtr ptr)
        {
            ushort len = (ushort)Marshal.ReadInt16(ptr);
            IntPtr buf = Marshal.ReadIntPtr(ptr + (IntPtr.Size == 8 ? 8 : 4));
            
            if (buf != IntPtr.Zero && len > 0)
            {
                return Marshal.PtrToStringUni(buf, len / 2);
            }
            return "";
        }

        private bool IsRestrictedHandle(IntPtr handle)
        {
            return false;
        }
    }
}
