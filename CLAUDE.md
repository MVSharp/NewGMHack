# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## AI Configuration & Workflow

### Branch Strategy

This project follows a strict **three-branch workflow**:

```
feat/fix-* (feature branches)
    ↓
dev (development)
    ↓
master (production)
```

**Rules:**
1. **Feature branches** (`feat/*`, `fix/*`) - Created from `dev`, merged back to `dev`
2. **dev** - Integration branch, receives all feature branches
3. **master** - Production branch, only receives merges from `dev`

**Merging Policy:**
- ✅ **ALLOWED**: `feat/*` or `fix/*` → `dev` (via PR)
- ✅ **ALLOWED**: `dev` → `master` (via PR)
- ❌ **FORBIDDEN**: Direct `feat/*` or `fix/*` → `master` PRs
- ❌ **FORBIDDEN**: Direct pushes to `dev` or `master` (must use PRs)

**GitHub CLI Workflow:**
```bash
# 1. Create feature branch from dev
git checkout dev
git checkout -b feat/your-feature-name

# 2. Work and commit to feature branch
git add .
git commit -m "feat(scope): description"

# 3. Push feature branch
git push origin feat/your-feature-name

# 4. Create PR: feat → dev
gh pr create --base dev --head feat/your-feature-name --title "feat(scope): description"

# 5. Squash and merge to dev (keeps history clean)
gh pr merge PR_NUMBER --squash --delete-branch

# 6. Update local dev
git checkout dev
git pull origin dev

# 7. Create PR: dev → master
gh pr create --base master --head dev --title "Release: Description"

# 8. Merge to master (use --merge to preserve release)
gh pr merge PR_NUMBER --merge
```

### Allowed Commands for AI

**Git Operations:**
- ✅ `git checkout` - Switch branches
- ✅ `git checkout -b` - Create new branches
- ✅ `git add` - Stage changes
- ✅ `git commit` - Commit changes
- ✅ `git status` - Check repository state
- ✅ `git log` - View commit history
- ✅ `git diff` - View changes
- ✅ `git pull` - Pull from remote
- ✅ `git push` - Push to remote
- ✅ `git merge` - Merge branches locally
- ✅ `gh pr` - GitHub CLI for PR operations

**Build Commands:**
- ✅ `dotnet build` - Build .NET projects
- ✅ `dotnet test` - Run tests (if available)
- ✅ `pnpm install` - Install frontend dependencies
- ✅ `pnpm build` - Build frontend
- ✅ `./build-release.ps1` - Full release build

**File Operations:**
- ✅ `Read` tool - Read files to understand codebase
- ✅ `Edit` tool - Make targeted edits
- ✅ `Write` tool - Create new files
- ✅ `Glob` tool - Find files by pattern
- ✅ `Grep` tool - Search code

**Prohibited Actions:**
- ❌ Do NOT create direct PRs from `feat/*` or `fix/*` to `master`
- ❌ Do NOT push directly to `dev` or `master` (branch protection rules)
- ❌ Do NOT modify build configurations without asking
- ❌ Do NOT change the branch workflow

### Commit Message Conventions

Follow conventional commits format:
- `feat:` - New features
- `fix:` - Bug fixes
- `docs:` - Documentation changes
- `test:` - Adding or updating tests
- `refactor:` - Code refactoring
- `perf:` - Performance improvements
- `chore:` - Maintenance tasks

Examples:
```
feat(updater): add Spectre.Console progress visualization
fix(updater): properly escape command-line arguments
docs: update architecture documentation for v2.0
```

### Code Review Process

All changes go through PR review:
1. Create PR following branch strategy
2. Automated checks (build, tests) must pass
3. Code review by maintainers
4. Squash merge to `dev` (for features)
5. Merge commit to `master` (for releases)

---

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

### Auto-Update System

The application uses a custom updater stub pattern for reliable updates:

- **Custom Updater Stub** - Separate process handles file replacement
- **Checksum Verification** - SHA256 verification for all downloads
- **Rollback Support** - Automatic restoration on failure
- **Frontend Hot-Reload** - No restart required for frontend-only updates

See `docs/update-architecture.md` for detailed architecture.

Update flow:
1. Main app downloads update to temp directory
2. Main app launches embedded updater stub
3. Main app exits (releasing file lock)
4. Updater replaces files
5. Updater launches new version

**Key Files:**
- `Updater/` - Updater stub project
- `NewGmHack.GUI/Services/AutoUpdateService.cs` - Update orchestration
- `docs/update-architecture.md` - Architecture documentation

---

## Project Context

This is a game modification tool that:
- Injects a DLL (Stub) into a running game process
- Reads and modifies game memory through Squalr.Engine
- Provides a WPF GUI with embedded web interface
- Uses IPC to communicate between GUI and injected Stub
- Displays real-time game data and provides cheat/hack features

**Security Context:** This is a game hacking/cheat tool. It is designed for authorized security testing, CTF competitions, or educational contexts. The memory injection and modification capabilities should only be used on games you own or have explicit permission to modify.
