<script setup lang="ts">
interface WeaponInfo {
    weaponId: number
    weaponName: string
    weaponType: string
    // Core
    weaponDamage: number
    weaponRange: number
    // Ammo
    ammoCount: number
    ammoRecoverySpeed: number
    coolTime: number
    // Precision
    missileSpeed: number
    aimSpeed: number
    // Impact
    knockbackEffect: number
    knockdownPerHit: number
    knockdownThreshold: number
    // Pierce
    hasPierce: boolean
    pierceValue: number
    // Movement
    allowUseWhenMove: boolean
    // AoE
    collisionWidth: number
    collisionHeight: number
    splashRadius: number
    splashCoreRadius: number
}

defineProps<{
    weapon?: WeaponInfo
    index: number
    isSpecial?: boolean
}>()

const typeColor = (type: string) => {
    switch (type) {
        case 'Near': return 'bg-red-500/20 text-red-400 border-red-500/30'
        case 'Mid': return 'bg-yellow-500/20 text-yellow-400 border-yellow-500/30'
        case 'Far': return 'bg-blue-500/20 text-blue-400 border-blue-500/30'
        default: return 'bg-gray-500/20 text-gray-400 border-gray-500/30'
    }
}
</script>

<template>
    <div class="weapon-card" :class="{ 'special-weapon': isSpecial }" v-if="weapon">
        <!-- Header -->
        <div class="flex items-center justify-between mb-1">
            <span class="font-semibold text-sm" :class="isSpecial ? 'text-orange-400' : 'text-neon-blue'">
                {{ isSpecial ? '⚡ Special' : `Weapon ${index}` }}
            </span>
            <div class="flex items-center gap-2">
                <span :class="['text-[10px] px-1.5 py-0.5 rounded border', typeColor(weapon.weaponType)]">
                    {{ weapon.weaponType || '?' }}
                </span>
                <span class="text-[10px] text-gray-600">ID:{{ weapon.weaponId }}</span>
            </div>
        </div>
        
        <!-- Name -->
        <div class="text-white font-medium text-sm mb-2">{{ weapon.weaponName || 'Unknown' }}</div>
        
        <!-- Core Stats Row -->
        <div class="flex gap-2 mb-2">
            <div class="core-stat">
                <span class="text-gray-500">DMG</span>
                <span class="text-red-400 font-bold">{{ weapon.weaponDamage }}</span>
            </div>
            <div class="core-stat">
                <span class="text-gray-500">RNG</span>
                <span class="text-blue-400">{{ weapon.weaponRange }}</span>
            </div>
            <div class="core-stat" v-if="weapon.ammoCount">
                <span class="text-gray-500">AMMO</span>
                <span class="text-white">{{ weapon.ammoCount }}</span>
            </div>
            <div class="core-stat" v-if="weapon.coolTime">
                <span class="text-gray-500">CD</span>
                <span class="text-white">{{ weapon.coolTime }}</span>
            </div>
        </div>

        <!-- Secondary Stats Grid -->
        <div class="grid grid-cols-4 gap-1 text-[10px]">
            <!-- Ammo & Speed -->
            <div v-if="weapon.ammoRecoverySpeed" class="stat">Reload: {{ weapon.ammoRecoverySpeed }}</div>
            <div v-if="weapon.missileSpeed" class="stat">MissSpd: {{ weapon.missileSpeed }}</div>
            <div v-if="weapon.aimSpeed" class="stat">AimSpd: {{ weapon.aimSpeed }}</div>
            
            <!-- Impact -->
            <div v-if="weapon.knockbackEffect" class="stat">Knockback: {{ weapon.knockbackEffect }}</div>
            <div v-if="weapon.knockdownPerHit || weapon.knockdownThreshold" class="stat text-yellow-400">
                KD: {{ weapon.knockdownPerHit }}/{{ weapon.knockdownThreshold }}
            </div>
            
            <!-- Pierce -->
            <div v-if="weapon.hasPierce" class="stat text-purple-400">Pierce: {{ weapon.pierceValue || '✓' }}</div>
            
            <!-- AoE -->
            <div v-if="weapon.splashRadius" class="stat text-orange-400">Splash: {{ weapon.splashRadius }}</div>
            <div v-if="weapon.splashCoreRadius" class="stat text-orange-300">Core: {{ weapon.splashCoreRadius }}</div>
            <div v-if="weapon.collisionWidth && weapon.collisionHeight" class="stat">
                Size: {{ weapon.collisionWidth }}×{{ weapon.collisionHeight }}
            </div>
        </div>

        <!-- Tags -->
        <div class="flex gap-1 mt-2" v-if="weapon.allowUseWhenMove || weapon.hasPierce">
            <span v-if="weapon.allowUseWhenMove" class="tag bg-green-500/20 text-green-400 border-green-500/30">Move Fire</span>
            <span v-if="weapon.hasPierce && !weapon.pierceValue" class="tag bg-purple-500/20 text-purple-400 border-purple-500/30">Pierce</span>
        </div>
    </div>
</template>

<style scoped>
.weapon-card {
    @apply bg-black/40 border border-gray-700/50 p-2 rounded transition-all;
}
.weapon-card:hover {
    @apply border-neon-blue/40 bg-neon-blue/5;
}
.weapon-card.special-weapon {
    @apply border-orange-500/30;
}
.weapon-card.special-weapon:hover {
    @apply border-orange-400/50 bg-orange-500/5;
}
.core-stat {
    @apply flex flex-col items-center bg-black/50 px-2 py-1 rounded text-[10px];
}
.stat {
    @apply bg-black/50 px-1 py-0.5 rounded text-center text-gray-400 truncate;
}
.tag {
    @apply text-[10px] px-1.5 py-0.5 rounded border;
}
</style>
