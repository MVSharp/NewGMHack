<script setup lang="ts">
interface OcParts {
    Part1: number
    Part2: number
    Part3: number
    Part4: number
}

defineProps<{
    parts?: OcParts
}>()
</script>

<template>
    <div class="flex gap-2">
        <div 
            v-for="n in 4" 
            :key="n"
            class="part-block"
            :class="{ 'has-part': parts && (parts as any)[`Part${n}`] > 0 }"
        >
            <span class="text-xs">{{ parts ? (parts as any)[`Part${n}`] || '-' : '-' }}</span>
        </div>
    </div>
</template>

<style scoped>
.part-block {
    @apply w-10 h-8 flex items-center justify-center rounded border border-gray-600 bg-black/30 text-gray-500;
    transition: all 0.2s;
}

.part-block.has-part {
    @apply border-neon-blue-50 text-neon-cyan bg-neon-blue-10;
}

.part-block:hover {
    @apply border-neon-cyan;
}
</style>
