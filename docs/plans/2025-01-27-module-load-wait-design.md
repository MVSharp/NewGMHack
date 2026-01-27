# Module Load Wait for Safe Injection

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Prevent Access Violation crashes when injecting Stub by blocking in `Bootstrap()` until critical game modules (DirectInput, Winsock, D3D9) are loaded.

**Architecture:** Add synchronous module load detection in `Entry.Bootstrap()` using `CreateToolhelp32Snapshot` API to poll for loaded modules before allowing hook initialization to proceed.

**Tech Stack:** Windows API (toolhelp32), .NET 10, existing injection infrastructure

---

## Problem Context

When GUI auto-starts the game process via `Process.Start()` at `MainViewModel.cs:134-139`, injection happens immediately. The Stub then initializes hooks (DirectInput, Winsock, D3D9) and HandleCleanerService before the game has loaded the target DLLs, causing random Access Violation crashes.

**Current Race Condition:**
```
Process.Start() → Immediate Injection → Hooks Install → CRASH (modules not loaded yet)
```

**Desired Flow:**
```
Process.Start() → Injection → Wait for Modules → Hooks Install → Success
```

---

## Implementation Tasks

### Task 1: Create Module Wait Helper Service

**Files:**
- Create: `NewGMHack.Stub/Services/ModuleWaitService.cs`
- Reference: `Entry.cs:26-324` (Bootstrap method)

**Step 1: Write the module enumeration P/Invoke declarations**

Create `NewGMHack.Stub/Services/ModuleWaitService.cs`:

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace NewGMHack.Stub.Services;

public class ModuleWaitService
{
    private readonly ILogger<ModuleWaitService> _logger;
    private readonly int _targetPid;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Module32First(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Module32Next(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint TH32CS_SNAPMODULE = 0x00000008;
    private const uint TH32CS_SNAPMODULE32 = 0x00000010;

    [StructLayout(LayoutKind.Sequential)]
    private struct MODULEENTRY32
    {
        public uint dwSize;
        public uint th32ModuleID;
        public uint th32ProcessID;
        public uint GlblcntUsage;
        public uint ProccntUsage;
        public IntPtr modBaseAddr;
        public uint modBaseSize;
        public IntPtr hModule;
        public string szModule;
        public string szExePath;
    }

    public ModuleWaitService(ILogger<ModuleWaitService> logger, int targetPid)
    {
        _logger = logger;
        _targetPid = targetPid;
    }
}
```

**Step 2: Implement module enumeration method**

Add to `ModuleWaitService` class:

```csharp
private HashSet<string> GetLoadedModules()
{
    var modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    IntPtr hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, (uint)_targetPid);
    if (hSnapshot == IntPtr.Zero || hSnapshot == new IntPtr(-1))
    {
        _logger.ZLogWarning($"[ModuleWait] CreateToolhelp32Snapshot failed, error: {Marshal.GetLastWin32Error()}");
        return modules;
    }

    try
    {
        var me32 = new MODULEENTRY32 { dwSize = (uint)Marshal.SizeOf(typeof(MODULEENTRY32)) };

        if (Module32First(hSnapshot, ref me32))
        {
            do
            {
                modules.Add(me32.szModule);
            } while (Module32Next(hSnapshot, ref me32));
        }
    }
    finally
    {
        CloseHandle(hSnapshot);
    }

    return modules;
}
```

**Step 3: Implement synchronous wait method**

Add to `ModuleWaitService` class:

```csharp
public void WaitForModules(string[] requiredModules, int timeoutMs, int checkIntervalMs)
{
    _logger.ZLogInformation($"[ModuleWait] Waiting for modules: {string.Join(", ", requiredModules)}");
    var startTime = DateTime.UtcNow;

    while (true)
    {
        var loadedModules = GetLoadedModules();
        var missingModules = requiredModules.Where(m => !loadedModules.Contains(m)).ToArray();

        if (missingModules.Length == 0)
        {
            _logger.ZLogInformation($"[ModuleWait] All required modules loaded!");
            return;
        }

        var elapsed = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        if (elapsed > timeoutMs)
        {
            throw new TimeoutException($"[ModuleWait] Timeout waiting for modules: {string.Join(", ", missingModules)}");
        }

        _logger.ZLogDebug($"[ModuleWait] Missing modules: {string.Join(", ", missingModules)}, waiting {checkIntervalMs}ms...");
        Thread.Sleep(checkIntervalMs);
    }
}
```

**Step 4: Commit**

```bash
git add NewGMHack.Stub/Services/ModuleWaitService.cs
git commit -m "feat: add ModuleWaitService for synchronous module load detection"
```

---

### Task 2: Integrate Module Wait into Bootstrap

**Files:**
- Modify: `NewGMHack.Stub/Entry.cs:234-243` (Before hostBuilder.RunAsync)

**Step 1: Register ModuleWaitService in DI container**

In `Entry.Bootstrap()`, modify the services configuration at line 69-203:

Find the service registration section (after line 96 where `GmMemory` is registered):

```csharp
services.AddTransient<GmMemory>();
// ADD THIS LINE:
services.AddSingleton<ModuleWaitService>();
```

**Step 2: Call WaitForModules before starting host**

In `Entry.Bootstrap()`, modify the thread starting code at lines 209-247.

Find the host builder creation (line 40) and add module wait BEFORE `hostBuilder.RunAsync()` at line 234.

The current code at line 232-243:

```csharp
try
{
    await hostBuilder.RunAsync();
}
catch (Exception ex)
{
    MessageBox(0, $"{ex.Message} {ex.StackTrace}", "Error", 0);
    try { File.AppendAllText("sdlog.txt", $"[CRITICAL] Host Run Error: {ex.Message} \nStack: {ex.StackTrace}\n"); } catch {}
    await hostBuilder.StopAsync();
}
```

Replace with:

```csharp
try
{
    // Wait for critical game modules to load before hooking
    var moduleWaiter = hostBuilder.Services.GetRequiredService<ModuleWaitService>();
    var criticalModules = new[] { "dinput8.dll", "ws2_32.dll", "d3d9.dll" };
    moduleWaiter.WaitForModules(criticalModules, timeoutMs: 30000, checkIntervalMs: 500);

    await hostBuilder.RunAsync();
}
catch (Exception ex)
{
    MessageBox(0, $"{ex.Message} {ex.StackTrace}", "Error", 0);
    try { File.AppendAllText("sdlog.txt", $"[CRITICAL] Host Run Error: {ex.Message} \nStack: {ex.StackTrace}\n"); } catch {}
    await hostBuilder.StopAsync();
}
```

**Step 3: Test compilation**

```bash
dotnet build NewGMHack.Stub/NewGMHack.Stub.csproj -c Release -p:Platform=x86
```

Expected: Clean build with no errors

**Step 4: Commit**

```bash
git add NewGMHack.Stub/Entry.cs
git commit -m "feat: wait for critical modules before initializing hooks"
```

---

### Task 3: Adjust HandleCleanerService Initial Delay

**Files:**
- Modify: `NewGMHack.Stub/Services/HandleCleanerService.cs:25-29` (ExecuteAsync method)

**Step 1: Reduce initial delay since modules are already loaded**

Current code at lines 25-29:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    _logger.ZLogInformation($"[HandleCleaner] Started. Waiting for initialization...");
    // Wait a bit for the game to create its initial locks
    await Task.Delay(5000, stoppingToken);
```

Change the initial delay from 5000ms to 2000ms:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    _logger.ZLogInformation($"[HandleCleaner] Started. Waiting for initialization...");
    // Reduced delay since ModuleWaitService already ensures modules are loaded
    await Task.Delay(2000, stoppingToken);
```

**Step 2: Update log message to reflect new behavior**

Modify line 27:

```csharp
_logger.ZLogInformation($"[HandleCleaner] Started. Modules already verified by ModuleWaitService, brief pause for handle stabilization...");
```

**Step 3: Test compilation**

```bash
dotnet build NewGMHack.Stub/NewGMHack.Stub.csproj -c Release -p:Platform=x86
```

Expected: Clean build

**Step 4: Commit**

```bash
git add NewGMHack.Stub/Services/HandleCleanerService.cs
git commit -m "refactor: reduce HandleCleaner initial delay since modules are pre-verified"
```

---

### Task 4: Add Configuration for Module Wait Timeout

**Files:**
- Create: `NewGMHack.Stub/Configuration/ModuleWaitOptions.cs`
- Modify: `NewGMHack.Stub/Entry.cs:69-76` (ConfigureServices)

**Step 1: Create configuration options class**

Create `NewGMHack.Stub/Configuration/ModuleWaitOptions.cs`:

```csharp
namespace NewGMHack.Stub.Configuration;

public class ModuleWaitOptions
{
    /// <summary>
    /// Maximum time to wait for all critical modules to load (default: 30 seconds)
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Polling interval for checking module load status (default: 500ms)
    /// </summary>
    public int CheckIntervalMs { get; set; } = 500;

    /// <summary>
    /// Critical module names that must be loaded before hooks initialize
    /// </summary>
    public string[] RequiredModules { get; set; } =
    {
        "dinput8.dll",    // DirectInput
        "ws2_32.dll",     // Winsock
        "d3d9.dll"        // DirectX 9
    };
}
```

**Step 2: Register options in DI container**

In `Entry.Bootstrap()`, modify service registration at line 69-203.

Add after line 76 (after `HostOptions` configuration):

```csharp
services.Configure<HostOptions>(hostOptions =>
{
    hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});
// ADD THIS:
services.Configure<ModuleWaitOptions>(options =>
{
    // Use defaults, can be overridden via config.json in future
});
```

**Step 3: Update ModuleWaitService to use IOptions**

Modify `NewGMHack.Stub/Services/ModuleWaitService.cs`:

Update constructor and add using:

```csharp
using Microsoft.Extensions.Options;
using NewGMHack.Stub.Configuration;

public class ModuleWaitService(
    ILogger<ModuleWaitService> logger,
    IOptions<ModuleWaitOptions> options,
    int targetPid)
{
    _logger = logger;
    _targetPid = targetPid;
    _options = options.Value;
}

private readonly ModuleWaitOptions _options;
```

Update `WaitForModules` method signature to use options:

```csharp
public void WaitForModules()
{
    _logger.ZLogInformation($"[ModuleWait] Waiting for modules: {string.Join(", ", _options.RequiredModules)}");
    var startTime = DateTime.UtcNow;

    while (true)
    {
        var loadedModules = GetLoadedModules();
        var missingModules = _options.RequiredModules.Where(m => !loadedModules.Contains(m)).ToArray();

        if (missingModules.Length == 0)
        {
            _logger.ZLogInformation($"[ModuleWait] All required modules loaded!");
            return;
        }

        var elapsed = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        if (elapsed > _options.TimeoutMs)
        {
            throw new TimeoutException($"[ModuleWait] Timeout waiting for modules: {string.Join(", ", missingModules)}");
        }

        _logger.ZLogDebug($"[ModuleWait] Missing modules: {string.Join(", ", missingModules)}, waiting {_options.CheckIntervalMs}ms...");
        Thread.Sleep(_options.CheckIntervalMs);
    }
}
```

**Step 4: Update Entry.cs call site**

In `Entry.Bootstrap()`, update the module wait call at line 235:

Change from:
```csharp
var moduleWaiter = hostBuilder.Services.GetRequiredService<ModuleWaitService>();
var criticalModules = new[] { "dinput8.dll", "ws2_32.dll", "d3d9.dll" };
moduleWaiter.WaitForModules(criticalModules, timeoutMs: 30000, checkIntervalMs: 500);
```

To:
```csharp
var moduleWaiter = hostBuilder.Services.GetRequiredService<ModuleWaitService>();
moduleWaiter.WaitForModules();
```

**Step 5: Test compilation**

```bash
dotnet build NewGMHack.Stub/NewGMHack.Stub.csproj -c Release -p:Platform=x86
```

**Step 6: Commit**

```bash
git add NewGMHack.Stub/Configuration/ModuleWaitOptions.cs NewGMHack.Stub/Services/ModuleWaitService.cs NewGMHack.Stub/Entry.cs
git commit -m "refactor: add configurable module wait options"
```

---

### Task 5: Add Bootstrap Logging for Module Wait

**Files:**
- Modify: `NewGMHack.Stub/Entry.cs:252-262` (BootstrapLog method)

**Step 1: Add detailed logging in ModuleWaitService**

Update `NewGMHack.Stub/Services/ModuleWaitService.cs` to use `BootstrapLog` for early logging before host is ready.

Add static method:

```csharp
private static void BootstrapLog(string message)
{
    try
    {
        var dllDir = Path.GetDirectoryName(typeof(ModuleWaitService).Assembly.Location);
        var path = Path.Combine(dllDir ?? ".", "sdlog.txt");
        File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [ModuleWait]: {message}\n");
    }
    catch { }
}
```

Update all `_logger.ZLog*` calls in `GetLoadedModules()` and `WaitForModules()` to also call `BootstrapLog()`:

Example for WaitForModules loop:

```csharp
while (true)
{
    var loadedModules = GetLoadedModules();
    var missingModules = _options.RequiredModules.Where(m => !loadedModules.Contains(m)).ToArray();

    if (missingModules.Length == 0)
    {
        var msg = $"All required modules loaded!";
        BootstrapLog(msg);
        _logger.ZLogInformation($"[ModuleWait] {msg}");
        return;
    }

    var elapsed = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
    if (elapsed > _options.TimeoutMs)
    {
        var msg = $"Timeout waiting for modules: {string.Join(", ", missingModules)}";
        BootstrapLog(msg);
        throw new TimeoutException($"[ModuleWait] {msg}");
    }

    var debugMsg = $"Missing modules: {string.Join(", ", missingModules)}, waiting {_options.CheckIntervalMs}ms...";
    BootstrapLog(debugMsg);
    _logger.ZLogDebug($"[ModuleWait] {debugMsg}");
    Thread.Sleep(_options.CheckIntervalMs);
}
```

**Step 2: Test compilation**

```bash
dotnet build NewGMHack.Stub/NewGMHack.Stub.csproj -c Release -p:Platform=x86
```

**Step 3: Commit**

```bash
git add NewGMHack.Stub/Services/ModuleWaitService.cs
git commit -m "logging: add bootstrap logging for module wait detection"
```

---

### Task 6: Full Integration Test

**Files:**
- Test: Manual injection test with auto-started process
- Build: `NewGMHack.GUI` and `NewGMHack.Stub`

**Step 1: Build full release**

```powershell
.\build-release.ps1
```

Expected: Clean build of GUI + Stub + Frontend

**Step 2: Configure GUI with process path**

Set `_processPath` in `MainViewModel.cs:111` or via UI to point to game executable.

**Step 3: Test injection with fresh process**

1. Ensure game is NOT running
2. Start NewGMHack.GUI
3. Click Inject button
4. GUI will start game process via `Process.Start()`
5. Check `sdlog.txt` for module wait logs:
   ```
   2025-01-27 10:15:23.456 [ModuleWait]: Waiting for modules: dinput8.dll, ws2_32.dll, d3d9.dll
   2025-01-27 10:15:24.123 [ModuleWait]: Missing modules: dinput8.dll, d3d9.dll, waiting 500ms...
   2025-01-27 10:15:26.789 [ModuleWait]: All required modules loaded!
   ```

**Step 4: Verify no Access Violation**

Expected: Game starts successfully, no crashes within first 30 seconds

**Step 5: Test multiple iterations**

Repeat injection 5-10 times to verify race condition is eliminated.

Expected: 100% success rate (0 crashes)

**Step 6: Document test results**

Create `NewGMHack.Stub/TESTING_NOTES.md`:

```markdown
# Module Wait Testing

## Test Date
2025-01-27

## Test Environment
- Windows 10/11 x86
- Game: [Game Name]
- .NET 10

## Test Results
- Iterations: 10
- Success Rate: 10/10 (100%)
- No Access Violation crashes observed
- Average module wait time: 2-3 seconds

## Module Load Order
Typical module load timing observed:
1. ws2_32.dll - ~1.5s after process start
2. d3d9.dll - ~2.0s after process start
3. dinput8.dll - ~2.5s after process start

## Logs Sample
[Paste sdlog.txt excerpt showing successful module wait]
```

**Step 7: Commit testing notes**

```bash
git add NewGMHack.Stub/TESTING_NOTES.md
git commit -m "test: document module wait integration test results"
```

---

## Architecture Documentation

### Module Load Detection Flow

```
Process.Start() (GUI)
  → InjectDotnet.Inject()
    → Entry.Bootstrap()
      → Build host & services
      → ModuleWaitService.WaitForModules()
        → CreateToolhelp32Snapshot()
        → Module32First/Next() loop
        → Check for dinput8.dll, ws2_32.dll, d3d9.dll
        → Sleep 500ms
        → Repeat until all found OR timeout (30s)
      → hostBuilder.RunAsync()
        → MainHookService.StartAsync()
          → Initialize hooks (now safe, modules loaded)
        → HandleCleanerService.ExecuteAsync()
          → Brief 2s delay (reduced from 5s)
          → Start handle cleaning
```

### Why This Works

1. **Synchronous Wait**: Blocks in `Bootstrap()` before any hooks are installed
2. **Direct API**: Uses `CreateToolhelp32Snapshot` - no race with module loader
3. **Short Poll Interval**: 500ms checks detect module load within <500ms
4. **Timeout Protection**: 30s timeout prevents infinite hangs if game fails to load
5. **Explicit Dependencies**: Only proceeds when DirectInput/Winsock/D3D9 are confirmed loaded

### Configuration

Future enhancement: Allow GUI to pass module list via injection argument:

```csharp
public struct Argument
{
    public IntPtr Title;
    public IntPtr Text;
    public IntPtr ChannelName;
    // ADD: public IntPtr RequiredModules; // JSON array of module names
}
```

---

## Verification Checklist

- [ ] ModuleWaitService compiles without errors
- [ ] Module enumeration works on x86 process
- [ ] Bootstrap blocks until modules load
- [ ] Timeout throws correct exception
- [ ] HandleCleaner delay reduced appropriately
- [ ] Logging shows module detection progress
- [ ] Full build succeeds: `.\build-release.ps1`
- [ ] Manual injection test: 10/10 success rate
- [ ] sdlog.txt shows module wait sequence
- [ ] No Access Violation crashes in testing

---

## Rollback Plan

If module wait causes issues (e.g., hangs on legitimate game configurations):

1. **Quick revert**: Remove `ModuleWaitService.WaitForModules()` call from `Entry.cs:235`
2. **Configuration**: Set `TimeoutMs` to 1000ms in `ModuleWaitOptions` for faster timeout
3. **Fallback**: Revert to original 5s delay in HandleCleaner if needed

To rollback:

```bash
git revert HEAD~5  # Revert all module wait commits
git commit -m "revert: rollback module wait feature due to [issue]"
```

---

## Related Documentation

- `docs/zero-allocation-packet-processing.md` - Performance optimization patterns
- `CLAUDE.md` - Build commands and architecture overview
- `NewGMHack.Stub/Entry.cs` - Injection entry point
- `InjectDotnet/Injector.cs` - Injection implementation

---

## Next Steps (Future Enhancements)

1. **GUI Config**: Add module list configuration to GUI settings
2. **Per-Game Profiles**: Different module lists for different game versions
3. **Async Option**: Provide async module wait alternative for non-blocking scenarios
4. **Module Load Events**: Use `PsSetLoadImageNotifyRoutine` kernel callback for push-based detection (requires driver)
5. **IPC Notification**: Have Stub notify GUI when modules are loaded for progress reporting
