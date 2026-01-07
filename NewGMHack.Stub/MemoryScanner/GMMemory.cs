using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZLogger;
using NewGMHack.CommunicationModel.Models;
using NewGMHack.Stub.Services.Scanning;
using NewGMHack.Stub.Services.Caching;

namespace NewGMHack.Stub.MemoryScanner
{
    public class GmMemory
    {
        private readonly ILogger<GmMemory> logger;
        private readonly IMemoryScanner _scanner;
        private readonly IEntityCache<MachineBaseInfo> _machineCache;
        private readonly IEntityCache<SkillBaseInfo> _skillCache;
        private readonly IEntityCache<WeaponBaseInfo> _weaponCache;
        
        /// <summary>
        /// Cache TTL - entries older than this will trigger a rescan
        /// </summary>
        private static readonly TimeSpan CacheTTL = TimeSpan.FromMinutes(60);

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        [DllImport("kernel32.dll")]
        private static extern int VirtualQuery(IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        private const uint MEM_COMMIT = 0x1000;
        private const uint PAGE_READONLY = 0x02;
        private const uint PAGE_READWRITE = 0x04;
        private const uint PAGE_WRITECOPY = 0x08;
        private const uint PAGE_EXECUTE_READ = 0x20;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private const uint PAGE_EXECUTE_WRITECOPY = 0x80;
        private const uint PAGE_GUARD = 0x100;
        private const uint PAGE_NOACCESS = 0x01;

        public GmMemory(
            ILogger<GmMemory> logger, 
            IMemoryScanner scanner,
            IEntityCache<MachineBaseInfo>? machineCache = null,
            IEntityCache<SkillBaseInfo>? skillCache = null,
            IEntityCache<WeaponBaseInfo>? weaponCache = null)
        {
            this.logger = logger;
            _scanner = scanner;
            // Use injected caches or fallback to in-memory
            _machineCache = machineCache ?? new InMemoryEntityCache<MachineBaseInfo>();
            _skillCache = skillCache ?? new InMemoryEntityCache<SkillBaseInfo>();
            _weaponCache = weaponCache ?? new InMemoryEntityCache<WeaponBaseInfo>();
        }

        #region Cache Methods

        public void CleanMachineCache() => _machineCache.Clear();
        public void CleanSkillCache()   => _skillCache.Clear();
        public void CleanWeaponCache()  => _weaponCache.Clear();

        public void CleanAllCaches()
        {
            _machineCache.Clear();
            _skillCache.Clear();
            _weaponCache.Clear();
        }

        public async Task<MachineBaseInfo?> GetCachedMachineAsync(uint id) => await _machineCache.GetAsync(id);
        public async Task<SkillBaseInfo?> GetCachedSkillAsync(uint id) => await _skillCache.GetAsync(id);
        public async Task<WeaponBaseInfo?> GetCachedWeaponAsync(uint id) => await _weaponCache.GetAsync(id);

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
            
            // Check cache with TTL
            if (await _machineCache.IsValidAsync(id, CacheTTL))
            {
                var cached = await _machineCache.GetAsync(id);
                if (cached != null)
                {
                    //logger.ZLogInformation($"ScanMachine cache hit: {id}");
                    return cached;
                }
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
                await _machineCache.SetAsync(id, result);
                LogMachineResult(result);
            }

            return result;
        }

        public async Task<SkillBaseInfo?> ScanSkill(uint skillId, CancellationToken token)
        {
            if (skillId == 0) return null;
            
            // Check cache with TTL
            if (await _skillCache.IsValidAsync(skillId, CacheTTL))
            {
                var cached = await _skillCache.GetAsync(skillId);
                if (cached != null)
                {
                    logger.ZLogInformation($"ScanSkill cache hit: {skillId}");
                    return cached;
                }
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
                await _skillCache.SetAsync(skillId, result);
                LogSkillResult(result);
            }

            return result;
        }

        public async Task<WeaponBaseInfo?> ScanWeapon(uint weaponId, CancellationToken token)
        {
            if (weaponId == 0) return null;
            
            // Check cache with TTL
            if (await _weaponCache.IsValidAsync(weaponId, CacheTTL))
            {
                var cached = await _weaponCache.GetAsync(weaponId);
                if (cached != null)
                {
                    logger.ZLogInformation($"ScanWeapon cache hit: {weaponId}");
                    return cached;
                }
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
                await _weaponCache.SetAsync(weaponId, result);
                LogWeaponResult(result);
            }

            return result;
        }

        public async Task<MachineBaseInfo?> ScanMachineWithDetails(uint id, CancellationToken token)
        {
            var machineInfo = await ScanMachine(id, token);
            if (machineInfo == null) return null;

            // Super Batch: Scan all Skills and Weapons in ONE pass
            await AssignDetailsAsync(machineInfo, token);

            // Prevent cycle scan: TransformId could be MachineId+1 or MachineId-1
            // Only scan transformed machine if it's a different machine (not adjacent ID)
            if (machineInfo.HasTransform && machineInfo.TransformId != 0 )
            {
                machineInfo.TransformedMachine = await ScanMachine(machineInfo.TransformId, token);
                if (machineInfo.TransformedMachine is not null)
                {
                    machineInfo.TransformedMachine.Skill1Info = machineInfo.Skill1Info;
                    machineInfo.TransformedMachine.Skill2Info = machineInfo.Skill2Info;
                    // Also scan weapons for transformed machine.
                    // Ideally we should have batched THIS too into the first call if we knew the ID.
                    // But we don't know the TransformId until we verify the first Machine.
                    // So this remains a second step, but improved.
                    await AssignDetailsAsync(machineInfo.TransformedMachine, token);
                }
            }

            return machineInfo;
        }

        public async Task<List<SkillBaseInfo>> ScanSkills(IEnumerable<uint> skillIds, CancellationToken token)
        {
            var results = new List<SkillBaseInfo>();
            var missingIds = new List<uint>();
            var distinctIds = skillIds.Where(id => id > 0).Distinct().ToList();

            if (distinctIds.Count == 0) return results;

            // 1. Check Cache
            foreach (var id in distinctIds)
            {
                if (await _skillCache.IsValidAsync(id, CacheTTL))
                {
                    var cached = await _skillCache.GetAsync(id);
                    if (cached != null)
                    {
                        results.Add(cached);
                        continue;
                    }
                }
                missingIds.Add(id);
            }

            if (missingIds.Count == 0) return results;

            // 2. Scan Missing
            string processName = Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ=="));
            var process = Process.GetProcessesByName(processName).FirstOrDefault();
            if (process == null) return results;

            var batchInput = missingIds.Select(id => (BuildSkillPattern(id), (string?)null, (int)id)).ToList();
            
            logger.ZLogInformation($"ScanSkills Batch: {missingIds.Count} items");
            var scanResults = await _scanner.ScanBatchAsync(process, batchInput, token);

            // 3. Process & Cache
            var bufferPool = ArrayPool<byte>.Shared;
            byte[] buffer = bufferPool.Rent(SKILL_BUFFER_SIZE);

            try
            {
                foreach (var id in missingIds)
                {
                    if (scanResults.TryGetValue((int)id, out var addresses))
                    {
                        foreach (var addr in addresses)
                        {
                            if (TryReadMemory((IntPtr)addr, buffer, SKILL_BUFFER_SIZE))
                            {
                                var span = buffer.AsSpan(0, SKILL_BUFFER_SIZE);
                                if (ValidateSkillData(span, id))
                                {
                                    var info = ParseSkillData(span);
                                    if (info != null)
                                    {
                                        logger.ZLogInformation($"Found skill {id} at 0x{addr:X}");
                                        await _skillCache.SetAsync(id, info);
                                        results.Add(info);
                                        break; 
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                bufferPool.Return(buffer);
            }

            return results;
        }

        public async Task<List<WeaponBaseInfo>> ScanWeapons(IEnumerable<uint> weaponIds, CancellationToken token)
        {
            var results = new List<WeaponBaseInfo>();
            var missingIds = new List<uint>();
            var distinctIds = weaponIds.Where(id => id > 0).Distinct().ToList();

            if (distinctIds.Count == 0) return results;

            // 1. Check Cache
            foreach (var id in distinctIds)
            {
                if (await _weaponCache.IsValidAsync(id, CacheTTL))
                {
                    var cached = await _weaponCache.GetAsync(id);
                    if (cached != null)
                    {
                        results.Add(cached);
                        continue;
                    }
                }
                missingIds.Add(id);
            }

            if (missingIds.Count == 0) return results;

            // 2. Scan Missing
            string processName = Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ=="));
            var process = Process.GetProcessesByName(processName).FirstOrDefault();
            if (process == null) return results;

            var batchInput = missingIds.Select(id => (BuildWeaponPattern(id), (string?)null, (int)id)).ToList();
            
            logger.ZLogInformation($"ScanWeapons Batch: {missingIds.Count} items");
            var scanResults = await _scanner.ScanBatchAsync(process, batchInput, token);

            // 3. Process & Cache
            var bufferPool = ArrayPool<byte>.Shared;
            byte[] buffer = bufferPool.Rent(WEAPON_BUFFER_SIZE);

            try
            {
                foreach (var id in missingIds)
                {
                    if (scanResults.TryGetValue((int)id, out var addresses))
                    {
                        foreach (var addr in addresses)
                        {
                            if (TryReadMemory((IntPtr)addr, buffer, WEAPON_BUFFER_SIZE))
                            {
                                var span = buffer.AsSpan(0, WEAPON_BUFFER_SIZE);
                                if (ValidateWeaponData(span, id))
                                {
                                    var info = ParseWeaponData(span);
                                    if (info != null)
                                    {
                                        logger.ZLogInformation($"Found weapon {id} at 0x{addr:X}");
                                        await _weaponCache.SetAsync(id, info);
                                        results.Add(info);
                                        break; 
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                bufferPool.Return(buffer);
            }

            return results;
        }

        public async Task<List<MachineBaseInfo>> ScanMachines(IEnumerable<uint> machineIds, CancellationToken token)
        {
            var results = new List<MachineBaseInfo>();
            var missingIds = new List<uint>();
            var distinctIds = machineIds.Where(id => id > 0).Distinct().ToList();

            if (distinctIds.Count == 0) return results;

            // 1. Check Cache
            foreach (var id in distinctIds)
            {
                if (await _machineCache.IsValidAsync(id, CacheTTL))
                {
                    var cached = await _machineCache.GetAsync(id);
                    if (cached != null)
                    {
                        results.Add(cached);
                        continue;
                    }
                }
                missingIds.Add(id);
            }

            if (missingIds.Count == 0) return results;

            // 2. Scan Missing
            string processName = Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ=="));
            var process = Process.GetProcessesByName(processName).FirstOrDefault();
            if (process == null) return results;

            var batchInput = missingIds.Select(id => 
            {
                var (pattern, mask) = BuildMachinePattern(id);
                return (pattern, mask, (int)id);
            }).ToList();
            
            logger.ZLogInformation($"ScanMachines Batch: {missingIds.Count} items");
            var scanResults = await _scanner.ScanBatchAsync(process, batchInput, token);

            // 3. Process & Cache
            var bufferPool = ArrayPool<byte>.Shared;
            byte[] buffer = bufferPool.Rent(MACHINE_BUFFER_SIZE);

            try
            {
                foreach (var id in missingIds)
                {
                    if (scanResults.TryGetValue((int)id, out var addresses))
                    {
                        foreach (var addr in addresses)
                        {
                            if (TryReadMemory((IntPtr)addr, buffer, MACHINE_BUFFER_SIZE))
                            {
                                var span = buffer.AsSpan(0, MACHINE_BUFFER_SIZE);
                                if (ValidateMachineData(span, id))
                                {
                                    var info = ParseMachineData(span);
                                    if (info != null)
                                    {
                                        logger.ZLogInformation($"Found machine {id} at 0x{addr:X}");
                                        LogMachineResult(info);
                                        await _machineCache.SetAsync(id, info);
                                        results.Add(info);
                                        break; 
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                bufferPool.Return(buffer);
            }

            return results;
        }

        private async Task AssignDetailsAsync(MachineBaseInfo info, CancellationToken token)
        {
            // Collect all IDs
            var skillIds = new List<uint>();
            if (info.SkillID1 != 0) skillIds.Add(info.SkillID1);
            if (info.SkillID2 != 0) skillIds.Add(info.SkillID2);

            var weaponIds = new List<uint>();
            if (info.Weapon1Code != 0) weaponIds.Add(info.Weapon1Code);
            if (info.Weapon2Code != 0) weaponIds.Add(info.Weapon2Code);
            if (info.Weapon3Code != 0) weaponIds.Add(info.Weapon3Code);
            if (info.SpecialAttackCode != 0) weaponIds.Add(info.SpecialAttackCode);
            weaponIds = weaponIds.Distinct().ToList();

            if (skillIds.Count == 0 && weaponIds.Count == 0) return;

            // PRE-CHECK CACHE to unnecessary scanning
            var missingSkillIds = new List<uint>();
            foreach (var id in skillIds)
            {
                if (await _skillCache.IsValidAsync(id, CacheTTL))
                {
                   var cached = await _skillCache.GetAsync(id);
                   if (cached != null)
                   {
                       // Assign immediately
                       if (id == info.SkillID1) info.Skill1Info = cached;
                       else if (id == info.SkillID2) info.Skill2Info = cached;
                       //logger.ZLogInformation($"AssignDetails cache hit for Skill:{id}");
                       continue;
                   }
                }
                missingSkillIds.Add(id);
            }

            var missingWeaponIds = new List<uint>();
            foreach (var id in weaponIds)
            {
                if (await _weaponCache.IsValidAsync(id, CacheTTL))
                {
                    var cached = await _weaponCache.GetAsync(id);
                    if (cached != null)
                    {
                        // Assign immediately
                        if (id == info.Weapon1Code) info.Weapon1Info = cached;
                        if (id == info.Weapon2Code) info.Weapon2Info = cached;
                        if (id == info.Weapon3Code) info.Weapon3Info = cached;
                        if (id == info.SpecialAttackCode) info.SpecialAttack = cached;
                        //logger.ZLogInformation($"AssignDetails cache hit for Weapon:{id}");
                        continue;
                    }
                }
                missingWeaponIds.Add(id);
            }

            if (missingSkillIds.Count == 0 && missingWeaponIds.Count == 0) return;


            string processName = Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ=="));
            var process = Process.GetProcessesByName(processName).FirstOrDefault();
            if (process == null) return;

            // 1. Build Batch Patterns (Only for missing items)
            var batchInput = new List<(byte[], string, int)>();
            
            foreach (var id in missingWeaponIds)
            {
                batchInput.Add((BuildWeaponPattern(id), null, (int)id));
            }
            foreach (var id in missingSkillIds)
            {
                // Offset Skill IDs just in case
                batchInput.Add((BuildSkillPattern(id), null, (int)id + 1000000));
            }

            // 2. Scan Batch
            var sw = Stopwatch.StartNew();
            logger.ZLogInformation($"BatchScan Input: {missingWeaponIds.Count} Weapons, {missingSkillIds.Count} Skills");
            
            var results = await _scanner.ScanBatchAsync(process, batchInput, token);
            
            sw.Stop();
            logger.ZLogInformation($"BatchScan found results in {sw.ElapsedMilliseconds}ms");


            // 3. Process Results
            var bufferPool = ArrayPool<byte>.Shared;
            int maxStructSize = Math.Max(WEAPON_BUFFER_SIZE, SKILL_BUFFER_SIZE); 
            byte[] buffer = bufferPool.Rent(maxStructSize);

            try
            {
                // WEAPONS - Cache found weapons
                foreach (var id in missingWeaponIds)
                {
                    WeaponBaseInfo? foundInfo = null;
                    if (results.TryGetValue((int)id, out var addresses))
                    {
                        foreach (var addr in addresses)
                        {
                            if (TryReadMemory((IntPtr)addr, buffer, WEAPON_BUFFER_SIZE))
                            {
                                var span = buffer.AsSpan(0, WEAPON_BUFFER_SIZE);
                                if (ValidateWeaponData(span, id))
                                {
                                    foundInfo = ParseWeaponData(span);
                                    if (foundInfo != null)
                                    {
                                        logger.ZLogInformation($"Found weapon at addr:0x{addr:X}");
                                        LogWeaponResult(foundInfo);
                                        // Cache the weapon
                                        await _weaponCache.SetAsync(id, foundInfo);
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (foundInfo != null)
                    {
                        if (id == info.Weapon1Code) info.Weapon1Info = foundInfo;
                        if (id == info.Weapon2Code) info.Weapon2Info = foundInfo;
                        if (id == info.Weapon3Code) info.Weapon3Info = foundInfo;
                        if (id == info.SpecialAttackCode) info.SpecialAttack = foundInfo;
                    }
                }

                // SKILLS - Cache found skills
                foreach (var id in missingSkillIds)
                {
                    int lookupId = (int)id + 1000000;
                    if (results.TryGetValue(lookupId, out var addresses))
                    {
                         foreach (var addr in addresses)
                        {
                            if (TryReadMemory((IntPtr)addr, buffer, SKILL_BUFFER_SIZE))
                            {
                                var span = buffer.AsSpan(0, SKILL_BUFFER_SIZE);
                                if (ValidateSkillData(span, id))
                                {
                                    var result = ParseSkillData(span);
                                    if (result != null)
                                    {
                                        logger.ZLogInformation($"Found skill at addr:0x{addr:X}");
                                        LogSkillResult(result);
                                        if (id == info.SkillID1) info.Skill1Info = result;
                                        else if (id == info.SkillID2) info.Skill2Info = result;
                                        await _skillCache.SetAsync(id, result);
                                        break; 
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                bufferPool.Return(buffer);
            }
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
                        bool success = TryReadMemory((IntPtr)address, pooledBuffer, bufferSize);
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
            if (info.HpActivateCondition.StartsWith("Unknown")) return null;
            if (string.IsNullOrWhiteSpace(info.SkillName)) return null;
            if (string.IsNullOrEmpty(info.Description)) return null;
            if(!string.IsNullOrEmpty(info.AuraEffect) && !info.AuraEffect.StartsWith(@"fxrs\",StringComparison.OrdinalIgnoreCase))return null;
            if (info.ExactHpActivatePercent > 100) return null;
            if (info.AttackIncrease         > 255 || info.DefenseIncrease > 255 || info.AgilityPercent > 100 ||
                info.ExactHpActivatePercent > 100) return null;

            if (info.AttackIncrease         < -255 || info.DefenseIncrease < -255 || info.AgilityPercent < 0 ||
                info.ExactHpActivatePercent < 0) return null;
            if (info.MeleeDamageIncrease > 100) return null;
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
            var mdrsSig = "m\0d\0r\0s\0\\\0"u8.ToArray();
            Array.Copy(mdrsSig, 0, patternBytes, 0x264, mdrsSig.Length);
            for (int i = 0; i < mdrsSig.Length; i++) maskChars[0x264 + i] = 'x';

            return (patternBytes, new string(maskChars));
        }

        private static byte[] BuildSkillPattern(uint skillId)
        {
            // SkillId is a uint (4 bytes) + 2 trailing 0x00 for better matching
            // Example: 70125 -> 0x000111ED -> ED 11 01 00 00 00
            var idBytes = BitConverter.GetBytes(skillId);
            return [idBytes[0], idBytes[1], idBytes[2], idBytes[3], 0x00];
        }

        private static byte[] BuildWeaponPattern(uint weaponId)
        {
            // WeaponId is a uint (4 bytes), so we need to match all 4 bytes
            var idBytes = BitConverter.GetBytes(weaponId);
            return
            [
                idBytes[0],  // Low byte
                idBytes[1],
                idBytes[2],
                idBytes[3]   // High byte (usually 0x00 for typical IDs)
            ];
        }
        
        
        #endregion

        //#endregion

        #region Memory Safety Logic

         private static bool IsMemoryRangeReadable(IntPtr address, int size)
        {
             // Basic pointer valid checks
            if (address == IntPtr.Zero || size <= 0) return false;
            long startAddr = (long)address;
            long endAddr = startAddr + size;

             // User mode range check
             if (IntPtr.Size == 4 && (startAddr < 0x10000 || endAddr > 0x80000000)) return false;
             if (IntPtr.Size == 8 && (startAddr < 0x10000 || endAddr > 0x7FFFFFFFFFF)) return false;

            long currentAddr = startAddr;
            while (currentAddr < endAddr)
            {
                MEMORY_BASIC_INFORMATION mbi;
                if (VirtualQuery((IntPtr)currentAddr, out mbi, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) == 0)
                    return false;

                if (mbi.State != MEM_COMMIT) return false;
                if ((mbi.Protect & PAGE_GUARD) == PAGE_GUARD) return false;
                if ((mbi.Protect & PAGE_NOACCESS) == PAGE_NOACCESS) return false;
                
                // Readable checks
                bool readable = (mbi.Protect & PAGE_READONLY) != 0 ||
                                (mbi.Protect & PAGE_READWRITE) != 0 ||
                                (mbi.Protect & PAGE_WRITECOPY) != 0 ||
                                (mbi.Protect & PAGE_EXECUTE_READ) != 0 ||
                                (mbi.Protect & PAGE_EXECUTE_READWRITE) != 0 ||
                                (mbi.Protect & PAGE_EXECUTE_WRITECOPY) != 0;

                if (!readable) return false;

                // Move to next region
                long regionEnd = (long)mbi.BaseAddress + (long)mbi.RegionSize;
                currentAddr = regionEnd;
            }

            return true;
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private bool TryReadMemory(IntPtr address, byte[] buffer, int size)
        {
            // Quick null/size check first
            if (address == IntPtr.Zero || size <= 0 || buffer == null || buffer.Length < size)
                return false;

            // Validate the whole range to prevent page boundary crossing crashes
            if (!IsMemoryRangeReadable(address, size)) return false;

            try
            {
                // Double-check just the start and end pages right before copy
                // This reduces race condition window
                MEMORY_BASIC_INFORMATION mbiStart, mbiEnd;
                if (VirtualQuery(address, out mbiStart, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) == 0)
                    return false;
                if (mbiStart.State != MEM_COMMIT || (mbiStart.Protect & PAGE_NOACCESS) != 0 || (mbiStart.Protect & PAGE_GUARD) != 0)
                    return false;

                IntPtr endAddr = address + size - 1;
                if (VirtualQuery(endAddr, out mbiEnd, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) == 0)
                    return false;
                if (mbiEnd.State != MEM_COMMIT || (mbiEnd.Protect & PAGE_NOACCESS) != 0 || (mbiEnd.Protect & PAGE_GUARD) != 0)
                    return false;

                unsafe
                {
                    fixed (byte* ptr = buffer)
                    {
                        // Direct memory copy since we are in same process
                        Buffer.MemoryCopy((void*)address, ptr, buffer.Length, size);
                    }
                }
                return true;
            }
            catch (AccessViolationException)
            {
                // Memory became invalid between check and copy
                return false;
            }
            catch (SEHException)
            {
                // Structured exception from memory access
                return false;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
