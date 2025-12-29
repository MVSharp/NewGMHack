<script setup lang="ts">
import { computed } from 'vue'
import { useSignalR } from '@/composables/useSignalR'
import { useI18n } from '@/composables/useI18n'

const { pilotInfo } = useSignalR()
const { t } = useI18n()

const pilotId = computed(() => pilotInfo.value?.personId ?? '--')
const condomId = computed(() => pilotInfo.value?.condomId ?? '--')
const condomName = computed(() => pilotInfo.value?.condomName ?? '--')
const slot = computed(() => pilotInfo.value?.slot ?? '--')
const weapon1 = computed(() => pilotInfo.value?.weapon1 ?? '--')
const weapon2 = computed(() => pilotInfo.value?.weapon2 ?? '--')
const weapon3 = computed(() => pilotInfo.value?.weapon3 ?? '--')
const coords = computed(() => {
    if (!pilotInfo.value) return 'X:-- Y:-- Z:--'
    const x = pilotInfo.value.x?.toFixed(2) ?? '--'
    const y = pilotInfo.value.y?.toFixed(2) ?? '--'
    const z = pilotInfo.value.z?.toFixed(2) ?? '--'
    return `X:${x} Y:${y} Z:${z}`
})
</script>

<template>
    <div class="h-full flex flex-col">
        <div class="panel-header text-neon-cyan font-rajdhani text-xl border-b border-neon-cyan/30 pb-3 mb-4 uppercase">
            {{ t('pilot_telemetry') }}
        </div>
        
        <!-- Info Card -->
        <div class="info-card mx-auto max-w-3xl w-full">
            <table class="data-table w-full">
                <tbody>
                    <tr>
                        <th class="w-1/3">{{ t('pilot_id') }}</th>
                        <td class="text-neon-cyan text-2xl font-bold glow-cyan">{{ pilotId }}</td>
                    </tr>
                    <tr>
                        <th>{{ t('condom_id') }}</th>
                        <td class="text-gundam-gold glow-gold">{{ condomId }}</td>
                    </tr>
                    <tr>
                        <th>{{ t('condom_name') }}</th>
                        <td class="text-white">{{ condomName }}</td>
                    </tr>
                    <tr>
                        <th>{{ t('slot') }}</th>
                        <td>{{ slot }}</td>
                    </tr>
                    <tr>
                        <th>{{ t('loadout') }}</th>
                        <td>
                            <div class="mb-1 text-gray-400">{{ t('weapon') }} 1: <span class="text-white">{{ weapon1 }}</span></div>
                            <div class="mb-1 text-gray-400">{{ t('weapon') }} 2: <span class="text-white">{{ weapon2 }}</span></div>
                            <div class="text-gray-400">{{ t('weapon') }} 3: <span class="text-white">{{ weapon3 }}</span></div>
                        </td>
                    </tr>
                    <tr>
                        <th>{{ t('coordinates') }}</th>
                        <td class="font-mono">{{ coords }}</td>
                    </tr>
                </tbody>
            </table>
        </div>
    </div>
</template>

<style scoped>
.info-card {
    @apply bg-bg-panel border border-neon-blue p-5 shadow-[0_0_20px_rgba(0,0,0,0.5)] transition-all duration-300;
}

.info-card:hover {
    @apply border-neon-cyan shadow-[0_0_30px_rgba(102,252,241,0.1)];
}

.data-table {
    @apply border-collapse font-rajdhani;
}

.data-table th,
.data-table td {
    @apply border-b border-white/10 p-4 text-left text-gray-400 transition-all duration-200;
}

.data-table th {
    @apply text-neon-cyan text-lg border-b-2 border-neon-blue;
}

.data-table tr:hover td {
    @apply bg-neon-cyan/5 text-white;
}

.glow-cyan { text-shadow: 0 0 10px rgba(102, 252, 241, 0.4); }
.glow-gold { text-shadow: 0 0 10px rgba(255, 215, 0, 0.4); }
</style>
