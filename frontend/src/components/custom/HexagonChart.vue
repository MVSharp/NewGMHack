<script setup lang="ts">
import { computed } from 'vue'

interface Stats {
    attack: number
    agility: number
    hp: number
    defense: number
    sp: number
    speed: number
}

interface HexStats {
    base: Stats
    ocBase: Stats
    ocBonus: Stats
}

const props = defineProps<{
    stats: HexStats
    showBase: boolean
    showOcBase: boolean
    showOcBonus: boolean
}>()

// Hexagon geometry
const size = 100
const cx = 130
const cy = 130
const maxValue = 100

// 6 axes at 60° intervals, rotated 30° left (so Attack is top-left)
const labels = ['Attack', 'Agility', 'HP', 'Defense', 'SP', 'Speed']
const statKeys = ['attack', 'agility', 'hp', 'defense', 'sp', 'speed'] as const

// Angles in radians
const angles = [
    -120, // Attack
    -60,  // Agility
    0,    // HP
    60,   // Defense
    120,  // SP
    180   // Speed
].map(deg => deg * Math.PI / 180)

function calcPoints(statValues: number[]): string {
    return statValues.map((val, i) => {
        const r = Math.min(val / maxValue, 1) * size
        const x = cx + r * Math.cos(angles[i])
        const y = cy + r * Math.sin(angles[i])
        return `${x},${y}`
    }).join(' ')
}

const labelPositions = computed(() => {
    const labelRadius = size + 18
    return labels.map((label, i) => ({
        label,
        x: cx + labelRadius * Math.cos(angles[i]),
        y: cy + labelRadius * Math.sin(angles[i])
    }))
})

const gridPoints = computed(() => {
    return [0.25, 0.5, 0.75, 1].map(scale => {
        return angles.map(a => {
            const r = scale * size
            return `${cx + r * Math.cos(a)},${cy + r * Math.sin(a)}`
        }).join(' ')
    })
})

const axisLines = computed(() => {
    return angles.map(a => ({
        x2: cx + size * Math.cos(a),
        y2: cy + size * Math.sin(a)
    }))
})

const totalStats = computed(() => {
    const b = props.stats.base
    const o = props.stats.ocBase
    const e = props.stats.ocBonus
    
    return statKeys.map((key, i) => ({
        label: labels[i],
        base: Math.round(b[key] * 10) / 10,
        withOc: Math.round((b[key] + o[key]) * 10) / 10,
        withBonus: Math.round((b[key] + o[key] + e[key]) * 10) / 10
    }))
})

const basePolygon = computed(() => {
    const s = props.stats.base
    return calcPoints([s.attack, s.agility, s.hp, s.defense, s.sp, s.speed])
})

const ocBasePolygon = computed(() => {
    const b = props.stats.base
    const o = props.stats.ocBase
    return calcPoints([
        b.attack + o.attack, b.agility + o.agility, b.hp + o.hp,
        b.defense + o.defense, b.sp + o.sp, b.speed + o.speed
    ])
})

const ocBonusPolygon = computed(() => {
    const b = props.stats.base
    const o = props.stats.ocBase
    const e = props.stats.ocBonus
    return calcPoints([
        b.attack + o.attack + e.attack, b.agility + o.agility + e.agility,
        b.hp + o.hp + e.hp, b.defense + o.defense + e.defense,
        b.sp + o.sp + e.sp, b.speed + o.speed + e.speed
    ])
})
</script>

<template>
    <div class="hex-wrapper">
        <div class="hex-chart">
            <svg viewBox="0 0 260 260" class="w-full">
                <polygon v-for="(points, i) in gridPoints" :key="i" :points="points"
                    fill="none" stroke="rgba(102,252,241,0.15)" stroke-width="1" />
                
                <line v-for="(axis, i) in axisLines" :key="'ax-'+i" :x1="cx" :y1="cy"
                    :x2="axis.x2" :y2="axis.y2"
                    stroke="rgba(102,252,241,0.2)" stroke-width="1" />

                <polygon v-if="showOcBonus" :points="ocBonusPolygon"
                    fill="rgba(245,158,11,0.2)" stroke="#f59e0b" stroke-width="2" />
                <polygon v-if="showOcBase" :points="ocBasePolygon"
                    fill="rgba(34,197,94,0.2)" stroke="#22c55e" stroke-width="2" />
                <polygon v-if="showBase" :points="basePolygon"
                    fill="rgba(59,130,246,0.25)" stroke="#3b82f6" stroke-width="2" />

                <text v-for="pos in labelPositions" :key="pos.label" :x="pos.x" :y="pos.y"
                    text-anchor="middle" dominant-baseline="middle"
                    class="text-sm fill-gray-300 font-rajdhani font-semibold">
                    {{ pos.label }}
                </text>
            </svg>
        </div>

        <div class="stats-panel">
            <div v-for="stat in totalStats" :key="stat.label" class="stat-row">
                <span class="stat-label">{{ stat.label }}</span>
                <div class="stat-values">
                    <span class="val-base" v-if="showBase">{{ stat.base }}</span>
                    <span class="val-oc" v-if="showOcBase">+{{ (stat.withOc - stat.base).toFixed(0) }}</span>
                    <span class="val-bonus" v-if="showOcBonus">+{{ (stat.withBonus - stat.withOc).toFixed(0) }}</span>
                    <span class="val-total">=&nbsp;{{ stat.withBonus }}</span>
                </div>
            </div>
        </div>
    </div>
</template>

<style scoped>
.hex-wrapper { @apply flex gap-4 items-center; }
.hex-chart { @apply w-64 shrink-0; }
.stats-panel { @apply flex-1 flex flex-col gap-1 text-xs min-w-32; }
.stat-row { @apply flex items-center justify-between py-1 px-2 rounded bg-black/30; }
.stat-label { @apply text-gray-400 font-medium w-16; }
.stat-values { @apply flex gap-2 items-center; }
.val-base { @apply text-blue-400; }
.val-oc { @apply text-green-400; }
.val-bonus { @apply text-orange-400; }
.val-total { @apply text-white font-bold; }
</style>
