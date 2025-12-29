<script setup lang="ts">
import { computed } from 'vue'
import { useSignalR } from '@/composables/useSignalR'
import { useI18n } from '@/composables/useI18n'

const { 
    isGameConnected, 
    isInjecting,
    dateRange,
    inject,
    deattach
} = useSignalR()

const { t, setLanguage, languages, currentLang } = useI18n()

// Status dot class
const statusClass = computed(() => {
    if (isGameConnected.value) return 'connected'
    if (isInjecting.value) return 'waiting'
    return ''
})

const statusText = computed(() => {
    if (isGameConnected.value) return t('connected')
    if (isInjecting.value) return t('waiting')
    return t('disconnected')
})

async function handleInject() {
    if (isGameConnected.value || isInjecting.value) return
    await inject()
}

async function handleDeattach() {
    if (!isGameConnected.value) return
    await deattach()
}
</script>

<template>
    <header class="header-container flex justify-between items-center p-3 px-5 bg-[rgba(31,40,51,0.5)] border-b-2 border-neon-cyan rounded transition-all duration-300 hover:bg-[rgba(41,50,61,0.6)]">
        <!-- Logo -->
        <div class="flex items-center gap-4">
            <img src="/condom.png" class="h-12 transition-transform duration-300 hover:scale-110" alt="Logo" />
            <div class="logo-text text-2xl font-bold text-neon-cyan font-rajdhani tracking-widest uppercase">
                {{ t('title') }}
            </div>
        </div>
        
        <!-- Right Controls -->
        <div class="flex items-center gap-5">
            <!-- System Controls (Status + Buttons) -->
            <div class="flex items-center gap-4">
                <!-- Status Dot -->
                <div 
                    :class="['status-dot', statusClass]"
                    :title="statusText"
                ></div>
                
                <!-- Inject Button -->
                <button 
                    class="btn-action"
                    :class="{ 
                        'glow-animate': !isGameConnected && !isInjecting,
                        'opacity-30 cursor-not-allowed': isGameConnected 
                    }"
                    :disabled="isGameConnected"
                    @click="handleInject"
                >
                    {{ isInjecting ? t('injecting') : t('inject') }}
                </button>
                
                <!-- Deattach Button -->
                <button 
                    class="btn-action"
                    :disabled="!isGameConnected"
                    :class="{ 'opacity-30 cursor-not-allowed': !isGameConnected }"
                    @click="handleDeattach"
                >
                    {{ t('deattach') }}
                </button>
            </div>

            <!-- Language Switcher with Emoji Flags -->
            <div class="flex items-center gap-2 border-l border-gray-700 pl-5">
                <button 
                    v-for="lang in languages" 
                    :key="lang.code"
                    @click="setLanguage(lang.code)"
                    :class="[
                        'lang-btn px-2 py-1 rounded transition-all duration-200',
                        currentLang === lang.code 
                            ? 'bg-neon-cyan/20 text-neon-cyan border border-neon-cyan' 
                            : 'text-gray-500 hover:text-neon-cyan hover:bg-white/5'
                    ]"
                    :title="lang.label"
                >
                    {{ lang.emoji }}
                </button>
            </div>

            <!-- Date Range -->
            <span class="font-rajdhani text-gundam-gold border border-neon-blue px-3 py-1 text-sm transition-all duration-300 hover:border-gundam-gold hover:shadow-[0_0_10px_rgba(255,215,0,0.3)]">
                {{ dateRange }}
            </span>

            <!-- Sync Status -->
            <span class="text-xs text-gray-500">
                {{ statusText }}
            </span>
        </div>
    </header>
</template>

<style scoped>
/* Logo Breathing Animation */
.logo-text {
    animation: breathe 3s ease-in-out infinite;
}

@keyframes breathe {
    0%, 100% { 
        text-shadow: 0 0 5px rgba(102, 252, 241, 0.3);
        opacity: 0.9;
    }
    50% { 
        text-shadow: 0 0 20px rgba(102, 252, 241, 0.8), 0 0 30px rgba(102, 252, 241, 0.4);
        opacity: 1;
    }
}

/* Status Dot */
.status-dot {
    @apply w-3 h-3 rounded-full bg-beam-pink shadow-[0_0_5px_var(--beam-pink)] transition-all;
}

.status-dot.connected {
    @apply bg-green-400 shadow-[0_0_10px_#00ffaa];
}

.status-dot.waiting {
    @apply bg-orange-400 shadow-[0_0_10px_#ffaa00];
    animation: blink 1s infinite;
}

/* Action Buttons */
.btn-action {
    @apply bg-black/50 border border-neon-blue text-neon-blue px-5 py-1 font-rajdhani font-bold tracking-wider transition-all duration-300;
}

.btn-action:hover:not(:disabled) {
    @apply bg-neon-cyan text-black shadow-[0_0_10px_var(--neon-cyan)];
}

.btn-action:disabled {
    @apply opacity-30 cursor-not-allowed border-gray-600 text-gray-600;
    animation: none !important;
}

/* Glow Animation for Inject button (breathing effect) */
.glow-animate {
    @apply border-beam-pink text-beam-pink;
    animation: glowPulse 2s infinite;
}

@keyframes glowPulse {
    0%, 100% { 
        box-shadow: 0 0 5px var(--beam-pink); 
        opacity: 0.9;
    }
    50% { 
        box-shadow: 0 0 20px var(--beam-pink), 0 0 30px rgba(255, 0, 85, 0.3); 
        opacity: 1;
    }
}

@keyframes blink {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.5; }
}

/* Language Button */
.lang-btn {
    font-size: 1.2rem;
}
</style>
