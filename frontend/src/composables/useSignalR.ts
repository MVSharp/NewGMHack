import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr'
import { ref, computed, onUnmounted } from 'vue'
import { api, type PilotInfo, type Roommate, type HistoryItem, type PlayerStats } from '@/services/api'
import debounce from 'lodash-es/debounce'

// === Types ===

export interface RewardNotification {
    playerId: number
    points: number
    kills: number
    deaths: number
    supports: number
    gbGain: number
    machineExp: number
    message: string
    timestamp: Date
}

interface StatusResponse {
    isConnected: boolean
}

// === Shared State (Singleton Pattern) ===

const connection = ref<HubConnection | null>(null)
const isConnected = ref(false)        // SignalR connected
const isGameConnected = ref(false)    // Game injected (from /api/status)
const isInjecting = ref(false)        // Currently injecting
let initialStatusFetchComplete = false  // Prevent race condition with SignalR events

const currentPlayerId = ref(0)
const pilotInfo = ref<PilotInfo | null>(null)
const roommates = ref<Roommate[]>([])
const combatLog = ref<HistoryItem[]>([])
const stats = ref<PlayerStats | null>(null)
const appVersion = ref('1.0.0.0')
const machineInfo = ref<any>(null)  // MachineInfo data

// Latest match display
const latestMatch = ref<{
    Points: number
    Kills: number
    Deaths: number
    Supports: number
    GBGain: number
    timestamp: string
    GameStatus: string | null
    GradeRank: string | null
} | null>(null)

// === Helper ===

function isDev(): boolean {
    return window.location.port === '5173' || import.meta.env.DEV
}

// === Mock Data Simulation ===

let mockIntervals: number[] = []

function startMockData() {
    console.warn('[MOCK] Starting Simulation Mode')
    // Start as DISCONNECTED to allow testing Injection flow
    isConnected.value = true
    isGameConnected.value = false 
    isInjecting.value = false

    // Simulate SignalR Data stream only when 'connected'
    mockIntervals.push(window.setInterval(async () => {
        if (!isGameConnected.value) return 

        // Mock Pilot
        pilotInfo.value = await api.getMe()
        if (pilotInfo.value.PersonId && currentPlayerId.value === 0) {
            currentPlayerId.value = pilotInfo.value.PersonId
        }

        // Randomly update machine info to simulate live data
        if (machineInfo.value?.MachineBaseInfo) {
             const base = machineInfo.value.MachineBaseInfo
             // Slight jitter for realism
             base.HP = 6000 + Math.floor(Math.random() * 100)
        }

        // Mock Stats populate
        if (currentPlayerId.value !== 0 && (!stats.value || Math.random() > 0.9)) {
            await refreshStats()
        }
    }, 1000))

    // Mock Roommates
    mockIntervals.push(window.setInterval(async () => {
        if (!isGameConnected.value) return
        roommates.value = await api.getRoommates()
    }, 3000))

    // Initial partial load (just so we have some data structures)
    setTimeout(async () => {
        pilotInfo.value = await api.getMe()
        machineInfo.value = await api.getMachineInfo()
    }, 100)
}

function stopMockData() {
    console.warn('[MOCK] Stopping Simulation Mode')
    mockIntervals.forEach(clearInterval)
    mockIntervals = []
}

// === Actions ===

async function inject() {
    isInjecting.value = true
    
    // MOCK MODE INTERCEPTION
    if (isDev()) {
        console.log('[MOCK] Simulating Injection...')
        setTimeout(() => {
            isGameConnected.value = true
            isInjecting.value = false
            console.log('[MOCK] Injection Successful')
        }, 2000)
        return
    }

    // REAL MODE
    try {
        await api.inject()
    } catch (e) {
        console.error('Inject failed:', e)
        isInjecting.value = false
    }
}

async function deattach() {
    // MOCK MODE INTERCEPTION
    if (isDev()) {
        console.log('[MOCK] Simulating Deattach...')
        setTimeout(() => {
            isGameConnected.value = false
            console.log('[MOCK] Deattached')
        }, 1000)
        return
    }

    // REAL MODE
    try {
        await api.deattach()
        isGameConnected.value = false
    } catch (e) {
        console.error('Deattach failed:', e)
    }
}


// === Status Polling (Production) ===

let statusInterval: number | null = null

async function refreshStatsImpl() {
    if (currentPlayerId.value === 0) return
    try {
        stats.value = await api.getStats(currentPlayerId.value)
        combatLog.value = await api.getHistory(currentPlayerId.value)
        if (combatLog.value.length > 0) {
            const latest = combatLog.value[0]!
            latestMatch.value = {
                Points: latest.Points,
                Kills: latest.Kills,
                Deaths: latest.Deaths,
                Supports: latest.Supports,
                GBGain: (latest.GBGain ?? 0) + (latest.TotalBonus ?? 0),
                timestamp: new Date(latest.CreatedAtUtc).toLocaleString(),
                GameStatus: latest.GameStatus ?? null,
                GradeRank: latest.GradeRank ?? null
            }
        }
    } catch (e) {
        console.error('Refresh stats error:', e)
    }
}

// Debounce to max 1 call per 500ms
const refreshStats = debounce(refreshStatsImpl, 500)

// === SignalR Connection ===

async function startSignalR() {
    const conn = new HubConnectionBuilder()
        .withUrl('/rewardHub')
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Information)
        .build()

    // ReceiveReward
    conn.on('ReceiveReward', (notification: any) => {
        console.log('Reward Received:', notification)
        latestMatch.value = {
            Points: notification.Points ?? 0,
            Kills: notification.Kills ?? 0,
            Deaths: notification.Deaths ?? 0,
            Supports: notification.Supports ?? 0,
            GBGain: (notification.GBGain ?? 0) + (notification.TotalBonus ?? 0),
            timestamp: new Date().toLocaleString(),
            GameStatus: notification.GameStatus ?? null,
            GradeRank: notification.GradeRank ?? null
        }

        const pid = notification.PlayerId ?? 0
        if (currentPlayerId.value === pid || currentPlayerId.value === 0) {
            if (currentPlayerId.value === 0) currentPlayerId.value = pid
            refreshStats()
        }
    })

    // UpdatePersonInfo
    conn.on('UpdatePersonInfo', (info: any) => {
        pilotInfo.value = {
            PersonId: info.PersonId ?? 0,
            CondomId: info.CondomId ?? 0,
            CondomName: info.CondomName ?? '--',
            Slot: info.Slot ?? 0,
            Weapon1: info.Weapon1 ?? '--',
            Weapon2: info.Weapon2 ?? '--',
            Weapon3: info.Weapon3 ?? '--',
            X: info.X ?? 0,
            Y: info.Y ?? 0,
            Z: info.Z ?? 0
        }

        const newPid = pilotInfo.value.PersonId
        if (newPid && newPid !== currentPlayerId.value) {
            currentPlayerId.value = newPid
            refreshStats()
        }
    })

    conn.on('UpdateRoommates', (list: any[]) => {
        roommates.value = list.map(r => ({
            Name: r.Name ?? 'Unknown',
            Id: r.Id ?? 0
        }))
    })

    conn.on('UpdateMachineInfo', (info: any) => {
        console.log('Machine Info Update:', info)
        machineInfo.value = info
    })

    conn.on('UpdateConnectionStatus', (status: { IsConnected: boolean }) => {
        console.log('Connection Status Update:', status.IsConnected)
        isGameConnected.value = status.IsConnected
        initialStatusFetchComplete = true  // Mark that SignalR has started sending updates
        if (status.IsConnected) {
            isInjecting.value = false
        }
    })

    conn.onclose(() => {
        isConnected.value = false
        console.warn('SignalR Disconnected')
    })

    conn.onreconnected(() => {
        isConnected.value = true
        console.log('SignalR Reconnected')
    })

    try {
        await conn.start()
        isConnected.value = true
        console.log('SignalR Connected')

        // Request initial connection status
        await fetch('/api/status')
            .then(res => res.json() as Promise<StatusResponse>)
            .then((data: StatusResponse) => {
                // Only set if SignalR hasn't updated yet (prevent race condition)
                if (!initialStatusFetchComplete) {
                    isGameConnected.value = data.isConnected
                }
            })
            .catch((err) => {
                console.error('Initial status fetch failed:', err)
                isGameConnected.value = false
            })

        connection.value = conn
    } catch (err) {
        console.error('SignalR Connection Failed', err)
        setTimeout(() => startSignalR(), 5000)
    }
}

async function searchPlayer(pid: number) {
    currentPlayerId.value = pid
    await refreshStats()
}

// === Main Entry ===

export function useSignalR() {
    async function start() {
        if (connection.value?.state === 'Connected') return

        // DEV MODE - Use mock data
        if (isDev()) {
            startMockData()
            return
        }

        // PRODUCTION - Real SignalR + Polling
        await startSignalR()

        // Initial fetch of machine info
        try {
            const initialInfo = await api.getMachineInfo()
            if (initialInfo && Object.keys(initialInfo).length > 0) {
                machineInfo.value = initialInfo
            }
        } catch (e) {
            console.error('Initial machine info fetch failed', e)
        }

        // Fetch app version
        try {
            const res = await fetch('/api/version')
            const data = await res.json()
            appVersion.value = data.version ?? '1.0.0.0'
        } catch {
            appVersion.value = '1.0.0.0'
        }

        // Polling removed - connection status now managed by SignalR UpdateConnectionStatus event
    }

    // Computed for UI
    const dateRange = computed(() => {
        if (!stats.value?.stats?.FirstSortieDate || !stats.value?.stats?.LastSortieDate) {
            return '-- / --'
        }
        const start = new Date(stats.value.stats.FirstSortieDate).toLocaleDateString()
        const end = new Date(stats.value.stats.LastSortieDate).toLocaleDateString()
        return `${start} - ${end}`
    })

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
        // Connection State
        isConnected,
        isGameConnected,
        isInjecting,

        // Data
        currentPlayerId,
        pilotInfo,
        roommates,
        combatLog,
        stats,
        latestMatch,
        dateRange,
        appVersion,
        machineInfo,

        // Actions
        start,
        inject,
        deattach,
        searchPlayer,
        refreshStats
    }
}
