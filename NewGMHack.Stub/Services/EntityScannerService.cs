using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewGMHack.Stub;
using SharpDX;
using ZLogger;
using Squalr.Engine.OS;
using System.Text;
using Squalr.Engine.Memory;

public class EntityScannerService : BackgroundService
{
    private readonly SelfInformation               _selfInfo;
    private readonly ILogger<EntityScannerService> _logger;

    private static readonly string ProcessName =
        Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ==")) + ".exe";

    private const int MaxEntities = 12;

    // Offsets from Lua (all uint for x86)
    //private const uint BaseOffset = 0x013100EC;
    private const           uint   BaseOffset     = 0x012DF06C;
    private static readonly uint[] Offsets        = { 0x40, 0x0, 0x8 };
    private const           uint   HpOffset       = 0x34;
    private const           uint   MaxHpOffset    = 0x38;
    private const           uint   PosPtrOffset   = 0x30;
    private static readonly uint[] XyzOffsets     = { 0x88, 0x8C, 0x90 };
    private const           uint   TeamOffset     = 0x2DC2;
    private const           uint   TypeOffset     = 0x2FE0;
    private const           uint   EntityIdOffset = 0x04;
    private const           uint   MySelfOffset   = 0x60;

    // Cached module base address
    private uint _cachedModuleBase = 0;

    public EntityScannerService(SelfInformation selfInfo, ILogger<EntityScannerService> logger)
    {
        _selfInfo = selfInfo;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EntityScannerService started.");

        var process = Processes.Default.GetProcesses()
                               .FirstOrDefault(p => p.ProcessName.Equals(ProcessName.Replace(".exe", ""),
                                                                         StringComparison.OrdinalIgnoreCase));

        while (process == null && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(100, stoppingToken);
            process = Processes.Default.GetProcesses()
                               .FirstOrDefault(p => p.ProcessName.Equals(ProcessName.Replace(".exe", ""),
                                                                         StringComparison.OrdinalIgnoreCase));
        }

        if (process == null)
        {
            _logger.LogWarning("Target process not found. Service stopping.");
            return;
        }

        Processes.Default.OpenedProcess = process;
        _logger.LogInformation($"Attached to process: {process.ProcessName} (PID: {process.Id})");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_selfInfo.ClientConfig.Features.GetFeature(FeatureName.EnableAutoAim).IsEnabled)
                {
                    await Task.Delay(500);
                    continue;
                }

                var found = await ScanEntities();
                if (!found)
                {
                    for (int i = 0; i < MaxEntities; i++)
                    {
                        _selfInfo.Targets[i].CurrentHp           = 0;
                        _selfInfo.Targets[i].MaxHp               = 0;
                        _selfInfo.Targets[i].EntityPtrAddress    = 0;
                        _selfInfo.Targets[i].EntityPosPtrAddress = 0;
                    }
                }

                var foundSelf = ScanMySelf();
                if (found && foundSelf)
                {
                    GetBestTarget();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in scan loop.");
            }

            await Task.Delay(1, stoppingToken);
        }

        _logger.LogInformation("EntityScannerService stopped.");
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    enum Keys
    {
        W     = 0x57,
        A     = 0x41,
        S     = 0x53,
        D     = 0x44,
        Space = 0x20,
        V     = 0x56
    }

    private bool ScanMySelf()
    {
        try
        {
            var moduleBase = GetModuleBaseAddress(ProcessName);
            if (moduleBase == 0) return false;

            var pointerBase = moduleBase + BaseOffset;
            if (!TryReadUInt(pointerBase,                      out var firstPtr)     || firstPtr     == 0) return false;
            if (!TryReadUInt(firstPtr + MySelfOffset, out var entityStruct) || entityStruct == 0) return false;
            if (!TryReadEntityData(entityStruct, out var entity)) return false;

            if (entity.CurrentHp > 300_000 || entity.MaxHp > 300_000) return false;
            if (entity.CurrentHp < 0       || entity.MaxHp < 0) return false;
            _selfInfo.PersonInfo.CurrentHp = entity.CurrentHp;
            _selfInfo.PersonInfo.MaxHp     = entity.MaxHp;
            _selfInfo.PersonInfo.X         = entity.Position.X;
            _selfInfo.PersonInfo.Y         = entity.Position.Y;
            _selfInfo.PersonInfo.Z         = entity.Position.Z;
            // Mission Bomb Teleport Feature
            if (_selfInfo.ClientConfig.Features.IsFeatureEnable(FeatureName.FreeMove))
            {
                Vector3 loc = new Vector3 { X = entity.Position.X, Y = entity.Position.Y , Z = entity.Position.Z };
                if ((GetAsyncKeyState((int)Keys.W)     & 0x8000) != 0) loc.Z += 50f;
                if ((GetAsyncKeyState((int)Keys.S)     & 0x8000) != 0) loc.Z -= 50f;
                if ((GetAsyncKeyState((int)Keys.A)     & 0x8000) != 0) loc.X -= 50f;
                if ((GetAsyncKeyState((int)Keys.D)     & 0x8000) != 0) loc.X += 50f;
                if ((GetAsyncKeyState((int)Keys.Space) & 0x8000) != 0) loc.Y += 50f;
                if ((GetAsyncKeyState((int)Keys.V)     & 0x8000) != 0) loc.Y -= 50f;
                if (TryReadUInt(entityStruct + PosPtrOffset, out var posPtr) && posPtr != 0)
                {
                    WriteFloat(posPtr + XyzOffsets[0], loc.X);
                    //WriteFloat(posPtr + XyzOffsets[1], loc.Y);
                    WriteFloat(posPtr + XyzOffsets[2], loc.Z);
                }
            }

            if (_selfInfo.ClientConfig.Features.IsFeatureEnable(FeatureName.IsIllusion))
            {
                var loc = GetRandomEntitesLoc();
                if (TryReadUInt(entityStruct + PosPtrOffset, out var posPtr) && posPtr != 0)
                {
                    WriteFloat(posPtr + XyzOffsets[0], loc.x);
                    WriteFloat(posPtr + XyzOffsets[1], loc.y);
                    WriteFloat(posPtr + XyzOffsets[2], loc.z);
                }
            }


            if (_selfInfo.ClientConfig.Features.IsFeatureEnable(FeatureName.SuckStarOverChina))
            {
                var targets = _selfInfo.Targets.Where(x => x.EntityPtrAddress != 0 && x.EntityPosPtrAddress != 0)
                                       .ToList();
                int   count  = targets.Count;
                float radius = 100.0f;

                for (int i = 0; i < count; i++)
                {
                    var e = targets[i];

                    // distribute using spherical coordinates
                    double theta = 2 * Math.PI * i / count;        // azimuth angle
                    double phi   = Math.Acos(2.0 * i / count - 1); // polar angle

                    float offsetX = (float)(radius * Math.Sin(phi) * Math.Cos(theta));
                    float offsetY = (float)(radius * Math.Sin(phi) * Math.Sin(theta));
                    float offsetZ = (float)(radius * Math.Cos(phi));

                    WriteFloat(e.EntityPosPtrAddress + XyzOffsets[0], _selfInfo.PersonInfo.X + offsetX);
                    WriteFloat(e.EntityPosPtrAddress + XyzOffsets[1], _selfInfo.PersonInfo.Y + offsetY);
                    WriteFloat(e.EntityPosPtrAddress + XyzOffsets[2], _selfInfo.PersonInfo.Z + offsetZ);
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private (float x, float y, float z) GetRandomEntitesLoc()
    {
        var entity = _selfInfo.Targets.FirstOrDefault(x => x.CurrentHp > 0);
        return entity != null
            ? (entity.Position.X, entity.Position.Y, entity.Position.Z)
            : (4096, 8191, 4096);
    }

    private async Task<bool> ScanEntities()
    {
        try
        {
            var moduleBase = GetModuleBaseAddress(ProcessName);
            if (moduleBase == 0) return false;

            var baseAddr = moduleBase + BaseOffset;
            if (!IsAddressValid(baseAddr)) return false;
            var entityAddr = ReadPointerChain(baseAddr, Offsets);
            if (entityAddr == 0) return false;

            if (!TryReadUInt(entityAddr - 0x8, out var listHead) || listHead == 0) return false;

            var       visited      = new HashSet<uint>();
            int       currentIndex = 0;
            int       scannedCount = 0;
            uint      current      = listHead;
            const int scanLimit    = 100;

            while (current != 0 && !visited.Contains(current) && scannedCount < scanLimit)
            {
                visited.Add(current);
                scannedCount++;

                if (!TryReadUInt(current + 0x8, out var dataAddr) || dataAddr == 0)
                {
                    current = TryReadUInt(current, out var next) ? next : 0;
                    continue;
                }

                if (!TryReadInt(dataAddr + EntityIdOffset, out var eid))
                {
                    current = TryReadUInt(current, out var next) ? next : 0;
                    continue;
                }

                if (TryReadEntityData(dataAddr, out var entity))
                {
                    entity.Id = eid;
                    if (currentIndex < MaxEntities)
                    {
                        _selfInfo.Targets[currentIndex++] = entity;
                    }
                }

                current = TryReadUInt(current, out var nextNode) ? nextNode : 0;
            }

            // Clear remaining slots
            for (int i = currentIndex; i < MaxEntities; i++)
            {
                _selfInfo.Targets[i].CurrentHp = 0;
                _selfInfo.Targets[i].MaxHp     = 0;
            }

            return currentIndex > 0;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"ScanEntities failed.|{ex.StackTrace}");
            return false;
        }
    }

    private bool TryReadEntityData(uint entityStruct, out Entity entity)
    {
        entity = new Entity();

        if (!TryReadInt(entityStruct + HpOffset,    out int hp) ||
            !TryReadInt(entityStruct + MaxHpOffset, out int maxHp))
            return false;

        if (!TryReadUInt(entityStruct + PosPtrOffset, out var posPtr) || posPtr == 0)
            return false;

        if (!TryReadFloat(posPtr + XyzOffsets[0], out float x) ||
            !TryReadFloat(posPtr + XyzOffsets[1], out float y) ||
            !TryReadFloat(posPtr + XyzOffsets[2], out float z))
            return false;
        if (hp > 600_000 || maxHp > 600_000) return false;
        if (hp < 0       || maxHp < 0) return false;
        var pos = new Vector3(x, y + 50, z);
        // if (!IsValidPosition(pos)) return false;

        entity.CurrentHp           = hp;
        entity.MaxHp               = maxHp;
        entity.Position            = pos;
        entity.EntityPtrAddress    = entityStruct;
        entity.EntityPosPtrAddress = posPtr;
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

            if (module == null) return 0;

            // Safely cast to uint (x86 process: address < 4GB)
            return module.BaseAddress <= uint.MaxValue ? (uint)module.BaseAddress : 0;
        }
        catch
        {
            return 0;
        }
    }

    private Entity GetBestTarget()
    {
        var    crosshair = new Vector2(_selfInfo.CrossHairX, _selfInfo.CrossHairY);
        Entity best      = null;
        float  bestDist  = float.MaxValue;

        foreach (var t in _selfInfo.Targets)
        {
            if (t.CurrentHp <= 0 || t.ScreenX <= 0 || t.ScreenY <= 0)
                continue;

            float dist = Vector2.Distance(crosshair, new Vector2(t.ScreenX, t.ScreenY));
            if (dist <= _selfInfo.AimRadius && dist < bestDist)
            {
                bestDist = dist;
                best     = t;
            }
        }

        foreach (var entity in _selfInfo.Targets)
        {
            entity.IsBest = (entity == best);
        }

        return best;
    }

    private static uint ReadPointerChain(uint baseAddr, uint[] offsets)
    {
        uint addr = baseAddr;
        foreach (var offset in offsets)
        {
            if (!TryReadUInt(addr, out addr) || addr == 0) return 0;
            addr = addr + offset;
        }

        return addr;
    }

    // Fixed: Valid x86 user-mode address range
    private static bool IsAddressValid(uint address)
    {
        // Avoid null, low memory, and kernel space
        return address >= 0x10000 && address < 0x80000000;
    }

    [HandleProcessCorruptedStateExceptions]
    [SecurityCritical]
    private static bool TryReadUInt(uint address, out uint value)
    {
        value = 0;
        if (!IsAddressValid(address)) return false;

        try
        {
            unsafe
            {
                value = *(uint*)address;
            }
            return true;
        }
        catch { return false; }
    }

    [HandleProcessCorruptedStateExceptions]
    [SecurityCritical]
    private static bool TryReadInt(uint address, out int value)
    {
        value = 0;
        if (!IsAddressValid(address)) return false;

        try
        {
            unsafe
            {
                value = *(int*)address;
            }
            return true;
        }
        catch { return false; }
    }

    [HandleProcessCorruptedStateExceptions]
    [SecurityCritical]
    private static bool TryReadFloat(uint address, out float value)
    {
        value = 0;
        if (!IsAddressValid(address)) return false;

        try
        {
            unsafe
            {
                value = *(float*)address;
            }
            return true;
        }
        catch { return false; }
    }

    [HandleProcessCorruptedStateExceptions]
    [SecurityCritical]
    public static void WriteFloat(uint address, float value)
    {
        if (!IsAddressValid(address)) return;

        try
        {
            unsafe
            {
                *(float*)address = value;
            }
        }
        catch { }
    }
}

