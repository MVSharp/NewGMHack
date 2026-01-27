<script setup lang="ts">
import Toast from './Toast.vue'
import { useToast } from '@/composables/useToast'

const { toasts, removeToast } = useToast()
</script>

<template>
    <div class="fixed top-4 right-4 z-50 flex flex-col gap-2 pointer-events-none">
        <Toast
            v-for="toast in toasts"
            :key="toast.id"
            :toast="toast"
            @close="removeToast(toast.id)"
            class="pointer-events-auto"
        />
    </div>
</template>
