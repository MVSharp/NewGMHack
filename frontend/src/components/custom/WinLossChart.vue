<script setup lang="ts">
import { computed, ref } from 'vue'
import { Bar } from 'vue-chartjs'
import { useI18n } from '@/composables/useI18n'
import {
    Chart as ChartJS,
    CategoryScale,
    LinearScale,
    BarElement,
    Title,
    Tooltip,
    Legend
} from 'chart.js'

ChartJS.register(
    CategoryScale,
    LinearScale,
    BarElement,
    Title,
    Tooltip,
    Legend
)

const props = defineProps<{
    total: { wins: number, losses: number, draws: number }
    hourly: { wins: number, losses: number }
    today: { wins: number, losses: number }
}>()

const { t } = useI18n()
const timePeriod = ref<'total' | 'today' | 'hourly'>('total')

const chartData = computed(() => {
    let data: number[] = []
    
    switch (timePeriod.value) {
        case 'total':
            data = [props.total.wins, props.total.losses, props.total.draws]
            break
        case 'today':
            data = [props.today.wins, props.today.losses, 0]
            break
        case 'hourly':
            data = [props.hourly.wins, props.hourly.losses, 0]
            break
    }

    return {
        labels: [t('win'), t('lost'), t('draw')],
        datasets: [{
            data,
            backgroundColor: [
                'rgba(16, 185, 129, 0.7)',  // emerald with transparency
                'rgba(239, 68, 68, 0.7)',   // red  
                'rgba(245, 158, 11, 0.7)'   // amber for draw
            ],
            borderColor: [
                'rgb(52, 211, 153)',        // lighter emerald border
                'rgb(248, 113, 113)',       // lighter red border
                'rgb(252, 211, 77)'         // lighter amber border
            ],
            borderWidth: 2,
            borderRadius: 4,
            hoverBackgroundColor: [
                'rgba(16, 185, 129, 0.9)',
                'rgba(239, 68, 68, 0.9)',
                'rgba(245, 158, 11, 0.9)'
            ]
        }]
    }
})

const chartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
        legend: { display: false }
    },
    scales: {
        x: {
            ticks: { 
                color: '#66FCF1',  // neon cyan
                font: { family: 'Rajdhani', weight: 'bold' as const }
            },
            grid: { display: false },
            border: { color: '#333' }
        },
        y: {
            beginAtZero: true,
            ticks: { 
                color: '#888',
                stepSize: 1
            },
            grid: { color: 'rgba(102, 252, 241, 0.1)' },  // subtle cyan grid
            border: { color: '#333' }
        }
    }
}

const periods = [
    { key: 'total' as const, label: 'total' },
    { key: 'today' as const, label: 'today' },
    { key: 'hourly' as const, label: 'hourly' }
]
</script>

<template>
    <div class="flex flex-col h-full">
        <div class="flex items-center justify-between mb-2">
            <span class="text-neon-cyan font-rajdhani text-sm uppercase tracking-wider">{{ t('win_loss') }}</span>
            <div class="flex gap-1">
                <button 
                    v-for="period in periods" 
                    :key="period.key"
                    @click="timePeriod = period.key"
                    :class="[
                        'px-2 py-0.5 text-xs font-rajdhani rounded transition-all uppercase tracking-wide',
                        timePeriod === period.key 
                            ? 'bg-neon-cyan/20 text-neon-cyan border border-neon-cyan/50 shadow-[0_0_8px_rgba(102,252,241,0.3)]' 
                            : 'bg-white/5 text-gray-500 hover:bg-white/10 border border-transparent'
                    ]"
                >
                    {{ t(period.label) }}
                </button>
            </div>
        </div>
        <div class="flex-1 min-h-0">
            <Bar :data="chartData" :options="chartOptions" class="h-full w-full" />
        </div>
    </div>
</template>
