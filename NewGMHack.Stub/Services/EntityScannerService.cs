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

    private static readonly string ProcessName = Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ==")) + ".exe";
    private const int MaxEntities = 12;

    // Offsets from Lua
    private const int BaseOffset = 0x013100EC;
    private static readonly int[] Offsets = { 0x40, 0x0, 0x8 };
    private const int HpOffset = 0x34;
    private const int MaxHpOffset = 0x38;
    private const int PosPtrOffset = 0x30;
    private static readonly int[] XyzOffsets = { 0x88, 0x8C, 0x90 };
    private const int TeamOffset = 0x2DC2;
    private const int TypeOffset = 0x2FE0;
    private const int EntityIdOffset = 0x04;

    public EntityScannerService(SelfInformation selfInfo, ILogger<EntityScannerService> logger)
    {
        _selfInfo = selfInfo;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EntityScannerService started.");

        var process = Processes.Default.GetProcesses()
            .FirstOrDefault(p => p.ProcessName.Equals(ProcessName.Replace(".exe", ""), StringComparison.OrdinalIgnoreCase));
        while (process == null)
        {
            process = Processes.Default.GetProcesses()
                .FirstOrDefault(p => p.ProcessName.Equals(ProcessName.Replace(".exe", ""), StringComparison.OrdinalIgnoreCase));
            await Task.Delay(100);
        }

        Processes.Default.OpenedProcess = process;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var found = await ScanEntities();
                if (!found)
                {
                    for (int i = 0; i < MaxEntities; i++)
                    {
                        _selfInfo.Targets[i].CurrentHp = 0;
                        _selfInfo.Targets[i].MaxHp = 0;
                    }
                }

                var found1 =ScanMySelf();
                if(found && found1)
                {
                    GetBestTarget(); 
                }
            }
            catch { }

            await Task.Delay(1, stoppingToken);
        }

        _logger.LogInformation("EntityScannerService stopped.");
    }

    private bool ScanMySelf()
    {
        try
        {
            uint moduleBase = GetModuleBaseAddress(ProcessName);
            if (moduleBase == 0) return false;

            uint pointerBase = moduleBase + (uint)BaseOffset;
            if (!TryReadUInt(pointerBase, out uint firstPtr) || firstPtr == 0) return false;

            if (!TryReadUInt(firstPtr + 0x60, out uint entityStruct) || entityStruct == 0) return false;

            if (!TryReadEntityData(entityStruct, out var entity)) return false;

            _selfInfo.PersonInfo.CurrentHp = entity.CurrentHp;
            _selfInfo.PersonInfo.MaxHp = entity.MaxHp;
            _selfInfo.PersonInfo.X = entity.Position.X;
            _selfInfo.PersonInfo.Y = entity.Position.Y;
            _selfInfo.PersonInfo.Z = entity.Position.Z;

            return true;
        }
        catch { return false; }
    }

    private async Task<bool> ScanEntities()
    {
        try
        {
            uint moduleBase = GetModuleBaseAddress(ProcessName);
            if (moduleBase == 0) return false;

            uint baseAddr = moduleBase + (uint)BaseOffset;
            uint entityAddr = ReadPointerChain(baseAddr, Offsets);
            if (entityAddr == 0) return false;

            if (!TryReadUInt(entityAddr - 0x8, out uint listHead) || listHead == 0) return false;

            var visited = new HashSet<uint>();
            int currentIndex = 0;
            uint current = listHead;

            while (current != 0 && !visited.Contains(current) && currentIndex < MaxEntities)
            {
                visited.Add(current);

                if (!TryReadUInt(current + 0x8, out uint dataAddr) || dataAddr == 0)
                {
                    current = TryReadUInt(current, out var next) ? next : 0;
                    continue;
                }

                if (!TryReadInt(dataAddr + EntityIdOffset, out var eid)) { current = TryReadUInt(current, out var next) ? next : 0; continue; }

                if (TryReadEntityData(dataAddr, out var entity))
                {
                    entity.Id = eid;
                    _selfInfo.Targets[currentIndex++] = entity;
                }

                current = TryReadUInt(current, out var nextNode) ? nextNode : 0;
            }

            for (int i = currentIndex; i < MaxEntities; i++)
            {
                _selfInfo.Targets[i].CurrentHp = 0;
                _selfInfo.Targets[i].MaxHp = 0;
            }

            return currentIndex > 0;
        }
        catch { return false; }
    }

    private bool TryReadEntityData(uint entityStruct, out Entity entity)
    {
        entity = new Entity();

        if (!TryReadInt(entityStruct + HpOffset, out int hp) ||
            !TryReadInt(entityStruct + MaxHpOffset, out int maxHp))
            return false;

        if (!TryReadUInt(entityStruct + PosPtrOffset, out uint posPtr) || posPtr == 0)
            return false;

        if (!TryReadFloat((uint)(posPtr + XyzOffsets[0]), out float x) ||
            !TryReadFloat((uint)(posPtr + XyzOffsets[1]), out float y) ||
            !TryReadFloat((uint)(posPtr + XyzOffsets[2]), out float z))
            return false;

        var pos = new Vector3(x, y + 50, z);
        if (!IsValidPosition(pos)) return false;

        entity.CurrentHp = hp;
        entity.MaxHp = maxHp;
        entity.Position = pos;

        return true;
    }

    private static bool IsValidPosition(Vector3 pos)
    {
        return pos.X > -8192 && pos.X < 8192 &&
               pos.Y > -8192 && pos.Y < 8192 &&
               pos.Z > -8192 && pos.Z < 8192;
    }

    private static uint GetModuleBaseAddress(string moduleName)
    {
        try
        {
            var module = Query.Default.GetModules()
                .FirstOrDefault(m => m.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
            return module != null ? (uint)module.BaseAddress : 0;
        }
        catch { return 0; }
    }

    private Entity GetBestTarget()
    {
        var crosshair = new Vector2(_selfInfo.CrossHairX, _selfInfo.CrossHairY); // Fixed coordinate bug
        Entity best = null;
        float bestDist = float.MaxValue;

        foreach (var t in _selfInfo.Targets)
        {
            if (t.CurrentHp <= 0 || t.ScreenX <= 0 || t.ScreenY <= 0)
                continue;

            float dist = Vector2.Distance(crosshair, new Vector2(t.ScreenX, t.ScreenY));
            if (dist <= _selfInfo.AimRadius && dist < bestDist)
            {
                bestDist = dist;
                best = t;
            }
        }

        foreach (var entity in _selfInfo.Targets)
        {
            entity.IsBest = (entity == best);
        }

        return best;
    }
    private static uint ReadPointerChain(uint baseAddr, int[] offsets)
    {
        uint addr = baseAddr;
        foreach (int offset in offsets)
        {
            if (!TryReadUInt(addr, out addr) || addr == 0) return 0;
            addr += (uint)offset;
        }
        return addr;
    }

    private static bool TryReadUInt(uint address, out uint value)
    {
        bool success;
        value = Reader.Default.Read<uint>((ulong)address, out success);
        return success;
    }

    private static bool TryReadInt(uint address, out int value)
    {
        bool success;
        value = Reader.Default.Read<int>((ulong)address, out success);
        return success;
    }

    private static bool TryReadFloat(uint address, out float value)
    {
        bool success;
        value = Reader.Default.Read<float>((ulong)address, out success);
        return success;
    }

    private static bool TryReadByte(uint address, out byte value)
    {
        bool success;
        value = Reader.Default.Read<byte>((ulong)address, out success);
        return success;
    }
}
//public class EntityScannerService : BackgroundService
//{
//    private readonly SelfInformation _selfInfo;
//    private readonly ILogger<EntityScannerService> _logger;

//    private static readonly string ProcessName = Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ==")) + ".exe";
//    private const int MaxEntities = 12;
//    //private const int BaseOffset = 0x5C1FEC; // old client offset
//    private const int BaseOffset = 0x013110EC;
//    //private static readonly int[] Offsets = { 0x50, 0x4, 0x8 };
//    private static readonly int[] Offsets = { 0x40, 0x0, 0x8 };
//    //private static readonly int[] Offsets2 = { 0x68, 0x4, 0x8 };
//    private const int HpOffset = 0x34;
//    private const int MaxHpOffset = 0x38;
//    private const int PosPtrOffset = 0x30;
//    private static readonly int[] XyzOffsets = { 0x88, 0x8C, 0x90 };
//    private const int TeamOffset = 0x2DC2;
//    private const int TypeOffset = 0x2FE0;
//    private const int validOffsetCheck = 0x04; // 30 + 4 then equal 3 
//    private const int validOffsetCheck2 = 0x1F4;// will be equals to it_
//    private const int SelfManagerOffset = 0x5C1FE4; // GOnline.exe+5C1FE4
//    private const int SelfHandleOffset = 0x08;
//    private const int SelfEntityStructOffset = 0x70;
//    private const int SelfNameOffset = 0x88;
//    private const int SelfVisibleOffset = 0x1E;
//    private const int EntityIdOffset = 0x04;
//    public EntityScannerService(SelfInformation selfInfo, ILogger<EntityScannerService> logger)
//    {
//        _selfInfo = selfInfo;
//        _logger = logger;
//    }

//    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//    {
//        _logger.ZLogInformation($"EntityScannerService started.");

//        var process = Processes.Default.GetProcesses()
//            .FirstOrDefault(p => p.ProcessName.Equals(ProcessName.Replace(".exe", ""), StringComparison.OrdinalIgnoreCase));
//        while(process ==null)
//        {

//             process = Processes.Default.GetProcesses()
//            .FirstOrDefault(p => p.ProcessName.Equals(ProcessName.Replace(".exe", ""), StringComparison.OrdinalIgnoreCase));
//            await Task.Delay(100);
//        }
//        Processes.Default.OpenedProcess = process;
//        while (!stoppingToken.IsCancellationRequested)
//        {
//            try
//            {

//           var r= await ScanEntities();
//            if(!r)
//            {

//                for (int i = 0; i < MaxEntities; i++)
//                {
//                    _selfInfo.Targets[i].CurrentHp = 0;
//                    _selfInfo.Targets[i].MaxHp = 0;
//                }
//            }
//              //var r1=  ScanMySelf();
//              //  if(r && r1)
//              //  {
//              //      GetBestTarget();
//              //  }
//            }
//            catch
//            {

//            }
//            await Task.Delay(1, stoppingToken);
//        }

//        _logger.ZLogInformation($"EntityScannerService stopped.");
//    }
//private bool ScanMySelf()
//{
//        try
//        {
//            uint moduleBase = GetModuleBaseAddress(ProcessName);
//            if (moduleBase == 0) return false;

//            uint managerAddr = checked(moduleBase + (uint)0x5C1FE4);
//            if (!TryReadUInt(managerAddr, out uint manager) || manager == 0) return false;

//            if (!TryReadUInt(managerAddr + 0x08, out uint entityHandle) || entityHandle == 0) return false;

//            if (!TryReadUInt(entityHandle + 0x70, out uint entityStruct) || entityStruct == 0) return false;

//            if (entityStruct == 0) return false;

//            if (!TryReadInt(entityStruct + HpOffset, out int hp) || hp < 0 || hp > 30000) return false;
//            if (!TryReadInt(entityStruct + MaxHpOffset, out int maxHp) || maxHp < 0 || maxHp > 30000) return false;

//            Vector3 pos = Vector3.Zero;
//            if (TryReadUInt(entityStruct + PosPtrOffset, out uint posPtr) && posPtr != 0)
//            {
//                if (TryReadFloat(posPtr + 0x88, out float x) &&
//                    TryReadFloat(posPtr + 0x8C, out float y) &&
//                    TryReadFloat(posPtr + 0x90, out float z))
//                {
//                    if (x > -8192 && x < 8192 && y > -8192 && y < 8192 && z > -8192 && z < 8192)
//                        pos = new Vector3(x, y + 50, z);
//                }
//            }

//            _selfInfo.PersonInfo.CurrentHp = hp;
//            _selfInfo.PersonInfo.MaxHp = maxHp;
//            _selfInfo.PersonInfo.X = pos.X;
//            _selfInfo.PersonInfo.Y = pos.Y;
//            _selfInfo.PersonInfo.Z = pos.Z;

//            return true;
//        }
//        catch { }
//        return false;
//}

 
//private Entity GetBestTarget()
//{
//    var crosshair = new Vector2(_selfInfo.CrossHairX, _selfInfo.CrossHairY); // Fixed coordinate bug
//    Entity best = null;
//    float bestDist = float.MaxValue;

//    foreach (var t in _selfInfo.Targets)
//    {
//        if (t.CurrentHp <= 0 || t.ScreenX <= 0 || t.ScreenY <= 0)
//            continue;

//        float dist = Vector2.Distance(crosshair, new Vector2(t.ScreenX, t.ScreenY));
//        if (dist <= _selfInfo.AimRadius && dist < bestDist)
//        {
//            bestDist = dist;
//            best = t;
//        }
//    }

//    foreach (var entity in _selfInfo.Targets)
//    {
//        entity.IsBest = (entity == best);
//    }

//    _lastBestEntityId = best?.Id ?? -1;
//    return best;
//}
//    private int _lastBestEntityId = -1;
//    private async Task<bool> ScanEntities()
//    {
//        //var process = Processes.Default.GetProcesses()
//        //    .FirstOrDefault(p => p.ProcessName.Equals(ProcessName.Replace(".exe", ""), StringComparison.OrdinalIgnoreCase));

//        //if (process == null) return;

//        try
//        {
//            uint moduleBase = GetModuleBaseAddress(ProcessName);
//            if (moduleBase == 0) return false;

//            uint baseAddr;
//            try { baseAddr = checked(moduleBase + (uint)BaseOffset); }
//            catch (OverflowException) { return false; }

//            uint entityAddr = ReadPointerChain(baseAddr, Offsets);
//            if (entityAddr == 0)
//            {
//                //entityAddr = ReadPointerChain(baseAddr, Offsets2);
//                //if (entityAddr == 0) return false;
//                return false;
//            }

//            if (!TryReadUInt(entityAddr - 0x8, out uint listHead) || listHead == 0) return false;

//            var visited = new HashSet<uint>();
//            int currentIndex = 0;
//            uint current = listHead;
//            int scannedCount = 0;
//            while (current != 0 && !visited.Contains(current) )
//            {
//                visited.Add(current);
//                scannedCount++;
//                if (scannedCount >= 100) break;
//                if (!TryReadUInt(current + 0x8, out uint dataAddr) || dataAddr == 0)
//                {
//                    current = TryReadUInt(current, out var next) ? next : 0;
//                    continue;
//                }

//                if (TryReadEntity(dataAddr, out var entity))
//                {

//                    if (currentIndex < MaxEntities)
//                    {

//                        _selfInfo.Targets[currentIndex] = entity;
//                        currentIndex++;
//                    }
//                    else
//                    {
//                        break;
//                    }
//                }
//                current = TryReadUInt(current, out var nextNode) ? nextNode : 0;
//            }

//            // Clear remaining slots
//            for (int i = currentIndex; i < MaxEntities; i++)
//            {
//                _selfInfo.Targets[i].CurrentHp = 0;
//                _selfInfo.Targets[i].MaxHp = 0;
//            }
//            return currentIndex >0;
//        }
//        catch { }
//        return false;
//    }

//    private bool TryReadEntity(uint dataAddr, out Entity entity)
//    {
//        entity = new Entity();
//        if (!TryReadInt(dataAddr + EntityIdOffset, out var eid))
//        {
//            return false;
//        }
//        if (!TryReadInt(dataAddr + HpOffset, out int hpInt) ||
//            !TryReadInt(dataAddr + MaxHpOffset, out int maxHpInt))
//            return false;

//        Vector3 position = Vector3.Zero;
//        if (TryReadUInt(dataAddr + PosPtrOffset, out uint posPtr) && posPtr != 0)
//        {
//            TryReadFloat((uint)(posPtr + XyzOffsets[0]), out float x);
//            TryReadFloat((uint)(posPtr + XyzOffsets[1]), out float y);
//            TryReadFloat((uint)(posPtr + XyzOffsets[2]), out float z);
//            position = new Vector3(x, y, z);
//        }
//        //if(TryReadByte((uint)(posPtr + validOffsetCheck) , out byte b))
//        //{
//        //    if (b != 0x03) return false;
//        //}
//        //if (!TryReadByte(dataAddr + TeamOffset, out byte team) ||
//        //    !TryReadInt(dataAddr + TypeOffset, out int typeValue))
//        //    return false;

//        if (maxHpInt < 0 || maxHpInt > 30000 || hpInt < 0 || hpInt > 30000 || position == Vector3.Zero)
//            return false;
//        if (position.X > 8192 || position.Y > 8192 || position.Z > 8192 || position.X < -8192 || position.Y < -8192 || position.Z < -8192) return false;
//        position.Y += 50;
//        entity.Id = eid;
//        entity.CurrentHp = hpInt;
//        //entity.CurrentHp = hpInt <= 1000 ? hpInt : (int)Reader.Default.Read<float>(dataAddr + HpOffset, out _);
//        entity.MaxHp = maxHpInt;
//        entity.Position = position;

//        return true;
//    }

//    private static uint GetModuleBaseAddress(string moduleName)
//    {
//        try
//        {
//            var module = Query.Default.GetModules()
//                .FirstOrDefault(m => m.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
//            return module != null ? (uint)module.BaseAddress : 0;
//        }
//        catch { return 0; }
//    }

//    private static uint ReadPointerChain(uint baseAddr, int[] offsets)
//    {
//        uint addr = baseAddr;
//        foreach (int offset in offsets)
//        {
//            if (!TryReadUInt(addr, out addr) || addr == 0) return 0;
//            try { addr = checked(addr + (uint)offset); }
//            catch { return 0; }
//        }
//        return addr;
//    }

//    // Helper methods for safe memory reads
//private static bool TryReadUInt(uint address, out uint value)
//{
//    bool success;
//    value = Reader.Default.Read<uint>((ulong)address, out success);
//    return success;
//}

//private static bool TryReadInt(uint address, out int value)
//{
//    bool success;
//    value = Reader.Default.Read<int>((ulong)address, out success);
//    return success;
//}

//private static bool TryReadFloat(uint address, out float value)
//{
//    bool success;
//    value = Reader.Default.Read<float>((ulong)address, out success);
//    return success;
//}

//private static bool TryReadByte(uint address, out byte value)
//{
//    bool success;
//    value = Reader.Default.Read<byte>((ulong)address, out success);
//    return success;
//}
//}
