<script setup lang="ts">
import { useSignalR } from '@/composables/useSignalR'
import { useI18n } from '@/composables/useI18n'

const { roommates } = useSignalR()
const { t } = useI18n()
</script>

<template>
    <div class="h-full flex flex-col">
        <div class="panel-header text-neon-cyan font-rajdhani text-xl border-b border-neon-cyan/30 pb-3 mb-4 uppercase">
            {{ t('lobby_recon') }}
        </div>
        
        <!-- Lobby Panel -->
        <div class="panel flex-1 min-h-0 flex flex-col">
            <div class="flex-1 overflow-y-auto">
                <table class="data-table w-full">
                    <thead>
                        <tr>
                            <th>{{ t('pilot_name') }}</th>
                            <th>{{ t('pilot_id') }}</th>
                            <th>{{ t('status') }}</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr v-if="roommates.length === 0">
                            <td colspan="3" class="text-center text-gray-600 py-8">
                                {{ t('scanning_lobby') }}
                            </td>
                        </tr>
                        <tr v-for="player in roommates" :key="player.id">
                            <td class="text-white font-bold">{{ player.name }}</td>
                            <td class="font-mono text-neon-cyan">{{ player.id }}</td>
                            <td><span class="text-neon-blue">{{ t('connected') }}</span></td>
                        </tr>
                    </tbody>
                </table>
            </div>
        </div>
    </div>
</template>

<style scoped>
.panel {
    @apply bg-bg-panel border border-white/5 p-4 transition-all duration-300;
}

.panel:hover {
    @apply border-white/10 shadow-lg;
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
</style>
