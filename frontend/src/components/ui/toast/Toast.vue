<script setup lang="ts">
import { computed } from 'vue'
import type { Toast } from '@/composables/useToast'

const props = defineProps<{
    toast: Toast
}>()

const emit = defineEmits<{
    close: []
}>()

const bgClass = computed(() => {
    switch (props.toast.type) {
        case 'success': return 'bg-emerald-500/90 border-emerald-400'
        case 'error': return 'bg-red-500/90 border-red-400'
        case 'warning': return 'bg-amber-500/90 border-amber-400'
        default: return 'bg-neon-blue/90 border-neon-cyan'
    }
})
</script>

<template>
    <div
        :class="[
            'flex items-center justify-between px-4 py-3 rounded border shadow-lg',
            'text-white font-rajdhani text-sm min-w-[300px] max-w-md',
            bgClass
        ]"
    >
        <span>{{ toast.message }}</span>
        <button
            @click="emit('close')"
            class="ml-3 text-white/80 hover:text-white"
        >
            ✕
        </button>
    </div>
</template>
