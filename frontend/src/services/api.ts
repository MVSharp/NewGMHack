import axios from 'axios'

const client = axios.create({
    baseURL: '/api',
    headers: { 'Content-Type': 'application/json' }
})

// === Types ===

export interface Feature {
    Id: string
    Name: string
    DisplayNameEn: string
    DisplayNameCn: string
    DisplayNameTw: string
    Enabled: boolean
    Description: string
}

export interface PlayerStats {
    stats: {
        Matches: number
        TotalGBGain: number
        TotalBonusGB: number
        TotalMachineExp: number
        MaxPoints: number
        TotalKills: number
        TotalDeaths: number
        TotalSupports: number
        FirstSortieDate: string | null
        LastSortieDate: string | null
    }
    hourly: {
        GBGainLastHour: number
        BonusLastHour: number
        MachineExpLastHour: number
        MatchesLastHour: number
    }
    today: {
        GBGainToday: number
        BonusToday: number
        MachineExpToday: number
        MatchesToday: number
    }
}

export interface HistoryItem {
    CreatedAtUtc: string
    Points: number
    Kills: number
    Deaths: number
    Supports: number
    GBGain: number
    TotalBonus: number
    MachineAddedExp: number
}

export interface PilotInfo {
    personId: number
    condomId: number
    condomName: string
    slot: number
    weapon1: string
    weapon2: string
    weapon3: string
    x: number
    y: number
    z: number
}

export interface Roommate {
    name: string
    id: number
}

export interface ConnectionStatus {
    isConnected: boolean
}

// === Mock Data for Dev ===

const MOCK_STATS: PlayerStats = {
    stats: {
        Matches: 142,
        TotalGBGain: 1250300,
        TotalBonusGB: 89000,
        TotalMachineExp: 45200,
        MaxPoints: 3200,
        TotalKills: 567,
        TotalDeaths: 234,
        TotalSupports: 189,
        FirstSortieDate: '2024-01-15T10:00:00Z',
        LastSortieDate: new Date().toISOString()
    },
    hourly: {
        GBGainLastHour: 12500,
        BonusLastHour: 2300,
        MachineExpLastHour: 850,
        MatchesLastHour: 3
    },
    today: {
        GBGainToday: 85000,
        BonusToday: 12000,
        MachineExpToday: 5200,
        MatchesToday: 18
    }
}

const MOCK_HISTORY: HistoryItem[] = [
    { CreatedAtUtc: new Date().toISOString(), Points: 2850, Kills: 5, Deaths: 2, Supports: 3, GBGain: 8500, TotalBonus: 1200, MachineAddedExp: 450 },
    { CreatedAtUtc: new Date(Date.now() - 3600000).toISOString(), Points: 2100, Kills: 3, Deaths: 1, Supports: 4, GBGain: 6200, TotalBonus: 800, MachineAddedExp: 320 },
    { CreatedAtUtc: new Date(Date.now() - 7200000).toISOString(), Points: 1800, Kills: 2, Deaths: 3, Supports: 2, GBGain: 5100, TotalBonus: 600, MachineAddedExp: 280 },
]

const MOCK_FEATURES: Feature[] = [
    { Id: 'InfiniteBoost', Name: 'InfiniteBoost', DisplayNameEn: 'Infinite Boost', DisplayNameCn: '无限推进', DisplayNameTw: '無限推進', Enabled: true, Description: 'Unlimited boost gauge' },
    { Id: 'GodMode', Name: 'GodMode', DisplayNameEn: 'God Mode', DisplayNameCn: '无敌模式', DisplayNameTw: '無敵模式', Enabled: false, Description: 'Invincibility' },
    { Id: 'RapidFire', Name: 'RapidFire', DisplayNameEn: 'Rapid Fire', DisplayNameCn: '快速射击', DisplayNameTw: '快速射擊', Enabled: false, Description: 'No weapon cooldown' },
]

const MOCK_PILOT: PilotInfo = {
    personId: 12345,
    condomId: 58,
    condomName: 'RX-78-2 GUNDAM',
    slot: 1,
    weapon1: 'Beam Rifle',
    weapon2: 'Beam Saber',
    weapon3: 'Shield',
    x: 1234.56,
    y: 78.90,
    z: 2345.67
}

const MOCK_ROOMMATES: Roommate[] = [
    { name: 'RedComet_Char', id: 10001 },
    { name: 'WhiteDevil', id: 10002 },
    { name: 'Zeon_Pilot03', id: 10003 },
]

// === Helper ===
function isDev(): boolean {
    return window.location.port === '5173' || import.meta.env.DEV
}

// === API ===

export const api = {
    // --- Features ---
    async getFeatures(): Promise<Feature[]> {
        if (isDev()) return MOCK_FEATURES
        const res = await client.get<any[]>('/features')
        return res.data.map(d => ({
            Id: d.Name || d.name,
            Name: d.Name || d.name,
            DisplayNameEn: d.DisplayNameEn || d.displayNameEn || d.Name || d.name,
            DisplayNameCn: d.DisplayNameCn || d.displayNameCn || d.Name || d.name,
            DisplayNameTw: d.DisplayNameTw || d.displayNameTw || d.Name || d.name,
            Enabled: d.IsEnabled ?? d.isEnabled ?? false,
            Description: d.Description || d.description || ''
        }))
    },

    async updateFeature(featureId: string, enabled: boolean): Promise<void> {
        if (isDev()) {
            console.log(`[MOCK] Toggled ${featureId} to ${enabled}`)
            return
        }
        await client.post('/features', { FeatureName: featureId, IsEnabled: enabled })
    },

    // --- Stats ---
    async getStats(pilotId: number): Promise<PlayerStats> {
        if (isDev()) return MOCK_STATS
        const res = await client.get<PlayerStats>(`/stats/${pilotId}`)
        return res.data
    },

    // --- History ---
    async getHistory(pilotId: number): Promise<HistoryItem[]> {
        if (isDev()) return MOCK_HISTORY
        const res = await client.get<HistoryItem[]>(`/history/${pilotId}`)
        return res.data
    },

    // --- Pilot Info ---
    async getMe(): Promise<PilotInfo> {
        if (isDev()) return MOCK_PILOT
        const res = await client.get<any>('/me')
        // Normalize casing
        const d = res.data
        return {
            personId: d.personId ?? d.PersonId ?? 0,
            condomId: d.condomId ?? d.CondomId ?? d.gundamId ?? d.GundamId ?? 0,
            condomName: d.condomName ?? d.CondomName ?? d.gundamName ?? d.GundamName ?? '--',
            slot: d.slot ?? d.Slot ?? 0,
            weapon1: d.weapon1 ?? d.Weapon1 ?? '--',
            weapon2: d.weapon2 ?? d.Weapon2 ?? '--',
            weapon3: d.weapon3 ?? d.Weapon3 ?? '--',
            x: d.x ?? d.X ?? 0,
            y: d.y ?? d.Y ?? 0,
            z: d.z ?? d.Z ?? 0
        }
    },

    // --- Roommates ---
    async getRoommates(): Promise<Roommate[]> {
        if (isDev()) return MOCK_ROOMMATES
        const res = await client.get<any[]>('/roommates')
        return res.data.map(r => ({
            name: r.name ?? r.Name ?? 'Unknown',
            id: r.id ?? r.Id ?? 0
        }))
    },

    // --- Status ---
    async getStatus(): Promise<ConnectionStatus> {
        if (isDev()) return { isConnected: true }
        try {
            const res = await client.get<any>('/status')
            return { isConnected: res.data.isConnected ?? res.data.IsConnected ?? false }
        } catch {
            return { isConnected: false }
        }
    },

    // --- Inject ---
    async inject(): Promise<void> {
        if (isDev()) {
            console.log('[MOCK] Inject called')
            return
        }
        await client.post('/inject')
    },

    // --- Deattach ---
    async deattach(): Promise<void> {
        if (isDev()) {
            console.log('[MOCK] Deattach called')
            return
        }
        await client.post('/deattach')
    }
}
