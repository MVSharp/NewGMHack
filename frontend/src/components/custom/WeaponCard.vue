<script setup lang="ts">
interface WeaponInfo {
    WeaponId: number
    WeaponName: string
    WeaponType: string
    // Core
    WeaponDamage: number
    WeaponRange: number
    // Ammo
    AmmoCount: number
    AmmoRecoverySpeed: number
    CoolTime: number
    // Precision
    MissileSpeed: number
    AimSpeed: number
    // Impact
    KnockbackEffect: number
    KnockdownPerHit: number
    KnockdownThreshold: number
    // Pierce
    HasPierce: boolean
    PierceValue: number
    // Movement
    AllowUseWhenMove: boolean
    // AoE
    CollisionWidth: number
    CollisionHeight: number
    SplashRadius: number
    SplashCoreRadius: number
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
                <span :class="['text-[10px] px-1.5 py-0.5 rounded border', typeColor(weapon.WeaponType)]">
                    {{ weapon.WeaponType || '?' }}
                </span>
                <span class="text-[10px] text-gray-600">ID:{{ weapon.WeaponId }}</span>
            </div>
        </div>
        
        <!-- Name -->
        <div class="text-white font-medium text-sm mb-2">{{ weapon.WeaponName || 'Unknown' }}</div>
        
        <!-- Core Stats Row -->
        <div class="flex gap-2 mb-2">
            <div class="core-stat">
                <span class="text-gray-500">DMG</span>
                <span class="text-red-400 font-bold">{{ weapon.WeaponDamage }}</span>
            </div>
            <div class="core-stat">
                <span class="text-gray-500">RNG</span>
                <span class="text-blue-400">{{ weapon.WeaponRange }}</span>
            </div>
            <div class="core-stat" v-if="weapon.AmmoCount">
                <span class="text-gray-500">AMMO</span>
                <span class="text-white">{{ weapon.AmmoCount }}</span>
            </div>
            <div class="core-stat" v-if="weapon.CoolTime">
                <span class="text-gray-500">CD</span>
                <span class="text-white">{{ weapon.CoolTime }}</span>
            </div>
        </div>

        <!-- Secondary Stats Grid -->
        <div class="grid grid-cols-4 gap-1 text-[10px]">
            <!-- Ammo & Speed -->
            <div v-if="weapon.AmmoRecoverySpeed" class="stat">Reload: {{ weapon.AmmoRecoverySpeed }}</div>
            <div v-if="weapon.MissileSpeed" class="stat">MissSpd: {{ weapon.MissileSpeed }}</div>
            <div v-if="weapon.AimSpeed" class="stat">AimSpd: {{ weapon.AimSpeed }}</div>
            
            <!-- Impact -->
            <div v-if="weapon.KnockbackEffect" class="stat">Knockback: {{ weapon.KnockbackEffect }}</div>
            <div v-if="weapon.KnockdownPerHit || weapon.KnockdownThreshold" class="stat text-yellow-400">
                KD: {{ weapon.KnockdownPerHit }}/{{ weapon.KnockdownThreshold }}
            </div>
            
            <!-- Pierce -->
            <div v-if="weapon.HasPierce" class="stat text-purple-400">Pierce: {{ weapon.PierceValue || '✓' }}</div>
            
            <!-- AoE -->
            <div v-if="weapon.SplashRadius" class="stat text-orange-400">Splash: {{ weapon.SplashRadius }}</div>
            <div v-if="weapon.SplashCoreRadius" class="stat text-orange-300">Core: {{ weapon.SplashCoreRadius }}</div>
            <div v-if="weapon.CollisionWidth && weapon.CollisionHeight" class="stat">
                Size: {{ weapon.CollisionWidth }}×{{ weapon.CollisionHeight }}
            </div>
        </div>

        <!-- Tags -->
        <div class="flex gap-1 mt-2" v-if="weapon.AllowUseWhenMove || weapon.HasPierce">
            <span v-if="weapon.AllowUseWhenMove" class="tag bg-green-500/20 text-green-400 border-green-500/30">Move Fire</span>
            <span v-if="weapon.HasPierce && !weapon.PierceValue" class="tag bg-purple-500/20 text-purple-400 border-purple-500/30">Pierce</span>
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
