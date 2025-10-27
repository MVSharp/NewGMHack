using Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewGMHack.Stub;
using SharpDX;
using ZLogger;
using Squalr.Engine.Memory;
using Squalr.Engine.OS;
using System.Text;
public class EntityScannerService : BackgroundService
    {
        private readonly SelfInformation _selfInfo;
        private readonly ILogger<EntityScannerService> _logger;
        private static string ProcessName = Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ=="));
        private const int MaxEntities = 12;
        private const int BaseOffset = 0x5C1FEC;
        private static int[] Offsets = { 0x50, 0x4, 0x8 };
        private const int HpOffset = 0x34;
        private const int MaxHpOffset = 0x38;
        private const int PosPtrOffset = 0x30;
        private static int[] XyzOffsets = { 0x88, 0x8C, 0x90 };
        private const int TeamOffset = 0x2DC2;
        private const int TypeOffset = 0x2FE0;

        public EntityScannerService(SelfInformation selfInfo, ILogger<EntityScannerService> logger) : base()
        {
            _selfInfo = selfInfo;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.ZLogInformation($"EntityScannerService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                await DoScan();
                await Task.Delay(100, stoppingToken);
            }

            _logger.ZLogInformation($"EntityScannerService stopped.");
        }

        private async Task DoScan()
        {
            // Clear previous targets

            // Attach to process
            var processes = Processes.Default.GetProcesses();
            var process = processes.FirstOrDefault(p => p.ProcessName.Equals(ProcessName.Replace(".exe", ""), StringComparison.OrdinalIgnoreCase));
            if (process == null)
            {
                return;
            }
            Processes.Default.OpenedProcess = process;

            // Get module base address
            uint moduleBase = GetModuleBaseAddress(ProcessName);
            if (moduleBase == 0)
            {
                return;
            }
            uint baseAddr;
            try
            {
                baseAddr = checked(moduleBase + (uint)BaseOffset);
            }
            catch (OverflowException)
            {
                return;
            }

            // Follow pointer chain to first entity
            _selfInfo.Targets.Clear();
            uint entityAddr = ReadPointerChain(baseAddr, Offsets);
            if (entityAddr == 0)
            {

                _selfInfo.Targets.Clear();
                return;
            }

            // Find list head (assuming entity is at data+0x8)
            bool success;
            uint listHead;
            try
            {
                listHead = Reader.Default.Read<uint>(checked(entityAddr - 0x8), out success);
            }
            catch
            {

                _selfInfo.Targets.Clear();
                return;
            }
            if (!success || listHead == 0)
            {

                _selfInfo.Targets.Clear();
                return;
            }

            // Iterate linked list
            var visited = new HashSet<uint>();
            int count = 0;
            uint current = listHead;
            while (current != 0 && !visited.Contains(current) && count < MaxEntities)
            {
                visited.Add(current);
                count++;

                // Read entity data pointer
                uint dataAddr;
                try
                {
                    dataAddr = Reader.Default.Read<uint>(checked(current + 0x8), out success);
                }
                catch
                {
                    break;
                }
                if (success && dataAddr != 0)
                {
                    // Read HP (try int, then float)
                    int hpInt;
                    float hpFloat;
                    try
                    {

                        hpInt = Reader.Default.Read<Int32>(checked(dataAddr + (uint)HpOffset), out success);
                        hpFloat = success ? Reader.Default.Read<Single>(checked(dataAddr + (uint)HpOffset), out success) : 0;
                    }
                    catch
                    {
                        continue;
                    }

                    int maxhpInt;
                    float maxhpFloat;
                    try
                    {

                        maxhpInt = Reader.Default.Read<Int32>(checked(dataAddr + (uint)MaxHpOffset), out success);
                        maxhpFloat = success ? Reader.Default.Read<Single>(checked(dataAddr + (uint)MaxHpOffset), out success) : 0;
                    }
                    catch
                    {
                        continue;
                    }
                    // Read position pointer
                    uint posPtr;
                    try
                    {

                        posPtr = Reader.Default.Read<uint>(checked(dataAddr + (uint)PosPtrOffset), out success);
                    }
                    catch
                    {
                        posPtr = 0;
                    }
                    float x = 0, y = 0, z = 0;
                    if (success && posPtr != 0)
                    {
                        try
                        {
                            x = Reader.Default.Read<Single>(checked(posPtr + (uint)XyzOffsets[0]), out success);
                            y = success ? Reader.Default.Read<Single>(checked(posPtr + (uint)XyzOffsets[1]), out success) : 0;
                            z = success ? Reader.Default.Read<Single>(checked(posPtr + (uint)XyzOffsets[2]), out success) : 0;
                        }
                        catch
                        {
                            // Skip if coordinates fail
                        }
                    }

                    // Read team and type (for validation)
                    byte team;
                    int typeValue;
                    try
                    {
                        team = Reader.Default.Read<Byte>(checked(dataAddr + (uint)TeamOffset), out success);
                        typeValue = success ? Reader.Default.Read<Int32>(checked(dataAddr + (uint)TypeOffset), out success) : 0;
                    }
                    catch
                    {
                        continue;
                    }

                    // Skip invalid entities (like Entity 1)
                    if (maxhpInt >=0 &&maxhpInt <= 30000 &&hpInt <= 30000 && hpInt >= 0 && (x != 0 || y != 0 || z != 0))
                    {

                        _selfInfo.Targets.Add(new Entity
                        {
                            CurrentHp = hpInt <= 1000 && hpInt >= 0 ? hpInt : (int)hpFloat,
                            MaxHp = maxhpInt, // Not available; set to 0 or assume same as CurrentHp
                            Position = new Vector3(x, y, z)
                        });
                    }
                }

                // Move to next node
                try
                {
                    current = Reader.Default.Read<uint>(current, out success);
                }
                catch
                {
                    break;
                }
                if (!success)
                {
                    break;
                }
            }

            // No need to log; Targets is updated
        }

        private static uint GetModuleBaseAddress(string moduleName)
        {
            try
            {
                var modules = Query.Default.GetModules();
                var module = modules.FirstOrDefault(m => m.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
                return module != null ? (uint)module.BaseAddress : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static uint ReadPointerChain(uint baseAddr, int[] offsets)
        {
            uint addr = baseAddr;
            bool success;
            foreach (int offset in offsets)
            {
                try
                {
                    addr = Reader.Default.Read<uint>(addr, out success);
                }
                catch
                {
                    return 0;
                }
                if (!success || addr == 0)
                {
                    return 0;
                }
                try
                {
                    addr = checked(addr + (uint)offset);
                }
                catch
                {
                    return 0;
                }
            }
            return addr;
        }
    }
