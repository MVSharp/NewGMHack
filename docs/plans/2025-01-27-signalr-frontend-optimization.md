# SignalR & Frontend Optimization Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Optimize SignalR communication and frontend performance by eliminating redundant polling, improving error handling, and fixing memory leaks.

**Architecture:**
- Frontend (Vue 3 + TypeScript) communicates with Backend (ASP.NET Core) via SignalR
- SignalR provides real-time updates: `ReceiveReward`, `UpdatePersonInfo`, `UpdateRoommates`, `UpdateMachineInfo`
- REST API provides initial data and user actions (inject, deattach, feature toggles)
- Current issue: Frontend polls REST API every 1s even though SignalR provides same data

**Tech Stack:**
- Frontend: Vue 3, TypeScript, SignalR client, Vite
- Backend: ASP.NET Core, SignalR, Kestrel web server
- Build: pnpm, dotnet CLI

**Key Optimizations:**
1. Remove redundant 1-second polling (SignalR already covers it)
2. Add connection status SignalR event
3. Standardize on PascalCase (remove dual-case fallbacks)
4. Fix memory leaks (interval cleanup)
5. Add debouncing for stats refresh
6. Improve error handling with user feedback

---

## Phase 1: Connection Status SignalR Event

### Task 1.1: Add UpdateConnectionStatus SignalR Event in Backend

**Files:**
- Modify: `NewGmHack.GUI/Services/HealthCheckServices.cs:15-41`

**Step 1: Read current HealthCheckServices implementation**

Read the file to understand current structure:
```bash
cat NewGmHack.GUI/Services/HealthCheckServices.cs
```

**Step 2: Add connection status tracking**

Modify `HealthCheckServices.cs` to detect connection state changes:

```csharp
// Add field at class level (after line 11)
private bool _wasConnected = false;

// Modify ExecuteAsync method (replace lines 20-34)
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    //return;
    bool isConnected = false;

    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            var healths = await handler.AskForHealth();
            healthCheckHandler.SetHealthStatus(healths);

            // Track connection state
            isConnected = healths != null && healths.Length > 0;

            // Broadcast connection status change
            if (isConnected != _wasConnected)
            {
                _wasConnected = isConnected;
                webChannel.Writer.TryWrite(new WebMessage("UpdateConnectionStatus", new { IsConnected = isConnected }));
            }

            var info = await handler.AskForInfo();
            personInfoHandler.SetInfo(info);
            webChannel.Writer.TryWrite(new WebMessage("UpdatePersonInfo", info));

            var roommates = await handler.GetRoommates();
            roomManager.UpdateRoomList(roommates);
            webChannel.Writer.TryWrite(new WebMessage("UpdateRoommates", roommates));

            await Task.Delay(1000, stoppingToken).ConfigureAwait(false); // Success - wait 1s
        }
        catch
        {
            // Connection lost
            if (_wasConnected)
            {
                _wasConnected = false;
                webChannel.Writer.TryWrite(new WebMessage("UpdateConnectionStatus", new { IsConnected = false }));
            }

            // If failed, wait longer to avoid spamming exceptions on disconnect
            await Task.Delay(2000, stoppingToken).ConfigureAwait(false);
        }
    }
}
```

**Step 3: Test compilation**

Build the project to verify no syntax errors:
```bash
dotnet build NewGmHack.GUI/NewGmHack.GUI.csproj -c Release -p:Platform=x86
```

Expected: Build succeeds with no errors

**Step 4: Commit**

```bash
git add NewGmHack.GUI/Services/HealthCheckServices.cs
git commit -m "feat: add UpdateConnectionStatus SignalR event for connection state changes"
```

---

### Task 1.2: Add UpdateConnectionStatus Handler in Frontend

**Files:**
- Modify: `frontend/src/composables/useSignalR.ts:21-24, 278-287, 307-348`

**Step 1: Write test for connection status update**

First, let's verify the current structure by reading the relevant sections:
```bash
# Check if there are any tests for useSignalR
ls frontend/src/composables/__tests__ 2>/dev/null || echo "No test directory exists"
```

**Step 2: Add UpdateConnectionStatus SignalR handler**

Modify `useSignalR.ts` to handle the new event. Find the SignalR event handlers section (around line 224-286) and add:

```typescript
// Add after line 276 (after UpdateMachineInfo handler)
conn.on('UpdateConnectionStatus', (status: { IsConnected: boolean }) => {
    console.log('Connection Status Update:', status.IsConnected)
    isGameConnected.value = status.IsConnected
    if (status.IsConnected) {
        isInjecting.value = false
    }
})
```

**Step 3: Test manually in development mode**

Start the dev server and verify:
```bash
cd frontend
pnpm dev
```

Expected:
- When game connects, `isGameConnected` becomes `true` automatically
- When game disconnects, `isGameConnected` becomes `false` automatically
- No more need to poll `/api/status`

**Step 4: Commit**

```bash
git add frontend/src/composables/useSignalR.ts
git commit -m "feat(frontend): handle UpdateConnectionStatus SignalR event"
```

---

## Phase 2: Remove Redundant Polling

### Task 2.1: Remove /api/me and /api/roommates Polling

**Files:**
- Modify: `frontend/src/composables/useSignalR.ts:161-189`

**Step 1: Identify pollData function**

Read the current `pollData` implementation:
```bash
sed -n '161,189p' frontend/src/composables/useSignalR.ts
```

**Step 2: Remove pollData calls from polling loop**

Modify the polling interval (around line 340-346) to remove `pollData()`:

```typescript
// Replace this block (lines 339-347):
if (!statusInterval) {
    statusInterval = window.setInterval(async () => {
        await pollStatus()
        await pollData()  // ← REMOVE THIS LINE
    }, 1000)

    await pollStatus()
    await pollData()  // ← REMOVE THIS LINE
}

// With this:
if (!statusInterval) {
    statusInterval = window.setInterval(async () => {
        await pollStatus()  // Keep only this until UpdateConnectionStatus works
    }, 1000)

    await pollStatus()
}
```

**Step 3: Verify data still updates via SignalR**

Test in development:
```bash
cd frontend
pnpm dev
```

Expected:
- Pilot info updates when `UpdatePersonInfo` SignalR event received
- Roommates update when `UpdateRoommates` SignalR event received
- UI stays in sync without polling

**Step 4: Commit**

```bash
git add frontend/src/composables/useSignalR.ts
git commit -m "refactor(frontend): remove redundant /api/me and /api/roommates polling"
```

---

### Task 2.2: Remove /api/status Polling After UpdateConnectionStatus Works

**Files:**
- Modify: `frontend/src/composables/useSignalR.ts:147-159, 339-347`

**Step 1: Verify UpdateConnectionStatus is working**

Check browser console for log messages:
- "Connection Status Update: true" when game connects
- "Connection Status Update: false" when game disconnects

**Step 2: Remove pollStatus function and polling interval**

Once UpdateConnectionStatus is confirmed working, remove polling entirely:

```typescript
// DELETE function pollStatus (lines 149-159)

// DELETE statusInterval polling (lines 339-347):
if (!statusInterval) {
    statusInterval = window.setInterval(async () => {
        await pollStatus()
    }, 1000)

    await pollStatus()
    await pollData()
}

// Replace with:
// Polling removed - connection status now managed by SignalR UpdateConnectionStatus event
```

**Step 3: Update isGameConnected initialization**

Make sure initial state is set correctly on SignalR connect:

```typescript
// Add to startSignalR() after conn.start() succeeds (around line 290)
try {
    await conn.start()
    isConnected.value = true
    console.log('SignalR Connected')

    // Request initial connection status
    await fetch('/api/status')
        .then(res => res.json())
        .then(data => { isGameConnected.value = data.isConnected })
        .catch(() => { isGameConnected.value = false })

    connection.value = conn
} catch (err) {
    // ... error handling
}
```

**Step 4: Test end-to-end**

```bash
cd frontend
pnpm build
# Load in production mode and verify all data updates via SignalR only
```

**Step 5: Commit**

```bash
git add frontend/src/composables/useSignalR.ts
git commit -m "refactor(frontend): remove all REST polling, use SignalR only"
```

---

## Phase 3: Standardize on PascalCase

### Task 3.1: Remove Dual-Case Fallbacks in useSignalR

**Files:**
- Modify: `frontend/src/composables/useSignalR.ts:224-276`

**Step 1: Verify backend sends PascalCase**

Check browser Network tab → SignalR messages:
- Should see `{"PlayerId": 123, "Points": 1000}` (PascalCase)
- NOT `{"playerId": 123, "points": 1000}` (camelCase)

**Step 2: Update ReceiveReward handler**

Replace lines 224-242:

```typescript
conn.on('ReceiveReward', (notification: any) => {
    console.log('Reward Received:', notification)

    // Remove all `?? notification.camelCase` fallbacks
    latestMatch.value = {
        points: notification.Points ?? 0,
        kills: notification.Kills ?? 0,
        deaths: notification.Deaths ?? 0,
        supports: notification.Supports ?? 0,
        gbGain: (notification.GBGain ?? 0) + (notification.TotalBonus ?? 0),
        timestamp: new Date().toLocaleString(),
        gameStatus: notification.GameStatus ?? null,
        gradeRank: notification.GradeRank ?? null
    }

    const pid = notification.PlayerId ?? 0
    if (currentPlayerId.value === pid || currentPlayerId.value === 0) {
        if (currentPlayerId.value === 0) currentPlayerId.value = pid
        refreshStats()
    }
})
```

**Step 3: Update UpdatePersonInfo handler**

Replace lines 244-264:

```typescript
conn.on('UpdatePersonInfo', (info: any) => {
    pilotInfo.value = {
        personId: info.PersonId ?? 0,
        condomId: info.CondomId ?? 0,
        condomName: info.CondomName ?? '--',
        slot: info.Slot ?? 0,
        weapon1: info.Weapon1 ?? '--',
        weapon2: info.Weapon2 ?? '--',
        weapon3: info.Weapon3 ?? '--',
        x: info.X ?? 0,
        y: info.Y ?? 0,
        z: info.Z ?? 0
    }

    const newPid = pilotInfo.value.personId
    if (newPid && newPid !== currentPlayerId.value) {
        currentPlayerId.value = newPid
        refreshStats()
    }
})
```

**Step 4: Update UpdateRoommates handler**

Replace lines 266-271:

```typescript
conn.on('UpdateRoommates', (list: any[]) => {
    roommates.value = list.map(r => ({
        name: r.Name ?? 'Unknown',
        id: r.Id ?? 0
    }))
})
```

**Step 5: Test all SignalR events**

```bash
cd frontend
pnpm dev
```

Expected:
- All data displays correctly
- No `undefined` values in UI
- Browser console shows correctly parsed data

**Step 6: Commit**

```bash
git add frontend/src/composables/useSignalR.ts
git commit -m "refactor(frontend): standardize on PascalCase, remove camelCase fallbacks"
```

---

### Task 3.2: Update TypeScript Interfaces to Match PascalCase

**Files:**
- Modify: `frontend/src/services/api.ts:8-97`
- Modify: `frontend/src/composables/useSignalR.ts:6-17`

**Step 1: Update PilotInfo interface**

In `api.ts` lines 76-87:

```typescript
export interface PilotInfo {
    PersonId: number
    CondomId: number
    CondomName: string
    Slot: number
    Weapon1: string
    Weapon2: string
    Weapon3: string
    X: number
    Y: number
    Z: number
}
```

**Step 2: Update HistoryItem interface**

In `api.ts` lines 60-74:

```typescript
export interface HistoryItem {
    CreatedAtUtc: string
    GameStatus: string | null
    GradeRank: string | null
    Points: number
    Kills: number
    Deaths: number
    Supports: number
    GBGain: number
    TotalBonus: number
    MachineAddedExp: number
    DamageScore: number | null
    TeamExpectationScore: number | null
    SkillFulScore: number | null
}
```

**Step 3: Update Roommate interface**

In `api.ts` lines 89-92:

```typescript
export interface Roommate {
    Name: string
    Id: number
}
```

**Step 4: Update api.getMe normalization**

Remove the case normalization in `api.ts` lines 426-436:

```typescript
async getMe(): Promise<PilotInfo> {
    if (isDev()) return MOCK_PILOT
    const res = await client.get<PilotInfo>('/me')
    return res.data  // No normalization needed, backend sends PascalCase
}
```

**Step 5: Update api.getRoommates normalization**

Remove case normalization in `api.ts` lines 443-446:

```typescript
async getRoommates(): Promise<Roommate[]> {
    if (isDev()) return MOCK_ROOMMATES
    const res = await client.get<Roommate[]>('/roommates')
    return res.data  // No normalization needed
}
```

**Step 6: Commit**

```bash
git add frontend/src/services/api.ts
git commit -m "refactor(frontend): update TypeScript interfaces to PascalCase"
```

---

## Phase 4: Fix Memory Leaks

### Task 4.1: Add Cleanup for Mock Intervals

**Files:**
- Modify: `frontend/src/composables/useSignalR.ts:54-97, 306-383`

**Step 1: Import onUnmounted**

Add to imports at top of file (line 2):

```typescript
import { ref, computed, onUnmounted } from 'vue'
```

**Step 2: Add cleanup function**

Add after `startMockData()` function (around line 97):

```typescript
function stopMockData() {
    console.warn('[MOCK] Stopping Simulation Mode')
    mockIntervals.forEach(clearInterval)
    mockIntervals = []
}
```

**Step 3: Add onUnmounted hook**

Add at end of `useSignalR()` function before return (around line 383):

```typescript
export function useSignalR() {
    // ... existing code ...

    // Cleanup on component unmount
    onUnmounted(() => {
        // Clear mock intervals
        if (isDev()) {
            stopMockData()
        }

        // Clear status polling interval
        if (statusInterval) {
            clearInterval(statusInterval)
            statusInterval = null
        }

        // Stop SignalR connection
        if (connection.value) {
            connection.value.stop().catch(err => {
                console.error('Error stopping SignalR:', err)
            })
        }
    })

    return {
        // ... existing return values
    }
}
```

**Step 4: Test cleanup**

```bash
cd frontend
pnpm dev
```

Expected:
- Navigate between tabs
- Check browser console - no "MOCK" intervals running after leaving page
- No memory leaks in Chrome DevTools Memory profiler

**Step 5: Commit**

```bash
git add frontend/src/composables/useSignalR.ts
git commit -m "fix(frontend): add cleanup for intervals and SignalR connection"
```

---

## Phase 5: Add Debouncing for Stats Refresh

### Task 5.1: Install lodash-es

**Files:**
- Modify: `frontend/package.json`
- Create: `frontend/package-lock.json` (auto-generated)

**Step 1: Install lodash-es**

```bash
cd frontend
pnpm add lodash-es
pnpm add -D @types/lodash-es
```

Expected: Packages added to package.json

**Step 2: Commit**

```bash
git add frontend/package.json frontend/pnpm-lock.yaml
git commit -m "chore(frontend): add lodash-es for debouncing"
```

---

### Task 5.2: Add Debounce to refreshStats

**Files:**
- Modify: `frontend/src/composables/useSignalR.ts:1-212`

**Step 1: Import debounce**

Add at top of file (line 3):

```typescript
import debounce from 'lodash-es/debounce'
```

**Step 2: Create debounced version of refreshStats**

Replace `refreshStats` function (lines 191-212) with debounced version:

```typescript
async function refreshStatsImpl() {
    if (currentPlayerId.value === 0) return
    try {
        stats.value = await api.getStats(currentPlayerId.value)
        combatLog.value = await api.getHistory(currentPlayerId.value)
        if (combatLog.value.length > 0) {
            const latest = combatLog.value[0]!
            latestMatch.value = {
                points: latest.Points,
                kills: latest.Kills,
                deaths: latest.Deaths,
                supports: latest.Supports,
                gbGain: (latest.GBGain ?? 0) + (latest.TotalBonus ?? 0),
                timestamp: new Date(latest.CreatedAtUtc).toLocaleString(),
                gameStatus: latest.GameStatus ?? null,
                gradeRank: latest.GradeRank ?? null
            }
        }
    } catch (e) {
        console.error('Refresh stats error:', e)
    }
}

// Debounce to max 1 call per 500ms
const refreshStats = debounce(refreshStatsImpl, 500)
```

**Step 3: Test debouncing behavior**

```bash
cd frontend
pnpm dev
```

Expected:
- Multiple rapid `refreshStats()` calls result in single API call
- 500ms delay between last call and actual execution
- No unnecessary API calls

**Step 4: Commit**

```bash
git add frontend/src/composables/useSignalR.ts
git commit -m "perf(frontend): add debouncing to refreshStats (500ms)"
```

---

## Phase 6: Improve Error Handling

### Task 6.1: Create Toast Notification System

**Files:**
- Create: `frontend/src/composables/useToast.ts`
- Create: `frontend/src/components/ui/toast/Toast.vue`
- Create: `frontend/src/components/ui/toast/ToastContainer.vue`

**Step 1: Create toast composable**

Create `frontend/src/composables/useToast.ts`:

```typescript
import { ref } from 'vue'

export interface Toast {
    id: number
    message: string
    type: 'success' | 'error' | 'info' | 'warning'
    duration?: number
}

const toasts = ref<Toast[]>([])

export function useToast() {
    function showToast(message: string, type: 'success' | 'error' | 'info' | 'warning' = 'info', duration = 3000) {
        const id = Date.now()
        toasts.value.push({ id, message, type, duration })

        if (duration > 0) {
            setTimeout(() => {
                removeToast(id)
            }, duration)
        }
    }

    function removeToast(id: number) {
        toasts.value = toasts.value.filter(t => t.id !== id)
    }

    return {
        toasts,
        showToast,
        removeToast
    }
}
```

**Step 2: Create Toast component**

Create `frontend/src/components/ui/toast/Toast.vue`:

```vue
<script setup lang="ts">
import { computed } from 'vue'
import type { Toast } from '@/composables/useToast'

const props = defineProps<{
    toast: Toast
}>()

const emit = defineEmits<{
    close: []
}>()

const bgClass = computed(() => {
    switch (props.toast.type) {
        case 'success': return 'bg-emerald-500/90 border-emerald-400'
        case 'error': return 'bg-red-500/90 border-red-400'
        case 'warning': return 'bg-amber-500/90 border-amber-400'
        default: return 'bg-neon-blue/90 border-neon-cyan'
    }
})
</script>

<template>
    <div
        :class="[
            'flex items-center justify-between px-4 py-3 rounded border shadow-lg',
            'text-white font-rajdhani text-sm min-w-[300px] max-w-md',
            bgClass
        ]"
    >
        <span>{{ toast.message }}</span>
        <button
            @click="emit('close')"
            class="ml-3 text-white/80 hover:text-white"
        >
            ✕
        </button>
    </div>
</template>
```

**Step 3: Create ToastContainer component**

Create `frontend/src/components/ui/toast/ToastContainer.vue`:

```vue
<script setup lang="ts">
import Toast from './Toast.vue'
import { useToast } from '@/composables/useToast'

const { toasts, removeToast } = useToast()
</script>

<template>
    <div class="fixed top-4 right-4 z-50 flex flex-col gap-2 pointer-events-none">
        <Toast
            v-for="toast in toasts"
            :key="toast.id"
            :toast="toast"
            @close="removeToast(toast.id)"
            class="pointer-events-auto"
        />
    </div>
</template>
```

**Step 4: Add ToastContainer to App.vue**

Modify `frontend/src/App.vue`:

```vue
<template>
    <div class="dashboard-wrapper">
        <ParticleBackground :active="isTransAm" />

        <!-- Header -->
        <AppHeader />

        <!-- Navigation -->
        <AppNav />

        <!-- Main View -->
        <main class="flex-1 overflow-hidden z-10 relative">
            <Transition name="fade" mode="out-in">
                <component :is="currentView" :key="currentTab" />
            </Transition>
        </main>

        <!-- Toast Notifications -->
        <ToastContainer />
    </div>
</template>

<script setup lang="ts">
import { onMounted, computed, watch } from 'vue'
import AppHeader from '@/components/layout/AppHeader.vue'
import AppNav from '@/components/layout/AppNav.vue'
import ParticleBackground from '@/components/layout/ParticleBackground.vue'
import ToastContainer from '@/components/ui/toast/ToastContainer.vue'
import Dashboard from '@/views/Dashboard.vue'
import Features from '@/views/Features.vue'
import Pilot from '@/views/Pilot.vue'
import Lobby from '@/views/Lobby.vue'
import MachineInfo from '@/views/MachineInfo.vue'
import { useTabs, TabNames } from '@/composables/useTabs'
import { useSignalR } from '@/composables/useSignalR'

// ... rest of script
</script>
```

**Step 5: Test toast notifications**

```bash
cd frontend
pnpm dev
```

Expected:
- Toasts appear in top-right corner
- Auto-dismiss after 3 seconds
- Click X to dismiss manually
- Proper color coding by type

**Step 6: Commit**

```bash
git add frontend/src/composables/useToast.ts
git add frontend/src/components/ui/toast/
git add frontend/src/App.vue
git commit -m "feat(frontend): add toast notification system"
```

---

### Task 6.2: Add Toast Notifications to Error Handlers

**Files:**
- Modify: `frontend/src/composables/useSignalR.ts:116-142, 193-211, 293-296`

**Step 1: Import useToast**

In `useSignalR.ts`, add import after line 3:

```typescript
import { useToast } from './useToast'
const { showToast } = useToast()
```

**Step 2: Add toast to inject error handler**

Modify inject function (lines 116-122):

```typescript
async function inject() {
    isInjecting.value = true

    if (isDev()) {
        console.log('[MOCK] Simulating Injection...')
        setTimeout(() => {
            isGameConnected.value = true
            isInjecting.value = false
            showToast('Injection successful (mock)', 'success')
            console.log('[MOCK] Injection Successful')
        }, 2000)
        return
    }

    try {
        await api.inject()
        showToast('Injection successful', 'success')
    } catch (e) {
        console.error('Inject failed:', e)
        showToast('Injection failed: ' + (e as Error).message, 'error')
        isInjecting.value = false
    }
}
```

**Step 3: Add toast to deattach error handler**

Modify deattach function (lines 136-142):

```typescript
async function deattach() {
    if (isDev()) {
        console.log('[MOCK] Simulating Deattach...')
        setTimeout(() => {
            isGameConnected.value = false
            showToast('Detached successfully (mock)', 'success')
            console.log('[MOCK] Deattached')
        }, 1000)
        return
    }

    try {
        await api.deattach()
        isGameConnected.value = false
        showToast('Detached successfully', 'success')
    } catch (e) {
        console.error('Deattach failed:', e)
        showToast('Failed to detach: ' + (e as Error).message, 'error')
    }
}
```

**Step 4: Add toast to refreshStats error handler**

Modify refreshStatsImpl function (around line 209):

```typescript
} catch (e) {
    console.error('Refresh stats error:', e)
    showToast('Failed to refresh stats: ' + (e as Error).message, 'error')
}
```

**Step 5: Add toast to SignalR connection errors**

Modify startSignalR function (around line 294):

```typescript
} catch (err) {
    console.error('SignalR Connection Failed', err)
    showToast('Failed to connect to server. Retrying in 5s...', 'error')
    setTimeout(() => startSignalR(), 5000)
}
```

**Step 6: Test error handling**

```bash
cd frontend
pnpm dev
```

Expected:
- All errors show user-friendly toast messages
- Success operations show confirmation toasts
- Console still has detailed error logs for debugging

**Step 7: Commit**

```bash
git add frontend/src/composables/useSignalR.ts
git commit -m "feat(frontend): add toast notifications to error handlers"
```

---

### Task 6.3: Fix Double API Call in Feature Toggle

**Files:**
- Modify: `frontend/src/views/Features.vue:32-58`

**Step 1: Remove redundant getFeatures call**

Replace toggleFeature function (lines 32-58):

```typescript
async function toggleFeature(feature: Feature, event: Event) {
    event.preventDefault()
    event.stopPropagation()

    if (togglingId.value === feature.Id) return

    const previousState = feature.Enabled
    const newState = !previousState

    // Optimistic update
    feature.Enabled = newState
    togglingId.value = feature.Id

    try {
        await api.updateFeature(feature.Id, newState)
        // Success - optimistic update already applied
    } catch (e) {
        console.error('Toggle Failed:', e)
        // Rollback on error
        feature.Enabled = previousState
        showToast(`Failed to toggle ${feature.Name}: ` + (e as Error).message, 'error')
    } finally {
        togglingId.value = null
    }
}
```

**Step 2: Import useToast**

Add to script setup section (line 4):

```typescript
import { useToast } from '@/composables/useToast'
const { showToast } = useToast()
```

**Step 3: Test feature toggle**

```bash
cd frontend
pnpm dev
```

Expected:
- Single API call per toggle
- UI updates immediately (optimistic)
- Error rollback works
- Toast notifications appear

**Step 4: Commit**

```bash
git add frontend/src/views/Features.vue
git commit -m "fix(frontend): remove redundant getFeatures call, add optimistic update"
```

---

## Phase 7: Production Build Optimizations

### Task 7.1: Strip Console Logs in Production

**Files:**
- Modify: `frontend/vite.config.ts`

**Step 1: Read current vite config**

```bash
cat frontend/vite.config.ts
```

**Step 2: Add esbuild options to drop console**

Modify build configuration:

```typescript
export default defineConfig({
    plugins: [
        vue(),
        // ... existing plugins
    ],
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url))
        }
    },
    build: {
        minify: 'esbuild',
        esbuild: {
            drop: import.meta.env.PROD ? ['console', 'debugger'] : [],
            logOverride: { 'this-is-undefined-in-esm': 'silent' }
        },
        rollupOptions: {
            output: {
                manualChunks: undefined
            }
        }
    }
})
```

**Step 3: Test production build**

```bash
cd frontend
pnpm build
# Check dist/index.js - console.log should be removed
grep -c "console.log" dist/assets/*.js || echo "Console logs stripped successfully"
```

Expected: No console.log calls in production build

**Step 4: Commit**

```bash
git add frontend/vite.config.ts
git commit -m "perf(frontend): strip console logs in production build"
```

---

### Task 7.2: Full Integration Test

**Files:**
- Test: Full frontend + backend integration

**Step 1: Build backend**

```bash
dotnet build NewGmHack.GUI/NewGmHack.GUI.csproj -c Release -p:Platform=x86
```

Expected: Build succeeds

**Step 2: Build frontend**

```bash
cd frontend
pnpm build
```

Expected: Build succeeds, dist/ folder created

**Step 3: Copy frontend dist to GUI wwwroot**

```bash
# From project root
Remove-Item -Recurse -Force "NewGmHack.GUI/wwwroot" -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path "NewGmHack.GUI/wwwroot" -Force
Copy-Item -Recurse -Force "frontend/dist/*" "NewGmHack.GUI/wwwroot/"
```

**Step 4: Test application**

Run the GUI application and verify:
- [ ] SignalR connects successfully
- [ ] Connection status updates via `UpdateConnectionStatus`
- [ ] Pilot info updates via `UpdatePersonInfo`
- [ ] Roommates update via `UpdateRoommates`
- [ ] Machine info updates via `UpdateMachineInfo`
- [ ] Rewards display via `ReceiveReward`
- [ ] No polling in browser Network tab
- [ ] Toast notifications work
- [ ] Feature toggles work optimistically
- [ ] No memory leaks (monitor Chrome DevTools)
- [ ] Console logs stripped in production

**Step 5: Final commit**

```bash
git add -A
git commit -m "test: complete integration test of SignalR optimizations"
```

---

## Testing Checklist

After completing all phases, verify:

### Performance Tests
- [ ] No REST API polling every 1s (check Network tab)
- [ ] SignalR messages arrive within 100ms
- [ ] Memory usage stable over 30 minutes (no leaks)
- [ ] CPU usage < 5% when idle
- [ ] debounced `refreshStats` prevents duplicate API calls

### Functional Tests
- [ ] Connection status updates automatically
- [ ] All data displays correctly (no `undefined` values)
- [ ] Feature toggles work with optimistic UI
- [ ] Error toast notifications appear for all failures
- [ ] Mock mode works in development

### Build Tests
- [ ] Production build has no console logs
- [ ] TypeScript compilation succeeds
- [ ] Vite build succeeds without warnings
- [ ] Backend builds successfully
- [ ] Application starts and connects to game

---

## Rollback Plan

If any phase causes issues:

1. **Revert specific commit:**
   ```bash
   git revert <commit-hash>
   ```

2. **Revert entire optimization:**
   ```bash
   git revert <range-of-commits>
   ```

3. **Fallback to polling:**
   - Restore `pollData()` calls in `useSignalR.ts`
   - Keep UpdateConnectionStatus but add polling as backup

---

## Documentation Updates

After implementation, update:

1. **CLAUDE.md** - Document new SignalR architecture
2. **README.md** - Add performance improvements section
3. **docs/zero-allocation-packet-processing.md** - Mention frontend optimizations

---

## Success Metrics

- **Network traffic:** -90% (eliminate 1s polling)
- **Time to data display:** <100ms (no polling delay)
- **Memory leaks:** 0 intervals泄漏
- **Error visibility:** 100% (all errors show toasts)
- **Build size:** -5% (console logs stripped)
- **Type safety:** 100% (no `any` types in SignalR handlers)

---

**Plan complete and saved to `docs/plans/2025-01-27-signalr-frontend-optimization.md`.**

Two execution options:

**1. Subagent-Driven (this session)** - I dispatch fresh subagent per task, review between tasks, fast iteration

**2. Parallel Session (separate)** - Open new session with executing-plans, batch execution with checkpoints

Which approach?
