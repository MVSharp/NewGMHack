<script setup lang="ts">
import { ref, watch, computed, onMounted } from 'vue'

const props = defineProps<{
    value: number
    duration?: number // Animation duration in ms (default 3000)
}>()

const displayValue = ref('')
const isAnimating = ref(false)
const previousValue = ref<number | null>(null)

const duration = computed(() => props.duration ?? 3000)

// Characters to cycle through for cyberpunk effect
const chars = '0123456789ABCDEF!@#$%&*'

function formatNumber(n: number): string {
    return n.toLocaleString()
}

function animateRoll(from: number, to: number) {
    if (from === to) return
    
    isAnimating.value = true
    const startTime = Date.now()
    const fromStr = formatNumber(from)
    const toStr = formatNumber(to)
    
    // Find positions that changed
    const maxLen = Math.max(fromStr.length, toStr.length)
    const paddedFrom = fromStr.padStart(maxLen, ' ')
    const paddedTo = toStr.padStart(maxLen, ' ')
    
    function animate() {
        const elapsed = Date.now() - startTime
        const progress = Math.min(elapsed / duration.value, 1)
        
        // Easing function (ease-out)
        const eased = 1 - Math.pow(1 - progress, 3)
        
        let result = ''
        for (let i = 0; i < maxLen; i++) {
            const fromChar = paddedFrom[i]
            const toChar = paddedTo[i]
            
            if (fromChar === toChar) {
                result += toChar
            } else if (progress >= 1) {
                result += toChar
            } else {
                // Calculate how far along this digit should be
                // Earlier digits settle faster
                const digitProgress = Math.min((eased * maxLen - i) / 2, 1)
                
                if (digitProgress >= 1) {
                    result += toChar
                } else if (digitProgress <= 0) {
                    result += fromChar
                } else {
                    // Cycle through random characters
                    const randomIdx = Math.floor(Math.random() * chars.length)
                    result += chars[randomIdx]
                }
            }
        }
        
        displayValue.value = result.trimStart()
        
        if (progress < 1) {
            requestAnimationFrame(animate)
        } else {
            displayValue.value = toStr
            isAnimating.value = false
        }
    }
    
    requestAnimationFrame(animate)
}

watch(() => props.value, (newVal, oldVal) => {
    if (oldVal !== undefined && oldVal !== newVal) {
        animateRoll(oldVal, newVal)
    } else {
        displayValue.value = formatNumber(newVal)
    }
    previousValue.value = newVal
})

onMounted(() => {
    displayValue.value = formatNumber(props.value)
    previousValue.value = props.value
})
</script>

<template>
    <span>{{ displayValue }}</span>
</template>

<style scoped>
/* Removed expensive glow animation */
</style>
