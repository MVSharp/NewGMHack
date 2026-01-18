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
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using ZLogger;
using NewGMHack.CommunicationModel.Models;
using NewGMHack.Stub.Services.Scanning;
using NewGMHack.Stub.Services.Caching;
using NewGMHack.Stub.Services.Loggers;

namespace NewGMHack.Stub.MemoryScanner
{
    public class GmMemory
    {
        private readonly ILogger<GmMemory> _logger;
        private readonly IMemoryScanner _scanner;
        private readonly IEntityCache<MachineBaseInfo> _machineCache;
        private readonly IEntityCache<SkillBaseInfo> _skillCache;
        private readonly IEntityCache<WeaponBaseInfo> _weaponCache;

        /// <summary>
        /// Cache TTL - entries older than this will trigger a rescan
        /// </summary>
        private static readonly TimeSpan CacheTTL = TimeSpan.FromDays(6);


        public GmMemory(
            ILogger<GmMemory> logger, 
            IMemoryScanner scanner,
            IEntityCache<MachineBaseInfo>? machineCache = null,
            IEntityCache<SkillBaseInfo>? skillCache = null,
            IEntityCache<WeaponBaseInfo>? weaponCache = null)
        {
            _logger = logger;
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
            var cached = await _machineCache.GetIfValidAsync(id, CacheTTL);
            if (cached != null)
            {
                //logger.LogScanMachineCacheHit(id);
                return cached;
            }

            var (pattern, mask) = BuildMachinePattern(id);
            var result = await ScanGeneric(
                                           id,
                                           pattern,
                                           mask,
                                           MACHINE_BUFFER_SIZE,
                                           (s,t)=>true,
                                           ParseMachineData,
                                           "Machine",
                                           token);

            if (result != null)
            {
                try 
                {
                    await _machineCache.SetAsync(id, result);
                } 
                catch (Exception ex) 
                { 
                    _logger.LogCacheMachineError(ex, id); 
                }
                _logger.LogMachineDebugInfo(result.MachineId, result.MdrsFilePath, result.ChineseName, result.EnglishName, result.Rank, result.HP, result.CombatType, result.SkillID1, result.SkillID2);
            }

            return result;
        }

        public async Task<SkillBaseInfo?> ScanSkill(uint skillId, CancellationToken token)
        {
            if (skillId == 0) return null;
            
            // Check cache with TTL
            var cached = await _skillCache.GetIfValidAsync(skillId, CacheTTL);
            if (cached != null)
            {
                _logger.LogScanSkillCacheHit(skillId);
                return cached;
            }

            var result = await ScanGeneric(
                                           skillId,
                                           BuildSkillPattern(skillId),
                                           null, // Exact match
                                           SKILL_BUFFER_SIZE,
                                           (s,t)=>true,
                                           ParseSkillData,
                                           "Skill",
                                           token);

            if (result != null)
            {
                try 
                {
                    await _skillCache.SetAsync(skillId, result);
                } 
                catch (Exception ex) 
                { 
                    _logger.LogCacheSkillError(ex, skillId); 
                }
                _logger.LogSkillDebugInfo(result.SkillId, result.SkillName, result.Movement, result.AttackIncrease, result.DefenseIncrease);
            }

            return result;
        }

        public async Task<WeaponBaseInfo?> ScanWeapon(uint weaponId, CancellationToken token)
        {
            if (weaponId == 0) return null;
            
            // Check cache with TTL
            var cached = await _weaponCache.GetIfValidAsync(weaponId, CacheTTL);
            if (cached != null)
            {
                _logger.LogScanWeaponCacheHit(weaponId);
                return cached;
            }

            var result = await ScanGeneric(
                                           weaponId,
                                           BuildWeaponPattern(weaponId),
                                           null, // Exact match
                                           WEAPON_BUFFER_SIZE,
                                           (s,t)=>true,
                                           ParseWeaponData,
                                           "Weapon",
                                           token);

            if (result != null)
            {
                try 
                {
                    await _weaponCache.SetAsync(weaponId, result);
                } 
                catch (Exception ex) 
                { 
                    _logger.LogCacheWeaponError(ex, weaponId); 
                }
                _logger.LogWeaponDebugInfo(result.WeaponId, result.WeaponName, result.WeaponType, result.WeaponDamage, result.WeaponRange, result.AmmoCount);
            }

            return result;
        }

        public async Task<MachineBaseInfo?> ScanMachineWithDetails(uint id, CancellationToken token)
        {
            // 1. Scan the main machine
            var machineInfo = await ScanMachine(id, token);
            if (machineInfo == null) return null;

            // 2. Check for transform and scan if exists
            if (machineInfo.HasTransform && machineInfo.TransformId != 0)
            {
                if (machineInfo.TransformId != id)
                {
                    machineInfo.TransformedMachine = await ScanMachine(machineInfo.TransformId, token);
                }
            }

            // 3. Collect ALL IDs (Machine + Transform)
            var allSkillIds = new HashSet<uint>();
            var allWeaponIds = new HashSet<uint>();

            void CollectIds(MachineBaseInfo info)
            {
                if (info.SkillID1 != 0) allSkillIds.Add(info.SkillID1);
                if (info.SkillID2 != 0) allSkillIds.Add(info.SkillID2);

                if (info.Weapon1Code != 0) allWeaponIds.Add(info.Weapon1Code);
                if (info.Weapon2Code != 0) allWeaponIds.Add(info.Weapon2Code);
                if (info.Weapon3Code != 0) allWeaponIds.Add(info.Weapon3Code);
                if (info.SpecialAttackCode != 0) allWeaponIds.Add(info.SpecialAttackCode);
            }

            CollectIds(machineInfo);
            if (machineInfo.TransformedMachine != null)
            {
                CollectIds(machineInfo.TransformedMachine);
            }

            if (allSkillIds.Count == 0 && allWeaponIds.Count == 0) return machineInfo;

            // 4. Check Caches & Identify Missing
            var foundSkills = new Dictionary<uint, SkillBaseInfo>();

            var validSkills = await _skillCache.GetManyIfValidAsync(allSkillIds, CacheTTL);
            foreach (var kvp in validSkills) foundSkills[kvp.Key] = kvp.Value;
            var missingSkillIds = allSkillIds.Where(id => !validSkills.ContainsKey(id)).ToList();

            var foundWeapons = new Dictionary<uint, WeaponBaseInfo>();

            var validWeapons = await _weaponCache.GetManyIfValidAsync(allWeaponIds, CacheTTL);
            foreach (var kvp in validWeapons) foundWeapons[kvp.Key] = kvp.Value;
            var missingWeaponIds = allWeaponIds.Where(id => !validWeapons.ContainsKey(id)).ToList();

            // 5. Single Batch Scan for Missing Items
            if (missingSkillIds.Count > 0 || missingWeaponIds.Count > 0)
            {
                string processName = Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ=="));
                var process = Process.GetProcessesByName(processName).FirstOrDefault();

                if (process != null)
                {
                    var batchInput = new List<(byte[], string, int)>();

                    // Add Weapons
                    foreach (var wid in missingWeaponIds)
                    {
                        batchInput.Add((BuildWeaponPattern(wid), null, (int)wid));
                    }
                    // Add Skills (Offset IDs by 1,000,000 to avoid collision)
                    const int SKILL_OFFSET = 1000000;
                    foreach (var sid in missingSkillIds)
                    {
                        batchInput.Add((BuildSkillPattern(sid), null, (int)sid + SKILL_OFFSET));
                    }

                    _logger.LogScanMachineWithDetailsBatch(missingWeaponIds.Count, missingSkillIds.Count);
                    var scanResults = await _scanner.ScanBatchAsync(process, batchInput, token);

                    // Process Results
                    var bufferPool = ArrayPool<byte>.Shared;
                    int maxStructSize = Math.Max(WEAPON_BUFFER_SIZE, SKILL_BUFFER_SIZE);
                    byte[] buffer = bufferPool.Rent(maxStructSize);

                    try
                    {
                        // Process Found Weapons
                        var newWeapons = new Dictionary<uint, WeaponBaseInfo>();
                        foreach (var wid in missingWeaponIds)
                        {
                            if (scanResults.TryGetValue((int)wid, out var addresses))
                            {
                                foreach (var addr in addresses)
                                {
                                    if (TryReadMemory((IntPtr)addr, buffer, WEAPON_BUFFER_SIZE))
                                    {
                                        var span = buffer.AsSpan(0, WEAPON_BUFFER_SIZE);
                                        //if (ValidateWeaponData(span, wid))
                                        //{
                                            var info = ParseWeaponData(span);
                                            if (info != null)
                                            {
                                                _logger.LogFoundWeapon(wid, addr);
                                                _logger.LogWeaponDebugInfo(info.WeaponId, info.WeaponName, info.WeaponType, info.WeaponDamage, info.WeaponRange, info.AmmoCount);
                                                newWeapons[wid] = info;
                                                foundWeapons[wid] = info;
                                                break; 
                                            }
                                        //}
                                    }
                                }
                            }
                        }
                        if (newWeapons.Count > 0)
                        {
                            await _weaponCache.SetManyAsync(newWeapons);
                        }

                        // Process Found Skills
                        var newSkills = new Dictionary<uint, SkillBaseInfo>();
                        foreach (var sid in missingSkillIds)
                        {
                            if (scanResults.TryGetValue((int)sid + SKILL_OFFSET, out var addresses))
                            {
                                foreach (var addr in addresses)
                                {
                                    if (TryReadMemory((IntPtr)addr, buffer, SKILL_BUFFER_SIZE))
                                    {
                                        var span = buffer.AsSpan(0, SKILL_BUFFER_SIZE);
                                        //if (ValidateSkillData(span, sid))
                                        //{
                                            var info = ParseSkillData(span);
                                            if (info != null)
                                            {
                                                _logger.LogFoundSkill(sid, addr);
                                                newSkills[sid] = info;
                                                foundSkills[sid] = info;
                                                break;
                                            }
                                        //}
                                    }
                                }
                            }
                        }
                        if (newSkills.Count > 0)
                        {
                            await _skillCache.SetManyAsync(newSkills);
                        }
                    }
                    finally
                    {
                        bufferPool.Return(buffer);
                    }
                }
            }

            // 6. Assign All Details to Objects
            void AssignDetails(MachineBaseInfo info)
            {
                if (info.SkillID1 != 0 && foundSkills.TryGetValue(info.SkillID1, out var s1)) info.Skill1Info = s1;
                if (info.SkillID2 != 0 && foundSkills.TryGetValue(info.SkillID2, out var s2)) info.Skill2Info = s2;

                if (info.Weapon1Code != 0 && foundWeapons.TryGetValue(info.Weapon1Code, out var w1)) info.Weapon1Info = w1;
                if (info.Weapon2Code != 0 && foundWeapons.TryGetValue(info.Weapon2Code, out var w2)) info.Weapon2Info = w2;
                if (info.Weapon3Code != 0 && foundWeapons.TryGetValue(info.Weapon3Code, out var w3)) info.Weapon3Info = w3;
                if (info.SpecialAttackCode != 0 && foundWeapons.TryGetValue(info.SpecialAttackCode, out var sp)) info.SpecialAttack = sp;
            }

            AssignDetails(machineInfo);
            if (machineInfo.TransformedMachine != null)
            {
                AssignDetails(machineInfo.TransformedMachine);
            }

            return machineInfo;
        }

        public async Task<List<MachineBaseInfo>> ScanMachinesWithDetails(IEnumerable<uint> ids, CancellationToken token)
        {
            // 1. Batch Scan Base Machines
            var distinctIds = ids.Where(id => id > 0).Distinct().ToList();
            if (distinctIds.Count == 0) return new List<MachineBaseInfo>();

            var machines = await ScanMachines(distinctIds, token);
            if (machines.Count == 0) return machines;

            // 2. Scan Transforms
            var transformIds = machines
                .Where(m => m.HasTransform && m.TransformId != 0 && m.TransformId != m.MachineId)
                .Select(m => m.TransformId)
                .Distinct()
                .ToList();

            var transforms = new List<MachineBaseInfo>();
            if (transformIds.Count > 0)
            {
                transforms = await ScanMachines(transformIds, token);
            }

            // Map transforms back to parents
            var transformMap = transforms.ToDictionary(m => m.MachineId);
            foreach (var machine in machines)
            {
                if (machine.HasTransform && machine.TransformId != 0 &&
                    transformMap.TryGetValue(machine.TransformId, out var trans))
                {
                    machine.TransformedMachine = trans;
                }
            }

            // 3. Collect ALL IDs (Machine + Transform)
            var allSkillIds = new HashSet<uint>();
            var allWeaponIds = new HashSet<uint>();

            void CollectIds(MachineBaseInfo info)
            {
                if (info.SkillID1 != 0) allSkillIds.Add(info.SkillID1);
                if (info.SkillID2 != 0) allSkillIds.Add(info.SkillID2);

                if (info.Weapon1Code != 0) allWeaponIds.Add(info.Weapon1Code);
                if (info.Weapon2Code != 0) allWeaponIds.Add(info.Weapon2Code);
                if (info.Weapon3Code != 0) allWeaponIds.Add(info.Weapon3Code);
                if (info.SpecialAttackCode != 0) allWeaponIds.Add(info.SpecialAttackCode);
            }

            foreach (var m in machines) CollectIds(m);
            foreach (var t in transforms) CollectIds(t);

            if (allSkillIds.Count == 0 && allWeaponIds.Count == 0) return machines;

            // 4. Check Caches & Identify Missing
            var foundSkills = new Dictionary<uint, SkillBaseInfo>();

            var validSkills = await _skillCache.GetManyIfValidAsync(allSkillIds, CacheTTL);
            foreach (var kvp in validSkills) foundSkills[kvp.Key] = kvp.Value;
            var missingSkillIds = allSkillIds.Where(id => !validSkills.ContainsKey(id)).ToList();

            var foundWeapons = new Dictionary<uint, WeaponBaseInfo>();

            var validWeapons = await _weaponCache.GetManyIfValidAsync(allWeaponIds, CacheTTL);
            foreach (var kvp in validWeapons) foundWeapons[kvp.Key] = kvp.Value;
            var missingWeaponIds = allWeaponIds.Where(id => !validWeapons.ContainsKey(id)).ToList();

            // 5. Single Batch Scan for Missing Items
            if (missingSkillIds.Count > 0 || missingWeaponIds.Count > 0)
            {
                string processName = Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ=="));
                var process = Process.GetProcessesByName(processName).FirstOrDefault();

                if (process != null)
                {
                    var batchInput = new List<(byte[], string, int)>();

                    // Add Weapons
                    foreach (var wid in missingWeaponIds)
                    {
                        batchInput.Add((BuildWeaponPattern(wid), null, (int)wid));
                    }
                    // Add Skills (Offset IDs by 1,000,000 to avoid collision)
                    const int SKILL_OFFSET = 1000000;
                    foreach (var sid in missingSkillIds)
                    {
                        batchInput.Add((BuildSkillPattern(sid), null, (int)sid + SKILL_OFFSET));
                    }

                    _logger.LogScanMachineWithDetailsBatch(missingWeaponIds.Count, missingSkillIds.Count);
                    var scanResults = await _scanner.ScanBatchAsync(process, batchInput, token);

                    // Process Results
                    var bufferPool = ArrayPool<byte>.Shared;
                    int maxStructSize = Math.Max(WEAPON_BUFFER_SIZE, SKILL_BUFFER_SIZE);
                    byte[] buffer = bufferPool.Rent(maxStructSize);

                    try
                    {
                        // Process Found Weapons
                        var newWeapons = new Dictionary<uint, WeaponBaseInfo>();
                        foreach (var wid in missingWeaponIds)
                        {
                            if (scanResults.TryGetValue((int)wid, out var addresses))
                            {
                                foreach (var addr in addresses)
                                {
                                    if (TryReadMemory((IntPtr)addr, buffer, WEAPON_BUFFER_SIZE))
                                    {
                                        var span = buffer.AsSpan(0, WEAPON_BUFFER_SIZE);
                                        //if (ValidateWeaponData(span, wid))
                                        //{
                                            var info = ParseWeaponData(span);
                                            if (info != null)
                                            {

                                                newWeapons[wid] = info;
                                                foundWeapons[wid] = info;
                                                break; 
                                            }
                                        //}
                                    }
                                }
                            }
                        }
                        if (newWeapons.Count > 0)
                        {
                            await _weaponCache.SetManyAsync(newWeapons);
                        }
 
                        // Process Found Skills
                        var newSkills = new Dictionary<uint, SkillBaseInfo>();
                        foreach (var sid in missingSkillIds)
                        {
                            if (scanResults.TryGetValue((int)sid + SKILL_OFFSET, out var addresses))
                            {
                                foreach (var addr in addresses)
                                {
                                    if (TryReadMemory((IntPtr)addr, buffer, SKILL_BUFFER_SIZE))
                                    {
                                        var span = buffer.AsSpan(0, SKILL_BUFFER_SIZE);
                                        //if (ValidateSkillData(span, sid))
                                        //{
                                            var info = ParseSkillData(span);
                                            if (info != null)
                                            {
                                                _logger.LogFoundSkill(sid, addr);
                                                _logger.LogSkillDebugInfo(info.SkillId, info.SkillName, info.Movement, info.AttackIncrease, info.DefenseIncrease);

                                                newSkills[sid] = info;
                                                foundSkills[sid] = info;
                                                break;
                                            }
                                        //}
                                    }
                                }
                            }
                        }
                        if (newSkills.Count > 0)
                        {
                            await _skillCache.SetManyAsync(newSkills);
                        }
                    }
                    finally
                    {
                        bufferPool.Return(buffer);
                    }
                }
            }

            // 6. Assign All Details to Objects
            void AssignDetails(MachineBaseInfo info)
            {
                if (info.SkillID1 != 0 && foundSkills.TryGetValue(info.SkillID1, out var s1)) info.Skill1Info = s1;
                if (info.SkillID2 != 0 && foundSkills.TryGetValue(info.SkillID2, out var s2)) info.Skill2Info = s2;

                if (info.Weapon1Code != 0 && foundWeapons.TryGetValue(info.Weapon1Code, out var w1)) info.Weapon1Info = w1;
                if (info.Weapon2Code != 0 && foundWeapons.TryGetValue(info.Weapon2Code, out var w2)) info.Weapon2Info = w2;
                if (info.Weapon3Code != 0 && foundWeapons.TryGetValue(info.Weapon3Code, out var w3)) info.Weapon3Info = w3;
                if (info.SpecialAttackCode != 0 && foundWeapons.TryGetValue(info.SpecialAttackCode, out var sp)) info.SpecialAttack = sp;
            }

            foreach (var m in machines) AssignDetails(m);
            foreach (var t in transforms) AssignDetails(t);

            return machines;
        }

        public async Task<List<SkillBaseInfo>> ScanSkills(IEnumerable<uint> skillIds, CancellationToken token)
        {
            var results = new List<SkillBaseInfo>();
            var missingIds = new List<uint>();
            var distinctIds = skillIds.Where(id => id > 0).Distinct().ToList();

            if (distinctIds.Count == 0) return results;

            // 1. Check Cache
            var cachedSkills = await _skillCache.GetManyIfValidAsync(distinctIds, CacheTTL);
            results.AddRange(cachedSkills.Values);
            
            missingIds.AddRange(distinctIds.Where(id => !cachedSkills.ContainsKey(id)));

            if (missingIds.Count == 0) return results;

            // 2. Scan Missing
            string processName = Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ=="));
            var process = Process.GetProcessesByName(processName).FirstOrDefault();
            if (process == null) return results;

            var batchInput = missingIds.Select(id => (BuildSkillPattern(id), (string?)null, (int)id)).ToList();
            
            _logger.LogScanSkillsBatch(missingIds.Count);
            var scanResults = await _scanner.ScanBatchAsync(process, batchInput, token);

            // 3. Process & Cache
            var bufferPool = ArrayPool<byte>.Shared;
            byte[] buffer = bufferPool.Rent(SKILL_BUFFER_SIZE);
            var newItems = new Dictionary<uint, SkillBaseInfo>();

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
                                //if (ValidateSkillData(span, id))
                                //{
                                    var info = ParseSkillData(span);
                                    if (info != null)
                                    {
                                        _logger.LogFoundSkill(id, addr);
                                        newItems[id] = info;
                                        results.Add(info);
                                        break; 
                                    }
                                //}
                            }
                        }
                    }
                }

                if (newItems.Count > 0)
                {
                    await _skillCache.SetManyAsync(newItems);
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
            var cachedWeapons = await _weaponCache.GetManyIfValidAsync(distinctIds, CacheTTL);
            results.AddRange(cachedWeapons.Values);
            
            missingIds.AddRange(distinctIds.Where(id => !cachedWeapons.ContainsKey(id)));

            if (missingIds.Count == 0) return results;

            // 2. Scan Missing
            string processName = Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ=="));
            var process = Process.GetProcessesByName(processName).FirstOrDefault();
            if (process == null) return results;

            var batchInput = missingIds.Select(id => (BuildWeaponPattern(id), (string?)null, (int)id)).ToList();
            
            _logger.LogScanWeaponsBatch(missingIds.Count);
            var scanResults = await _scanner.ScanBatchAsync(process, batchInput, token);

            // 3. Process & Cache
            var bufferPool = ArrayPool<byte>.Shared;
            byte[] buffer = bufferPool.Rent(WEAPON_BUFFER_SIZE);
            var newItems = new Dictionary<uint, WeaponBaseInfo>();

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
                                //if (ValidateWeaponData(span, id))
                                //{
                                    var info = ParseWeaponData(span);
                                    if (info != null)
                                    {
                                        _logger.LogFoundWeapon(id, addr);
                                        newItems[id] = info;
                                        results.Add(info);
                                        break; 
                                    }
                                //}
                            }
                        }
                    }
                }

                if (newItems.Count > 0)
                {
                    await _weaponCache.SetManyAsync(newItems);
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
            
            _logger.LogScanMachinesBatch(missingIds.Count);
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
                                //if (ValidateMachineData(span, id))
                                //{
                                    var info = ParseMachineData(span);
                                    if (info != null)
                                    {
                                        _logger.LogFoundMachine(id, addr);
                                        _logger.LogMachineDebugInfo(info.MachineId, info.MdrsFilePath, info.ChineseName, info.EnglishName, info.Rank, info.HP, info.CombatType, info.SkillID1, info.SkillID2);
                                        try { await _machineCache.SetAsync(id, info); } catch (Exception ex) { _logger.LogCacheMachineError(ex, id); }
                                        results.Add(info);
                                        break; 
                                    }
                                //}
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
            _logger.LogScanGenericInput(typeName, id);
            try
            {
                string processName = Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ=="));
                var    process     = Process.GetProcessesByName(processName).FirstOrDefault();
                if (process == null)
                {
                    _logger.LogScanGenericProcessNotFound();
                    return null;
                }

                var sw        = Stopwatch.StartNew();
                
                // Use the injected memory scanner
                var addresses = await _scanner.ScanAsync(process, pattern, mask, token);
                
                sw.Stop();
                _logger.LogScanGenericFoundAddresses(typeName, addresses.Count, sw.ElapsedMilliseconds);

                if (addresses.Count == 0)
                {
                    _logger.LogScanGenericNotFound(typeName.ToLower(), id);
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

                        _logger.LogScanGenericFoundAtAddress(typeName.ToLower(), address);
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
                _logger.LogScanGenericError(typeName, ex.Message);
                return null;
            }
        }

        #endregion

        #region Validation Functions

        //private static bool ValidateMachineData(ReadOnlySpan<byte> data, uint expectedId)
        //{
        //    var raw = MemoryMarshal.Read<MachineBaseInfoStruct>(data);
        //    if (raw.MachineId != expectedId) return false;
        //    return true;
        //}

        //private static bool ValidateSkillData(ReadOnlySpan<byte> data, uint expectedId)
        //{
        //    var raw = MemoryMarshal.Read<SkillBaseInfoStruct>(data);
        //    if ((raw.SkillId & 0x00FFFFFF) != (expectedId & 0x00FFFFFF)) return false;
        //    return true;
        //}

        //private static bool ValidateWeaponData(ReadOnlySpan<byte> data, uint expectedId)
        //{
        //    var raw = MemoryMarshal.Read<WeaponBaseInfoStruct>(data);
        //    if ((raw.WeaponId & 0x0000FFFF) != (expectedId & 0x0000FFFF)) return false;
        //    return true;
        //}

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
        
        
   private static async Task SetCacheSafe<T>(IEntityCache<T> cache, uint id, T info) where T : class
   {
       try
       {
           await cache.SetAsync(id, info);
       }
       catch (Exception ex)
       {
       }
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


        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int VirtualQuery(IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, int dwLength);

        [StructLayout(LayoutKind.Sequential)]
        struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        private const uint MEM_COMMIT = 0x1000;
        private const uint PAGE_READWRITE = 0x04;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private bool TryReadMemory(IntPtr address, byte[] buffer, int size)
        {
            try
            {
                if (address == IntPtr.Zero) return false;

                // Validate memory page before reading to avoid expensive AV exceptions
                if (VirtualQuery(address, out var mbi, Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
                    return false;

                if (mbi.State != MEM_COMMIT)
                    return false;

                // Ensure readable
                bool isReadable = (mbi.Protect == PAGE_READWRITE || mbi.Protect == PAGE_EXECUTE_READWRITE);
                if (!isReadable)
                    return false;

                // Ensure range is within region
                long regionEnd = mbi.BaseAddress.ToInt64() + mbi.RegionSize.ToInt64();
                long readEnd = address.ToInt64() + size;
                if (readEnd > regionEnd) 
                    return false; // Crosses page boundary into potentially invalid memory
                
                // Use unsafe ReadRaw instead of ReadProcessMemory
                ReadRaw((nuint)address, buffer.AsSpan(0, size));
                return true;
            }
            catch (Exception)
            {
                // Access Violation or other memory error
                return false;
            }
        }

#endregion
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ReadRef<T>(nuint offset, ref T value) where T : unmanaged
            => value = Unsafe.ReadUnaligned<T>((void*)offset);

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ReadRaw(nuint offset, Span<byte> value)
        {
            try
            {

                fixed (byte* result = value)
                    Unsafe.CopyBlockUnaligned(result, (void*)offset, (uint)value.Length);
            }
            catch
            {

            }

        }
    }
}
