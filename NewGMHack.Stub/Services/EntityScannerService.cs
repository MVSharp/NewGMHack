using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewGMHack.Stub;
using SharpDX;
using ZLogger;
using NewGMHack.Stub.Services.Loggers;
using System.Text;
using System.Diagnostics;
using System.Linq;

public class EntityScannerService : BackgroundService
{
    private readonly SelfInformation               _selfInfo;
    private readonly ILogger<EntityScannerService> _logger;

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

    private struct EntityScanData
    {
        public int Id;
        public int CurrentHp;
        public int MaxHp;
        public Vector3 Position;
        public uint EntityPtrAddress;
        public uint EntityPosPtrAddress;
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

    //private static readonly string ProcessName =
    //    Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ==")) + ".exe";

    private const int MaxEntities = 12;

    // Offsets from Lua (all uint for x86)
    //private const uint BaseOffset = 0x013100EC;
    private const           uint   BaseOffset     =  0x012E606C;
    private static readonly uint[] Offsets        = { 0x40, 0x0, 0x8 };
    private const           uint   HpOffset       = 0x34;
    private const           uint   MaxHpOffset    = 0x38;
    private const           uint   PosPtrOffset   = 0x30;
    private static readonly uint[] XyzOffsets     = { 0x88, 0x8C, 0x90 };
    private const           uint   TeamOffset     = 0x2DC2;
    private const           uint   TypeOffset     = 0x2FE0;
    private const           uint   EntityIdOffset = 0x04;
    private const           uint   MySelfOffset   = 0x60;

    private const float FreeMoveSpeed = 50f;

    // Cached module base address
    private uint _cachedModuleBase = 0;
    private Process _gameProcess;

    public EntityScannerService(SelfInformation selfInfo, ILogger<EntityScannerService> logger)
    {
        _selfInfo = selfInfo;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogServiceStarted();

        _gameProcess = Process.GetCurrentProcess();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Only skip if BOTH overlay AND autoaim are disabled
                bool overlayEnabled = _selfInfo.ClientConfig.Features.GetFeature(FeatureName.EnableOverlay).IsEnabled;
                bool autoAimEnabled = _selfInfo.ClientConfig.Features.GetFeature(FeatureName.EnableAutoAim).IsEnabled;
                if (!overlayEnabled && !autoAimEnabled)
                {
                    await Task.Delay(500, stoppingToken);
                    continue;
                }

                var scanResults = ScanEntities();
                if (scanResults != null)
                {
                    var targets = _selfInfo.Targets;
                    var usedSlots = new bool[targets.Count];
                    var scanUsed = new bool[scanResults.Count];

                    // 1. Pass 1: Update existing matches (preserve object identity/IsBest flags if possible)
                    for (int t = 0; t < targets.Count; t++)
                    {
                        var targetId = targets[t].Id;
                        if (targetId == 0) continue;

                        for (int s = 0; s < scanResults.Count; s++)
                        {
                            if (!scanUsed[s] && scanResults[s].Id == targetId)
                            {
                                UpdateTarget(targets[t], scanResults[s]);
                                usedSlots[t] = true;
                                scanUsed[s] = true;
                                break;
                            }
                        }
                    }

                    // 2. Pass 2: Place new entities into free or stale slots
                    int currentScanIdx = 0;
                    for (int t = 0; t < targets.Count; t++)
                    {
                        if (usedSlots[t]) continue;

                        // Find next unused scan result
                        while (currentScanIdx < scanResults.Count && scanUsed[currentScanIdx])
                        {
                            currentScanIdx++;
                        }

                        if (currentScanIdx < scanResults.Count)
                        {
                            UpdateTarget(targets[t], scanResults[currentScanIdx]);
                            usedSlots[t] = true;
                            scanUsed[currentScanIdx] = true;
                        }
                        else
                        {
                            // No more new entities, clear if it has data
                            if (targets[t].Id != 0)
                            {
                                ClearTarget(targets[t]);
                            }
                        }
                    }
                }

                var (playerPos, viewMatrix, foundSelf) = ScanMySelf();

                // Apply camera-relative movement if FreeMove enabled
                if (foundSelf && _selfInfo.ClientConfig.Features.IsFeatureEnable(FeatureName.FreeMove))
                {
                    Vector3 newPos = ApplyFreeMovement(playerPos, viewMatrix);
                    WritePlayerPosition(newPos);
                }
            }
            catch (Exception ex)
            {
                _logger.LogScanLoopError(ex);
            }

            await Task.Delay(10, stoppingToken);
        }

        _logger.LogServiceStopped();
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

    /// <summary>
    /// Scans player entity and extracts position, View Matrix, and validation status.
    /// Pure data reader - no modification, no feature flags.
    /// </summary>
    /// <returns>
    /// A tuple containing:
    ///   - <c>Position</c>: Current player world position (X, Y, Z)
    ///   - <c>ViewMatrix</c>: Camera view matrix for camera-relative calculations (row-major layout)
    ///   - <c>IsValid</c>: <c>true</c> if data extraction succeeded; <c>false</c> if any read failed or data is invalid
    /// </returns>
    /// <remarks>
    /// <para><b>View Matrix Layout (row-major, SharpDX/Direct3D):</b></para>
    /// <code>
    /// | Right.X   Right.Y   Right.Z   0 |
    /// | Up.X      Up.Y      Up.Z      0 |
    /// | Forward.X Forward.Y Forward.Z 0 |
    /// | Tx        Ty        Tz        1 |
    /// </code>
    /// <para>Rows 0, 1, 2 contain the camera's Right, Up, and Forward direction vectors.</para>
    /// <para>The View Matrix is used by <see cref="ApplyFreeMovement"/> to transform WASD key inputs
    /// into camera-relative movement vectors.</para>
    ///
    /// <para><b>Pointer Chain Traversed:</b></para>
    /// <para><c>moduleBase + BaseOffset</c> → <c>firstPtr</c> → <c>entityStruct</c> → player data</para>
    ///
    /// <para><b>Validation Rules:</b></para>
    /// <list type="bullet">
    ///   <item>Module base address must be valid (non-zero)</item>
    ///   <item>All pointer reads must succeed (non-null pointers)</item>
    ///   <item>HP values must be in valid range (0 to 300,000)</item>
    /// </list>
    ///
    /// <para><b>Side Effects:</b></para>
    /// <para>Updates <c>_selfInfo.PersonInfo</c> with current HP and position for other features.</para>
    /// </remarks>
    private (Vector3 Position, Matrix ViewMatrix, bool IsValid) ScanMySelf()
    {
        try
        {
            var moduleBase = GetModuleBaseAddress();
            if (moduleBase == 0) return (Vector3.Zero, Matrix.Identity, false);

            // Read View Matrix for camera-relative movement calculation
            Matrix viewMatrix = Matrix.Identity;
            if (_selfInfo.DevicePtr != IntPtr.Zero)
            {
                try
                {
                    var device = new SharpDX.Direct3D9.Device(_selfInfo.DevicePtr);
                    viewMatrix = device.GetTransform(SharpDX.Direct3D9.TransformState.View);
                }
                catch
                {
                    // Fallback to identity if Device read fails
                    viewMatrix = Matrix.Identity;
                }
            }

            var pointerBase = moduleBase + BaseOffset;
            if (!TryReadUInt(pointerBase,                      out var firstPtr)     || firstPtr     == 0) return (Vector3.Zero, Matrix.Identity, false);
            if (!TryReadUInt(firstPtr + MySelfOffset, out var entityStruct) || entityStruct == 0) return (Vector3.Zero, Matrix.Identity, false);
            if (!TryReadEntityData(entityStruct, out var entity)) return (Vector3.Zero, Matrix.Identity, false);

            if (entity.CurrentHp > 300_000 || entity.MaxHp > 300_000) return (Vector3.Zero, Matrix.Identity, false);
            if (entity.CurrentHp < 0       || entity.MaxHp < 0) return (Vector3.Zero, Matrix.Identity, false);
            _selfInfo.PersonInfo.CurrentHp = entity.CurrentHp;
            _selfInfo.PersonInfo.MaxHp     = entity.MaxHp;
            _selfInfo.PersonInfo.X         = entity.Position.X;
            _selfInfo.PersonInfo.Y         = entity.Position.Y;
            _selfInfo.PersonInfo.Z         = entity.Position.Z;

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

            if (_selfInfo.ClientConfig.Features.IsFeatureEnable(FeatureName.FreezeEnemy))
            {

                var targets = _selfInfo.Targets.Where(x => x.EntityPtrAddress != 0 && x.EntityPosPtrAddress != 0)
                                       .ToList();
                int   count  = targets.Count;

                for (int i = 0; i < count; i++)
                {
                    var e = targets[i];
                    WriteFloat(e.EntityPosPtrAddress + XyzOffsets[0],  0 );
                    WriteFloat(e.EntityPosPtrAddress + XyzOffsets[1], 200 + i * 50);
                    WriteFloat(e.EntityPosPtrAddress + XyzOffsets[2], 0);
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

            return (entity.Position, viewMatrix, true);
        }
        catch
        {
            return (Vector3.Zero, Matrix.Identity, false);
        }
    }

    /// <summary>
    /// Applies camera-relative movement based on WASD key states.
    /// Uses View Matrix to transform key inputs into camera-space movement.
    /// </summary>
    /// <param name="position">Current player position in world coordinates.</param>
    /// <param name="viewMatrix">View Matrix containing camera rotation (row-major layout).</param>
    /// <returns>
    /// Modified position if movement keys are pressed; otherwise returns the original <paramref name="position"/>.
    /// </returns>
    /// <remarks>
    /// <para><b>Coordinate System:</b></para>
    /// <list type="bullet">
    ///   <item><b>WASD Movement:</b> Camera-relative (follows camera direction)</item>
    ///   <item><b>Space/V Movement:</b> World Y-axis (vertical, independent of camera)</item>
    /// </list>
    ///
    /// <para><b>Key Bindings:</b></para>
    /// <list type="table">
    ///   <listheader><term>Key</term><description>Movement</description></listheader>
    ///   <item><term>W</term><description>Forward (along camera's forward vector)</description></item>
    ///   <item><term>S</term><description>Backward (opposite to camera's forward vector)</description></item>
    ///   <item><term>A</term><description>Left (opposite to camera's right vector)</description></item>
    ///   <item><term>D</term><description>Right (along camera's right vector)</description></item>
    ///   <item><term>Space</term><description>Up (world +Y axis)</description></item>
    ///   <item><term>V</term><description>Down (world -Y axis)</description></item>
    /// </list>
    ///
    /// <para><b>View Matrix to Direction Vectors (simplified):</b></para>
    /// <code>
    /// // Extract forward from row 2 (M31, M33)
    /// float forwardX = viewMatrix.M31;
    /// float forwardZ = viewMatrix.M33;
    ///
    /// // Normalize
    /// float dirX = forwardX / length;
    /// float dirZ = forwardZ / length;
    ///
    /// // Forward: (dirX, 0, dirZ)
    /// // Right: perpendicular clockwise = (dirZ, 0, -dirX)
    /// </code>
    /// <para>
    /// <b>Note:</b> Right vector is calculated as perpendicular to forward, not extracted from matrix.
    /// This avoids issues with alternating vector signs in View Matrix rows.
    /// </para>
    ///
    /// <para><b>Movement Speed:</b></para>
    /// <para>Defined by <c>FreeMoveSpeed</c> constant (50 units per tick).</para>
    /// <para>Multiple keys can be pressed simultaneously for diagonal movement.</para>
    ///
    /// <para><b>Edge Cases Handled:</b></para>
    /// <list type="bullet">
    ///   <item>No keys pressed → returns original position</item>
    ///   <item>Invalid forward vector (length < 0.001) → returns original position</item>
    ///   <item>Right vector calculated from forward (perpendicular, not from matrix)</item>
    /// </list>
    ///
    /// <para><b>Usage:</b></para>
    /// <para>Called by <see cref="ExecuteAsync"/> when FreeMove feature is enabled.</para>
    /// <para>Result is written to game memory via <see cref="WritePlayerPosition"/>.</para>
    /// </remarks>
    private Vector3 ApplyFreeMovement(Vector3 position, Matrix viewMatrix)
    {
        Vector3 movement = Vector3.Zero;

        // Extract forward direction from View Matrix row 2
        float forwardX = viewMatrix.M31;
        float forwardZ = viewMatrix.M33;

        // Detect if we need to negate based on sign pattern
        // When M31 and M33 have opposite signs (quadrants 2 & 4), negate
        bool needNegation = (forwardX * forwardZ) < 0;

        if (needNegation)
        {
            forwardX = -forwardX;
            forwardZ = -forwardZ;
        }

        float forwardLength = (float)Math.Sqrt(forwardX * forwardX + forwardZ * forwardZ);

        if (forwardLength < 0.001f)
            return position;

        // Normalize forward
        float dirX = forwardX / forwardLength;
        float dirZ = forwardZ / forwardLength;

        // Forward vector (camera facing direction)
        Vector3 cameraForward = new Vector3(dirX, 0f, dirZ);

        // Calculate right vector as perpendicular to forward (90° clockwise)
        // If forward is (dirX, dirZ), then right is (dirZ, -dirX)
        Vector3 cameraRight = new Vector3(dirZ, 0f, -dirX);

        // Calculate camera-relative movement
        if ((GetAsyncKeyState((int)Keys.W) & 0x8000) != 0)
            movement += cameraForward * FreeMoveSpeed;  // Forward
        if ((GetAsyncKeyState((int)Keys.S) & 0x8000) != 0)
            movement -= cameraForward * FreeMoveSpeed;  // Backward
        if ((GetAsyncKeyState((int)Keys.A) & 0x8000) != 0)
            movement -= cameraRight * FreeMoveSpeed;   // Left
        if ((GetAsyncKeyState((int)Keys.D) & 0x8000) != 0)
            movement += cameraRight * FreeMoveSpeed;   // Right
        if ((GetAsyncKeyState((int)Keys.Space) & 0x8000) != 0)
            movement.Y += FreeMoveSpeed;              // Up (world Y)
        if ((GetAsyncKeyState((int)Keys.V) & 0x8000) != 0)
            movement.Y -= FreeMoveSpeed;              // Down (world Y)

        // Apply movement to position
        return position + movement;
    }

    /// <summary>
    /// Writes player position to game memory at the entity position pointer.
    /// Traverses the pointer chain to locate the position struct and writes X/Z coordinates.
    /// </summary>
    /// <param name="newPos">New position to write (all components provided, but only X and Z are written).</param>
    /// <remarks>
    /// <para><b>Pointer Chain Traversed:</b></para>
    /// <para><c>moduleBase + BaseOffset</c> → <c>firstPtr</c> → <c>firstPtr + MySelfOffset</c> → <c>entityStruct</c> → <c>entityStruct + PosPtrOffset</c> → <c>posPtr</c> → position data</para>
    ///
    /// <para><b>Memory Layout:</b></para>
    /// <code>
    /// posPtr + XyzOffsets[0] → X coordinate (written)
    /// posPtr + XyzOffsets[1] → Y coordinate (skipped - height is game-controlled)
    /// posPtr + XyzOffsets[2] → Z coordinate (written)
    /// </code>
    ///
    /// <para><b>Y Coordinate Handling:</b></para>
    /// <para>The Y coordinate (height/altitude) is intentionally NOT written to maintain game-controlled gravity and ground collision.
    /// Only X (horizontal) and Z (depth) coordinates are modified for horizontal movement.</para>
    ///
    /// <para><b>Error Handling:</b></para>
    /// <list type="bullet">
    ///   <item>Fails silently if any pointer in the chain is null or invalid</item>
    ///   <item>No exceptions thrown - all errors are caught and logged internally</item>
    ///   <item>Uses <see cref="TryReadUInt"/> and <see cref="IsMemoryWritable"/> for safe memory access</item>
    /// </list>
    ///
    /// <para><b>Usage:</b></para>
    /// <para>Called by <see cref="ExecuteAsync"/> to apply camera-relative movement from <see cref="ApplyFreeMovement"/>.</para>
    /// </remarks>
    private void WritePlayerPosition(Vector3 newPos)
    {
        var moduleBase = GetModuleBaseAddress();
        if (moduleBase == 0) return;

        var pointerBase = moduleBase + BaseOffset;
        if (!TryReadUInt(pointerBase, out var firstPtr) || firstPtr == 0) return;

        if (!TryReadUInt(firstPtr + MySelfOffset, out var entityStruct) || entityStruct == 0) return;

        if (!TryReadUInt(entityStruct + PosPtrOffset, out var posPtr) || posPtr == 0) return;

        WriteFloat(posPtr + XyzOffsets[0], newPos.X);
        //WriteFloat(posPtr + XyzOffsets[1], newPos.Y); // Y is height, usually not modified
        WriteFloat(posPtr + XyzOffsets[2], newPos.Z);
    }

    private (float x, float y, float z) GetRandomEntitesLoc()
    {
        var entity = _selfInfo.Targets.FirstOrDefault(x => x.CurrentHp > 0);
        return entity != null
            ? (entity.Position.X, entity.Position.Y, entity.Position.Z)
            : (4096, 8191, 4096);
    }

    private List<EntityScanData>? ScanEntities()
    {
        try
        {
            var moduleBase = GetModuleBaseAddress();
            if (moduleBase == 0) return null;

            var baseAddr = moduleBase + BaseOffset;
            if (!IsAddressValid(baseAddr)) return null;
            var entityAddr = ReadPointerChain(baseAddr, Offsets);
            if (entityAddr == 0) return null;

            if (!TryReadUInt(entityAddr - 0x8, out var listHead) || listHead == 0) return null;

            var visited = new HashSet<uint>();
            int scannedCount = 0;
            uint current = listHead;
            const int scanLimit = 100;

            var results = new List<EntityScanData>(MaxEntities);

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

                if (TryReadEntityData(dataAddr, out var data))
                {
                    data.Id = eid;
                    if (results.Count < MaxEntities)
                    {
                        results.Add(data);
                    }
                }

                current = TryReadUInt(current, out var nextNode) ? nextNode : 0;
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogScanEntitiesFailed(ex);
            return null;
        }
    }

    private bool TryReadEntityData(uint entityStruct, out EntityScanData entity)
    {
        entity = new EntityScanData();

        if (!TryReadInt(entityStruct + HpOffset, out int hp) ||
            !TryReadInt(entityStruct + MaxHpOffset, out int maxHp))
            return false;

        if (!TryReadUInt(entityStruct + PosPtrOffset, out var posPtr) || posPtr == 0)
            return false;

        if (!TryReadFloat(posPtr + XyzOffsets[0], out float x) ||
            !TryReadFloat(posPtr + XyzOffsets[1], out float y) ||
            !TryReadFloat(posPtr + XyzOffsets[2], out float z))
            return false;
        if (hp > 600_000 || maxHp > 600_000) return false;
        if (hp < 0 || maxHp < 0) return false;
        var pos = new Vector3(x, y + 50, z);

        entity.CurrentHp = hp;
        entity.MaxHp = maxHp;
        entity.Position = pos;
        entity.EntityPtrAddress = entityStruct;
        entity.EntityPosPtrAddress = posPtr;
        return true;
    }



    private static bool IsValidPosition(Vector3 pos)
    {
        return pos.X > -8192 && pos.X < 8192 &&
               pos.Y > -8192 && pos.Y < 8192 &&
               pos.Z > -8192 && pos.Z < 8192;
    }

    private uint GetModuleBaseAddress()
    {
        try
        {
            if (_gameProcess == null || _gameProcess.HasExited) return 0;
            return (uint)_gameProcess.MainModule.BaseAddress;
            // foreach (ProcessModule module in _gameProcess.Modules)
            // {
            //     if (module.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
            //     {
            //         return (uint)module.BaseAddress;
            //     }
            // }
            // return 0;
        }
        catch
        {
            return 0;
        }
    }

    //private Entity GetBestTarget()
    //{
    //    var    crosshair = new Vector2(_selfInfo.CrossHairX, _selfInfo.CrossHairY);
    //    Entity best      = null;
    //    float  bestDist  = float.MaxValue;

    //    foreach (var t in _selfInfo.Targets)
    //    {
    //        if (t.CurrentHp <= 0 || t.ScreenX <= 0 || t.ScreenY <= 0)
    //            continue;

    //        float dist = Vector2.Distance(crosshair, new Vector2(t.ScreenX, t.ScreenY));
    //        if (dist <= _selfInfo.AimRadius && dist < bestDist)
    //        {
    //            bestDist = dist;
    //            best     = t;
    //        }
    //    }

    //    foreach (var entity in _selfInfo.Targets)
    //    {
    //        entity.IsBest = (entity == best);
    //    }

    //    return best;
    //}

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
    // Fixed: Valid x86 user-mode address range
    private static bool IsAddressValid(uint address)
    {
        // Avoid null, low memory, and kernel space
        return address >= 0x10000 && address < 0x80000000;
    }

    private static bool IsMemoryReadable(uint address)
    {
        if (!IsAddressValid(address)) return false;

        MEMORY_BASIC_INFORMATION mbi;
        if (VirtualQuery((IntPtr)address, out mbi, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) == 0)
            return false;

        if (mbi.State != MEM_COMMIT) return false;
        if ((mbi.Protect & PAGE_GUARD) == PAGE_GUARD) return false;
        if ((mbi.Protect & PAGE_NOACCESS) == PAGE_NOACCESS) return false;

        return true; // Any committed memory that isn't Guard/NoAccess is generally readable in this context
    }

    private static bool IsMemoryWritable(uint address)
    {
        if (!IsAddressValid(address)) return false;

        MEMORY_BASIC_INFORMATION mbi;
        if (VirtualQuery((IntPtr)address, out mbi, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) == 0)
            return false;

        if (mbi.State != MEM_COMMIT) return false;
        if ((mbi.Protect & PAGE_GUARD) == PAGE_GUARD) return false;

        // check for write permissions
        bool writable = (mbi.Protect & PAGE_READWRITE) != 0 ||
                        (mbi.Protect & PAGE_WRITECOPY) != 0 ||
                        (mbi.Protect & PAGE_EXECUTE_READWRITE) != 0 ||
                        (mbi.Protect & PAGE_EXECUTE_WRITECOPY) != 0;

        return writable;
    }

    private static bool TryReadUInt(uint address, out uint value)
    {
        value = 0;
        if (!IsMemoryReadable(address)) return false;

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

    private static bool TryReadInt(uint address, out int value)
    {
        value = 0;
        if (!IsMemoryReadable(address)) return false;

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

    private static bool TryReadFloat(uint address, out float value)
    {
        value = 0;
        if (!IsMemoryReadable(address)) return false;

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

    public static void WriteFloat(uint address, float value)
    {
        if (!IsMemoryWritable(address)) return;

        try
        {
            unsafe
            {
                *(float*)address = value;
            }
        }
        catch { }
    }

    private void UpdateTarget(Entity target, EntityScanData data)
    {
        target.Id = data.Id;
        target.CurrentHp = data.CurrentHp;
        target.MaxHp = data.MaxHp;
        target.Position = data.Position;
        target.EntityPtrAddress = data.EntityPtrAddress;
        target.EntityPosPtrAddress = data.EntityPosPtrAddress;
    }

    private void ClearTarget(Entity target)
    {
        target.Id = 0;
        target.CurrentHp = 0;
        target.MaxHp = 0;
        target.EntityPtrAddress = 0;
        target.EntityPosPtrAddress = 0;
        target.IsBest = false;
    }
}

