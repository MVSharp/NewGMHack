# Auto-Update Implementation - Next Session Context

**Date:** 2025-01-22
**Status:** Planning complete, awaiting user decision on approach

---

## Quick Summary

User wants automatic updates for NewGMHack before GUI startup.
- Current: Dummy `version.txt` check (disabled)
- Goal: Auto-update GUI, Stub, and Frontend from GitHub master branch

---

## Files Already Created

1. **`AUTO_UPDATE_PLAN.md`** - Complete custom implementation plan
2. **`TODO_AUTO_UPDATE.md`** (this file) - Session context for next time

---

## Key Decisions Discussed

### Question 1: Existing Solutions
**Answer:** Yes, use **Autoupdater.NET Official**
- GitHub: https://github.com/ravibpatel/AutoUpdater.NET
- NuGet: `Autoupdater.NETOfficial`
- Supports GitHub Releases out of the box
- Simple setup (~5-10 lines of code)
- Updates all file types (exe, dll, wwwroot)

### Question 2: API Key Security
**Answer:** Never commit API keys to repo
**Safe options:**
1. **Environment Variable** (Recommended): `$env:GITHUB_TOKEN`
2. **GitHub CLI** (`gh`): Use `gh auth login` once
3. **GitHub Actions**: Automatic secrets management
4. **Windows Credential Manager**: Store securely

---

## Three Implementation Options

### Option A: Autoupdater.NET (RECOMMENDED)
**Pros:**
- ✅ Proven, battle-tested library
- ✅ Fast implementation (~1-2 hours)
- ✅ Built-in GitHub Releases support
- ✅ Built-in update UI
**Cons:**
- External dependency
- Less customization

### Option B: Custom Implementation (Original Plan)
**Pros:**
- ✅ Full control
- ✅ No external dependencies
- ✅ Tailored to NewGMHack needs
**Cons:**
- Slower to implement (~1-2 days)
- More maintenance

### Option C: Hybrid Approach
**Pros:**
- ✅ Library handles core updates
- ✅ Custom UI/behavior where needed
**Cons:**
- More complex setup

---

## Current Project Structure

```
NewGMHack/
├── NewGmHack.GUI/           # WPF Main App (needs AutoUpdateService)
│   └── Services/
│       ├── VersionCheckService.cs  # Current dummy check (line 29: return true)
│       └── (AutoUpdateService.cs)  # TO BE CREATED
├── NewGMHack.Stub/          # Injected DLL (needs updating)
├── frontend/                # Vue.js + Vite (builds to wwwroot)
│   └── dist/                # Build output
├── build-release.ps1        # Current build script
├── check-version.ps1        # Version update script
├── version.txt              # Current version: 1.0.747.10419
└── (publish-release.ps1)    # TO BE CREATED
```

---

## Implementation Checklist (When Ready)

### Phase 1: Setup & Decision
- [ ] User chooses: Option A (Library) or Option B (Custom) or Option C (Hybrid)
- [ ] Decide update behavior: Silent auto-update vs user prompt
- [ ] Decide Stub injection behavior: Hot-reload or restart required
- [ ] Decide rollback strategy: Auto-rollback or manual

### Phase 2: Developer Workflow (publish-release.ps1)
- [ ] Create `publish-release.ps1` script
- [ ] Integrate with existing `build-release.ps1`
- [ ] Set up GitHub CLI authentication (`gh auth login`)
- [ ] OR set up GITHUB_TOKEN environment variable
- [ ] Test release creation

### Phase 3: Client-Side Implementation

**If using Autoupdater.NET (Option A):**
- [ ] Install NuGet package: `Autoupdater.NETOfficial`
- [ ] Add to `NewGmHack.GUI.csproj`
- [ ] Modify `App.xaml.cs` OnStartup method
- [ ] Configure AutoUpdater settings
- [ ] Test update flow

**If Custom (Option B):**
- [ ] Create `Models/UpdateInfo.cs`
- [ ] Create `Services/AutoUpdateService.cs`
  - GitHub Releases API integration
  - Download logic
  - Update application logic
  - Restart mechanism
- [ ] Create `Views/UpdateWindow.xaml`
- [ ] Create `ViewModels/UpdateViewModel.cs`
- [ ] Modify `App.xaml.cs` OnStartup method
- [ ] Test update flow

### Phase 4: Testing
- [ ] Test version check (no update available)
- [ ] Test version check (update available)
- [ ] Test download progress
- [ ] Test file replacement
- [ ] Test application restart
- [ ] Test rollback on failure
- [ ] Test GUI update
- [ ] Test Stub update
- [ ] Test Frontend update

### Phase 5: Documentation
- [ ] Document developer workflow
- [ ] Document user experience
- [ ] Update README with auto-update info

---

## Commands for Next Session

### When ready to implement, tell me:
```
"Let's implement the auto-update system with Option A"  # Autoupdater.NET
"Let's implement the auto-update system with Option B"  # Custom
"Let's implement the auto-update system with Option C"  # Hybrid
```

### Or ask me to:
- "Show me a demo of Autoupdater.NET"
- "Compare the options again"
- "Update the plan with [specific requirement]"

---

## Open Questions for User

1. **Update behavior:**
   - Silent auto-update on startup?
   - Show dialog with [Update Now] [Skip] buttons?

2. **Stub injection:**
   - When Stub updates, does game process need restart?
   - Or can Stub hot-reload on next feature toggle?

3. **Rollback:**
   - Automatic rollback if update fails?
   - Manual restore from .backup folder?

4. **Implementation approach:**
   - Option A (Autoupdater.NET) - fast & simple?
   - Option B (Custom) - full control?
   - Option C (Hybrid) - best of both?

---

## Quick Reference Links

- Autoupdater.NET GitHub: https://github.com/ravibpatel/AutoUpdater.NET
- GitHub Releases API: https://docs.github.com/en/rest/releases
- GitHub CLI (gh): https://cli.github.com/
- Original Plan: `AUTO_UPDATE_PLAN.md`

---

## Session Notes

**Discussed:**
- ✅ Current state analysis (version.txt, VersionCheckService.cs)
- ✅ Existing solutions research (Squirrel, Velopack, Autoupdater.NET, etc.)
- ✅ API key security concerns and solutions
- ✅ Three implementation approaches

**To Be Done:**
- ⏳ User decision on approach (A/B/C)
- ⏳ Implementation
- ⏳ Testing
- ⏳ Documentation

---

## Git Status Reminder

```
M NewGMHack.Stub/MemoryScanner/GMMemory.cs
M NewGMHack.Stub/Services/PacketProcessorService.cs
?? NewGMHack.CommunicationModel/PacketStructs/Recv/SlotInfoRev.cs
?? NewGMHack.CommunicationModel/PacketStructs/Send/GetSlotInfo.cs
```

These files have uncommitted changes - commit them before working on auto-update to avoid conflicts.

---

**END OF CONTEXT**

Next message should start with: *"Let's implement the auto-update system..."* or similar.
