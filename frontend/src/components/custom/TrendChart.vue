<script setup lang="ts">
import { computed } from 'vue'
import { Line } from 'vue-chartjs'
import {
    Chart as ChartJS,
    CategoryScale,
    LinearScale,
    PointElement,
    LineElement,
    Title,
    Tooltip,
    Legend,
    Filler
} from 'chart.js'

ChartJS.register(
    CategoryScale,
    LinearScale,
    PointElement,
    LineElement,
    Title,
    Tooltip,
    Legend,
    Filler
)

const props = defineProps<{
    labels: string[]
    gbData: number[]
    expData: number[]
}>()

const chartData = computed(() => ({
    labels: props.labels,
    datasets: [
        {
            label: 'TOTAL GAIN',
            data: props.gbData,
            borderColor: '#66fcf1',
            backgroundColor: 'rgba(102, 252, 241, 0.1)',
            tension: 0.4,
            fill: true
        },
        {
            label: 'MACHINE EXP',
            data: props.expData,
            borderColor: '#c5c6c7',
            backgroundColor: 'rgba(197, 198, 199, 0.1)',
            tension: 0.4,
            fill: true
        }
    ]
}))

const chartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: 'index' as const, intersect: false },
    plugins: {
        legend: {
            labels: {
                color: 'white',
                font: { family: 'Rajdhani' }
            }
        }
    },
    scales: {
        x: {
            ticks: { color: '#888' },
            grid: { color: '#333' }
        },
        y: {
            ticks: { color: '#888' },
            grid: { color: '#333' }
        }
    }
}
</script>

<template>
    <div class="h-full w-full">
        <Line 
            :data="chartData" 
            :options="chartOptions"
            class="h-full w-full"
        />
    </div>
</template>
