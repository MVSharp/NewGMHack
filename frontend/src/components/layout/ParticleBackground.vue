<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
    active: boolean // true = Trans-Am Mode
}>()

const options = computed(() => {
    const isTransAm = props.active
    
    return {
        background: {
            color: {
                value: "transparent",
            },
        },
        fpsLimit: 30, // Hardware-friendly limit
        interactivity: {
            events: {
                onClick: { enable: false, mode: "push" },
                onHover: { enable: false, mode: "repulse" }, // Disabled interaction for background
                resize: true,
            },
        },
        particles: isTransAm ? {
            // TRANS-AM CONFIG (Speed Lines)
            color: { value: "#66fcf1" }, // Cyan (requested)
            links: {
                enable: false, // No connections in speed mode
            },
            move: {
                direction: "top-right", // Speed lines direction
                enable: true,
                outModes: { default: "out" },
                random: false,
                speed: 6, // Fast
                straight: true,
            },
            number: {
                density: { enable: false, area: 800 }, // Disable density to prevent high counts on large screens
                value: 50, // Reduced from 80
            },
            opacity: {
                value: 0.5,
            },
            shape: { type: "circle" }, // Dots look like lines when moving fast enough or with trail
            size: {
                value: { min: 1, max: 3 },
            },
        } : {
            // IDLE CONFIG (Network)
            color: { value: "#66fcf1" },
            links: {
                color: "#66fcf1",
                distance: 150,
                enable: true,
                opacity: 0.5,
                width: 1,
            },
            move: {
                direction: "none",
                enable: true,
                outModes: { default: "out" },
                random: false,
                speed: 0.5, // Slow drift
                straight: false,
            },
            number: {
                density: { enable: false, area: 800 }, // Disable density
                value: 30, // Aggressively reduced from 50
            },
            opacity: {
                value: 0.5,
            },
            shape: { type: "circle" },
            size: {
                value: { min: 1, max: 3 },
            },
        },
        detectRetina: false, // CRITICAL: Disabling Retina detection saves massive GPU resources on 4K/standard screens
    }
})
</script>

<template>
    <div class="particle-container">
        <vue-particles id="tsparticles" :options="options" />
    </div>
</template>

<style scoped>
.particle-container {
    position: absolute;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    z-index: -1;
    pointer-events: none;
}
</style>
