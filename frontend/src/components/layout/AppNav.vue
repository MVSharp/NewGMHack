<script setup lang="ts">
import { useTabs, TabNames } from '@/composables/useTabs'
import { useI18n } from '@/composables/useI18n'

const { currentTab, setTab } = useTabs()
const { t } = useI18n()

const tabs = [
    { name: TabNames.Dashboard, labelKey: 'dashboard' },
    { name: TabNames.Features, labelKey: 'features' },
    { name: TabNames.Pilot, labelKey: 'pilot' },
    { name: TabNames.Lobby, labelKey: 'lobby' }
]
</script>

<template>
    <nav class="system-nav flex justify-center gap-5 border-b-2 border-neon-cyan/30 pb-4 bg-black/30">
        <button
            v-for="tab in tabs"
            :key="tab.name"
            :class="['nav-btn', { active: currentTab === tab.name }]"
            @click="setTab(tab.name)"
        >
            {{ t(tab.labelKey) }}
        </button>
    </nav>
</template>

<style scoped>
/* Navigation Buttons */
.nav-btn {
    @apply bg-transparent border border-neon-blue text-neon-blue px-8 py-2 font-rajdhani text-lg uppercase tracking-widest relative overflow-hidden transition-all duration-300 font-semibold;
}

.nav-btn:hover {
    @apply bg-neon-cyan/10 text-neon-cyan border-neon-cyan shadow-[0_0_15px_rgba(102,252,241,0.2)];
    text-shadow: 0 0 8px var(--neon-cyan);
}

.nav-btn.active {
    @apply bg-neon-cyan text-bg-dark border-neon-cyan shadow-[0_0_20px_rgba(102,252,241,0.4)] font-bold;
}
</style>
