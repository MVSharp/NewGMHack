# DEPRECATED - This document has been superseded

**Status:** This planning document is outdated. The auto-update system has been implemented and documented in `docs/update-architecture.md`.

**Please refer to:** `docs/update-architecture.md` for the current, implemented architecture.

---

# NewGMHack Auto-Update System Architecture Plan

## Overview
Design an automatic update system where:
- As developer, you push code/merge to master branch
- Create a GitHub Release with build artifacts
- Users' GUI client automatically checks and updates all components on startup
- Updates include: GUI executable, Stub DLL, and Frontend (wwwroot)

## Current State Analysis

### What Exists
- `version.txt` in repo root (currently contains: 1.0.747.10419)
- `VersionCheckService.cs` - checks version but returns early (line 29: `return (true, null)`)
- `build-release.ps1` - builds frontend, GUI, and Stub, updates version.txt
- Version auto-increment system in `NewGMHack.Stub.csproj`

### What Needs to Change
- Replace manual version.txt checking with GitHub Releases API
- Implement download and update mechanism
- Handle safe application restart
- Update three components: GUI, Stub, Frontend

---

## Proposed Architecture

### 1. Server-Side (Developer Workflow)

#### GitHub Release Structure
```
Release Tag: v1.0.747.10419
Release Name: NewGMHack v1.0.747.10419
Assets:
  ├── NewGMHack.zip (complete package)
  ├── NewGMHack.GUI.exe (GUI update)
  ├── NewGMHack.Stub.dll (Stub update)
  └── wwwroot.zip (Frontend update)
```

#### Updated Build & Release Script: `publish-release.ps1`
```powershell
# Steps:
1. Run build-release.ps1 (existing)
2. Package outputs into release artifacts
3. Create GitHub Release using gh CLI or GitHub API
4. Upload release assets
5. Commit version.txt change (if needed)
```

#### GitHub Actions Alternative (Optional)
```yaml
# .github/workflows/release.yml
on:
  push:
    tags:
      - 'v*'
jobs:
  release:
    runs-on: windows-latest
    steps:
      - build frontend
      - build .NET projects (x86)
      - create release
      - upload artifacts
```

### 2. Client-Side (Update Service)

#### New Service: `AutoUpdateService.cs`

**Responsibilities:**
1. Check GitHub Releases for newer version
2. Download update packages
3. Verify file integrity (SHA256 checksums)
4. Apply updates safely
5. Trigger application restart

**Location:** `NewGmHack.GUI/Services/AutoUpdateService.cs`

**Key Methods:**
```csharp
public class AutoUpdateService
{
    // Check if update is available
    public async Task<UpdateInfo?> CheckForUpdateAsync()

    // Download update files to temp directory
    public async Task<bool> DownloadUpdateAsync(UpdateInfo update, IProgress<double> progress)

    // Apply downloaded updates
    public async Task<bool> ApplyUpdateAsync(UpdateInfo update)

    // Restart application with new version
    public void RestartApplication()
}
```

#### Update Flow

```
┌─────────────────────────────────────────────────────────────┐
│ 1. GUI Startup                                              │
│    ├── Show splash screen "Checking for updates..."         │
│    └── Call AutoUpdateService.CheckForUpdateAsync()         │
└─────────────────────────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. Version Check (GitHub Releases API)                      │
│    ├── Fetch latest release from GitHub                     │
│    ├── Compare with local version                           │
│    └── Return UpdateInfo if update available                │
└─────────────────────────────────────────────────────────────┘
                             │
                ┌────────────┴────────────┐
                │ No update available?     │
                └────────────┬────────────┘
                             │ NO
                             ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. User Prompt (optional)                                   │
│    ├── Show update available dialog                         │
│    ├── Display changelog/release notes                      │
│    └── User chooses: Update Now / Skip / Remind Later       │
└─────────────────────────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│ 4. Download Update                                          │
│    ├── Download to temp directory                           │
│    ├── Show progress bar                                    │
│    ├── Verify SHA256 checksums                              │
│    └── Prepare backup of current files                      │
└─────────────────────────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│ 5. Apply Update                                             │
│    ├── Close WebView2/frontend                              │
│    ├── Backup current files to .backup folder               │
│    ├── Extract/update files:                                │
│    │   ├── NewGMHack.GUI.exe (needs restart)               │
│    │   ├── NewGMHack.Stub.dll                               │
│    │   └── wwwroot/* (frontend files)                       │
│    └── Update local version info                            │
└─────────────────────────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│ 6. Safe Restart                                             │
│    ├── Save application state                               │
│    ├── Launch updater process (helper exe)                  │
│    ├── Or use Process.Start with delayed restart            │
│    └── Exit current application                             │
└─────────────────────────────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│ 7. Verification                                             │
│    ├── New version starts                                   │
│    ├── Verify all components loaded                         │
│    ├── Clean up .backup folder                              │
│    └── Log update success                                   │
└─────────────────────────────────────────────────────────────┘
```

### 3. Update Scenarios

#### Scenario A: GUI Update Required
- Download `NewGMHack.GUI.exe` from release
- Save to temp location
- Use updater helper to replace on next restart

#### Scenario B: Stub Update Required
- Download `NewGMHack.Stub.dll`
- Replace in running directory
- Next injection will use new Stub

#### Scenario C: Frontend Update Required
- Download `wwwroot.zip`
- Extract to temp wwwroot
- Replace existing wwwroot contents
- Reload WebView2 if running

#### Scenario D: All Components Update
- Download complete `NewGMHack.zip`
- Extract all files
- Safe restart required

### 4. Safety & Rollback

#### Backup Strategy
```csharp
// Before update, create backup
backup/
├── NewGMHack.GUI.exe
├── NewGMHack.Stub.dll
└── wwwroot/
```

#### Rollback on Failure
```csharp
try
{
    // Apply update
    await ApplyUpdateAsync(update);
}
catch (Exception ex)
{
    // Restore from backup
    RestoreBackup();
    LogError($"Update failed: {ex.Message}");
    // Continue with old version
}
```

#### Integrity Verification
- SHA256 checksums in release notes
- Verify downloaded files before applying
- Verify after update, cleanup backup only on success

---

## Implementation Components

### New Files to Create

1. **`NewGmHack.GUI/Services/AutoUpdateService.cs`**
   - Main update logic
   - GitHub API integration
   - Download and apply updates

2. **`NewGmHack.GUI/Models/UpdateInfo.cs`**
   ```csharp
   public class UpdateInfo
   {
       public string Version { get; set; }
       string ReleaseUrl { get; set; }
       string Changelog { get; set; }
       List<UpdateFile> Files { get; set; }
   }

   public class UpdateFile
   {
       string Name { get; set; }
       string DownloadUrl { get; set; }
       string Sha256Checksum { get; set; }
       string TargetPath { get; set; }
   }
   ```

3. **`NewGmHack.GUI/ViewModels/UpdateViewModel.cs`**
   - UI logic for update dialog
   - Progress reporting
   - User interactions

4. **`NewGmHack.GUI/Views/UpdateWindow.xaml`**
   - Update progress window
   - Changelog display
   - Update controls

5. **`publish-release.ps1`** (root directory)
   - Builds release artifacts
   - Creates GitHub release
   - Uploads packages

6. **`NewGmHack.GUI/updater.exe`** (optional, C# console app)
   - Helper process for replacing GUI exe while running
   - Waits for main process to exit
   - Replaces files and restarts

### Files to Modify

1. **`NewGmHack.GUI/Services/VersionCheckService.cs`**
   - Remove/disable or refactor into AutoUpdateService
   - Current implementation is too simple

2. **`NewGmHack.GUI/App.xaml.cs`**
   - Add update check on startup:
   ```csharp
   protected override async void OnStartup(StartupEventArgs e)
   {
       base.OnStartup(e);

       // Check for updates before main window
       var updateService = new AutoUpdateService();
       var update = await updateService.CheckForUpdateAsync();

       if (update != null)
       {
           // Show update dialog or auto-update
           await ShowUpdateDialog(update);
       }

       // Show main window
       new MainWindow().Show();
   }
   ```

3. **`NewGmHack.GUI/NewGmHack.GUI.csproj`**
   - Add NuGet packages:
     - `Octokit` (GitHub API client)
     - Or use `HttpClient` with raw GitHub API

4. **`build-release.ps1`**
   - Modify to generate SHA256 checksums
   - Output to build artifacts directory

---

## GitHub Releases API Strategy

### Checking for Updates
```csharp
GET https://api.github.com/repos/MVSharp/NewGMHack/releases/latest

Response:
{
  "tag_name": "v1.0.747.10419",
  "name": "NewGMHack v1.0.747.10419",
  "body": "Release notes...",
  "published_at": "2025-01-22T10:00:00Z",
  "assets": [
    {
      "name": "NewGMHack.zip",
      "browser_download_url": "https://github.com/.../NewGMHack.zip",
      "size": 12345678
    }
  ]
}
```

### Version Comparison
```csharp
var localVersion = Assembly.GetExecutingAssembly().GetName().Version;
var remoteVersion = new Version(release.TagName.TrimStart('v'));

if (remoteVersion > localVersion)
{
    // Update available
}
```

---

## Developer Workflow Summary

### First Time Setup
1. Create GitHub personal access token (for releases)
2. Store token in environment variable or GitHub secrets
3. Run `publish-release.ps1` script

### Publishing New Release
```powershell
# 1. Make code changes
git add .
git commit -m "feat: new feature"
git push origin master

# 2. Create and publish release
.\publish-release.ps1

# This script:
# - Builds frontend
# - Builds .NET projects
# - Packages artifacts
# - Creates GitHub release
# - Uploads files
```

---

## User Experience

### Silent Update (Preferred for small updates)
```
Startup → Checking updates → Downloading (background) →
Ready to install → On exit, apply update → Next start: new version
```

### Prompted Update (For major updates)
```
Startup → Checking updates → Dialog: "Update available! Changelog..."
User: [Update Now] [Skip] [Remind Tomorrow]
Update Now → Download → Apply → Restart
```

### Update Window UI
```
┌─────────────────────────────────────────────────┐
│  Update Available                               │
├─────────────────────────────────────────────────┤
│                                                 │
│  New version: v1.0.750.12345                    │
│  Current version: v1.0.747.10419                │
│                                                 │
│  What's new:                                    │
│  • Added feature X                              │
│  • Fixed bug Y                                  │
│                                                 │
│  [████████████░░] 75% (3.2 MB / 4.3 MB)        │
│                                                 │
│  [Update Now]  [Skip]  [Remind Later]           │
└─────────────────────────────────────────────────┘
```

---

## Alternatives Considered

### Option A: Squirrel.Windows (NOT Recommended)
- Pros: Mature, battle-tested
- Cons: Complex setup, Windows-only, overkill for simple app
- Verdict: Too complex for current needs

### Option B: ClickOnce (NOT Recommended)
- Pros: Built-in to .NET
- Cons: Requires specific hosting, inflexible, outdated
- Verdict: Not suitable for GitHub-hosted releases

### Option C: Custom Solution (RECOMMENDED)
- Pros: Full control, GitHub-native, simple
- Cons: Requires implementation
- Verdict: **Best fit for NewGMHack architecture**

---

## Implementation Phases

### Phase 1: Basic Update Check
- Create `AutoUpdateService.cs`
- Check GitHub Releases API
- Show "Update available" notification
- Manual download/install (user does it)

### Phase 2: Auto Download
- Download updates automatically
- Show progress UI
- User confirms install

### Phase 3: Auto Install
- Automatic file replacement
- Safe restart mechanism
- Backup and rollback

### Phase 4: Developer Automation
- `publish-release.ps1` script
- GitHub Actions CI/CD
- Automated checksums

---

## Open Questions for User

1. **Update Behavior Preference:**
   - Silent auto-update on startup?
   - Show update dialog and let user choose?
   - Download in background, install on exit?

2. **Stub Injection Behavior:**
   - When Stub updates, does injected game process need restart?
   - Or can Stub be hot-reloaded?

3. **Rollback Strategy:**
   - Automatic rollback on failure?
   - Manual "restore previous version" option?

4. **Channel Strategy:**
   - Single release channel (master) only?
   - Or beta/pre-release channels for testing?

---

## Security Considerations

1. **Code Signing** (Optional but recommended)
   - Sign executables with certificate
   - Verify signature before update

2. **Checksums**
   - SHA256 for all downloadable files
   - Verify before applying update

3. **HTTPS Only**
   - All downloads via HTTPS (GitHub provides this)

4. **Integrity Check**
   - Post-update verification
   - Only remove backup after successful start

---

## Next Steps

Upon approval of this plan:

1. Create `AutoUpdateService.cs` - GitHub API integration
2. Create `UpdateWindow.xaml` - Update UI
3. Modify `App.xaml.cs` - Startup update check
4. Create `publish-release.ps1` - Release automation
5. Test update flow end-to-end
6. Deploy first release with auto-update

---

## Summary

The proposed solution:
- ✅ Uses GitHub Releases (native to your workflow)
- ✅ Updates all components (GUI, Stub, Frontend)
- ✅ Automatic on startup
- ✅ Safe with backup/rollback
- ✅ Developer-friendly (one script to publish)
- ✅ User-friendly (progressive, transparent)
- ✅ Minimal code changes to existing structure
