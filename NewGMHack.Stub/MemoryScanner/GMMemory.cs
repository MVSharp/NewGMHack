using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZLogger;
using NewGMHack.CommunicationModel.Models;

namespace NewGMHack.Stub.MemoryScanner
{
    /// <summary>
    /// Fast memory scanner using SIMD (Vector&lt;byte&gt;) for pattern matching
    /// </summary>
    public class GmMemory(ILogger<GmMemory> logger)
    {
        #region Win32 APIs

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(
            IntPtr hProcess, IntPtr     lpBaseAddress, [Out] byte[] lpBuffer,
            int    dwSize,   out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool VirtualQueryEx(
            IntPtr                       hProcess, IntPtr lpAddress,
            out MEMORY_BASIC_INFORMATION lpBuffer, uint   dwLength);

        [StructLayout(LayoutKind.Sequential)]
        struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint   AllocationProtect;
            public IntPtr RegionSize;
            public uint   State;
            public uint   Protect;
            public uint   Type;
        }

        private const uint MEM_COMMIT             = 0x1000;
        private const uint MEM_PRIVATE            = 0x20000;  // Private memory (not mapped/image)
        private const uint MEM_IMAGE              = 0x1000000; // Memory mapped from image
        private const uint PAGE_READWRITE         = 0x04;
        private const uint PAGE_READONLY          = 0x02;
        private const uint PAGE_EXECUTE_READ      = 0x20;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        #endregion

        #region Caches

        private readonly ConcurrentDictionary<uint, MachineBaseInfo> _machineCache = new();
        private readonly ConcurrentDictionary<uint, SkillBaseInfo>   _skillCache   = new();
        private readonly ConcurrentDictionary<uint, WeaponBaseInfo>  _weaponCache  = new();

        public void CleanMachineCache() => _machineCache.Clear();
        public void CleanSkillCache()   => _skillCache.Clear();
        public void CleanWeaponCache()  => _weaponCache.Clear();

        public void CleanAllCaches()
        {
            _machineCache.Clear();
            _skillCache.Clear();
            _weaponCache.Clear();
        }

        // Cache getters for API access
        public MachineBaseInfo? GetCachedMachine(uint id) => _machineCache.TryGetValue(id, out var v) ? v : null;
        public SkillBaseInfo? GetCachedSkill(uint id) => _skillCache.TryGetValue(id, out var v) ? v : null;
        public WeaponBaseInfo? GetCachedWeapon(uint id) => _weaponCache.TryGetValue(id, out var v) ? v : null;
        public IEnumerable<MachineBaseInfo> GetAllCachedMachines() => _machineCache.Values;
        public IEnumerable<SkillBaseInfo> GetAllCachedSkills() => _skillCache.Values;
        public IEnumerable<WeaponBaseInfo> GetAllCachedWeapons() => _weaponCache.Values;

        #endregion

        #region Buffer Sizes

        private const int MACHINE_BUFFER_SIZE = 0x290; // 656 bytes (matches MachineBaseInfoStruct)
        private const int SKILL_BUFFER_SIZE   = 0x300; // 768 bytes (matches SkillBaseInfoStruct)
        private const int WEAPON_BUFFER_SIZE  = 0xB0;  // 176 bytes

        #endregion

        #region Public Scan Methods

        /// <summary>
        /// Scan for machine info by ID
        /// </summary>
        public async Task<MachineBaseInfo?> ScanMachine(uint id, CancellationToken token)
        {
            if (id == 0) return null;
            if (_machineCache.TryGetValue(id, out var cached))
            {
                logger.ZLogInformation($"ScanMachine cache hit: {id}");
                return cached;
            }

            var result = await ScanGeneric(
                                           id,
                                           BuildMachinePattern(id),
                                           MACHINE_BUFFER_SIZE,
                                           ValidateMachineData,
                                           ParseMachineData,
                                           "Machine",
                                           token);

            if (result != null)
            {
                _machineCache.TryAdd(id, result);
                LogMachineResult(result);
            }

            return result;
        }

        /// <summary>
        /// Scan for skill info by ID
        /// </summary>
        public async Task<SkillBaseInfo?> ScanSkill(uint skillId, CancellationToken token)
        {
            if (skillId == 0) return null;
            if (_skillCache.TryGetValue(skillId, out var cached))
            {
                logger.ZLogInformation($"ScanSkill cache hit: {skillId}");
                return cached;
            }

            var result = await ScanGeneric(
                                           skillId,
                                           BuildSkillPattern(skillId),
                                           SKILL_BUFFER_SIZE,
                                           ValidateSkillData,
                                           ParseSkillData,
                                           "Skill",
                                           token);

            if (result != null)
            {
                _skillCache.TryAdd(skillId, result);
                LogSkillResult(result);
            }

            return result;
        }

        /// <summary>
        /// Scan for weapon info by ID
        /// </summary>
        public async Task<WeaponBaseInfo?> ScanWeapon(uint weaponId, CancellationToken token)
        {
            if (weaponId == 0) return null;
            if (_weaponCache.TryGetValue(weaponId, out var cached))
            {
                logger.ZLogInformation($"ScanWeapon cache hit: {weaponId}");
                return cached;
            }

            var result = await ScanGeneric(
                                           weaponId,
                                           BuildWeaponPattern(weaponId),
                                           WEAPON_BUFFER_SIZE,
                                           ValidateWeaponData,
                                           ParseWeaponData,
                                           "Weapon",
                                           token);

            if (result != null)
            {
                _weaponCache.TryAdd(weaponId, result);
                LogWeaponResult(result);
            }

            return result;
        }

        /// <summary>
        /// Scan for machine and also populate skill, weapon, and transform info
        /// </summary>
        public async Task<MachineBaseInfo?> ScanMachineWithDetails(uint id, CancellationToken token)
        {
            var machineInfo = await ScanMachine(id, token);
            if (machineInfo == null) return null;

            // Run skill and weapon scans in parallel for the base machine
            var skillTask = AssignSkillsAsync(machineInfo, token);
            var weaponTask = AssignWeaponsAsync(machineInfo, token);

            await Task.WhenAll(skillTask, weaponTask);

            // Scan transformed machine (prevent duplicate if TransformId+1 == MachineId)
            if (machineInfo.HasTransform && machineInfo.TransformId != 0 &&
                machineInfo.TransformId + 1 != machineInfo.MachineId)
            {
                machineInfo.TransformedMachine = await ScanMachine(machineInfo.TransformId, token);
                if (machineInfo.TransformedMachine is not null)
                {
                    // Copy skills from base
                    machineInfo.TransformedMachine.Skill1Info = machineInfo.Skill1Info;
                    machineInfo.TransformedMachine.Skill2Info = machineInfo.Skill2Info;

                    // Scan weapons for transformed
                    await AssignWeaponsAsync(machineInfo.TransformedMachine, token);
                }
            }

            return machineInfo;
        }

        private async Task AssignSkillsAsync(MachineBaseInfo info, CancellationToken token)
        {
            var tasks = new List<Task<SkillBaseInfo?>>();
            Task<SkillBaseInfo?>? t1 = null, t2 = null;

            if (info.SkillID1 != 0) t1 = ScanSkill(info.SkillID1, token);
            if (info.SkillID2 != 0) t2 = ScanSkill(info.SkillID2, token);

            if (t1 != null) tasks.Add(t1);
            if (t2 != null) tasks.Add(t2);

            if (tasks.Count > 0) await Task.WhenAll(tasks);

            if (t1 != null) info.Skill1Info = await t1;
            if (t2 != null) info.Skill2Info = await t2;
        }

        private async Task AssignWeaponsAsync(MachineBaseInfo info, CancellationToken token)
        {
            var tasks = new List<Task<WeaponBaseInfo?>>();
            Task<WeaponBaseInfo?>? w1 = null, w2 = null, w3 = null, sp = null;

            if (info.Weapon1Code != 0) w1 = ScanWeapon(info.Weapon1Code, token);
            if (info.Weapon2Code != 0) w2 = ScanWeapon(info.Weapon2Code, token);
            if (info.Weapon3Code != 0) w3 = ScanWeapon(info.Weapon3Code, token);
            if (info.SpecialAttackCode != 0) sp = ScanWeapon(info.SpecialAttackCode, token);

            if (w1 != null) tasks.Add(w1);
            if (w2 != null) tasks.Add(w2);
            if (w3 != null) tasks.Add(w3);
            if (sp != null) tasks.Add(sp);

            if (tasks.Count > 0) await Task.WhenAll(tasks);

            if (w1 != null) info.Weapon1Info = await w1;
            if (w2 != null) info.Weapon2Info = await w2;
            if (w3 != null) info.Weapon3Info = await w3;
            if (sp != null) info.SpecialAttack = await sp;
        }

        #endregion

        #region Generic Scan Implementation

        private async Task<T?> ScanGeneric<T>(
            uint                                 id,
            byte[]                               pattern,
            int                                  bufferSize,
            Func<ReadOnlySpan<byte>, uint, bool> validateRaw,
            Func<ReadOnlySpan<byte>, T?>         parseData,
            string                               typeName,
            CancellationToken                    token) where T : class
        {
            logger.ZLogInformation($"Scan{typeName} Input: {id}");
            try
            {
                string processName = Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ=="));
                var    process     = Process.GetProcessesByName(processName).FirstOrDefault();
                if (process == null)
                {
                    logger.ZLogInformation($"Process not found");
                    return null;
                }

                var sw        = Stopwatch.StartNew();
                var addresses = await Task.Run(() => ScanProcessMemorySIMD(process, pattern), token);
                sw.Stop();
                logger.ZLogInformation($"Scan{typeName} SIMD found {addresses.Count} addresses in {sw.ElapsedMilliseconds}ms");

                if (addresses.Count == 0)
                {
                    logger.ZLogInformation($"Not found {typeName.ToLower()}:{id}");
                    return null;
                }

                var    bufferPool   = ArrayPool<byte>.Shared;
                byte[] pooledBuffer = bufferPool.Rent(bufferSize);

                try
                {
                    foreach (var address in addresses)
                    {
                        bool success =
                            ReadProcessMemory(process.Handle, (IntPtr)address, pooledBuffer, bufferSize, out _);
                        if (!success) continue;

                        var span = pooledBuffer.AsSpan(0, bufferSize);

                        // Validate raw data
                        if (!validateRaw(span, id)) continue;

                        // Parse and validate transformed data
                        var result = parseData(span);
                        if (result == null) continue;

                        logger.ZLogInformation($"Found {typeName.ToLower()} at addr:0x{address:X}");
                        return result;
                    }
                }
                finally
                {
                    bufferPool.Return(pooledBuffer);
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.ZLogInformation($"Scan{typeName} Error: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Validation Functions

        private static bool ValidateMachineData(ReadOnlySpan<byte> data, uint expectedId)
        {
            var raw = MemoryMarshal.Read<MachineBaseInfoStruct>(data);
            if (raw.MachineId != expectedId) return false;
            return true;
        }

        private static bool ValidateSkillData(ReadOnlySpan<byte> data, uint expectedId)
        {
            var raw = MemoryMarshal.Read<SkillBaseInfoStruct>(data);
            // Match lower 24 bits
            if ((raw.SkillId & 0x00FFFFFF) != (expectedId & 0x00FFFFFF)) return false;
            return true;
        }

        private static bool ValidateWeaponData(ReadOnlySpan<byte> data, uint expectedId)
        {
            var raw = MemoryMarshal.Read<WeaponBaseInfoStruct>(data);
            // Match lower 16 bits
            if ((raw.WeaponId & 0x0000FFFF) != (expectedId & 0x0000FFFF)) return false;
            return true;
        }

        #endregion

        #region Parse Functions

        private static MachineBaseInfo? ParseMachineData(ReadOnlySpan<byte> data)
        {
            var raw  = MemoryMarshal.Read<MachineBaseInfoStruct>(data);
            var info = MachineBaseInfo.FromRaw(raw);

            // Validation: name or MdrsFilePath must not be blank
            if (string.IsNullOrWhiteSpace(info.ChineseName) && string.IsNullOrWhiteSpace(info.EnglishName))
                return null;

            // Validation: MdrsFilePath must start with "mdrs\\" and end with ".mod" after trim
            var path = info.MdrsFilePath?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (!path.StartsWith("mdrs\\", StringComparison.OrdinalIgnoreCase)) return null;
            //if (!path.EndsWith(".mod", StringComparison.OrdinalIgnoreCase)) return null;

            // Validation: SkillID and Weapon codes must be <= 99999
            const uint MAX_ID = 99999;
            if (info.Weapon1Code == 0 && info is { Weapon2Code: 0, Weapon3Code: 0 }) return null;
            if (info is { SkillID1 : 0, SkillID2 : 0 }) return null;
            //if ((info.Weapon1Code != 0 && info.Weapon2Code != 0 && info.Weapon1Code + 1 != info.Weapon2Code) ||
            //    (info.Weapon2Code != 0 && info.Weapon3Code != 0 && info.Weapon2Code + 1 != info.Weapon3Code))
            //    return null;
            //if (info.SkillID1    > MAX_ID || info.SkillID2    > MAX_ID) return null;
            //if (info.Weapon1Code > MAX_ID || info.Weapon2Code > MAX_ID || info.Weapon3Code > MAX_ID) return null;

            return info;
        }

        private static SkillBaseInfo? ParseSkillData(ReadOnlySpan<byte> data)
        {
            var raw  = MemoryMarshal.Read<SkillBaseInfoStruct>(data);
            var info = SkillBaseInfo.FromRaw(raw);

            // Validation: skill name must not be blank
            if (string.IsNullOrWhiteSpace(info.SkillName)) return null;
            if (string.IsNullOrEmpty(info.Description)) return null;
            if (info.AttackIncrease         > 255 || info.DefenseIncrease > 255 || info.AgilityPercent > 100 ||
                info.ExactHpActivatePercent > 100) return null;

            if (info.AttackIncrease         < -255 || info.DefenseIncrease < -255 || info.AgilityPercent < 0 ||
                info.ExactHpActivatePercent < 0) return null;
            return info;
        }

        private static WeaponBaseInfo? ParseWeaponData(ReadOnlySpan<byte> data)
        {
            var raw = MemoryMarshal.Read<WeaponBaseInfoStruct>(data);
            var info = WeaponBaseInfo.FromRaw(raw);
            
            // Validation: weapon name must not be blank
            if (string.IsNullOrWhiteSpace(info.WeaponName)) return null;
            
            // Validation: WeaponType == 0 or > 10 is invalid
            if (raw.WeaponType == 0 || raw.WeaponType > 10) return null;
            
            // Validation: KnockdownPerHit > KnockdownThreshold is invalid
            if (raw.KnockdownPerHit > raw.KnockdownThreshold && raw.KnockdownThreshold != 0) return null;
            
            return info;
        }
        
        #endregion

        #region Logging Functions
        
        private void LogMachineResult(MachineBaseInfo info)
        {
            logger.ZLogInformation($"[Machine] ID:{info.MachineId} {info.MdrsFilePath}  Name:{info.ChineseName}/{info.EnglishName} " +
                $"Rank:{info.Rank} HP:{info.HP} Combat:{info.CombatType} Skills:{info.SkillID1},{info.SkillID2}");
        }

        private void LogSkillResult(SkillBaseInfo info)
        {
            logger.ZLogInformation($"[Skill] ID:{info.SkillId} Name:{info.SkillName} " +
                $"Movement:{info.Movement} Atk:{info.AttackIncrease} Def:{info.DefenseIncrease}");
        }

        private void LogWeaponResult(WeaponBaseInfo info)
        {
            logger.ZLogInformation($"[Weapon] ID:{info.WeaponId} Name:{info.WeaponName} " +
                $"Type:{info.WeaponType} Dmg:{info.WeaponDamage} Range:{info.WeaponRange} Ammo:{info.AmmoCount}");
        }
        
        #endregion

        #region Pattern Builders
        
        /// <summary>
        /// Machine pattern: 4-byte little-endian + 00 00
        /// </summary>
        private static byte[] BuildMachinePattern(uint id)
        {
            string hex = id.ToString("X").PadLeft(4, '0');
            return new byte[]
            {
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(0, 2), 16),
                0x00,
                0x00
            };
        }

        /// <summary>
        /// Skill pattern: 3-byte little-endian + 00 00
        /// Example: 70121 (0x011189) -> E9 11 01 00 00
        /// </summary>
        private static byte[] BuildSkillPattern(uint skillId)
        {
            return new byte[]
            {
                (byte)(skillId & 0xFF),
                (byte)((skillId >> 8) & 0xFF),
                (byte)((skillId >> 16) & 0xFF),
                0x00,
                0x00
            };
        }

        /// <summary>
        /// Weapon pattern: 2-byte little-endian + 00 00 00
        /// Example: 28751 (0x704F) -> 4F 70 00 00 00
        /// </summary>
        private static byte[] BuildWeaponPattern(uint weaponId)
        {
            return new byte[]
            {
                (byte)(weaponId & 0xFF),
                (byte)((weaponId >> 8) & 0xFF),
                0x00,
                0x00,
                0x00
            };
        }
        
        #endregion

        #region SIMD Memory Scan
        
        private List<long> ScanProcessMemorySIMD(Process process, byte[] pattern)
        {
            var results = new List<long>();
            var bufferPool = ArrayPool<byte>.Shared;
            
            IntPtr address = IntPtr.Zero;
            IntPtr maxAddress = (IntPtr)0x7FFFFFFF;

            byte firstByte = pattern[0];
            int patternLength = pattern.Length;

            while (address.ToInt64() < maxAddress.ToInt64())
            {
                if (!VirtualQueryEx(process.Handle, address, out var mbi, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()))
                    break;
                
                bool isValidType = (mbi.Type == MEM_PRIVATE || mbi.Type == MEM_IMAGE);
                if (mbi.State == MEM_COMMIT && isValidType && IsReadable(mbi.Protect))
                {
                    int regionSize = (int)mbi.RegionSize.ToInt64();
                    
                    if (regionSize > 0 && regionSize <= 32 * 1024 * 1024)
                    {
                        byte[] buffer = bufferPool.Rent(regionSize);
                        try
                        {
                            if (ReadProcessMemory(process.Handle, mbi.BaseAddress, buffer, regionSize, out var bytesRead))
                            {
                                int actualSize = (int)bytesRead.ToInt64();
                                if (actualSize >= patternLength)
                                {
                                    var matches = FindPatternSIMD(buffer.AsSpan(0, actualSize), pattern, firstByte);
                                    foreach (var offset in matches)
                                    {
                                        results.Add(mbi.BaseAddress.ToInt64() + offset);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            bufferPool.Return(buffer);
                        }
                    }
                }

                address = (IntPtr)(mbi.BaseAddress.ToInt64() + mbi.RegionSize.ToInt64());
                if (mbi.RegionSize == IntPtr.Zero) break;
            }

            return results;
        }

        private static List<int> FindPatternSIMD(ReadOnlySpan<byte> data, byte[] pattern, byte firstByte)
        {
            var results = new List<int>();
            int patternLength = pattern.Length;
            int dataLength = data.Length;
            int vectorSize = Vector<byte>.Count;
            
            if (dataLength < patternLength) return results;

            Vector<byte> searchVector = new Vector<byte>(firstByte);
            
            int i = 0;
            int limit = dataLength - patternLength;
            int vectorLimit = limit - vectorSize + 1;

            while (i < vectorLimit)
            {
                var chunk = new Vector<byte>(data.Slice(i, vectorSize));
                var equals = Vector.Equals(chunk, searchVector);
                
                if (equals != Vector<byte>.Zero)
                {
                    for (int j = 0; j < vectorSize && i + j <= limit; j++)
                    {
                        if (data[i + j] == firstByte && MatchPattern(data.Slice(i + j), pattern))
                        {
                            results.Add(i + j);
                        }
                    }
                }
                i += vectorSize;
            }

            while (i <= limit)
            {
                if (data[i] == firstByte && MatchPattern(data.Slice(i), pattern))
                {
                    results.Add(i);
                }
                i++;
            }

            return results;
        }

        private static bool MatchPattern(ReadOnlySpan<byte> data, byte[] pattern)
        {
            for (int i = 0; i < pattern.Length; i++)
            {
                if (data[i] != pattern[i]) return false;
            }
            return true;
        }

        private static bool IsReadable(uint protect)
        {
            return protect == PAGE_READONLY ||
                   protect == PAGE_READWRITE ||
                   protect == PAGE_EXECUTE_READ ||
                   protect == PAGE_EXECUTE_READWRITE;
        }
        
        #endregion
    }
}
