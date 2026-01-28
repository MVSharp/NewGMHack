# Focus-Aware Input Injection Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Ensure AutoReady continues working when game loses focus (mouse leaves window), while Aimbot only activates when game is focused and right mouse button is held.

**Architecture:** Introduce helper method `ShouldInjectAimbot()` that centralizes the business logic for aimbot activation (focus check + feature flag). AutoReady remains independent of focus state. Maintains existing right-click safety check inside `InjectAimbot()`.

**Tech Stack:** C# .NET 10.0, Reloaded.Hooks, SharpDX.DirectInput, Windows API (user32.dll)

---

## Context

### Current Behavior
- **Aimbot**: Works when right mouse held (line 103 DirectInputLogicProcessor.cs) ✓
- **AutoReady**: Stops working when mouse leaves game window (focus lost) ✗

### Root Cause
`DirectInputHookManager.cs:151-193` has duplicate focus checking logic:
- When focused: Pass real input + inject AutoReady + inject Aimbot
- When not focused: **ZERO all input** + inject AutoReady + inject Aimbot

The problem is Aimbot runs in background when game loses focus (even though it has right-click check), and the input zeroing may interfere with AutoReady.

### Solution Strategy
1. Extract `ShouldInjectAimbot()` helper method
2. Add focus check before Aimbot injection (safety layer #2)
3. Keep AutoReady independent of focus
4. Maintain existing right-click check (safety layer #1)

---

## Task 1: Add Helper Method `ShouldInjectAimbot()`

**Files:**
- Modify: `NewGMHack.Stub/Hooks/DirectInputHookManager.cs:132-194`

**Step 1: Write the helper method**

```csharp
/// <summary>
/// Determines if aimbot should be injected based on focus state and feature flag.
/// Aimbot requires BOTH: game focused AND feature enabled.
/// The right-click button check happens separately in InjectAimbot().
/// </summary>
private bool ShouldInjectAimbot()
{
    bool isAutoAim = self.ClientConfig.Features.IsFeatureEnable(FeatureName.EnableAutoAim);
    bool isGameFocused = IsGameFocused();

    // Aimbot: only when game is focused (even if right-click is held elsewhere)
    // This prevents aimbot from working when user is in browser/other apps
    return isAutoAim && isGameFocused;
}
```

**Step 2: Locate insertion point**

Add this method after the `IsGameFocused()` method (after line 130).

**Step 3: Verify compilation**

Run: `dotnet build NewGMHack.Stub/NewGMHack.Stub.csproj -c Release -p:Platform=x86`
Expected: SUCCESS (0 errors)

**Step 4: Commit changes**

```bash
git add NewGMHack.Stub/Hooks/DirectInputHookManager.cs
git commit -m "feat: add ShouldInjectAimbot() helper method

Centralizes aimbot activation logic with focus check.
Aimbot now requires both: feature enabled AND game focused.
Right-click button check remains in InjectAimbot() as safety layer #1.

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 2: Replace Aimbot Injection Calls with Helper Method

**Files:**
- Modify: `NewGMHack.Stub/Hooks/DirectInputHookManager.cs:166-170, 186-190`

**Step 1: Update focused branch (line 166-170)**

**Before:**
```csharp
// Aimbot: inject mouse delta when aiming (using refined algorithm in logicProcessor)
if (isAutoAim && deviceType == DeviceType.Mouse && size == Marshal.SizeOf<DIMOUSESTATE>())
{
    logicProcessor.InjectAimbot(dataPtr);
}
```

**After:**
```csharp
// Aimbot: inject mouse delta when aiming (using refined algorithm in logicProcessor)
if (deviceType == DeviceType.Mouse && size == Marshal.SizeOf<DIMOUSESTATE>() && ShouldInjectAimbot())
{
    logicProcessor.InjectAimbot(dataPtr);
}
```

**Step 2: Update non-focused branch (line 186-190)**

**Before:**
```csharp
// Aimbot also works in background
if (isAutoAim && deviceType == DeviceType.Mouse && size == Marshal.SizeOf<DIMOUSESTATE>())
{
    logicProcessor.InjectAimbot(dataPtr);
}
```

**After:**
```csharp
// Aimbot: disabled when game is not focused
// (ShouldInjectAimbot() returns false, preventing background injection)
if (deviceType == DeviceType.Mouse && size == Marshal.SizeOf<DIMOUSESTATE>() && ShouldInjectAimbot())
{
    logicProcessor.InjectAimbot(dataPtr);
}
```

**Step 3: Verify compilation**

Run: `dotnet build NewGMHack.Stub/NewGMHack.Stub.csproj -c Release -p:Platform=x86`
Expected: SUCCESS (0 errors)

**Step 4: Commit changes**

```bash
git add NewGMHack.Stub/Hooks/DirectInputHookManager.cs
git commit -m "refactor: use ShouldInjectAimbot() in both focused/unfocused branches

Replaces direct aimbot flag checks with centralized helper method.
Both branches now use same logic for consistency.
Aimbot now properly disabled when game loses focus.

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 3: Remove Unused Local Variable

**Files:**
- Modify: `NewGMHack.Stub/Hooks/DirectInputHookManager.cs:152-153`

**Step 1: Check if isAutoAim is still used**

Search for all uses of `isAutoAim` variable in `HookedGetDeviceState()` method.

**Step 2: Remove unused variable declaration**

If `isAutoAim` is no longer used after Task 2, remove line 153:

```csharp
bool isAutoAim = self.ClientConfig.Features.IsFeatureEnable(FeatureName.EnableAutoAim);
```

**Note:** If the variable is still used elsewhere in the method, skip this task.

**Step 3: Verify compilation**

Run: `dotnet build NewGMHack.Stub/NewGMHack.Stub.csproj -c Release -p:Platform=x86`
Expected: SUCCESS (0 errors, no CS0219 warnings about unused variable)

**Step 4: Commit changes**

```bash
git add NewGMHack.Stub/Hooks/DirectInputHookManager.cs
git commit -m "refactor: remove unused isAutoAim local variable

Now using ShouldInjectAimbot() helper method instead.
Clean up unused local variable declaration.

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 4: Add XML Documentation to HookedGetDeviceState

**Files:**
- Modify: `NewGMHack.Stub/Hooks/DirectInputHookManager.cs:132`

**Step 1: Add method documentation**

```csharp
/// <summary>
/// Hooked GetDeviceState implementation that handles input injection.
///
/// Behavior:
/// - When GAME IS FOCUSED:
///   - Pass through real user input (keyboard/mouse)
///   - Inject AutoReady synthetic input (if enabled)
///   - Inject Aimbot mouse movement (if enabled AND right-click held)
///
/// - When GAME IS NOT FOCUSED:
///   - Zero all real input (prevent background control)
///   - Inject AutoReady synthetic input (if enabled) - WORKS IN BACKGROUND
///   - NEVER inject Aimbot (disabled by ShouldInjectAimbot())
///
/// Safety layers:
/// 1. Focus check: ShouldInjectAimbot() returns false when backgrounded
/// 2. Button check: InjectAimbot() checks right mouse button internally
///
/// This ensures AutoReady works even when user is in browser/other app,
/// but Aimbot only works when game is actively focused.
/// </summary>
private int HookedGetDeviceState(IntPtr devicePtr, int size, IntPtr dataPtr, DeviceType deviceType)
```

**Step 2: Verify documentation renders correctly**

Run: `dotnet build NewGMHack.Stub/NewGMHack.Stub.csproj -c Release -p:Platform=x86`
Expected: SUCCESS (XML comments compiled into XML doc file)

**Step 3: Commit changes**

```bash
git add NewGMHack.Stub/Hooks/DirectInputHookManager.cs
git commit -m "docs: add XML documentation to HookedGetDeviceState

Clarifies focus behavior for Aimbot vs AutoReady.
Documents two safety layers: focus check + button check.
Explains background behavior for each feature.

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 5: Manual Testing

**Files:**
- Build output: `bin/x86/Release/net10.0-windows7.0/NewGMHack.Stub.dll`

**Step 1: Build the project**

Run: `.\build-release.ps1`
Expected: Clean build with 0 errors

**Step 2: Deploy and test Aimbot behavior**

**Test Case 1: Aimbot when game focused**
1. Launch game and inject Stub
2. Enable AutoAim feature
3. Hold right mouse button while in game window
4. **Expected:** Crosshair moves toward targets

**Test Case 2: Aimbot when game NOT focused**
1. Hold right mouse button
2. Move mouse to browser/second monitor (game loses focus)
3. **Expected:** Aimbot STOPS working immediately (crosshair doesn't move)

**Test Case 3: Aimbot re-enables when focus returns**
1. Return mouse to game window (game regains focus)
2. Still holding right mouse button
3. **Expected:** Aimbot resumes working

**Step 3: Deploy and test AutoReady behavior**

**Test Case 4: AutoReady when game focused**
1. Enable AutoReady feature
2. Stay in game window
3. **Expected:** F5/ESC/click events work correctly

**Test Case 5: AutoReady when game NOT focused**
1. Move mouse to browser (game loses focus)
2. Wait for AutoReady triggers
3. **Expected:** F5/ESC/click events STILL WORK (background operation)

**Test Case 6: AutoReady when minimized**
1. Minimize game window
2. Wait for AutoReady triggers
3. **Expected:** F5/ESC/click events work in background

**Step 4: Document test results**

Create test report notes (not committed):
```markdown
# Test Results - Focus-Aware Input Injection

Aimbot Focus Behavior:
- [ ] Works when game focused
- [ ] Stops when game loses focus
- [ ] Resumes when focus returns

AutoReady Background Behavior:
- [ ] Works when game focused
- [ ] Works when mouse outside window (game still focused)
- [ ] Works when game minimized/backgrounded
- [ ] Does NOT interfere with other applications

Notes:
-
```

**Step 5: Commit if tests pass**

If all test cases pass:
```bash
git add .
git commit -m "test: manual testing complete - focus-aware input injection

Verified:
- Aimbot only works when game is focused (stops on focus loss)
- AutoReady works in background (even when game minimized)
- Right-click safety check still functional
- No interference with other applications

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 6: Update Design Documentation

**Files:**
- Modify: `docs/plans/2025-01-28-focus-aware-input-injection.md` (this file)

**Step 1: Document the implementation**

Add to bottom of this file:

```markdown
## Implementation Notes

### Changes Made
1. Added `ShouldInjectAimbot()` helper method
2. Replaced direct aimbot checks with helper method calls
3. Cleaned up unused `isAutoAim` variable
4. Added comprehensive XML documentation

### Safety Layers (Defense in Depth)
1. **Layer 1 (Internal)**: `InjectAimbot()` checks right mouse button (line 103)
2. **Layer 2 (External)**: `ShouldInjectAimbot()` checks game focus

This ensures aimbot cannot activate accidentally, even if user holds right-click
while switching windows.

### AutoReady Independence
AutoReady injection is NOT wrapped in `ShouldInjectAutoReady()` check.
It remains independent of focus state to enable background operation.
```

**Step 2: Commit documentation**

```bash
git add docs/plans/2025-01-28-focus-aware-input-injection.md
git commit -m "docs: add implementation notes to plan

Documents safety layers and AutoReady independence.
Records defense-in-depth approach for aimbot activation.

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Related Documentation

- **DirectInput Hook Architecture**: `NewGMHack.Stub/Hooks/DirectInputHookManager.cs:11-221`
- **Aimbot Algorithm**: `NewGMHack.Stub/Services/DirectInputLogicProcessor.cs:96-232`
- **Input State Tracking**: `NewGMHack.Stub/Services/InputStateTracker.cs`
- **Feature Configuration**: `NewGMHack.CommunicationModel/Models/FeatureName.cs`

## Testing Checklist

- [ ] Aimbot works when game focused + right-click held
- [ ] Aimbot stops immediately when focus lost
- [ ] Aimbot resumes when focus returns
- [ ] AutoReady works when game focused
- [ ] AutoReady works when mouse outside window (game still focused)
- [ ] AutoReady works when game minimized/backgrounded
- [ ] No interference with other applications during background operation
- [ ] Right-click safety check still functional
- [ ] Build succeeds with 0 errors
- [ ] No CA (code analysis) warnings introduced

## Rollback Plan

If issues occur, revert commits in reverse order:
```bash
git revert HEAD~4..HEAD
```

This will undo all changes while preserving commit history.
