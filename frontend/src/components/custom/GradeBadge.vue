<script setup lang="ts">
const props = defineProps<{
    grade: string | null
    size?: 'sm' | 'md' | 'lg'
}>()

const gradeColors = {
    'A+': 'bg-gradient-to-r from-amber-400 to-yellow-300 text-black shadow-[0_0_15px_rgba(251,191,36,0.6)]',
    'A': 'bg-gradient-to-r from-cyan-400 to-teal-400 text-black shadow-[0_0_12px_rgba(34,211,238,0.5)]',
    'B+': 'bg-gradient-to-r from-green-400 to-emerald-400 text-black shadow-[0_0_10px_rgba(52,211,153,0.4)]',
    'B': 'bg-gradient-to-r from-lime-400 to-green-400 text-black',
    'C+': 'bg-gradient-to-r from-yellow-400 to-amber-400 text-black',
    'C': 'bg-gradient-to-r from-orange-400 to-amber-500 text-black',
    'D': 'bg-gradient-to-r from-red-400 to-rose-400 text-white',
    'F': 'bg-gradient-to-r from-gray-500 to-gray-600 text-white',
} as const

type GradeKey = keyof typeof gradeColors

const sizeClasses = {
    'sm': 'text-xs px-1.5 py-0.5',
    'md': 'text-sm px-2 py-1',
    'lg': 'text-2xl px-4 py-2 font-bold'
} as const

function getGradeClass(grade: string | null): string {
    if (!grade) return gradeColors['F']
    if (grade in gradeColors) {
        return gradeColors[grade as GradeKey]
    }
    return gradeColors['F']
}

function getSizeClass(size: 'sm' | 'md' | 'lg' | undefined): string {
    return sizeClasses[size ?? 'md']
}
</script>

<template>
    <span 
        v-if="grade"
        :class="[
            'inline-flex items-center justify-center font-rajdhani font-bold rounded transition-all duration-300',
            getGradeClass(grade),
            getSizeClass(size)
        ]"
    >
        {{ grade }}
    </span>
    <span 
        v-else 
        :class="[
            'inline-flex items-center justify-center font-rajdhani text-gray-500 rounded',
            getSizeClass(size)
        ]"
    >
        --
    </span>
</template>
