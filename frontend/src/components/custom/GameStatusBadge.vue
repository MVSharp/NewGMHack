<script setup lang="ts">
import { useI18n } from '@/composables/useI18n'

const props = defineProps<{
    status: string | null
    size?: 'sm' | 'md' | 'lg'
    showText?: boolean
}>()

const { t } = useI18n()

// Cyberpunk-style icons using simple geometric shapes
const statusConfig = {
    'Win': { 
        bg: 'bg-gradient-to-r from-emerald-600/30 to-emerald-400/20 border-emerald-500', 
        textClass: 'text-emerald-400',
        icon: '▲',   // Victory triangle pointing up
        textKey: 'win' 
    },
    'Lost': { 
        bg: 'bg-gradient-to-r from-red-600/30 to-red-400/20 border-red-500', 
        textClass: 'text-red-400',
        icon: '▼',    // Defeat triangle pointing down
        textKey: 'lost' 
    },
    'Draw': { 
        bg: 'bg-gradient-to-r from-amber-600/30 to-amber-400/20 border-amber-500', 
        textClass: 'text-amber-400',
        icon: '◆',   // Diamond for draw/neutral
        textKey: 'draw' 
    },
} as const

type StatusKey = keyof typeof statusConfig

const sizeClasses = {
    'sm': 'text-xs px-1.5 py-0.5 gap-1',
    'md': 'text-sm px-2 py-1 gap-1.5',
    'lg': 'text-base px-3 py-1.5 gap-2'
} as const

function getConfig(status: string | null) {
    if (status && status in statusConfig) {
        return statusConfig[status as StatusKey]
    }
    return statusConfig['Draw']
}

function getSizeClass(size: 'sm' | 'md' | 'lg' | undefined): string {
    return sizeClasses[size ?? 'md']
}
</script>

<template>
    <span 
        :class="[
            'inline-flex items-center justify-center font-rajdhani font-bold rounded border transition-all duration-300',
            getConfig(status).bg,
            getConfig(status).textClass,
            getSizeClass(size)
        ]"
    >
        <span class="animate-pulse">{{ getConfig(status).icon }}</span>
        <span v-if="showText" class="uppercase tracking-wider">{{ t(getConfig(status).textKey) }}</span>
    </span>
</template>
