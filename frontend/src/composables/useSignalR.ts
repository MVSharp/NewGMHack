import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr'
import { ref, computed } from 'vue'
import { api, type PilotInfo, type Roommate, type HistoryItem, type PlayerStats } from '@/services/api'

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

// === Shared State (Singleton Pattern) ===

const connection = ref<HubConnection | null>(null)
const isConnected = ref(false)        // SignalR connected
const isGameConnected = ref(false)    // Game injected (from /api/status)
const isInjecting = ref(false)        // Currently injecting

const currentPlayerId = ref(0)
const pilotInfo = ref<PilotInfo | null>(null)
const roommates = ref<Roommate[]>([])
const combatLog = ref<HistoryItem[]>([])
const stats = ref<PlayerStats | null>(null)
const appVersion = ref('1.0.0.0')
const machineInfo = ref<any>(null)  // MachineInfo data

// Latest match display
const latestMatch = ref<{
    points: number
    kills: number
    deaths: number
    supports: number
    gbGain: number
    timestamp: string
    gameStatus: string | null
    gradeRank: string | null
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
        if (pilotInfo.value.personId && currentPlayerId.value === 0) {
            currentPlayerId.value = pilotInfo.value.personId
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

async function pollStatus() {
    try {
        const status = await api.getStatus()
        isGameConnected.value = status.isConnected
        if (status.isConnected) {
            isInjecting.value = false
        }
    } catch {
        isGameConnected.value = false
    }
}

async function pollData() {
    if (!isGameConnected.value) return

    try {
        // Fetch pilot info
        pilotInfo.value = await api.getMe()
        const newPid = pilotInfo.value.personId

        if (newPid && newPid > 0) {
            // Always update currentPlayerId if we have a valid one
            if (currentPlayerId.value !== newPid) {
                console.log(`Player ID changed: ${currentPlayerId.value} -> ${newPid}`)
                currentPlayerId.value = newPid
                await refreshStats()
            }

            // Always refresh stats if we don't have any yet, or periodically
            if (!stats.value || !combatLog.value || combatLog.value.length === 0) {
                await refreshStats()
            }
        }

        // Fetch roommates
        roommates.value = await api.getRoommates()

    } catch (e) {
        console.error('Poll data error:', e)
    }
}

async function refreshStats() {
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
            points: notification.points ?? notification.Points ?? 0,
            kills: notification.kills ?? notification.Kills ?? 0,
            deaths: notification.deaths ?? notification.Deaths ?? 0,
            supports: notification.supports ?? notification.Supports ?? 0,
            gbGain: (notification.gbGain ?? notification.GBGain ?? 0) + (notification.totalBonus ?? notification.TotalBonus ?? 0),
            timestamp: new Date().toLocaleString(),
            gameStatus: notification.gameStatus ?? notification.GameStatus ?? null,
            gradeRank: notification.gradeRank ?? notification.GradeRank ?? null
        }
        
        const pid = notification.playerId ?? notification.PlayerId ?? 0
        if (currentPlayerId.value === pid || currentPlayerId.value === 0) {
            if (currentPlayerId.value === 0) currentPlayerId.value = pid
            refreshStats()
        }
    })

    // UpdatePersonInfo
    conn.on('UpdatePersonInfo', (info: any) => {
        pilotInfo.value = {
            personId: info.personId ?? info.PersonId ?? 0,
            condomId: info.condomId ?? info.CondomId ?? info.gundamId ?? info.GundamId ?? 0,
            condomName: info.condomName ?? info.CondomName ?? info.gundamName ?? info.GundamName ?? '--',
            slot: info.slot ?? info.Slot ?? 0,
            weapon1: info.weapon1 ?? info.Weapon1 ?? '--',
            weapon2: info.weapon2 ?? info.Weapon2 ?? '--',
            weapon3: info.weapon3 ?? info.Weapon3 ?? '--',
            x: info.x ?? info.X ?? 0,
            y: info.y ?? info.Y ?? 0,
            z: info.z ?? info.Z ?? 0
        }
        
        const newPid = pilotInfo.value.personId
        if (newPid && newPid !== currentPlayerId.value) {
            currentPlayerId.value = newPid
            refreshStats()
        }
    })

    conn.on('UpdateRoommates', (list: any[]) => {
        roommates.value = list.map(r => ({
            name: r.name ?? r.Name ?? 'Unknown',
            id: r.id ?? r.Id ?? 0
        }))
    })

    conn.on('UpdateMachineInfo', (info: any) => {
        console.log('Machine Info Update:', info)
        machineInfo.value = info
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

        // Start status polling
        if (!statusInterval) {
            statusInterval = window.setInterval(async () => {
                await pollStatus()
                await pollData()
            }, 1000)

            await pollStatus()
            await pollData()
        }
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
