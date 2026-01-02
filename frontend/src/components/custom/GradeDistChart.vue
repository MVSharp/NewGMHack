<script setup lang="ts">
import { computed, ref } from 'vue'
import { Doughnut } from 'vue-chartjs'
import { useI18n } from '@/composables/useI18n'
import {
    Chart as ChartJS,
    ArcElement,
    Title,
    Tooltip,
    Legend
} from 'chart.js'

ChartJS.register(
    ArcElement,
    Title,
    Tooltip,
    Legend
)

const props = defineProps<{
    history: Array<{ GradeRank: string | null }>
}>()

const { t } = useI18n()

const gradeLabels = ['A+', 'A', 'B+', 'B', 'C+', 'C', 'D', 'F']
// Cyberpunk neon gradient colors
const gradeColors = [
    'rgba(251, 191, 36, 0.85)',   // A+ - gold/amber
    'rgba(34, 211, 238, 0.85)',   // A - cyan
    'rgba(52, 211, 153, 0.85)',   // B+ - emerald
    'rgba(132, 204, 22, 0.85)',   // B - lime
    'rgba(250, 204, 21, 0.85)',   // C+ - yellow
    'rgba(251, 146, 60, 0.85)',   // C - orange
    'rgba(248, 113, 113, 0.85)',  // D - red
    'rgba(107, 114, 128, 0.7)'    // F - gray
]

const timePeriod = ref<'total' | 'last5'>('total')

const chartData = computed(() => {
    const counts = new Map<string, number>([
        ['A+', 0], ['A', 0], ['B+', 0], ['B', 0],
        ['C+', 0], ['C', 0], ['D', 0], ['F', 0]
    ])
    
    // Filter based on time period (last 5 or all)
    const dataToUse = timePeriod.value === 'last5' 
        ? props.history.slice(0, 5) 
        : props.history

    for (const item of dataToUse) {
        const grade = item.GradeRank ?? 'F'
        const current = counts.get(grade)
        if (current !== undefined) {
            counts.set(grade, current + 1)
        }
    }

    // Filter out grades with 0 count for cleaner pie chart
    const filteredLabels: string[] = []
    const filteredData: number[] = []
    const filteredColors: string[] = []

    gradeLabels.forEach((g, i) => {
        const count = counts.get(g) ?? 0
        if (count > 0) {
            filteredLabels.push(g)
            filteredData.push(count)
            filteredColors.push(gradeColors[i]!)
        }
    })

    // If no data, show placeholder
    if (filteredData.length === 0) {
        return {
            labels: ['--'],
            datasets: [{
                data: [1],
                backgroundColor: ['rgba(50, 50, 50, 0.5)'],
                borderWidth: 0
            }]
        }
    }

    return {
        labels: filteredLabels,
        datasets: [{
            data: filteredData,
            backgroundColor: filteredColors,
            borderColor: 'rgba(10, 10, 10, 0.8)',
            borderWidth: 2,
            hoverBorderColor: '#66FCF1',
            hoverBorderWidth: 3
        }]
    }
})

const chartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    cutout: '45%',
    plugins: {
        legend: {
            display: true,
            position: 'right' as const,
            labels: {
                color: '#66FCF1',
                usePointStyle: true,
                padding: 8,
                font: { 
                    size: 10,
                    family: 'Rajdhani',
                    weight: 'bold' as const
                }
            }
        }
    }
}

const periods = [
    { key: 'total' as const, label: 'all' },
    { key: 'last5' as const, label: 'last_5' }
]
</script>

<template>
    <div class="flex flex-col h-full">
        <div class="flex items-center justify-between mb-2">
            <span class="text-neon-cyan font-rajdhani text-sm uppercase tracking-wider">{{ t('grade_stats') }}</span>
            <div class="flex gap-1">
                <button 
                    v-for="period in periods" 
                    :key="period.key"
                    @click="timePeriod = period.key"
                    :class="[
                        'px-2 py-0.5 text-xs font-rajdhani rounded transition-all uppercase tracking-wide',
                        timePeriod === period.key 
                            ? 'bg-neon-cyan-20 text-neon-cyan border border-neon-cyan-50 shadow-[0_0_8px_rgba(102,252,241,0.3)]' 
                            : 'bg-white/5 text-gray-500 hover:bg-white/10 border border-transparent'
                    ]"
                >
                    {{ t(period.label) }}
                </button>
            </div>
        </div>
        <div class="flex-1 min-h-0 flex items-center justify-center">
            <Doughnut :data="chartData" :options="chartOptions" class="max-h-full" />
        </div>
    </div>
</template>
