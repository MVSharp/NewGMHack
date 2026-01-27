<script setup lang="ts">
import { onMounted, computed, watch } from 'vue'
import AppHeader from '@/components/layout/AppHeader.vue'
import AppNav from '@/components/layout/AppNav.vue'
import ParticleBackground from '@/components/layout/ParticleBackground.vue'
import Dashboard from '@/views/Dashboard.vue'
import Features from '@/views/Features.vue'
import Pilot from '@/views/Pilot.vue'
import Lobby from '@/views/Lobby.vue'
import MachineInfo from '@/views/MachineInfo.vue'
import ToastContainer from '@/components/ui/toast/ToastContainer.vue'
import { useTabs, TabNames } from '@/composables/useTabs'
import { useSignalR } from '@/composables/useSignalR'

const { currentTab } = useTabs()
const { start, isGameConnected } = useSignalR()

const currentView = computed(() => {
    switch (currentTab.value) {
        case TabNames.Dashboard: return Dashboard
        case TabNames.Features: return Features
        case TabNames.Pilot: return Pilot
        case TabNames.Lobby: return Lobby
        case TabNames.MachineInfo: return MachineInfo
        default: return Dashboard
    }
})

// Trans-Am Mode Logic
const isTransAm = computed(() => isGameConnected.value)

watch(isTransAm, (val) => {
    if (val) {
        document.body.classList.add('trans-am')
    } else {
        document.body.classList.remove('trans-am')
    }
}, { immediate: true })

onMounted(() => {
    start()
})
</script>

<template>
    <div class="dashboard-wrapper">
        <ParticleBackground :active="isTransAm" />
        
        <!-- Header -->
        <AppHeader />
        
        <!-- Navigation -->
        <AppNav />
        
        <!-- Main View -->
        <main class="flex-1 overflow-hidden z-10 relative">
            <Transition name="fade" mode="out-in">
                <component :is="currentView" :key="currentTab" />
            </Transition>
        </main>

        <!-- Toast Notifications -->
        <ToastContainer />
    </div>
</template>

<style>
/* Matches old frontend layout exactly */
.dashboard-wrapper {
    display: grid;
    grid-template-rows: auto auto 1fr;
    gap: 15px;
    height: 100vh;
    padding: 15px;
    box-sizing: border-box;
    /* Make background semi-transparent to show particles */
    background: linear-gradient(135deg, rgba(5, 5, 5, 0.85) 0%, rgba(16, 16, 21, 0.9) 100%);
    position: relative;
    overflow: hidden;
}

/* Tab Transition */
.fade-enter-active,
.fade-leave-active {
    transition: opacity 0.3s ease, transform 0.3s ease;
}

.fade-enter-from {
    opacity: 0;
    transform: translateY(5px);
}

.fade-leave-to {
    opacity: 0;
    transform: translateY(-5px);
}
</style>
