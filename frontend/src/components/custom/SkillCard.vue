<script setup lang="ts">
interface SkillInfo {
    skillId: number
    skillName: string
    description: string
    hpActivateCondition: string
    exactHpActivatePercent: number
    // Combat
    attackIncrease: number
    defenseIncrease: number
    meleeDamageIncrease: number
    // Movement
    movement: number
    forwardSpeedPercent: number
    agilityPercent: number
    urgentEscape: number
    // Boost
    boostRecoveryPercent: number
    boostCapacityIncrease: number
    // Support
    radarRangeIncrease: number
    spIncreaseSpeedPercent: number
    weaponReloadIncrease: number
    // Damage Reduction
    nearDamageReductionPercent: number
    midDamageReductionPercent: number
    // Target
    appliesToSelf: boolean
}

defineProps<{
    skill?: SkillInfo
    index: number
}>()

// Helper to format stat with sign
const formatStat = (val: number, suffix = '') => {
    if (!val) return null
    return (val > 0 ? '+' : '') + val + suffix
}
</script>

<template>
    <div class="skill-card" v-if="skill">
        <!-- Header -->
        <div class="flex items-center justify-between mb-1">
            <span class="text-neon-cyan font-semibold text-sm">Skill {{ index }}</span>
            <div class="flex items-center gap-2">
                <span class="text-[10px] px-1.5 py-0.5 rounded" :class="skill.appliesToSelf ? 'bg-blue-500/20 text-blue-400' : 'bg-green-500/20 text-green-400'">
                    {{ skill.appliesToSelf ? '🎯 Self' : '👥 Team' }}
                </span>
                <span class="text-[10px] text-gray-600">ID:{{ skill.skillId }}</span>
            </div>
        </div>
        
        <!-- Name & Description -->
        <div class="text-white font-medium text-sm">{{ skill.skillName || 'Unknown' }}</div>
        <div class="text-gray-500 text-[10px] mb-2 line-clamp-1">{{ skill.description || 'No description' }}</div>
        
        <!-- Activation Condition -->
        <div class="text-[10px] mb-2 px-1.5 py-0.5 bg-orange-500/10 rounded border border-orange-500/30" 
             v-if="skill.hpActivateCondition && skill.hpActivateCondition !== 'None'">
            <span class="text-orange-400">⚡ {{ skill.hpActivateCondition }}</span>
            <span v-if="skill.exactHpActivatePercent" class="text-orange-300"> @ {{ skill.exactHpActivatePercent }}% HP</span>
        </div>

        <!-- Stats Grid - Only show non-zero values -->
        <div class="grid grid-cols-4 gap-1 text-[10px]">
            <!-- Combat -->
            <div v-if="skill.attackIncrease" class="stat text-red-400">ATK {{ formatStat(skill.attackIncrease) }}</div>
            <div v-if="skill.defenseIncrease" class="stat text-blue-400">DEF {{ formatStat(skill.defenseIncrease) }}</div>
            <div v-if="skill.meleeDamageIncrease" class="stat text-red-300">Melee {{ formatStat(skill.meleeDamageIncrease) }}</div>
            
            <!-- Movement -->
            <div v-if="skill.forwardSpeedPercent" class="stat text-purple-400">Spd {{ formatStat(skill.forwardSpeedPercent, '%') }}</div>
            <div v-if="skill.agilityPercent" class="stat text-green-400">Agi {{ formatStat(skill.agilityPercent, '%') }}</div>
            <div v-if="skill.movement" class="stat text-purple-300">Move {{ formatStat(skill.movement) }}</div>
            <div v-if="skill.urgentEscape" class="stat text-cyan-400">Escape {{ formatStat(skill.urgentEscape) }}</div>
            
            <!-- Boost -->
            <div v-if="skill.boostRecoveryPercent" class="stat text-yellow-400">Boost Recovery {{ formatStat(skill.boostRecoveryPercent, '%') }}</div>
            <div v-if="skill.boostCapacityIncrease" class="stat text-yellow-300">Boost Capacity {{ formatStat(skill.boostCapacityIncrease) }}</div>
            
            <!-- Support -->
            <div v-if="skill.radarRangeIncrease" class="stat text-teal-400">Radar {{ formatStat(skill.radarRangeIncrease) }}</div>
            <div v-if="skill.spIncreaseSpeedPercent" class="stat text-pink-400">SP {{ skill.spIncreaseSpeedPercent }}%</div>
            <div v-if="skill.weaponReloadIncrease" class="stat text-amber-400">Reload {{ formatStat(skill.weaponReloadIncrease) }}</div>
            
            <!-- Damage Reduction -->
            <div v-if="skill.nearDamageReductionPercent" class="stat text-sky-400">NearDR {{ skill.nearDamageReductionPercent }}%</div>
            <div v-if="skill.midDamageReductionPercent" class="stat text-sky-300">MidDR {{ skill.midDamageReductionPercent }}%</div>
        </div>
    </div>
</template>

<style scoped>
.skill-card {
    @apply bg-black/40 border border-gray-700/50 p-2 rounded transition-all;
}
.skill-card:hover {
    @apply border-neon-cyan/40 bg-neon-cyan/5;
}
.stat {
    @apply bg-black/60 px-1 py-0.5 rounded text-center truncate;
}
</style>
