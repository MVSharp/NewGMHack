<script setup lang="ts">
interface SkillInfo {
    SkillId: number
    SkillName: string
    Description: string
    HpActivateCondition: string
    ExactHpActivatePercent: number
    // Combat
    AttackIncrease: number
    DefenseIncrease: number
    MeleeDamageIncrease: number
    // Movement
    Movement: number
    ForwardSpeedPercent: number
    AgilityPercent: number
    UrgentEscape: number
    // Boost
    BoostRecoveryPercent: number
    BoostCapacityIncrease: number
    // Support
    RadarRangeIncrease: number
    SpIncreaseSpeedPercent: number
    WeaponReloadIncrease: number
    // Damage Reduction
    NearDamageReductionPercent: number
    MidDamageReductionPercent: number
    // Target
    AppliesToSelf: boolean
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
                <span class="text-[10px] px-1.5 py-0.5 rounded" :class="skill.AppliesToSelf ? 'bg-blue-500/20 text-blue-400' : 'bg-green-500/20 text-green-400'">
                    {{ skill.AppliesToSelf ? '🎯 Self' : '👥 Team' }}
                </span>
                <span class="text-[10px] text-gray-600">ID:{{ skill.SkillId }}</span>
            </div>
        </div>
        
        <!-- Name & Description -->
        <div class="text-white font-medium text-sm">{{ skill.SkillName || 'Unknown' }}</div>
        <div class="text-gray-500 text-[10px] mb-2 line-clamp-1">{{ skill.Description || 'No description' }}</div>
        
        <!-- Activation Condition -->
        <div class="text-[10px] mb-2 px-1.5 py-0.5 bg-orange-500/10 rounded border border-orange-500/30" 
             v-if="skill.HpActivateCondition && skill.HpActivateCondition !== 'None'">
            <span class="text-orange-400">⚡ {{ skill.HpActivateCondition }}</span>
            <span v-if="skill.ExactHpActivatePercent" class="text-orange-300"> @ {{ skill.ExactHpActivatePercent }}% HP</span>
        </div>

        <!-- Stats Grid - Only show non-zero values -->
        <div class="grid grid-cols-4 gap-1 text-[10px]">
            <!-- Combat -->
            <div v-if="skill.AttackIncrease" class="stat text-red-400">ATK {{ formatStat(skill.AttackIncrease) }}</div>
            <div v-if="skill.DefenseIncrease" class="stat text-blue-400">DEF {{ formatStat(skill.DefenseIncrease) }}</div>
            <div v-if="skill.MeleeDamageIncrease" class="stat text-red-300">Melee {{ formatStat(skill.MeleeDamageIncrease) }}</div>
            
            <!-- Movement -->
            <div v-if="skill.ForwardSpeedPercent" class="stat text-purple-400">Spd {{ formatStat(skill.ForwardSpeedPercent, '%') }}</div>
            <div v-if="skill.AgilityPercent" class="stat text-green-400">Agi {{ formatStat(skill.AgilityPercent, '%') }}</div>
            <div v-if="skill.Movement" class="stat text-purple-300">Move {{ formatStat(skill.Movement) }}</div>
            <div v-if="skill.UrgentEscape" class="stat text-cyan-400">Escape {{ formatStat(skill.UrgentEscape) }}</div>
            
            <!-- Boost -->
            <div v-if="skill.BoostRecoveryPercent" class="stat text-yellow-400">Boost Recovery {{ formatStat(skill.BoostRecoveryPercent, '%') }}</div>
            <div v-if="skill.BoostCapacityIncrease" class="stat text-yellow-300">Boost Capacity {{ formatStat(skill.BoostCapacityIncrease) }}</div>
            
            <!-- Support -->
            <div v-if="skill.RadarRangeIncrease" class="stat text-teal-400">Radar {{ formatStat(skill.RadarRangeIncrease) }}</div>
            <div v-if="skill.SpIncreaseSpeedPercent" class="stat text-pink-400">SP {{ skill.SpIncreaseSpeedPercent }}%</div>
            <div v-if="skill.WeaponReloadIncrease" class="stat text-amber-400">Reload {{ formatStat(skill.WeaponReloadIncrease) }}</div>
            
            <!-- Damage Reduction -->
            <div v-if="skill.NearDamageReductionPercent" class="stat text-sky-400">NearDR {{ skill.NearDamageReductionPercent }}%</div>
            <div v-if="skill.MidDamageReductionPercent" class="stat text-sky-300">MidDR {{ skill.MidDamageReductionPercent }}%</div>
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
