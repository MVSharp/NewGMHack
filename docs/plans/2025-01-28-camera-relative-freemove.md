# Camera-Relative FreeMove Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement camera-relative 3D movement for FreeMove feature using View Matrix transformation, replacing the current world-axis movement with proper camera-direction-based movement.

**Architecture:** Refactor ScanMySelf into pure data reader and separate ApplyFreeMovement modifier. Extract camera direction vectors from View Matrix to transform WASD key inputs into camera-relative movement vectors.

**Tech Stack:** C# .NET 10.0, SharpDX.Matrix, SharpDX.Vector3/Vector4, Windows API (GetAsyncKeyState)

---

## Context

### Current Behavior (World-Axis Movement)
Location: `NewGMHack.Stub/Services/EntityScannerService.cs:203-218`

```csharp
// Currently just adds/subtracts fixed amounts to X/Y/Z
if (_selfInfo.ClientConfig.Features.IsFeatureEnable(FeatureName.FreeMove))
{
    Vector3 loc = new Vector3 { X = entity.Position.X, Y = entity.Position.Y , Z = entity.Position.Z };
    if ((GetAsyncKeyState((int)Keys.W) & 0x8000) != 0) loc.Z += 50f;
    if ((GetAsyncKeyState((int)Keys.S) & 0x8000) != 0) loc.Z -= 50f;
    if ((GetAsyncKeyState((int)Keys.A) & 0x8000) != 0) loc.X -= 50f;
    if ((GetAsyncKeyState((int)Keys.D) & 0x8000) != 0) loc.X += 50f;
    // ... writes back to memory
}
```

**Problem:** This modifies world X/Z axes, not camera-relative. When camera rotates, "W" still moves along world Z, not where camera is facing.

### Desired Behavior (Camera-Relative Movement)
- **W:** Move forward (along camera's forward vector)
- **S:** Move backward (opposite of camera's forward vector)
- **A:** Move left (opposite of camera's right vector)
- **D:** Move right (along camera's right vector)
- **Space/V:** Move up/down (world Y axis, unchanged)

### Solution Strategy
1. Refactor `ScanMySelf()` into two methods: `ScanMySelf()` (read-only) + `ApplyFreeMovement()` (modifier)
2. Extract View Matrix from Device using `GetTransform(TransformState.View)`
3. Calculate camera direction vectors from View Matrix columns
4. Transform key inputs into camera-relative offsets
5. Write modified position back to memory

---

## Task 1: Add View Matrix Return Value to ScanMySelf

**Files:**
- Modify: `NewGMHack.Stub/Services/EntityScannerService.cs:183-230`

**Step 1: Update method signature**

Current signature:
```csharp
private bool ScanMySelf()
```

New signature:
```csharp
private (Vector3 Position, Matrix ViewMatrix, bool IsValid) ScanMySelf()
```

**Step 2: Read View Matrix from Device**

Add after line 192 (after validating moduleBase):
```csharp
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
```

**Step 3: Update return statement**

Current return (line 198):
```csharp
return true;
```

New return (replace the `return true;` at line 233):
```csharp
return (entity.Position, viewMatrix, true);
```

All other return statements should return `(Vector3.Zero, Matrix.Identity, false)`.

**Step 4: Update caller in scan loop**

Modify the call at line 153:
```csharp
// Before:
var foundSelf = ScanMySelf();

// After:
var (playerPos, viewMatrix, foundSelf) = ScanMySelf();
```

**Step 5: Build and verify**

Run: `dotnet build NewGMHack.Stub/NewGMHack.Stub.csproj -c Release -p:Platform=x86`
Expected: SUCCESS (0 errors)

**Step 6: Commit changes**

```bash
git add NewGMHack.Stub/Services/EntityScannerService.cs
git commit -m "refactor: return View Matrix from ScanMySelf

Prepare for camera-relative FreeMove implementation.
ScanMySelf now returns (Position, ViewMatrix, IsValid) tuple.
View Matrix read from Device using GetTransform(View).

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 2: Extract FreeMove Logic into Separate Method

**Files:**
- Modify: `NewGMHack.Stub/Services/EntityScannerService.cs:183-230`

**Step 1: Create new ApplyFreeMovement method**

Add this new method immediately after `ScanMySelf()` (after the closing brace of `ScanMySelf`, around line 235):

```csharp
/// <summary>
/// Applies camera-relative movement based on WASD key states.
/// Uses View Matrix to transform key inputs into camera-space movement.
/// </summary>
/// <param name="position">Current player position</param>
/// <param name="viewMatrix">View Matrix containing camera rotation</param>
/// <returns>Modified position if keys pressed, original if none</returns>
private Vector3 ApplyFreeMovement(Vector3 position, Matrix viewMatrix)
{
    Vector3 movement = Vector3.Zero;
    float speed = 50f; // Movement speed (units per key press)

    // Extract camera direction vectors from View Matrix
    // View Matrix layout (row-major):
    //   Right.X    Up.X     Forward.X    Translation.X
    //   Right.Y    Up.Y     Forward.Y    Translation.Y
    //   Right.Z    Up.Z     Forward.Z    Translation.Z

    // Column 1 (Right vector): M11, M21, M31
    Vector3 cameraRight = new Vector3(viewMatrix.M11, viewMatrix.M21, viewMatrix.M31);

    // Column 3 (Forward vector): M13, M23, M33 (negated in View Matrix)
    Vector3 cameraForward = new Vector3(-viewMatrix.M13, -viewMatrix.M23, -viewMatrix.M33);

    // Normalize vectors (in case of scaling)
    cameraRight = Vector3.Normalize(cameraRight);
    cameraForward = Vector3.Normalize(cameraForward);

    // Calculate camera-relative movement
    if ((GetAsyncKeyState((int)Keys.W) & 0x8000) != 0)
        movement += cameraForward * speed;  // Forward
    if ((GetAsyncKeyState((int)Keys.S) & 0x8000) != 0)
        movement -= cameraForward * speed;  // Backward
    if ((GetAsyncKeyState((int)Keys.A) & 0x8000) != 0)
        movement -= cameraRight * speed;   // Left (opposite of right)
    if ((GetAsyncKeyState((int)Keys.D) & 0x8000) != 0)
        movement += cameraRight * speed;   // Right
    if ((GetAsyncKeyState((int)Keys.Space) & 0x8000) != 0)
        movement.Y += speed;              // Up (world Y)
    if ((GetAsyncKeyState((int)Keys.V) & 0x8000) != 0)
        movement.Y -= speed;              // Down (world Y)

    // Apply movement to position
    return position + movement;
}
```

**Step 2: Update ScanMySelf to remove FreeMove logic**

Remove lines 203-218 from `ScanMySelf()` (the entire FreeMove block inside the method).

The `ScanMySelf()` method should now only:
- Read player entity data
- Update `_selfInfo.PersonInfo` with HP/Position
- Return the tuple `(Position, ViewMatrix, IsValid)`
- NO feature flags, NO movement logic, NO memory writes

**Step 3: Add FreeMove call in scan loop**

Add this after line 154 (where `ScanMySelf()` is called):
```csharp
// Apply camera-relative movement if FreeMove enabled
if (foundSelf && _selfInfo.ClientConfig.Features.IsFeatureEnable(FeatureName.FreeMove))
{
    Vector3 newPos = ApplyFreeMovement(playerPos, viewMatrix);

    // Write back to memory (reuse existing logic from old ScanMySelf)
    // Need to re-read entityStruct pointer to get position pointer
    var moduleBase = GetModuleBaseAddress();
    if (moduleBase != 0)
    {
        var pointerBase = moduleBase + BaseOffset;
        if (TryReadUInt(pointerBase, out var firstPtr) && firstPtr != 0)
        {
            if (TryReadUInt(firstPtr + MySelfOffset, out var entityStruct) && entityStruct != 0)
            {
                if (TryReadUInt(entityStruct + PosPtrOffset, out var posPtr) && posPtr != 0)
                {
                    WriteFloat(posPtr + XyzOffsets[0], newPos.X);
                    // WriteFloat(posPtr + XyzOffsets[1], newPos.Y); // Y commented out in original
                    WriteFloat(posPtr + XyzOffsets[2], newPos.Z);
                }
            }
        }
    }
}
```

**Step 4: Build and verify**

Run: `dotnet build NewGMHack.Stub/NewGMHack.Stub.csproj -c Release -p:Platform=x86`
Expected: SUCCESS (0 errors)

**Step 5: Commit changes**

```bash
git add NewGMHack.Stub/Services/EntityScannerService.cs
git commit -m "refactor: extract FreeMove into separate method

Separate concerns: ScanMySelf() reads data, ApplyFreeMovement() modifies it.
Implement camera-relative movement using View Matrix direction vectors.
W/S/A/D move along camera forward/right axes, Space/V move along world Y.
Remove FreeMove logic from ScanMySelf to keep it pure data reader.

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 3: Extract Position Read Logic into Helper Method

**Files:**
- Modify: `NewGMHack.Stub/Services/EntityScannerService.cs:183-250`

**Step 1: Extract WritePlayerPosition helper method**

The memory write logic is now duplicated (once in old ScanMySelf, once in new FreeMove call). Extract it:

```csharp
/// <summary>
/// Writes player position to memory at the entity position pointer.
/// </summary>
/// <param name="newPos">New position to write</param>
private void WritePlayerPosition(Vector3 newPos)
{
    var moduleBase = GetModuleBaseAddress();
    if (moduleBase == 0) return;

    var pointerBase = moduleBase + BaseOffset;
    if (!TryReadUInt(pointerBase, out var firstPtr) || firstPtr == 0) return;

    if (!TryReadUInt(firstPtr + MySelfOffset, out var entityStruct) || entityStruct == 0) return;

    if (!TryReadUInt(entityStruct + PosPtrOffset, out var posPtr) || posPtr == 0) return;

    WriteFloat(posPtr + XyzOffsets[0], newPos.X);
    // WriteFloat(posPtr + XyzOffsets[1], newPos.Y); // Y is height, usually not modified
    WriteFloat(posPtr + XyzOffsets[2], newPos.Z);
}
```

**Step 2: Simplify FreeMove call in scan loop**

Replace the entire FreeMove block added in Task 1 Step 3 with:

```csharp
// Apply camera-relative movement if FreeMove enabled
if (foundSelf && _selfInfo.ClientConfig.Features.IsFeatureEnable(FeatureName.FreeMove))
{
    Vector3 newPos = ApplyFreeMovement(playerPos, viewMatrix);
    WritePlayerPosition(newPos);
}
```

**Step 3: Build and verify**

Run: `dotnet build NewGMHack.Stub/NewGMHack.Stub.csproj -c Release -p:Platform=x86`
Expected: SUCCESS (0 errors)

**Step 4: Commit changes**

```bash
git add NewGMHack.Stub/Services/EntityScannerService.cs
git commit -m "refactor: extract WritePlayerPosition helper method

Remove duplicate memory write logic.
Simplifies FreeMove call site.
Single responsibility: WritePlayerPosition handles all pointer traversal.

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 4: Add XML Documentation

**Files:**
- Modify: `NewGMHack.Stub/Services/EntityScannerService.cs:183-235`

**Step 1: Add XML docs to ScanMySelf**

Add before the `ScanMySelf()` method:
```csharp
/// <summary>
/// Scans player entity and extracts position, View Matrix, and validation status.
/// Pure data reader - no modification, no feature flags.
///
/// Returns:
///   - Position: Current player world position (X, Y, Z)
///   - ViewMatrix: Camera view matrix for camera-relative calculations
///   - IsValid: True if data extraction succeeded
///
/// The View Matrix is used by ApplyFreeMovement() to transform WASD key inputs
/// into camera-relative movement vectors.
/// </summary>
private (Vector3 Position, Matrix ViewMatrix, bool IsValid) ScanMySelf()
```

**Step 2: Add XML docs to ApplyFreeMovement**

Already added in Task 2, verify it's present and complete.

**Step 3: Add XML docs to WritePlayerPosition**

Already added in Task 3, verify it's present and complete.

**Step 4: Build and verify**

Run: `dotnet build NewGMHack.Stub/NewGMHack.Stub.csproj -c Release -p:Platform=x86`
Expected: SUCCESS (0 errors), XML comments compiled

**Step 5: Commit changes**

```bash
git add NewGMHack.Stub/Services/EntityScannerService.cs
git commit -m "docs: add XML documentation to FreeMove methods

Document ScanMySelf() data reader contract.
Explain ApplyFreeMovement() camera-relative transformation.
Document WritePlayerPosition() memory write logic.

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 5: Manual Testing

**Files:**
- Build output: `bin/x86/Release/net10.0-windows7.0/NewGMHack.Stub.dll`

**Step 1: Build the project**

Run: `.\build-release.ps1`
Expected: Clean build with 0 errors

**Step 2: Deploy and test basic movement**

**Test Case 1: Forward movement (W key)**
1. Inject Stub into game
2. Enable FreeMove feature
3. Face camera North (world +Z direction)
4. Hold W key
5. **Expected:** Player moves North (along world +Z)

**Test Case 2: Verify camera-relative movement**
1. Rotate camera 90° to the right (now facing East)
2. Hold W key
3. **Expected:** Player moves East (along world +X), NOT North
4. This confirms movement is camera-relative, not world-axis

**Test Case 3: Right strafe (D key)**
1. Face camera North
2. Hold D key
3. **Expected:** Player moves East (right of camera view)

**Test Case 4: Left strafe (A key)**
1. Face camera North
2. Hold A key
3. **Expected:** Player moves West (left of camera view)

**Test Case 5: Up/Down (Space/V)**
1. Hold Space key
2. **Expected:** Player moves up (+Y, flying)
3. Hold V key
4. **Expected:** Player moves down (-Y)

**Test Case 6: Combined movement**
1. Face camera diagonally
2. Hold W + D simultaneously
3. **Expected:** Player moves diagonally forward-right

**Step 3: Document test results**

Create test report notes:
```markdown
# Test Results - Camera-Relative FreeMove

Basic Movement:
- [ ] Forward (W) moves along camera forward vector
- [ ] Backward (S) moves opposite to camera forward
- [ ] Right (D) moves along camera right vector
- [ ] Left (A) moves opposite to camera right
- [ ] Up (Space) moves +Y
- [ ] Down (V) moves -Y

Camera-Relative Verification:
- [ ] Rotating camera changes movement direction
- [ ] Movement stays consistent with camera view

Combined Input:
- [ ] W+D moves forward-right diagonally
- [ ] Any WASD combination works correctly

Notes:
-
```

**Step 4: Commit if tests pass**

If all test cases pass:
```bash
git add .
git commit -m "test: manual testing complete - camera-relative FreeMove

Verified:
- WASD movement is camera-relative (follows camera direction)
- Movement updates correctly when camera rotates
- Up/Down (Space/V) still use world Y axis
- Combined key inputs work correctly
- Memory writes successful

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 6: Performance Optimization (Optional)

**Files:**
- Modify: `NewGMHack.Stub/Services/EntityScannerService.cs`

**Step 1: Consider caching View Matrix**

Currently View Matrix is read on every scan (every 10ms). Consider:

**Option A: Cache View Matrix in SelfInformation**
- Update View Matrix in D3D9HookManager.OnResetDevice()
- Access via `_selfInfo.ViewMatrix` instead of Device read
- Reduces Device API calls significantly

**Option B: Cache View Matrix in EntityScannerService**
- Add private field `Matrix _cachedViewMatrix`
- Only re-read when timestamp changes (throttle)
- Simpler but adds state to scanner

**Option C: Read only when needed**
- Current implementation (read every scan)
- Simple but more Device calls
- May be acceptable performance-wise

**Step 2: Decide if optimization is needed**

Profile the application with FreeMove enabled:
- Is FreeMove causing performance issues?
- How many scans per second? (currently ~100 scans/sec)
- Is Device.GetTransform() expensive?

If performance is acceptable (likely is), skip optimization (YAGNI principle).

If optimization needed, implement Option A (best for performance).

**Step 4: Commit if optimization added**

```bash
git add NewGMHack.Stub/Services/EntityScannerService.cs NewGMHack.Stub/SelfInformation.cs NewGMHack.Stub/Hooks/D3D9HookManager.cs
git commit -m "perf: cache View Matrix to reduce Device API calls

Update View Matrix in D3D9HookManager.OnResetDevice().
Access cached matrix via SelfInformation.ViewMatrix.
Reduces GetTransform calls from 100/sec to 1/scene change.

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Task 7: Add Movement Speed Configuration (Optional Enhancement)

**Files:**
- Modify: `NewGMHack.CommunicationModel/Models/ClientConfig.cs`
- Modify: `NewGMHack.Stub/Services/EntityScannerService.cs`

**Step 1: Add FreeMoveSpeed to ClientConfig**

Add property:
```csharp
public float FreeMoveSpeed { get; set; } = 50f;
```

**Step 2: Read speed from config in ApplyFreeMovement**

Replace hardcoded `50f` with:
```csharp
float speed = _selfInfo.ClientConfig.FreeMoveSpeed;
```

**Step 3: Build and test**

Run: `dotnet build NewGMHack.Stub/NewGMHack.Stub.csproj -c Release -p:Platform=x86`
Expected: SUCCESS

**Step 4: Commit**

```bash
git add NewGMHack.Stub/Services/EntityScannerService.cs NewGMHack.CommunicationModel/Models/ClientConfig.cs
git commit -m "feat: add configurable FreeMove speed

Add FreeMoveSpeed property to ClientConfig (default: 50f).
Allows runtime adjustment of movement speed without code changes.
GUI can now expose speed slider for user customization.

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"
```

---

## Related Documentation

- **EntityScannerService**: `NewGMHack.Stub/Services/EntityScannerService.cs:183-250`
- **OverlayManager WorldToScreen**: `NewGMHack.Stub/Hooks/OverlayManager.cs:315-325` (similar View Matrix usage)
- **DirectInputLogicProcessor GetBestTarget**: `NewGMHack.Stub/Services/DirectInputLogicProcessor.cs:261-293` (Device.GetTransform pattern)
- **SharpDX.Matrix Documentation**: https://docs.sharpdx.org/ (Matrix layout, GetTransform)
- **SelfInformation**: `NewGMHack.Stub/SelfInformation.cs` (DevicePtr property)

## Testing Checklist

- [ ] ScanMySelf returns (Position, ViewMatrix, IsValid) tuple
- [ ] View Matrix is successfully read from Device
- [ ] ApplyFreeMovement correctly extracts direction vectors from View Matrix
- [ ] W key moves along camera forward vector
- [ ] S key moves opposite to camera forward
- [ ] A key moves opposite to camera right
- [ ] D key moves along camera right vector
- [ ] Space/V keys move along world Y axis
- [ ] Movement updates correctly when camera rotates
- [ ] Memory writes succeed (position actually changes in game)
- [ ] Build succeeds with 0 errors
- [ ] No regression in other features (IsIllusion, FreezeEnemy)

## Rollback Plan

If issues occur, revert commits in reverse order:
```bash
git revert HEAD~N..HEAD
```

Where N is the number of commits to revert.

---

## Implementation Notes

### View Matrix Layout

SharpDX Matrix is row-major. For View Matrix:
- **Column 0**: Right vector (X components: M11, M21, M31)
- **Column 2**: Forward vector (X components: M13, M23, M33) - **negated** in View Matrix
- **Column 1**: Up vector (not used for movement)

The negation of forward vector is important - View Matrix stores -Forward to enable the transform.

### Direction Vector Calculation

```csharp
Vector3 cameraRight = new Vector3(viewMatrix.M11, viewMatrix.M21, viewMatrix.M31);
Vector3 cameraForward = new Vector3(-viewMatrix.M13, -viewMatrix.M23, -viewMatrix.M33);
```

### Normalization

Normalize vectors to ensure consistent speed regardless of View Matrix scaling:
```csharp
cameraRight = Vector3.Normalize(cameraRight);
cameraForward = Vector3.Normalize(cameraForward);
```

### Movement Calculation

```csharp
Vector3 movement = Vector3.Zero;
if (W) movement += cameraForward * speed;  // Forward
if (S) movement -= cameraForward * speed;  // Backward
if (D) movement += cameraRight * speed;   // Right
if (A) movement -= cameraRight * speed;   // Left
return position + movement;
```

### Key Design Decisions

1. **Separation of Concerns**: ScanMySelf = read, ApplyFreeMovement = modify
2. **Parameter Injection**: View Matrix passed as parameter, not accessed globally
3. **Traditional FPS Controls**: WASD = camera-relative, Space/V = world Y
4. **View Matrix Source**: Read from Device using GetTransform(View)
5. **Helper Methods**: WritePlayerPosition encapsulates pointer traversal logic
6. **No Premature Optimization**: Read View Matrix every scan (Task 6 optional if needed)

### Before/After Comparison

**Before (World-Axis):**
```csharp
loc.Z += 50f;  // Always moves in world +Z, regardless of camera
```

**After (Camera-Relative):**
```csharp
movement += cameraForward * speed;  // Moves in direction camera is facing
```
