# Auto-Update System Guide

## Overview

NewGMHack now has a fully automated update system using:
- **Autoupdater.NET.Official** (v1.9.2) for update mechanics
- **GitHub Actions** for CI/CD builds
- **Smart component detection** for optimized updates
- **Frontend hot-reload** for seamless UI updates
- **Automatic rollback** on update failures

---

## Features

### ✅ Force Updates
- Updates are **mandatory** - users cannot skip them
- Application checks for updates on every startup
- If GUI or Stub changes, app downloads and restarts automatically
- If only frontend changes, WebView2 hot-reloads without app restart

### ✅ Smart Component Detection
GitHub Actions automatically detects which components changed:
- **Frontend**: `frontend/**` changes → `wwwroot.zip` asset
- **GUI**: `NewGmHack.GUI/**` changes → `NewGMHack.GUI.exe` asset
- **Stub**: `NewGMHack.Stub/**` changes → `NewGMHack.Stub.dll` asset

Only changed components are built and included in releases!

### ✅ Frontend Hot-Reload
When only frontend changes:
1. Download `wwwroot.zip` in background
2. Extract to temp directory
3. Replace `wwwroot/` contents
4. Reload WebView2
5. User sees updated UI **without app restart**

### ✅ Automatic Rollback
If update fails:
1. Backup created before update (`wwwroot/`, `.exe`, `.dll`)
2. On error, automatically restore from `.backup/` folder
3. Log error and continue with old version
4. Retry on next startup

### ✅ SHA256 Verification
All downloads verified against checksums:
- `checksums.txt` included in every release
- Prevents corrupted downloads
- Prevents tampering

---

## Developer Workflow

### Creating a New Release

#### Option 1: GitHub Actions CI/CD (Recommended)

1. **Commit and push changes:**
   ```bash
   git add .
   git commit -m "feat: new feature"
   git push origin master
   ```

2. **Create and push version tag:**
   ```bash
   git tag v1.0.748.10520
   git push origin v1.0.748.10520
   ```

3. **GitHub Actions automatically:**
   - Detects which components changed
   - Builds frontend (if changed) with `pnpm build`
   - Builds GUI (if changed) with `dotnet build -c Release -p:Platform=x86`
   - Builds Stub (if changed) with `dotnet build -c Release -p:Platform=x86`
   - Generates SHA256 checksums
   - Creates `update.xml` for AutoUpdater.NET
   - Creates GitHub Release with assets
   - Uploads all files to release

#### Option 2: Manual Release Script

Use the provided `publish-release.ps1` script (similar to existing `build-release.ps1`):

```powershell
.\publish-release.ps1
```

This script:
1. Runs `build-release.ps1` (builds all components)
2. Packages outputs
3. Creates GitHub Release using `gh` CLI
4. Uploads assets

### Release Assets Structure

Every release includes:
```
v1.0.748.10520/
├── NewGMHack.GUI.exe       # GUI executable (if changed)
├── NewGMHack.Stub.dll      # Stub DLL (if changed)
├── wwwroot.zip             # Frontend files (if changed)
├── update.xml              # AutoUpdater.NET metadata
└── checksums.txt           # SHA256 hashes
```

### GitHub Release Notes

Each release automatically includes:
- Component update status (which parts changed)
- SHA256 checksums
- Installation instructions

---

## User Experience

### Scenario 1: No Update Available

1. User launches `NewGMHack.GUI.exe`
2. Splash screen: "Checking for updates..." (0.5 seconds)
3. No update found
4. App starts normally ✅

### Scenario 2: Frontend Update Only (Hot-Reload)

1. User launches app
2. Check detects new frontend version
3. Download `wwwroot.zip` in background (2-3 seconds)
4. Extract to `wwwroot/`
5. WebView2 auto-reloads
6. User sees updated UI **without restart** ✅

### Scenario 3: GUI or Stub Update (Force Update)

1. User launches app
2. Check detects new version
3. **Modal dialog appears:**
   ```
   ┌─────────────────────────────────┐
   │  📦 Update Available            │
   ├─────────────────────────────────┤
   │  New version: v1.0.748.10520    │
   │  Your version: v1.0.747.10419   │
   │                                 │
   │  Downloading update...          │
   │  [████████████░░] 75% (3.2 MB) │
   │                                 │
   │  Please wait...                 │
   └─────────────────────────────────┘
   ```
4. Download completes
5. Backup created
6. Files replaced
7. App exits and restarts
8. New version launches ✅

### Scenario 4: Update Failure (Rollback)

1. Download starts but fails (network error)
2. OR checksum verification fails
3. Error logged
4. Backup automatically restored
5. App continues with old version
6. Will retry on next startup ✅

---

## Configuration

### Disabling Auto-Update (DEBUG Mode)

In **DEBUG builds**, auto-update is automatically disabled:
```csharp
#if DEBUG
    // Skips update check
#endif
```

### Modifying Update Behavior

Edit `AutoUpdateService.cs`:

```csharp
// Force update (mandatory)
AutoUpdater.Mandatory = true;

// Optional update (user can skip)
AutoUpdater.Mandatory = false;
AutoUpdater.ShowSkipButton = true;

// Silent download, install on exit
AutoUpdater.UpdateMode = Mode.Normal;
```

### Changing GitHub Repository

Edit `AutoUpdateService.cs` constants:
```csharp
private const string GitHubOwner = "MVSharp";
private const string GitHubRepo = "NewGMHack";
```

---

## Troubleshooting

### Update Not Triggering

**Check:**
1. Are you in DEBUG mode? (Updates disabled)
2. Is GitHub repository URL correct in `AutoUpdateService.cs`?
3. Does the latest release have a higher version tag?
4. Is GitHub API accessible from your network?

**Debug:**
Check logs in `logs/` folder for:
```
[INFO] Checking for updates...
[INFO] Already up to date: 1.0.747.10419
```

### Frontend Not Hot-Reloading

**Check:**
1. Does release contain `wwwroot.zip`?
2. Is `wwwroot/` path correct?
3. Is `FrontendUpdateRequired` event wired up in `App.xaml.cs`?

**Debug:**
Check logs for:
```
[INFO] Only frontend changed - applying hot-reload
[INFO] Downloading wwwroot.zip...
```

### Update Stuck Downloading

**Check:**
1. Internet connection
2. GitHub Release URL accessible
3. Sufficient disk space
4. Antivirus not blocking

**Solution:**
Force-close app, delete `.backup/` folder, restart.

### Rollback Not Working

**Check:**
1. `.backup/` folder exists
2. Backup files are not corrupted
3. File permissions allow write

**Solution:**
Manual restore:
```powershell
# Copy backup files back to main directory
Copy-Item .backup\* . -Recurse -Force
```

---

## Testing

### Local Testing (Without GitHub)

To test update flow without creating releases:

1. **Mock GitHub response:**
   Modify `AutoUpdateService.cs` to return test data

2. **Use local files:**
   Place test `wwwroot.zip` in temp directory
   Call `ApplyFrontendUpdateAsync()` directly

### Testing Rollback

1. Intentionally corrupt `wwwroot.zip` download
2. Or modify checksums to wrong value
3. Run update, verify backup restores

### Testing Hot-Reload

1. Make frontend change (CSS/JS)
2. Create release with only `wwwroot.zip`
3. Run app, verify WebView2 reloads without restart

---

## File Structure

```
NewGMHack/
├── .github/workflows/
│   └── release.yml              # CI/CD workflow
├── NewGmHack.GUI/
│   ├── Services/
│   │   ├── AutoUpdateService.cs # Main update logic
│   │   └── VersionCheckService.cs (old, can delete)
│   ├── App.xaml.cs              # Startup update check
│   └── NewMainWindow.xaml.cs    # WebView2 reload
├── AUTO_UPDATE_GUIDE.md         # This file
└── AUTO_UPDATE_PLAN.md          # Original design doc
```

---

## Dependencies

### NuGet Packages
- **Autoupdater.NET.Official** (v1.9.2)
  - GitHub Releases support
  - Update dialogs
  - Download and apply logic

### .NET Build Tools
- **.NET 10.0 SDK** (x86 builds)
- **pnpm** (frontend builds)
- **GitHub Actions** (CI/CD)

---

## Security Considerations

### HTTPS Only
All downloads via HTTPS (GitHub provides this automatically)

### Checksum Verification
- SHA256 for all files
- Verified before applying
- Prevents corruption/tampering

### Backup & Rollback
- Automatic backup before update
- Restore on failure
- Only remove backup on successful launch

### Code Signing (Optional)
You can add code signing to `release.yml`:
```yaml
- name: Sign executables
  run: |
    signtool sign /f certificate.pfx /p password bin/**/*.exe
```

---

## Next Steps

### Optional Enhancements

1. **Beta Channel:**
   - Check for `pre-release` tags
   - Allow users to opt into beta updates

2. **Delta Updates:**
   - Only download changed files (not full zip)
   - Faster updates for large frontends

3. **Scheduled Checks:**
   - Check for updates every X hours (not just on startup)
   - Background download, notify when ready

4. **Update History:**
   - Keep track of installed versions
   - Show "What's New" dialog after update

5. **Auto-Update the Updater:**
   - GitHub Actions can update itself
   - Use workflow dispatch for manual triggers

---

## Support

If you encounter issues:

1. Check `logs/` folder for detailed logs
2. Verify GitHub Actions workflow ran successfully
3. Confirm release assets are present
4. Test network connectivity to GitHub
5. Review this guide's troubleshooting section

---

## Summary

The auto-update system provides:
- ✅ **Mandatory updates** (users always on latest version)
- ✅ **Smart detection** (only build what changed)
- ✅ **Hot-reload** (seamless frontend updates)
- ✅ **Rollback** (safe failure recovery)
- ✅ **Automation** (one command to release)
- ✅ **Verification** (SHA256 checksums)

**Sources:**
- [Autoupdater.NET.Official on NuGet](https://www.nuget.org/packages/Autoupdater.NET.Official)
- [AutoUpdater.NET GitHub Repository](https://github.com/ravibpatel/AutoUpdater.NET)
