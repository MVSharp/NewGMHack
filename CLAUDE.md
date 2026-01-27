# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

### Full Release Build
```powershell
.\build-release.ps1
```
This script:
1. Builds frontend with Vite (`pnpm build`)
2. Copies frontend dist to `NewGmHack.GUI/wwwroot`
3. Cleans and builds .NET projects (x86 Release)
4. Updates `version.txt` from Stub DLL version
5. Cleans up temporary files

### Individual Component Builds

**Frontend:**
```bash
cd frontend
pnpm install
pnpm build
```

**.NET Projects:**
```bash
dotnet build NewGmHack.GUI/NewGmHack.GUI.csproj -c Release -p:Platform=x86
dotnet build NewGMHack.Stub/NewGMHack.Stub.csproj -c Release -p:Platform=x86
```

**Check Version:**
```powershell
.\check-version.ps1
```

### Output Location
```
bin/x86/Release/net10.0-windows7.0/NewGmHack.GUI.exe
```

---

## High-Level Architecture

NewGMHack is a game modification tool with a multi-process architecture:

### Core Components

1. **NewGmHack.GUI** (WPF Application)
   - Main executable and user interface
   - Uses MahApps.Metro for WPF controls
   - Hosts WebView2 control displaying Vue.js frontend
   - Manages dependency injection with `Microsoft.Extensions.Hosting`
   - Entry point: `App.xaml.cs` sets up IHost with services

2. **NewGMHack.Stub** (Injected DLL)
   - Injected into target game process
   - Handles game memory reading/writing via Squalr.Engine
   - Uses Reloaded.Hooks for function hooking
   - Communicates with GUI via IPC
   - Auto-incrementing version: `1.0.{days_since_2024-01-01}.{seconds/2}`

3. **NewGMHack.CommunicationModel** (Shared Library)
   - IPC protocols and data structures
   - MessagePack-serialized packet structures
   - Defines `PacketStructs/Send` and `PacketStructs/Recv`
   - Uses `SharedMemory.RpcBuffer` for communication

4. **InjectDotnet** (Injection Library)
   - Multi-target .NET library (net6.0-net10.0)
   - Handles DLL injection into target processes

5. **Frontend** (Vue.js + TypeScript)
   - Built with Vite, optimized for WebView2
   - Embedded into GUI as `wwwroot`
   - Uses Tailwind CSS with custom neon/gundam theme
   - Communicates with GUI backend

### Communication Architecture

```
┌─────────────────┐     IPC      ┌─────────────────┐
│  GUI Process    │◄────────────►│  Stub (Game)    │
│                 │  RpcBuffer   │                 │
│  ┌───────────┐  │              │  Squalr.Engine │
│  │ WebView2  │  │              │  Reloaded.Hooks│
│  └───────────┘  │              └─────────────────┘
└─────────────────┘
```

**Key IPC Details:**
- Shared memory buffer named "Sdhook"
- MessagePack serialization for all IPC messages
- `NotificationHandler` handles incoming messages from Stub
- `RemoteHandler` sends requests to Stub
- Operations defined in `Operation` enum (Health, Info, SetProperty, etc.)

### MVVM Pattern (GUI)

- **Views**: `NewGmHack.GUI/Views/` - WPF XAML windows
- **ViewModels**: `NewGmHack.GUI/ViewModels/` - View logic and state
- **Services**: `NewGmHack.GUI/Services/` - Business logic
- **Abstracts**: `NewGmHack.GUI/Abstracts/` - Interfaces (`GMHackFeatures`, handlers)

### Startup Flow

1. `App.xaml.cs` initializes IHost with DI container
2. Registers `NotificationHandler` and `RpcBuffer`
3. Creates and shows `NewMainWindow`
4. WebView2 loads frontend from `wwwroot/index.html`
5. Injection process loads Stub into target game

---

## Development Notes

### Frontend Development

**Package Manager:** pnpm (specified in package.json)

**Build Config:**
- Vite with Vue 3 + TypeScript
- WebView2 optimizations: no sourcemaps, terser minification, console.log removal
- Tailwind CSS with custom design system (neon-cyan, neon-blue, beam-pink, gundam-gold)
- Path alias: `@` → `frontend/src`

**Frontend Build:**
```bash
cd frontend
pnpm build
# Output: frontend/dist/
```

### Memory Layout & Packet Structures

The codebase extensively uses manual memory layout for game packet structures:
```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SomePacket
{
    public fixed byte Unknown[0x20];  // Fixed-size unknown data
}
```

- Always use `Pack = 1` for game structures
- Use `fixed byte` arrays for unknown/padding fields
- Offset values are critical - match game memory layout exactly

### Logging

Uses ZLogger for high-performance structured logging:
- Rolling files: `logs/{date}_{sequence}.log`
- Daily rotation, 10MB per file
- Configured in `App.xaml.cs`

### Version Management

- Version stored in `version.txt` at repo root
- Auto-updated by `build-release.ps1` from Stub DLL
- Current: `1.0.747.10419`
- Planned: GitHub Releases-based auto-update (see `AUTO_UPDATE_PLAN.md`)

### Platform Requirements

- **x86 builds only** - Target game is x86
- **.NET 10.0** - Latest framework for GUI and Stub
- **Windows-only** - WPF, WebView2, SharpDX dependencies

### Performance Optimizations

The packet processing pipeline uses zero-allocation patterns for high-throughput operation (10k+ packets/sec):

- **Ref struct enumerators** (PacketRefEnumerator, MethodPacketEnumerator) - zero alloc enumeration
- **Direct MemoryMarshal reads** - no ByteReader wrapper overhead
- **ArrayPool** - buffer reuse for accumulator growth
- **Sync/Async split** - sync packets (~80% of traffic) processed with zero allocation
- **ZLinq** - zero-allocation LINQ operations using AsValueEnumerable()

See `docs/zero-allocation-packet-processing.md` for detailed architecture and performance improvements.

---

## Common Patterns

### Adding a New Packet Structure

1. Create struct in `NewGMHack.CommunicationModel/PacketStructs/Send/` or `/Recv/`
2. Use `[StructLayout(LayoutKind.Sequential, Pack = 1)]`
3. Add `fixed byte` fields for unknown data
4. Add Operation enum value if needed
5. Update `PacketProcessorService` to handle new packet

### Adding GUI Features

1. Create ViewModel in `NewGmHack.GUI/ViewModels/`
2. Create View in `NewGmHack.GUI/Views/`
3. Register in `App.xaml.cs` DI container
4. Add to `GMHackFeatures` abstract if it's a toggleable feature

### Adding Stub Services

1. Create service class in `NewGMHack.Stub/Services/`
2. Create logger in `NewGMHack.Stub/Services/Loggers/` if needed
3. Register in Stub's service provider
4. Handle IPC requests via `PacketProcessorService`

---

## Key Dependencies

- **Squalr.Engine** - Memory scanning and reading
- **Reloaded.Hooks** - Function hooking in game
- **SharpDX** - DirectX integration
- **MessagePack** - Binary serialization for IPC
- **SharedMemory** - Inter-process communication
- **ZLogger** - High-performance logging
- **MahApps.Metro** - WPF UI controls
- **Microsoft.Extensions.Hosting** - Dependency injection and hosting

---

## Planned Features

See `AUTO_UPDATE_PLAN.md` for comprehensive auto-update system architecture (GitHub Releases-based, auto-updates GUI/Stub/Frontend).

See `TODO_AUTO_UPDATE.md` for implementation session context when ready to proceed with auto-update.

---

## Project Context

This is a game modification tool that:
- Injects a DLL (Stub) into a running game process
- Reads and modifies game memory through Squalr.Engine
- Provides a WPF GUI with embedded web interface
- Uses IPC to communicate between GUI and injected Stub
- Displays real-time game data and provides cheat/hack features

**Security Context:** This is a game hacking/cheat tool. It is designed for authorized security testing, CTF competitions, or educational contexts. The memory injection and modification capabilities should only be used on games you own or have explicit permission to modify.
