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
    MachineModel: {
        MachineId: 58,
        Slot: 1,
        Level: 45,
        Colors: ['#FF4444', '#FFFFFF', '#3366FF', '#FFCC00', '#44FF44', '#FF44FF'],
        BatteryPercent: 85.5,
        BatteryRaw: 1710,
        BattleCount: 342,
        CurrentExp: 125000,
        OcMaxLevel: 8,
        IsLocked: false,
        BrushPolish: 150,
        ExtraSkillParts: 2,
        BuyInTime: new Date().toISOString(),
        Oc1Parts: { Part1: 1001, Part2: 1002, Part3: 1003, Part4: 0 },
        Oc2Parts: { Part1: 2001, Part2: 2002, Part3: 0, Part4: 0 },
        OcBaseBonusPoints: { Speed: 12, Hp: 15, Attack: 18, Defense: 10, Agility: 14, Special: 8, Total: 77 },
        OcBonusExtraPoints: { Speed: 5, Hp: 8, Attack: 6, Defense: 4, Agility: 7, Special: 3, Total: 33 }
    },
    MachineBaseInfo: {
        MachineId: 58,
        ChineseName: 'RX-78-2 高达',
        EnglishName: 'RX-78-2 GUNDAM',
        Rank: 'A',
        Quality: 3,
        Rarity: 2,
        CombatType: 'Mid',
        AttackSpeedLevel: 4,
        HP: 6500,
        ShieldHP: 1200,
        ShieldType: 'ALL',
        ShieldDirection: 'Front',
        ShieldDeductionPercentage: 80,
        Attack: 45,
        Defense: 38,
        Agility: 22,
        ForwardSpeed: 28,
        MoveSpeed: 25,
        BzdSpeed: 32,
        TrackSpeed: 18,
        TrackAcceleration: 1.25,
        BoostCapacity: 150,
        BoostRecoverySpeed: 12,
        BoostConsumption: 8,
        RadarRange: 800,
        RespawnTimeSeconds: 12,
        HasTransform: true,
        TransformId: 59,
        TransformedMachine: {
            MachineId: 59,
            ChineseName: 'RX-78-2 高达 [变形]',
            EnglishName: 'RX-78-2 GUNDAM [Transform]',
            Rank: 'A',
            Quality: 3,
            Rarity: 2,
            CombatType: 'Far',
            HP: 5800,
            ShieldHP: 800,
            Attack: 55,
            Defense: 30,
            Agility: 35,
            ForwardSpeed: 40,
            MoveSpeed: 38,
            BzdSpeed: 45
        },
        Skill1Info: {
            SkillId: 70001,
            SkillName: '机体适性向上',
            Description: 'Increases attack and defense when HP is below 50%',
            HpActivateCondition: 'HpMeet',
            ExactHpActivatePercent: 50,
            AttackIncrease: 15,
            DefenseIncrease: 10,
            MeleeDamageIncrease: 8,
            Movement: 0,
            ForwardSpeedPercent: 0,
            AgilityPercent: 0,
            UrgentEscape: 0,
            BoostRecoveryPercent: 0,
            BoostCapacityIncrease: 0,
            RadarRangeIncrease: 0,
            SpIncreaseSpeedPercent: 0,
            WeaponReloadIncrease: 0,
            NearDamageReductionPercent: 15,
            MidDamageReductionPercent: 10,
            AppliesToSelf: true
        },
        Skill2Info: {
            SkillId: 70002,
            SkillName: '团队支援',
            Description: 'Boosts team agility and SP accumulation',
            HpActivateCondition: 'None',
            ExactHpActivatePercent: 0,
            AttackIncrease: 0,
            DefenseIncrease: 0,
            MeleeDamageIncrease: 0,
            Movement: 5,
            ForwardSpeedPercent: 8,
            AgilityPercent: 12,
            UrgentEscape: 3,
            BoostRecoveryPercent: 10,
            BoostCapacityIncrease: 20,
            RadarRangeIncrease: 50,
            SpIncreaseSpeedPercent: 15,
            WeaponReloadIncrease: 5,
            NearDamageReductionPercent: 0,
            MidDamageReductionPercent: 0,
            AppliesToSelf: false
        },
        Weapon1Info: {
            WeaponId: 28751,
            WeaponName: 'Beam Rifle',
            WeaponType: 'Far',
            WeaponDamage: 180,
            WeaponRange: 450,
            AmmoCount: 8,
            AmmoRecoverySpeed: 3,
            CoolTime: 2,
            MissileSpeed: 120,
            AimSpeed: 85,
            KnockbackEffect: 10,
            KnockdownPerHit: 15,
            KnockdownThreshold: 100,
            HasPierce: false,
            PierceValue: 0,
            AllowUseWhenMove: true,
            CollisionWidth: 0.5,
            CollisionHeight: 0.5,
            SplashRadius: 0,
            SplashCoreRadius: 0
        },
        Weapon2Info: {
            WeaponId: 28752,
            WeaponName: 'Beam Saber',
            WeaponType: 'Near',
            WeaponDamage: 280,
            WeaponRange: 50,
            AmmoCount: 2,
            AmmoRecoverySpeed: 5,
            CoolTime: 1,
            MissileSpeed: 0,
            AimSpeed: 0,
            KnockbackEffect: 35,
            KnockdownPerHit: 40,
            KnockdownThreshold: 100,
            HasPierce: true,
            PierceValue: 2,
            AllowUseWhenMove: false,
            CollisionWidth: 2.0,
            CollisionHeight: 1.5,
            SplashRadius: 0,
            SplashCoreRadius: 0
        },
        Weapon3Info: {
            WeaponId: 28753,
            WeaponName: 'Hyper Bazooka',
            WeaponType: 'Mid',
            WeaponDamage: 220,
            WeaponRange: 200,
            AmmoCount: 3,
            AmmoRecoverySpeed: 8,
            CoolTime: 4,
            MissileSpeed: 80,
            AimSpeed: 50,
            KnockbackEffect: 50,
            KnockdownPerHit: 45,
            KnockdownThreshold: 100,
            HasPierce: false,
            PierceValue: 0,
            AllowUseWhenMove: true,
            CollisionWidth: 1.0,
            CollisionHeight: 1.0,
            SplashRadius: 15,
            SplashCoreRadius: 8
        },
        SpecialAttack: {
            WeaponId: 99001,
            WeaponName: 'Last Shooting',
            WeaponType: 'Far',
            WeaponDamage: 4500,
            WeaponRange: 600,
            AmmoCount: 1,
            AmmoRecoverySpeed: 0,
            CoolTime: 0,
            MissileSpeed: 200,
            AimSpeed: 0,
            KnockbackEffect: 100,
            KnockdownPerHit: 100,
            KnockdownThreshold: 100,
            HasPierce: true,
            PierceValue: 5,
            AllowUseWhenMove: false,
            CollisionWidth: 3.0,
            CollisionHeight: 3.0,
            SplashRadius: 25,
            SplashCoreRadius: 15
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
