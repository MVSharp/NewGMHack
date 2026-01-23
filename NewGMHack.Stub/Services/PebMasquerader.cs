using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using GmHack.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace NewGMHack.Stub.Services
{
    public class PebMasquerader : BackgroundService
    {
        private readonly ILogger<PebMasquerader> _logger;

        public PebMasquerader(ILogger<PebMasquerader> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[Stealth] PEB Masquerading started.");
            
            try 
            {
                MasqueradePEB();
                _logger.LogInformation("[Stealth] PEB Masquerading applied successfully.");
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "[Stealth] Failed to masquerade PEB.");
            }

            // Keep the service alive just in case we need to re-apply or monitor, 
            // though PEB changes are usually permanent for the process lifetime.
            await Task.Delay(-1, stoppingToken);
        }

        private void MasqueradePEB()
        {
            var pebAddress = GetPebAddress();
            if (pebAddress == IntPtr.Zero)
            {
                _logger.LogError("[Stealth] Could not locate PEB.");
                return;
            }
            
            // Read ProcessParameters pointer from PEB
            // PEB.ProcessParameters is at offset 0x20 on x64 (usually) and 0x10 on x86
            // NOTE: Assuming x86 target based on NewGMHack.Stub context (often game hacking is x86 or x64 specific)
            // If the game is x86 (32-bit):
            //   ProcessParameters is at PEB + 0x10
            // If x64:
            //   ProcessParameters is at PEB + 0x20
            
            bool is64Bit = IntPtr.Size == 8;
            int processParamsOffset = is64Bit ? 0x20 : 0x10;
            
            IntPtr processParamsPtr = Marshal.ReadIntPtr(pebAddress + processParamsOffset);
            
            if (processParamsPtr == IntPtr.Zero)
            {
                _logger.LogError("[Stealth] ProcessParameters pointer is null.");
                return;
            }

            // RTL_USER_PROCESS_PARAMETERS struct offsets (approximate for Windows)
            // ImagePathName is a UNICODE_STRING
            // x86: ImagePathName at 0x38, CommandLine at 0x40
            // x64: ImagePathName at 0x60, CommandLine at 0x70
            
            int imagePathNameOffset = is64Bit ? 0x60 : 0x38;
            int commandLineOffset = is64Bit ? 0x70 : 0x40;

            // Generate fake name
            string fakeName = $"C:\\Windows\\System32\\svchost.exe_{Guid.NewGuid().ToString("N").Substring(0, 5)}";
            string fakeCmd = is64Bit ? $"C:\\Windows\\System32\\svchost.exe -k {Guid.NewGuid()}" : fakeName; // Keep simple

            OverwriteUnicodeString(processParamsPtr + imagePathNameOffset, fakeName);
            OverwriteUnicodeString(processParamsPtr + commandLineOffset, fakeCmd);
        }

        private void OverwriteUnicodeString(IntPtr stringStructPtr, string newString)
        {
            // UNICODE_STRING structure:
            // USHORT Length;
            // USHORT MaximumLength;
            // PWSTR  Buffer;
            
            short length = Marshal.ReadInt16(stringStructPtr);
            short maxLength = Marshal.ReadInt16(stringStructPtr + 2);
            IntPtr bufferPtr = Marshal.ReadIntPtr(stringStructPtr + (IntPtr.Size == 8 ? 8 : 4)); // Alignment padding
            
            // If we just overwrite the pointer, we leak memory and might crash if logic assumes the buffer is in a specific heap block.
            // Safer strategy for "stealth": Overwrite the CONTENT of the existing buffer if it fits. 
            // If we want to be robust, we should allocate new memory, copy string, and point Buffer to it.
            
            // Let's allocate new unmanaged memory to be safe and set the pointer.
            // This avoids buffer overflow if our new string is longer than the old one.
            
            byte[] stringBytes = Encoding.Unicode.GetBytes(newString + "\0");
            IntPtr newBuffer = Marshal.AllocHGlobal(stringBytes.Length);
            Marshal.Copy(stringBytes, 0, newBuffer, stringBytes.Length);
            
            // Update struct
            Marshal.WriteInt16(stringStructPtr, 0, (short)(stringBytes.Length - 2)); // Length (no null)
            Marshal.WriteInt16(stringStructPtr, 2, (short)stringBytes.Length);       // MaxLength
            
            // Write new buffer pointer
            if (IntPtr.Size == 8)
                Marshal.WriteInt64(stringStructPtr + 8, newBuffer.ToInt64());
            else
                Marshal.WriteInt32(stringStructPtr + 4, newBuffer.ToInt32());
            
            _logger.LogInformation($"[Stealth] Overwrote UNICODE_STRING at {stringStructPtr.ToString("X")} with '{newString}'");
        }

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(
            IntPtr ProcessHandle,
            int ProcessInformationClass,
            outPROCESS_BASIC_INFORMATION ProcessInformation,
            int ProcessInformationLength,
            out int ReturnLength);

        [StructLayout(LayoutKind.Sequential)]
        private struct outPROCESS_BASIC_INFORMATION
        {
            public IntPtr ExitStatus;
            public IntPtr PebBaseAddress;
            public IntPtr AffinityMask;
            public IntPtr BasePriority;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        private IntPtr GetPebAddress()
        {
            if (IntPtr.Size == 4)
            {
                // x86 inline assembler substitute or intrinsic
                // But in C#, we can use NtQueryInformationProcess with ProcessBasicInformation (0)
                return GetPebViaNtQuery();
            }
            else
            {
                // x64
                return GetPebViaNtQuery();
            }
        }

        private IntPtr GetPebViaNtQuery()
        {
            var pbi = new outPROCESS_BASIC_INFORMATION();
            int status = NtQueryInformationProcess(
                Process.GetCurrentProcess().Handle, 
                0, // ProcessBasicInformation
                pbi, 
                Marshal.SizeOf(pbi), 
                out int returnLength);

            return pbi.PebBaseAddress;
        }
    }
}
