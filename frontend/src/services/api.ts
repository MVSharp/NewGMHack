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
        Wins: number
        Losses: number
        Draws: number
        WinRate: number
        TotalPoints: number
        TotalGBGain: number
        TotalBonusGB: number
        TotalMachineAddedExp: number
        TotalMachineExp: number
        MaxPoints: number
        TotalKills: number
        TotalDeaths: number
        TotalSupports: number
        AvgDamageScore: number
        AvgTeamScore: number
        AvgSkillScore: number
        FirstSortieDate: string | null
        LastSortieDate: string | null
    }
    hourly: {
        MatchesLastHour: number
        WinsLastHour: number
        LossesLastHour: number
        GBGainLastHour: number
        BonusLastHour: number
        MachineExpLastHour: number
    }
    today: {
        MatchesToday: number
        WinsToday: number
        LossesToday: number
        GBGainToday: number
        BonusToday: number
        MachineExpToday: number
    }
}

export interface HistoryItem {
    CreatedAtUtc: string
    GameStatus: string | null  // "Win", "Lost", "Draw"
    GradeRank: string | null   // "A+", "A", "B+", etc.
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
        Wins: 85,
        Losses: 47,
        Draws: 10,
        WinRate: 59.86,
        TotalPoints: 285000,
        TotalGBGain: 1250300,
        TotalBonusGB: 89000,
        TotalMachineAddedExp: 45200,
        TotalMachineExp: 38000,
        MaxPoints: 3200,
        TotalKills: 567,
        TotalDeaths: 234,
        TotalSupports: 189,
        AvgDamageScore: 72,
        AvgTeamScore: 68,
        AvgSkillScore: 75,
        FirstSortieDate: '2024-01-15T10:00:00Z',
        LastSortieDate: new Date().toISOString()
    },
    hourly: {
        MatchesLastHour: 3,
        WinsLastHour: 2,
        LossesLastHour: 1,
        GBGainLastHour: 12500,
        BonusLastHour: 2300,
        MachineExpLastHour: 850
    },
    today: {
        MatchesToday: 18,
        WinsToday: 11,
        LossesToday: 6,
        GBGainToday: 85000,
        BonusToday: 12000,
        MachineExpToday: 5200
    }
}

const MOCK_HISTORY: HistoryItem[] = [
    { CreatedAtUtc: new Date().toISOString(), GameStatus: 'Win', GradeRank: 'A+', Points: 2850, Kills: 5, Deaths: 2, Supports: 3, GBGain: 8500, TotalBonus: 1200, MachineAddedExp: 450, DamageScore: 85, TeamExpectationScore: 78, SkillFulScore: 92 },
    { CreatedAtUtc: new Date(Date.now() - 3600000).toISOString(), GameStatus: 'Lost', GradeRank: 'B', Points: 2100, Kills: 3, Deaths: 1, Supports: 4, GBGain: 6200, TotalBonus: 800, MachineAddedExp: 320, DamageScore: 65, TeamExpectationScore: 70, SkillFulScore: 68 },
    { CreatedAtUtc: new Date(Date.now() - 7200000).toISOString(), GameStatus: 'Win', GradeRank: 'A', Points: 2400, Kills: 4, Deaths: 2, Supports: 2, GBGain: 7100, TotalBonus: 900, MachineAddedExp: 380, DamageScore: 78, TeamExpectationScore: 72, SkillFulScore: 80 },
    { CreatedAtUtc: new Date(Date.now() - 10800000).toISOString(), GameStatus: 'Draw', GradeRank: 'C+', Points: 1800, Kills: 2, Deaths: 3, Supports: 2, GBGain: 5100, TotalBonus: 600, MachineAddedExp: 280, DamageScore: 55, TeamExpectationScore: 60, SkillFulScore: 58 },
    { CreatedAtUtc: new Date(Date.now() - 14400000).toISOString(), GameStatus: 'Win', GradeRank: 'B+', Points: 2200, Kills: 3, Deaths: 2, Supports: 3, GBGain: 6800, TotalBonus: 750, MachineAddedExp: 340, DamageScore: 70, TeamExpectationScore: 68, SkillFulScore: 72 },
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

const MOCK_MACHINEINFO = {
    machineModel: {
        machineId: 58,
        slot: 1,
        level: 45,
        colors: ['#FF4444', '#FFFFFF', '#3366FF', '#FFCC00', '#44FF44', '#FF44FF'],
        batteryPercent: 85.5,
        batteryRaw: 1710,
        battleCount: 342,
        currentExp: 125000,
        ocMaxLevel: 8,
        isLocked: false,
        brushPolish: 150,
        extraSkillParts: 2,
        buyInTime: new Date().toISOString(),
        oc1Parts: { part1: 1001, part2: 1002, part3: 1003, part4: 0 },
        oc2Parts: { part1: 2001, part2: 2002, part3: 0, part4: 0 },
        ocBasePoints: { speed: 12, hp: 15, attack: 18, defense: 10, agility: 14, special: 8 },
        ocBonusPoints: { speed: 5, hp: 8, attack: 6, defense: 4, agility: 7, special: 3 }
    },
    machineBaseInfo: {
        machineId: 58,
        chineseName: 'RX-78-2 高达',
        englishName: 'RX-78-2 GUNDAM',
        rank: 'A',
        quality: 3,
        rarity: 2,
        combatType: 'Mid',
        attackSpeedLevel: 4,
        hp: 6500,
        shieldHP: 1200,
        shieldType: 'ALL',
        shieldDirection: 'Front',
        shieldDeductionPercentage: 80,
        attack: 45,
        defense: 38,
        agility: 22,
        forwardSpeed: 28,
        moveSpeed: 25,
        bzdSpeed: 32,
        trackSpeed: 18,
        trackAcceleration: 1.25,
        boostCapacity: 150,
        boostRecoverySpeed: 12,
        boostConsumption: 8,
        radarRange: 800,
        respawnTimeSeconds: 12,
        hasTransform: true,
        transformId: 59,
        transformedMachine: {
            machineId: 59,
            chineseName: 'RX-78-2 高达 [变形]',
            englishName: 'RX-78-2 GUNDAM [Transform]',
            rank: 'A',
            quality: 3,
            rarity: 2,
            combatType: 'Far',
            hp: 5800,
            shieldHP: 800,
            attack: 55,
            defense: 30,
            agility: 35,
            forwardSpeed: 40,
            moveSpeed: 38,
            bzdSpeed: 45
        },
        skill1Info: {
            skillId: 70001,
            skillName: '机体适性向上',
            description: 'Increases attack and defense when HP is below 50%',
            hpActivateCondition: 'HpMeet',
            exactHpActivatePercent: 50,
            attackIncrease: 15,
            defenseIncrease: 10,
            meleeDamageIncrease: 8,
            movement: 0,
            forwardSpeedPercent: 0,
            agilityPercent: 0,
            urgentEscape: 0,
            boostRecoveryPercent: 0,
            boostCapacityIncrease: 0,
            radarRangeIncrease: 0,
            spIncreaseSpeedPercent: 0,
            weaponReloadIncrease: 0,
            nearDamageReductionPercent: 15,
            midDamageReductionPercent: 10,
            appliesToSelf: true
        },
        skill2Info: {
            skillId: 70002,
            skillName: '团队支援',
            description: 'Boosts team agility and SP accumulation',
            hpActivateCondition: 'None',
            exactHpActivatePercent: 0,
            attackIncrease: 0,
            defenseIncrease: 0,
            meleeDamageIncrease: 0,
            movement: 5,
            forwardSpeedPercent: 8,
            agilityPercent: 12,
            urgentEscape: 3,
            boostRecoveryPercent: 10,
            boostCapacityIncrease: 20,
            radarRangeIncrease: 50,
            spIncreaseSpeedPercent: 15,
            weaponReloadIncrease: 5,
            nearDamageReductionPercent: 0,
            midDamageReductionPercent: 0,
            appliesToSelf: false
        },
        weapon1Info: {
            weaponId: 28751,
            weaponName: 'Beam Rifle',
            weaponType: 'Far',
            weaponDamage: 180,
            weaponRange: 450,
            ammoCount: 8,
            ammoRecoverySpeed: 3,
            coolTime: 2,
            missileSpeed: 120,
            aimSpeed: 85,
            knockbackEffect: 10,
            knockdownPerHit: 15,
            knockdownThreshold: 100,
            hasPierce: false,
            pierceValue: 0,
            allowUseWhenMove: true,
            collisionWidth: 0.5,
            collisionHeight: 0.5,
            splashRadius: 0,
            splashCoreRadius: 0
        },
        weapon2Info: {
            weaponId: 28752,
            weaponName: 'Beam Saber',
            weaponType: 'Near',
            weaponDamage: 280,
            weaponRange: 50,
            ammoCount: 2,
            ammoRecoverySpeed: 5,
            coolTime: 1,
            missileSpeed: 0,
            aimSpeed: 0,
            knockbackEffect: 35,
            knockdownPerHit: 40,
            knockdownThreshold: 100,
            hasPierce: true,
            pierceValue: 2,
            allowUseWhenMove: false,
            collisionWidth: 2.0,
            collisionHeight: 1.5,
            splashRadius: 0,
            splashCoreRadius: 0
        },
        weapon3Info: {
            weaponId: 28753,
            weaponName: 'Hyper Bazooka',
            weaponType: 'Mid',
            weaponDamage: 220,
            weaponRange: 200,
            ammoCount: 3,
            ammoRecoverySpeed: 8,
            coolTime: 4,
            missileSpeed: 80,
            aimSpeed: 50,
            knockbackEffect: 50,
            knockdownPerHit: 45,
            knockdownThreshold: 100,
            hasPierce: false,
            pierceValue: 0,
            allowUseWhenMove: true,
            collisionWidth: 1.0,
            collisionHeight: 1.0,
            splashRadius: 15,
            splashCoreRadius: 8
        },
        specialAttack: {
            weaponId: 99001,
            weaponName: 'Last Shooting',
            weaponType: 'Far',
            weaponDamage: 4500,
            weaponRange: 600,
            ammoCount: 1,
            ammoRecoverySpeed: 0,
            coolTime: 0,
            missileSpeed: 200,
            aimSpeed: 0,
            knockbackEffect: 100,
            knockdownPerHit: 100,
            knockdownThreshold: 100,
            hasPierce: true,
            pierceValue: 5,
            allowUseWhenMove: false,
            collisionWidth: 3.0,
            collisionHeight: 3.0,
            splashRadius: 25,
            splashCoreRadius: 15
        }
    }
}

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
    },

    // --- Machine Info ---
    async getMachineInfo(): Promise<any> {
        if (isDev()) return MOCK_MACHINEINFO
        try {
            const res = await client.get<any>('/machineinfo')
            return res.data
        } catch {
            return null
        }
    }
}
