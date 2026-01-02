<script setup lang="ts">
import { computed } from 'vue'
import { useSignalR } from '@/composables/useSignalR'
import { useI18n } from '@/composables/useI18n'
import TrendChart from '@/components/custom/TrendChart.vue'
import AnimatedNumber from '@/components/custom/AnimatedNumber.vue'
import GradeBadge from '@/components/custom/GradeBadge.vue'
import GameStatusBadge from '@/components/custom/GameStatusBadge.vue'
import WinLossChart from '@/components/custom/WinLossChart.vue'
import GradeDistChart from '@/components/custom/GradeDistChart.vue'

const { 
    stats, 
    latestMatch, 
    combatLog 
} = useSignalR()

const { t } = useI18n()

// Computed values for KPIs
const totalMatches = computed(() => stats.value?.stats?.Matches ?? 0)
const totalGb = computed(() => stats.value?.stats?.TotalGBGain ?? 0)
const totalBonus = computed(() => stats.value?.stats?.TotalBonusGB ?? 0)
const totalGain = computed(() => totalGb.value + totalBonus.value)
const totalExp = computed(() => stats.value?.stats?.TotalMachineExp ?? 0)
const totalKills = computed(() => stats.value?.stats?.TotalKills ?? 0)
const totalDeaths = computed(() => stats.value?.stats?.TotalDeaths ?? 0)
const totalSupports = computed(() => stats.value?.stats?.TotalSupports ?? 0)
const winRate = computed(() => stats.value?.stats?.WinRate ?? 0)

// Win/Loss stats for chart
const winLossTotal = computed(() => ({
    wins: stats.value?.stats?.Wins ?? 0,
    losses: stats.value?.stats?.Losses ?? 0,
    draws: stats.value?.stats?.Draws ?? 0
}))
const winLossHourly = computed(() => ({
    wins: stats.value?.hourly?.WinsLastHour ?? 0,
    losses: stats.value?.hourly?.LossesLastHour ?? 0
}))
const winLossToday = computed(() => ({
    wins: stats.value?.today?.WinsToday ?? 0,
    losses: stats.value?.today?.LossesToday ?? 0
}))

// Hourly stats
const hourGb = computed(() => stats.value?.hourly?.GBGainLastHour ?? 0)
const hourBonus = computed(() => stats.value?.hourly?.BonusLastHour ?? 0)
const hourTotal = computed(() => hourGb.value + hourBonus.value)
const hourExp = computed(() => stats.value?.hourly?.MachineExpLastHour ?? 0)
const hourMatches = computed(() => stats.value?.hourly?.MatchesLastHour ?? 0)

// Today stats
const todayGb = computed(() => stats.value?.today?.GBGainToday ?? 0)
const todayBonus = computed(() => stats.value?.today?.BonusToday ?? 0)
const todayTotal = computed(() => todayGb.value + todayBonus.value)
const todayExp = computed(() => stats.value?.today?.MachineExpToday ?? 0)
const todayMatches = computed(() => stats.value?.today?.MatchesToday ?? 0)

// Chart data
const chartLabels = computed(() => {
    return [...combatLog.value].reverse().map(h => 
        new Date(h.CreatedAtUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    )
})
const chartGbData = computed(() => {
    return [...combatLog.value].reverse().map(h => h.GBGain + (h.TotalBonus ?? 0))
})
const chartExpData = computed(() => {
    return [...combatLog.value].reverse().map(h => h.MachineAddedExp)
})
</script>

<template>
    <div class="flex flex-col h-full gap-4 overflow-hidden">
        <!-- KPI Row (9 Cards) -->
        <div class="grid grid-cols-9 gap-2">
            <div class="kpi-card border-t-gundam-gold">
                <div class="kpi-label">{{ t('total_battle') }}</div>
                <div class="kpi-value"><AnimatedNumber :value="totalMatches" /></div>
            </div>
            <div class="kpi-card border-t-emerald-500">
                <div class="kpi-label">{{ t('win_rate') }}</div>
                <div class="kpi-value text-emerald-400">{{ winRate.toFixed(1) }}%</div>
            </div>
            <div class="kpi-card border-t-gundam-gold">
                <div class="kpi-label">{{ t('gb_earned') }}</div>
                <div class="kpi-value text-gundam-gold glow-gold"><AnimatedNumber :value="totalGb" /></div>
            </div>
            <div class="kpi-card border-t-bonus-orange">
                <div class="kpi-label">{{ t('total_bonus') }}</div>
                <div class="kpi-value text-bonus-orange glow-orange"><AnimatedNumber :value="totalBonus" /></div>
            </div>
            <div class="kpi-card border-t-neon-cyan">
                <div class="kpi-label">{{ t('total_gain') }}</div>
                <div class="kpi-value text-neon-cyan glow-cyan"><AnimatedNumber :value="totalGain" /></div>
            </div>
            <div class="kpi-card border-t-machine-silver">
                <div class="kpi-label">{{ t('total_exp') }}</div>
                <div class="kpi-value text-machine-silver glow-silver"><AnimatedNumber :value="totalExp" /></div>
            </div>
            <div class="kpi-card border-t-beam-pink">
                <div class="kpi-label">{{ t('total_kills') }}</div>
                <div class="kpi-value"><AnimatedNumber :value="totalKills" /></div>
            </div>
            <div class="kpi-card border-t-beam-pink">
                <div class="kpi-label">{{ t('total_deaths') }}</div>
                <div class="kpi-value"><AnimatedNumber :value="totalDeaths" /></div>
            </div>
            <div class="kpi-card border-t-beam-pink">
                <div class="kpi-label">{{ t('total_support') }}</div>
                <div class="kpi-value"><AnimatedNumber :value="totalSupports" /></div>
            </div>
        </div>

        <!-- Main Content: Left + Right Columns -->
        <div class="grid grid-cols-[2fr_1.2fr] gap-4 flex-1 overflow-hidden">
            <!-- Left Column -->
            <div class="flex flex-col gap-4 h-full">
                <!-- Latest Battle Report Panel -->
                <div class="panel">
                    <div class="panel-header">
                        <span>{{ t('latest_report') }}</span>
                        <span class="text-sm text-gray-500 font-mono">{{ latestMatch?.timestamp ?? '--' }}</span>
                    </div>
                    <div class="flex items-stretch gap-4">
                        <!-- Performance Score -->
                        <div class="text-center border border-neon-blue p-4 flex-1 bg-black/30 transition-all duration-300 hover:border-neon-cyan hover:shadow-[0_0_15px_rgba(102,252,241,0.2)] flex flex-col justify-center">
                            <div class="text-xs text-gray-500 mb-2">{{ t('perf_score') }}</div>
                            <div class="text-5xl font-bold font-rajdhani text-white">
                                <AnimatedNumber :value="latestMatch?.points ?? 0" />
                            </div>
                        </div>
                        <!-- Grade Display -->
                        <div class="text-center border border-amber-500/30 p-4 w-24 bg-black/30 transition-all duration-300 hover:border-amber-400 flex flex-col justify-center">
                            <div class="text-xs text-gray-500 mb-2">{{ t('grade') }}</div>
                            <GradeBadge :grade="latestMatch?.gradeRank ?? null" size="lg" />
                        </div>
                        <!-- Win/Loss Status (no separate box, just badge) -->
                        <div class="flex flex-col justify-center items-center w-24">
                            <GameStatusBadge :status="latestMatch?.gameStatus ?? null" size="lg" :showText="true" />
                        </div>
                        <!-- Stats Grid -->
                        <div class="grid grid-cols-2 gap-3 flex-1">
                            <div class="mini-stat">
                                <div class="text-xs text-gray-400">{{ t('kills') }}</div>
                                <div class="text-2xl text-neon-cyan font-bold"><AnimatedNumber :value="latestMatch?.kills ?? 0" /></div>
                            </div>
                            <div class="mini-stat">
                                <div class="text-xs text-gray-400">{{ t('deaths') }}</div>
                                <div class="text-2xl text-neon-cyan font-bold"><AnimatedNumber :value="latestMatch?.deaths ?? 0" /></div>
                            </div>
                            <div class="mini-stat">
                                <div class="text-xs text-gray-400">{{ t('assist') }}</div>
                                <div class="text-2xl text-neon-cyan font-bold"><AnimatedNumber :value="latestMatch?.supports ?? 0" /></div>
                            </div>
                            <div class="mini-stat">
                                <div class="text-xs text-gray-400">{{ t('gain') }}</div>
                                <div class="text-xl text-gundam-gold font-bold"><AnimatedNumber :value="latestMatch?.gbGain ?? 0" /></div>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Charts Row -->
                <div class="grid grid-cols-3 gap-4 flex-1 min-h-0">
                    <!-- Economic Trend Chart (smaller) -->
                    <div class="panel min-h-0">
                        <div class="panel-header text-sm">{{ t('trend_header') }}</div>
                        <div class="flex-1 min-h-0">
                            <TrendChart 
                                :labels="chartLabels" 
                                :gb-data="chartGbData" 
                                :exp-data="chartExpData" 
                            />
                        </div>
                    </div>
                    <!-- Win/Loss Chart -->
                    <div class="panel min-h-0">
                        <WinLossChart 
                            :total="winLossTotal"
                            :hourly="winLossHourly"
                            :today="winLossToday"
                        />
                    </div>
                    <!-- Grade Distribution Chart -->
                    <div class="panel min-h-0">
                        <GradeDistChart :history="combatLog" />
                    </div>
                </div>
            </div>

            <!-- Right Column -->
            <div class="flex flex-col gap-4 h-full">
                <!-- Pilot Efficiency Table -->
                <div class="panel">
                    <div class="panel-header">{{ t('pilot_efficiency') }}</div>
                    <table class="w-full text-sm">
                        <thead>
                            <tr class="text-neon-cyan border-b border-gray-700">
                                <th class="text-left p-2">{{ t('metric') }}</th>
                                <th class="text-left p-2">{{ t('hourly') }}</th>
                                <th class="text-left p-2">{{ t('today') }}</th>
                            </tr>
                        </thead>
                        <tbody class="font-rajdhani">
                            <tr class="text-white font-bold border-b border-white/5 table-row-hover">
                                <td class="p-2">{{ t('total_gain') }}</td>
                                <td class="p-2 text-neon-cyan glow-cyan"><AnimatedNumber :value="hourTotal" /></td>
                                <td class="p-2 text-neon-cyan glow-cyan"><AnimatedNumber :value="todayTotal" /></td>
                            </tr>
                            <tr class="border-b border-white/5 table-row-hover">
                                <td class="p-2 text-gray-400">{{ t('base_gb') }}</td>
                                <td class="p-2"><AnimatedNumber :value="hourGb" /></td>
                                <td class="p-2"><AnimatedNumber :value="todayGb" /></td>
                            </tr>
                            <tr class="border-b border-white/5 table-row-hover">
                                <td class="p-2 text-gray-400">{{ t('bonus') }}</td>
                                <td class="p-2 text-bonus-orange glow-orange"><AnimatedNumber :value="hourBonus" /></td>
                                <td class="p-2 text-bonus-orange glow-orange"><AnimatedNumber :value="todayBonus" /></td>
                            </tr>
                            <tr class="border-b border-white/5 table-row-hover">
                                <td class="p-2 text-gray-400">{{ t('exp') }}</td>
                                <td class="p-2"><AnimatedNumber :value="hourExp" /></td>
                                <td class="p-2"><AnimatedNumber :value="todayExp" /></td>
                            </tr>
                            <tr class="table-row-hover">
                                <td class="p-2 text-gray-400">{{ t('battles') }}</td>
                                <td class="p-2"><AnimatedNumber :value="hourMatches" /></td>
                                <td class="p-2"><AnimatedNumber :value="todayMatches" /></td>
                            </tr>
                        </tbody>
                    </table>
                </div>

                <!-- Combat Log -->
                <div class="panel flex-1 min-h-0 flex flex-col">
                    <div class="panel-header">{{ t('combat_log') }}</div>
                    <div class="flex-1 overflow-y-auto pr-1">
                        <ul v-if="combatLog.length > 0" class="space-y-1">
                            <li 
                                v-for="(match, idx) in combatLog" 
                                :key="idx"
                                :class="[
                                    'history-item',
                                    match.Points > 2000 ? 'border-l-beam-pink bg-beam-pink-5' : ''
                                ]"
                            >
                                <div class="flex items-center gap-2">
                                    <GameStatusBadge :status="match.GameStatus" size="sm" />
                                    <GradeBadge :grade="match.GradeRank" size="sm" />
                                    <span class="text-gray-500 text-xs">
                                        {{ new Date(match.CreatedAtUtc).toLocaleTimeString() }}
                                    </span>
                                </div>
                                <span>
                                    <span class="text-neon-cyan">{{ t('gain') }}: {{ (match.GBGain + (match.TotalBonus ?? 0)).toLocaleString() }}</span>
                                    <span class="text-bonus-orange text-xs ml-2">({{ t('bonus') }}: {{ (match.TotalBonus ?? 0).toLocaleString() }})</span>
                                </span>
                                <span class="text-gray-400">{{ t('exp') }}: {{ match.MachineAddedExp.toLocaleString() }}</span>
                            </li>
                        </ul>
                        <div v-else class="text-center text-gray-600 py-8">
                            {{ t('waiting_data') }}
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>
</template>

<style scoped>
.kpi-card {
    @apply bg-bg-panel p-3 text-center border-t-[3px] shadow-md transition-all duration-300;
}

.kpi-card:hover {
    @apply shadow-lg transform -translate-y-0.5;
}

.kpi-label {
    @apply text-xs text-text-dim tracking-wider mb-1 font-rajdhani uppercase;
}
.kpi-value {
    @apply text-xl font-bold text-white font-rajdhani;
}

.panel {
    @apply bg-bg-panel border border-white/5 p-4 flex flex-col transition-all duration-300;
}

.panel:hover {
    @apply border-white/10 shadow-lg;
}

.panel-header {
    @apply text-neon-cyan font-rajdhani text-lg border-b border-neon-cyan-30 pb-2 mb-3 flex justify-between items-center uppercase;
}

.mini-stat {
    @apply bg-black/40 p-3 text-center transition-all duration-300;
}

.mini-stat:hover {
    @apply bg-black/60 shadow-[0_0_10px_rgba(102,252,241,0.1)];
}

.history-item {
    @apply bg-white/5 p-2 flex justify-between items-center font-rajdhani border-l-[3px] border-transparent transition-all duration-200;
}

.history-item:hover {
    @apply bg-white/10;
}

.table-row-hover {
    @apply transition-all duration-200;
}

.table-row-hover:hover {
    @apply bg-white/5;
}

/* Glow Effects */
.glow-gold { text-shadow: 0 0 10px rgba(255, 215, 0, 0.4); }
.glow-orange { text-shadow: 0 0 10px rgba(255, 170, 0, 0.4); }
.glow-cyan { text-shadow: 0 0 10px rgba(102, 252, 241, 0.4); }
.glow-silver { text-shadow: 0 0 10px rgba(224, 224, 224, 0.4); }
</style>
