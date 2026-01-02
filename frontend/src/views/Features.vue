<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { api, type Feature } from '@/services/api'
import { useI18n } from '@/composables/useI18n'

const { t, currentLang } = useI18n()

const features = ref<Feature[]>([])
const loading = ref(true)
const togglingId = ref<string | null>(null)

async function loadFeatures() {
    loading.value = true
    try {
        features.value = await api.getFeatures()
    } catch (e) {
        console.error('Load Features Failed:', e)
    } finally {
        loading.value = false
    }
}

// Get display name based on current language
function getDisplayName(feature: Feature): string {
    switch (currentLang.value) {
        case 'zh-CN': return feature.DisplayNameCn || feature.DisplayNameEn || feature.Name
        case 'zh-TW': return feature.DisplayNameTw || feature.DisplayNameEn || feature.Name
        default: return feature.DisplayNameEn || feature.Name
    }
}

async function toggleFeature(feature: Feature, event: Event) {
    event.preventDefault()
    event.stopPropagation()
    
    if (togglingId.value === feature.Id) return
    
    const newState = !feature.Enabled
    togglingId.value = feature.Id
    
    try {
        feature.Enabled = newState
        await api.updateFeature(feature.Id, newState)
        
        const updatedFeatures = await api.getFeatures()
        updatedFeatures.forEach(updated => {
            const existing = features.value.find(f => f.Id === updated.Id)
            if (existing) {
                existing.Enabled = updated.Enabled
            }
        })
    } catch (e) {
        console.error('Toggle Failed:', e)
        feature.Enabled = !newState
    } finally {
        togglingId.value = null
    }
}

function getStatusText(feature: Feature): string {
    if (togglingId.value === feature.Id) return t('updating')
    return feature.Enabled ? t('active') : t('offline')
}

onMounted(() => {
    loadFeatures()
})
</script>

<template>
    <div class="h-full flex flex-col">
        <div class="panel-header flex justify-between items-center mb-4">
            <span class="text-neon-cyan font-rajdhani text-xl uppercase">{{ t('system_overrides') }}</span>
            <div class="flex items-center gap-4">
                <span class="text-gray-500 text-sm">
                    {{ t('active') }}: <span class="text-neon-cyan">{{ features.filter(f => f.Enabled).length }}</span>
                </span>
                <button 
                    @click="loadFeatures"
                    class="text-neon-blue hover:text-neon-cyan text-sm font-rajdhani transition-colors duration-200"
                >
                    {{ t('refresh') }}
                </button>
            </div>
        </div>
        
        <div v-if="loading" class="flex-1 flex items-center justify-center">
            <div class="text-gray-500 font-rajdhani animate-pulse">{{ t('loading_modules') }}</div>
        </div>
        
        <div v-else class="features-grid flex-1 overflow-y-auto">
            <div 
                v-for="feature in features" 
                :key="feature.Id"
                class="feature-card"
            >
                <div class="flex-1">
                    <!-- Feature Name - Uses language-specific display name -->
                    <div class="feature-name" :title="feature.Description">
                        {{ getDisplayName(feature) }}
                    </div>
                    <div class="feature-desc text-xs text-gray-500 mt-1" v-if="feature.Description">
                        {{ feature.Description }}
                    </div>
                    <div class="feature-status text-xs mt-2" :class="feature.Enabled ? 'text-neon-cyan' : 'text-gray-600'">
                        {{ getStatusText(feature) }}
                    </div>
                </div>
                <div 
                    class="toggle-switch"
                    :class="{ 
                        active: feature.Enabled,
                        updating: togglingId === feature.Id
                    }"
                    @click="toggleFeature(feature, $event)"
                >
                    <div class="toggle-knob"></div>
                </div>
            </div>
        </div>
    </div>
</template>

<style scoped>
.panel-header {
    @apply border-b border-neon-cyan-30 pb-3;
}

.features-grid {
    @apply grid gap-5 p-5;
    grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
}

.feature-card {
    @apply bg-bg-panel border border-neon-blue p-5 flex items-center justify-between transition-all duration-300;
}

.feature-card:hover {
    @apply border-neon-cyan shadow-[0_0_15px_rgba(102,252,241,0.15)];
}

.feature-name {
    @apply text-lg text-white font-rajdhani font-semibold;
}

.toggle-switch {
    @apply w-12 h-6 bg-gray-800 rounded-full relative cursor-pointer transition-all duration-300 border border-gray-600 flex-shrink-0 ml-4;
}

.toggle-switch.active {
    @apply bg-neon-cyan border-neon-cyan shadow-[0_0_10px_var(--neon-cyan)];
}

.toggle-switch.updating {
    @apply opacity-50 cursor-wait;
}

.toggle-knob {
    @apply w-5 h-5 bg-white rounded-full absolute top-0.5 left-0.5 transition-transform duration-300;
}

.toggle-switch.active .toggle-knob {
    @apply translate-x-6;
}
</style>
