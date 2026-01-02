<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue'

const props = defineProps<{
    active: boolean // true = Trans-Am Mode (Red/Fast), false = Idle (Cyan/Slow)
}>()

const canvasRef = ref<HTMLCanvasElement | null>(null)
let ctx: CanvasRenderingContext2D | null = null
let animationFrameId: number
let particles: Particle[] = []

// Configuration
const config = {
    idle: {
        count: 80,
        color: '#66fcf1',
        speedBase: 0.5,
        connectDistance: 150,
        size: 2
    },
    transAm: {
        count: 150,
        color: '#66fcf1', // Scan/Data color instead of Red
        speedBase: 4.0,   // Much faster
        connectDistance: 100,
        size: 3
    }
}

class Particle {
    x: number
    y: number
    vx: number
    vy: number
    size: number
    
    constructor(w: number, h: number, isTransAm: boolean) {
        this.x = Math.random() * w
        this.y = Math.random() * h
        this.size = isTransAm ? Math.random() * 2 + 1 : Math.random() * 2
        
        const speed = isTransAm ? config.transAm.speedBase : config.idle.speedBase
        // Trans-Am: Upward flow with some diagonal
        if (isTransAm) {
            this.vx = (Math.random() - 0.5) * speed
            this.vy = -Math.random() * speed * 2 - 1
        } else {
            // Idle: Random drift
            this.vx = (Math.random() - 0.5) * speed
            this.vy = (Math.random() - 0.5) * speed
        }
    }

    update(w: number, h: number) {
        this.x += this.vx
        this.y += this.vy

        // Wrap around screen
        if (this.x < 0) this.x = w
        if (this.x > w) this.x = 0
        if (this.y < 0) this.y = h
        if (this.y > h) this.y = 0
    }

    draw(ctx: CanvasRenderingContext2D, color: string) {
        ctx.beginPath()
        ctx.arc(this.x, this.y, this.size, 0, Math.PI * 2)
        ctx.fillStyle = color
        ctx.fill()
    }
}

function initParticles(width: number, height: number) {
    const isTransAm = props.active
    const cfg = isTransAm ? config.transAm : config.idle
    particles = []
    
    for (let i = 0; i < cfg.count; i++) {
        particles.push(new Particle(width, height, isTransAm))
    }
}

function animate() {
    if (!canvasRef.value || !ctx) return
    const w = canvasRef.value.width
    const h = canvasRef.value.height

    ctx.clearRect(0, 0, w, h)
    
    const isTransAm = props.active
    const cfg = isTransAm ? config.transAm : config.idle

    // Update and Draw Particles
    particles.forEach(p => {
        p.update(w, h)
        p.draw(ctx!, cfg.color)
    })

    // Draw Connections
    if (!isTransAm) {
        // Only connect in idle mode for "network" look
        ctx.strokeStyle = cfg.color
        ctx.lineWidth = 0.2
        
        for (let i = 0; i < particles.length; i++) {
            const p1 = particles[i]
            if (!p1) continue
            
            for (let j = i + 1; j < particles.length; j++) {
                const p2 = particles[j]
                if (!p2) continue

                const dx = p1.x - p2.x
                const dy = p1.y - p2.y
                const dist = Math.sqrt(dx * dx + dy * dy)

                if (dist < cfg.connectDistance) {
                    ctx.globalAlpha = 1 - (dist / cfg.connectDistance)
                    ctx.beginPath()
                    ctx.moveTo(p1.x, p1.y)
                    ctx.lineTo(p2.x, p2.y)
                    ctx.stroke()
                    ctx.globalAlpha = 1.0
                }
            }
        }
    } else {
        // Trans-Am Mode: Speed lines effect
        ctx.strokeStyle = cfg.color
        ctx.lineWidth = 0.5
        
        particles.forEach(p => {
             ctx!.globalAlpha = 0.3
             ctx!.beginPath()
             ctx!.moveTo(p.x, p.y)
             ctx!.lineTo(p.x - p.vx * 3, p.y - p.vy * 3) // Trail behind
             ctx!.stroke()
             ctx!.globalAlpha = 1.0
        })
    }

    animationFrameId = requestAnimationFrame(animate)
}

function handleResize() {
    if (!canvasRef.value) return
    const container = canvasRef.value.parentElement
    if (container) {
        canvasRef.value.width = container.clientWidth
        canvasRef.value.height = container.clientHeight
    }
    // Re-init on significant resize to maintain density
    if (particles.length === 0) {
        initParticles(canvasRef.value.width, canvasRef.value.height)
    }
}

// Watch for mode change to reset particles
watch(() => props.active, () => {
    if (canvasRef.value) {
        initParticles(canvasRef.value.width, canvasRef.value.height)
    }
})

onMounted(() => {
    if (canvasRef.value) {
        ctx = canvasRef.value.getContext('2d')
        handleResize()
        window.addEventListener('resize', handleResize)
        animate()
    }
})

onUnmounted(() => {
    window.removeEventListener('resize', handleResize)
    cancelAnimationFrame(animationFrameId)
})
</script>

<template>
    <div class="particle-container">
        <canvas ref="canvasRef"></canvas>
    </div>
</template>

<style scoped>
.particle-container {
    position: absolute;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    z-index: -1; /* Behind everything */
    pointer-events: none;
    overflow: hidden;
    background: transparent;
}
</style>
