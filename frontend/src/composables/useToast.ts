import { ref } from 'vue'

export interface Toast {
    id: number
    message: string
    type: 'success' | 'error' | 'info' | 'warning'
    duration?: number
}

const toasts = ref<Toast[]>([])

export function useToast() {
    function showToast(message: string, type: 'success' | 'error' | 'info' | 'warning' = 'info', duration = 3000) {
        const id = Date.now()
        toasts.value.push({ id, message, type, duration })

        if (duration > 0) {
            setTimeout(() => {
                removeToast(id)
            }, duration)
        }
    }

    function removeToast(id: number) {
        toasts.value = toasts.value.filter(t => t.id !== id)
    }

    return {
        toasts,
        showToast,
        removeToast
    }
}
