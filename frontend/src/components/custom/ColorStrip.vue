<script setup lang="ts">
defineProps<{
    colors: string[]
}>()
</script>

<template>
    <div class="flex gap-2">
        <div 
            v-for="(color, idx) in colors" 
            :key="idx"
            class="color-block"
            :style="{ backgroundColor: color }"
            :title="`Color ${idx + 1}: ${color}`"
        ></div>
    </div>
</template>

<style scoped>
.color-block {
    @apply w-8 h-6 rounded border border-white/20 shadow-inner;
    transition: transform 0.2s, box-shadow 0.2s;
}

.color-block:hover {
    @apply scale-110;
    box-shadow: 0 0 10px currentColor;
}
</style>
