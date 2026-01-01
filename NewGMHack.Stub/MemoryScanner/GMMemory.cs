using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZLogger;
using NewGMHack.CommunicationModel.Models;
using NewGMHack.Stub.Services.Scanning;

namespace NewGMHack.Stub.MemoryScanner
{
    public class GmMemory
    {
        private readonly ILogger<GmMemory> logger;
        private readonly IMemoryScanner _scanner;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(
            IntPtr hProcess, IntPtr     lpBaseAddress, [Out] byte[] lpBuffer,
            int    dwSize,   out IntPtr lpNumberOfBytesRead);

        public GmMemory(ILogger<GmMemory> logger, IMemoryScanner scanner)
        {
            this.logger = logger;
            _scanner = scanner;
        }

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

        public MachineBaseInfo? GetCachedMachine(uint id) => _machineCache.TryGetValue(id, out var v) ? v : null;
        public SkillBaseInfo? GetCachedSkill(uint id) => _skillCache.TryGetValue(id, out var v) ? v : null;
        public WeaponBaseInfo? GetCachedWeapon(uint id) => _weaponCache.TryGetValue(id, out var v) ? v : null;
        public IEnumerable<MachineBaseInfo> GetAllCachedMachines() => _machineCache.Values;
        public IEnumerable<SkillBaseInfo> GetAllCachedSkills() => _skillCache.Values;
        public IEnumerable<WeaponBaseInfo> GetAllCachedWeapons() => _weaponCache.Values;

        #endregion

        #region Buffer Sizes

        private const int MACHINE_BUFFER_SIZE = 0x290;
        private const int SKILL_BUFFER_SIZE   = 0x300;
        private const int WEAPON_BUFFER_SIZE  = 0x140; 

        #endregion

        #region Public Scan Methods

        public async Task<MachineBaseInfo?> ScanMachine(uint id, CancellationToken token)
        {
            if (id == 0) return null;
            if (_machineCache.TryGetValue(id, out var cached))
            {
                logger.ZLogInformation($"ScanMachine cache hit: {id}");
                return cached;
            }

            var (pattern, mask) = BuildMachinePattern(id);
            var result = await ScanGeneric(
                                           id,
                                           pattern,
                                           mask,
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
                                           null, // Exact match
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
                                           null, // Exact match
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

        public async Task<MachineBaseInfo?> ScanMachineWithDetails(uint id, CancellationToken token)
        {
            var machineInfo = await ScanMachine(id, token);
            if (machineInfo == null) return null;

            var skillTask = AssignSkillsAsync(machineInfo, token);
            var weaponTask = AssignWeaponsAsync(machineInfo, token);

            await Task.WhenAll(skillTask, weaponTask);

            if (machineInfo.HasTransform && machineInfo.TransformId != 0 &&
                machineInfo.TransformId + 1 != machineInfo.MachineId)
            {
                machineInfo.TransformedMachine = await ScanMachine(machineInfo.TransformId, token);
                if (machineInfo.TransformedMachine is not null)
                {
                    machineInfo.TransformedMachine.Skill1Info = machineInfo.Skill1Info;
                    machineInfo.TransformedMachine.Skill2Info = machineInfo.Skill2Info;
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
            string?                              mask,
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
                
                // Use the injected memory scanner
                var addresses = await _scanner.ScanAsync(process, pattern, mask, token);
                
                sw.Stop();
                logger.ZLogInformation($"Scan{typeName} found {addresses.Count} addresses in {sw.ElapsedMilliseconds}ms");

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

                        if (!validateRaw(span, id)) continue;

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
            if ((raw.SkillId & 0x00FFFFFF) != (expectedId & 0x00FFFFFF)) return false;
            return true;
        }

        private static bool ValidateWeaponData(ReadOnlySpan<byte> data, uint expectedId)
        {
            var raw = MemoryMarshal.Read<WeaponBaseInfoStruct>(data);
            if ((raw.WeaponId & 0x0000FFFF) != (expectedId & 0x0000FFFF)) return false;
            return true;
        }

        #endregion

        #region Parse Functions

        private static MachineBaseInfo? ParseMachineData(ReadOnlySpan<byte> data)
        {
            var raw  = MemoryMarshal.Read<MachineBaseInfoStruct>(data);
            var info = MachineBaseInfo.FromRaw(raw);

            if (string.IsNullOrWhiteSpace(info.ChineseName) && string.IsNullOrWhiteSpace(info.EnglishName))
                return null;

            var path = info.MdrsFilePath?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (!path.StartsWith("mdrs\\", StringComparison.OrdinalIgnoreCase)) return null;

            if (info.Weapon1Code == 0 && info is { Weapon2Code: 0, Weapon3Code: 0 }) return null;
            if (info is { SkillID1 : 0, SkillID2 : 0 }) return null;

            return info;
        }

        private static SkillBaseInfo? ParseSkillData(ReadOnlySpan<byte> data)
        {
            var raw  = MemoryMarshal.Read<SkillBaseInfoStruct>(data);
            var info = SkillBaseInfo.FromRaw(raw);

            if (string.IsNullOrWhiteSpace(info.SkillName)) return null;
            if (string.IsNullOrEmpty(info.Description)) return null;
            if(!string.IsNullOrEmpty(info.AuraEffect) && !info.AuraEffect.StartsWith(@"fxrs\",StringComparison.OrdinalIgnoreCase))return null;
            if (info.ExactHpActivatePercent > 100) return null;
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
            
            if (string.IsNullOrWhiteSpace(info.WeaponName)) return null;
            if (raw.WeaponType == 0 || raw.WeaponType > 10) return null;
            if (!string.IsNullOrEmpty(info.TraceEffect) && !info.TraceEffect.StartsWith(@"fxrs\",StringComparison.OrdinalIgnoreCase)) return null;
            if (!string.IsNullOrEmpty(info.AttackEffect) && !info.AttackEffect.StartsWith(@"fxrs\",StringComparison.OrdinalIgnoreCase)) return null;
            if (!string.IsNullOrEmpty(info.AttackSound)  && !info.AttackSound.StartsWith(@"sdrs\",StringComparison.OrdinalIgnoreCase)) return null;
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
        /// Machine pattern: 4-byte little-endian + WILDCARDS + mdrs signature
        /// </summary>
        private static (byte[] pattern, string mask) BuildMachinePattern(uint id)
        {
            // Total size needed: offset (0x264) + signature length (10) = 622 bytes
            int totalSize = 0x264 + 10;
            var patternBytes = new byte[totalSize];
            var maskChars = new char[totalSize];

            // Initialize mask with wildcards
            for (int i = 0; i < totalSize; i++) maskChars[i] = '?';

            // 1. ID at offset 0 (4 bytes)
            var idBytes = BitConverter.GetBytes(id);
            Array.Copy(idBytes, 0, patternBytes, 0, 4);
            for (int i = 0; i < 4; i++) maskChars[i] = 'x';

            // 2. Signature "mdrs\" at offset 0x264
            var mdrsSig = new byte[] 
            { 
                0x6D, 0x00, 0x64, 0x00, 0x72, 0x00, 0x73, 0x00, 0x5C, 0x00 
            };
            Array.Copy(mdrsSig, 0, patternBytes, 0x264, mdrsSig.Length);
            for (int i = 0; i < mdrsSig.Length; i++) maskChars[0x264 + i] = 'x';

            return (patternBytes, new string(maskChars));
        }

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
    }
}
