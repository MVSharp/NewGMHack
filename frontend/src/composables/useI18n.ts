import { ref, computed } from 'vue'

export type Language = 'en' | 'zh-CN' | 'zh-TW'

const translations: Record<Language, Record<string, string>> = {
    'en': {
        title: 'SD HACK BY MICHAEL VAN',
        dashboard: 'DASHBOARD',
        features: 'SYSTEM HACKS',
        pilot: 'PILOT INFO',
        lobby: 'LOBBY RECON',
        inject: 'INJECT',
        injecting: 'INJECTING...',
        deattach: 'DEATTACH',
        connected: 'CONNECTED',
        disconnected: 'DISCONNECTED',
        waiting: 'WAITING...',
        // Dashboard
        total_battle: 'TOTAL BATTLE',
        gb_earned: 'GB EARNED',
        total_bonus: 'TOTAL BONUS',
        total_gain: 'TOTAL GAIN (INC BONUS)',
        total_exp: 'TOTAL MACHINE EXP',
        max_score: 'MAX SCORE',
        total_kills: 'TOTAL KILLS',
        total_deaths: 'TOTAL DEATHS',
        total_support: 'TOTAL SUPPORT',
        latest_report: 'LATEST BATTLE REPORT',
        perf_score: 'PERFORMANCE SCORE',
        kills: 'KILLS',
        deaths: 'DEATHS',
        assist: 'ASSIST',
        points: 'POINTS',
        trend_header: 'ECONOMIC TREND (LAST 10 BATTLES)',
        pilot_efficiency: 'PILOT EFFICIENCY',
        metric: 'METRIC',
        hourly: 'HOURLY',
        today: 'TODAY',
        base_gb: 'BASE GB',
        bonus: 'BONUS',
        exp: 'EXP',
        battles: 'BATTLES',
        combat_log: 'COMBAT LOG',
        gain: 'GAIN',
        waiting_data: 'Waiting for data stream...',
        // Pilot
        pilot_telemetry: 'PILOT TELEMETRY',
        pilot_id: 'PILOT ID',
        condom_id: 'CONDOM ID',
        condom_name: 'CONDOM NAME',
        slot: 'SLOT',
        loadout: 'LOADOUT',
        coordinates: 'COORDINATES',
        weapon: 'WEAPON',
        // Lobby
        lobby_recon: 'LOBBY RECON',
        pilot_name: 'PILOT NAME',
        status: 'STATUS',
        scanning_lobby: 'SCANNING LOBBY...',
        // Features
        system_overrides: 'SYSTEM OVERRIDES',
        active: 'ACTIVE',
        offline: 'OFFLINE',
        updating: 'UPDATING...',
        refresh: 'REFRESH',
        loading_modules: 'LOADING MODULES...'
    },
    'zh-CN': {
        title: 'SD渣男 - Michael Van',
        dashboard: '仪表盘',
        features: '系统破解',
        pilot: '机师信息',
        lobby: '大厅侦察',
        inject: '注入',
        injecting: '注入中...',
        deattach: '解除',
        connected: '已连接',
        disconnected: '断开连接',
        waiting: '等待中...',
        total_battle: '总战斗数',
        gb_earned: 'GB获取',
        total_bonus: '总奖励',
        total_gain: '总收益(含奖励)',
        total_exp: '总机体经验',
        max_score: '最高得分',
        total_kills: '总击坠',
        total_deaths: '总被击坠',
        total_support: '总助攻',
        latest_report: '最新战斗报告',
        perf_score: '表现评分',
        kills: '击坠',
        deaths: '被击坠',
        assist: '助攻',
        points: '得分',
        trend_header: '经济趋势(最近10场)',
        pilot_efficiency: '机师效率',
        metric: '指标',
        hourly: '每小时',
        today: '今日',
        base_gb: '基础GB',
        bonus: '奖励',
        exp: '经验值',
        battles: '战斗次数',
        combat_log: '战斗日志',
        gain: '收益',
        waiting_data: '等待数据流...',
        pilot_telemetry: '机师遥测',
        pilot_id: '机师ID',
        condom_id: 'Condom ID',
        condom_name: 'Condom名称',
        slot: '槽位',
        loadout: '武装',
        coordinates: '坐标',
        weapon: '武器',
        lobby_recon: '大厅侦察',
        pilot_name: '机师名称',
        status: '状态',
        scanning_lobby: '扫描大厅中...',
        system_overrides: '系统覆盖',
        active: '激活',
        offline: '离线',
        updating: '更新中...',
        refresh: '刷新',
        loading_modules: '加载模块中...'
    },
    'zh-TW': {
        title: 'SD渣男 - Michael Van',
        dashboard: '儀表盤',
        features: '系統破解',
        pilot: '機師資訊',
        lobby: '大廳偵察',
        inject: '注入',
        injecting: '注入中...',
        deattach: '解除',
        connected: '已連接',
        disconnected: '斷開連接',
        waiting: '等待中...',
        total_battle: '總戰鬥數',
        gb_earned: 'GB獲取',
        total_bonus: '總獎勵',
        total_gain: '總收益(含獎勵)',
        total_exp: '總機體經驗',
        max_score: '最高得分',
        total_kills: '總擊墜',
        total_deaths: '總被擊墜',
        total_support: '總助攻',
        latest_report: '最新戰鬥報告',
        perf_score: '表現評分',
        kills: '擊墜',
        deaths: '被擊墜',
        assist: '助攻',
        points: '得分',
        trend_header: '經濟趨勢(最近10場)',
        pilot_efficiency: '機師效率',
        metric: '指標',
        hourly: '每小時',
        today: '今日',
        base_gb: '基礎GB',
        bonus: '獎勵',
        exp: '經驗值',
        battles: '戰鬥次數',
        combat_log: '戰鬥日誌',
        gain: '收益',
        waiting_data: '等待數據流...',
        pilot_telemetry: '機師遙測',
        pilot_id: '機師ID',
        condom_id: 'Condom ID',
        condom_name: 'Condom名稱',
        slot: '槽位',
        loadout: '武裝',
        coordinates: '座標',
        weapon: '武器',
        lobby_recon: '大廳偵察',
        pilot_name: '機師名稱',
        status: '狀態',
        scanning_lobby: '掃描大廳中...',
        system_overrides: '系統覆蓋',
        active: '啟用',
        offline: '離線',
        updating: '更新中...',
        refresh: '刷新',
        loading_modules: '載入模組中...'
    }
}

// Load saved language or default to 'en'
const savedLang = (typeof localStorage !== 'undefined' ? localStorage.getItem('lang') : null) as Language | null
const currentLang = ref<Language>(savedLang || 'en')

export function useI18n() {
    function t(key: string): string {
        return translations[currentLang.value][key] || key
    }

    function setLanguage(lang: Language) {
        currentLang.value = lang
        if (typeof localStorage !== 'undefined') {
            localStorage.setItem('lang', lang)
        }
    }

    const langEmoji = computed(() => {
        switch (currentLang.value) {
            case 'en': return '🇺🇸'
            case 'zh-CN': return '🇨🇳'
            case 'zh-TW': return '🇹🇼'
            default: return '🌐'
        }
    })

    return {
        t,
        setLanguage,
        currentLang,
        langEmoji,
        languages: [
            { code: 'en' as Language, emoji: '🇺🇸', label: 'EN' },
            { code: 'zh-CN' as Language, emoji: '🇨🇳', label: '简' },
            { code: 'zh-TW' as Language, emoji: '🇹🇼', label: '繁' }
        ]
    }
}
