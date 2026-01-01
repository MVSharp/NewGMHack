<script setup lang="ts">
import { ref, computed } from 'vue'
import { useSignalR } from '@/composables/useSignalR'
import HexagonChart from '@/components/custom/HexagonChart.vue'
import ColorStrip from '@/components/custom/ColorStrip.vue'
import OcPartsDisplay from '@/components/custom/OcPartsDisplay.vue'
import SkillCard from '@/components/custom/SkillCard.vue'
import WeaponCard from '@/components/custom/WeaponCard.vue'

const { machineInfo } = useSignalR()

// Layer visibility toggles
const showBase = ref(true)
const showOcBase = ref(true)
const showOcBonus = ref(true)

// Collapsed sections
const skillsCollapsed = ref(false)
const weaponsCollapsed = ref(false)

// Transform mode
const showTransform = ref(false)

// Current machine data (normal or transformed)
const currentMachine = computed(() => {
    if (showTransform.value && machineBaseInfo.value?.TransformedMachine) {
        return machineBaseInfo.value.TransformedMachine
    }
    return machineBaseInfo.value
})

// Computed hex values from MachineBaseInfo + OC points
const hexStats = computed(() => {
    const base = currentMachine.value
    const ocBase = machineModel.value?.OcBaseBonusPoints
    const ocBonus = machineModel.value?.OcBonusExtraPoints
    const specialAtk = base?.SpecialAttack

    return {
        base: {
            attack: base?.Attack ?? 0,
            agility: (base?.Agility ?? 0) * 2,
            hp: (base?.HP ?? 0) * 0.01,
            defense: base?.Defense ?? 0,
            sp: (specialAtk?.WeaponDamage ?? 0) * 0.01,
            speed: (base?.ForwardSpeed ?? 0) * 2
        },
        ocBase: {
            attack: ocBase?.Attack ?? 0,
            agility: ocBase?.Agility ?? 0,
            hp: ocBase?.Hp ?? 0,
            defense: ocBase?.Defense ?? 0,
            sp: ocBase?.Special ?? 0,
            speed: ocBase?.Speed ?? 0
        },
        ocBonus: {
            attack: ocBonus?.Attack ?? 0,
            agility: ocBonus?.Agility ?? 0,
            hp: ocBonus?.Hp ?? 0,
            defense: ocBonus?.Defense ?? 0,
            sp: ocBonus?.Special ?? 0,
            speed: ocBonus?.Speed ?? 0
        }
    }
})

const machineModel = computed(() => machineInfo.value?.MachineModel)
const machineBaseInfo = computed(() => machineInfo.value?.MachineBaseInfo)
const hasTransform = computed(() => machineBaseInfo.value?.HasTransform ?? false)

const skills = computed(() => [
    currentMachine.value?.Skill1Info,
    currentMachine.value?.Skill2Info
].filter(Boolean))

const weapons = computed(() => [
    currentMachine.value?.Weapon1Info,
    currentMachine.value?.Weapon2Info,
    currentMachine.value?.Weapon3Info,
    currentMachine.value?.SpecialAttack
].filter(Boolean))
</script>

<template>
    <div class="h-full flex flex-col overflow-y-auto p-4 gap-4">
        <!-- Header with Transform Toggle -->
        <div class="flex items-center justify-between border-b border-neon-cyan/30 pb-3">
            <div class="text-neon-cyan font-rajdhani text-xl uppercase">Machine Info</div>
            <button 
                v-if="hasTransform"
                :class="['transform-btn', { active: showTransform }]"
                @click="showTransform = !showTransform"
            >
                {{ showTransform ? '◀ Normal' : 'Transform ▶' }}
            </button>
        </div>

        <!-- Main Content Grid -->
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-4 flex-1 min-h-0">
            
            <!-- Left Column -->
            <div class="flex flex-col gap-3 overflow-y-auto">
                <!-- Machine Overview -->
                <div class="info-card">
                    <h3 class="section-title">Machine Overview</h3>
                    <div class="grid grid-cols-3 gap-2 text-xs">
                        <div><span class="label">ID:</span> <span class="value">{{ currentMachine?.MachineId ?? '--' }}</span></div>
                        <div><span class="label">Rank:</span> <span class="value-gold">{{ currentMachine?.Rank ?? '--' }}</span></div>
                        <div><span class="label">Quality:</span> <span class="value">{{ '⭐'.repeat(currentMachine?.Quality || 0) }} ({{ currentMachine?.Quality ?? 0 }})</span></div>
                        <div class="col-span-2"><span class="label">CN:</span> <span class="value">{{ currentMachine?.ChineseName || '--' }}</span></div>
                        <div><span class="label">Rarity:</span> <span class="value">{{ '⭐'.repeat(currentMachine?.Rarity || 0) }} ({{ currentMachine?.Rarity ?? 0 }})</span></div>
                        <div class="col-span-2"><span class="label">EN:</span> <span class="value">{{ currentMachine?.EnglishName || '--' }}</span></div>
                        <div><span class="label">Combat:</span> <span class="value-blue">{{ currentMachine?.CombatType ?? '--' }}</span></div>
                    </div>
                </div>

                <!-- User Machine Data -->
                <div class="info-card" v-if="machineModel">
                    <h3 class="section-title">User Machine Data</h3>
                    <div class="grid grid-cols-3 gap-2 text-xs">
                        <div><span class="label">Slot:</span> <span class="value">{{ machineModel.Slot }}</span></div>
                        <div><span class="label">Level:</span> <span class="value">{{ machineModel.Level }}</span></div>
                        <div><span class="label">OC Max:</span> <span class="value">{{ machineModel.OcMaxLevel ?? '--' }}</span></div>
                        <div><span class="label">Battles:</span> <span class="value">{{ machineModel.BattleCount }}</span></div>
                        <div><span class="label">Battery:</span> <span class="value">{{ machineModel.BatteryPercent?.toFixed(1) }}%</span></div>
                        <div><span class="label">Locked:</span> <span :class="machineModel.IsLocked ? 'text-red-400' : 'text-green-400'">{{ machineModel.IsLocked ? 'Yes' : 'No' }}</span></div>
                        <div><span class="label">EXP:</span> <span class="value">{{ machineModel.CurrentExp ?? '--' }}</span></div>
                        <div><span class="label">Polish:</span> <span class="value">{{ machineModel.BrushPolish ?? '--' }}</span></div>
                        <div><span class="label">Extra Parts:</span> <span class="value">{{ machineModel.ExtraSkillParts ?? '--' }}</span></div>
                        <div class="col-span-3"><span class="label">Buy In Time:</span> <span class="value">{{ machineModel.BuyInTime ? new Date(machineModel.BuyInTime).toLocaleString() : '--' }}</span></div>
                    </div>
                    <!-- Colors -->
                    <div class="mt-3" v-if="machineModel.Colors">
                        <span class="label text-xs">Colors:</span>
                        <ColorStrip :colors="machineModel.Colors" class="mt-1" />
                    </div>
                    <!-- OC Parts -->
                    <div class="grid grid-cols-2 gap-3 mt-3">
                        <div><span class="label text-xs">OC Set 1:</span><OcPartsDisplay :parts="machineModel.Oc1Parts" class="mt-1" /></div>
                        <div><span class="label text-xs">OC Set 2:</span><OcPartsDisplay :parts="machineModel.Oc2Parts" class="mt-1" /></div>
                    </div>
                </div>

                <!-- Hexagon Stats -->
                <div class="info-card">
                    <div class="flex items-center justify-between mb-3">
                        <h3 class="section-title mb-0">Stats Hexagon</h3>
                        <div class="flex gap-1">
                            <button :class="['toggle-btn', { active: showBase }]" @click="showBase = !showBase" style="--btn-color: #3b82f6">Base</button>
                            <button :class="['toggle-btn', { active: showOcBase }]" @click="showOcBase = !showOcBase" style="--btn-color: #22c55e">+OC</button>
                            <button :class="['toggle-btn', { active: showOcBonus }]" @click="showOcBonus = !showOcBonus" style="--btn-color: #f59e0b">+Bonus</button>
                        </div>
                    </div>
                    <HexagonChart :stats="hexStats" :showBase="showBase" :showOcBase="showOcBase" :showOcBonus="showOcBonus" />
                </div>
            </div>

            <!-- Right Column -->
            <div class="flex flex-col gap-3 overflow-y-auto">
                <!-- Base Stats -->
                <div class="info-card" v-if="currentMachine">
                    <h3 class="section-title">Base Stats</h3>
                    <div class="grid grid-cols-4 gap-2 text-xs">
                        <div><span class="label">HP:</span> <span class="value">{{ currentMachine.HP }}</span></div>
                        <div><span class="label">Shield:</span> <span class="value">{{ currentMachine.ShieldHP }}</span></div>
                        <div><span class="label">Attack:</span> <span class="value">{{ currentMachine.Attack }}</span></div>
                        <div><span class="label">Defense:</span> <span class="value">{{ currentMachine.Defense }}</span></div>
                        <div><span class="label">Agility:</span> <span class="value">{{ currentMachine.Agility }}</span></div>
                        <div><span class="label">FwdSpeed:</span> <span class="value">{{ currentMachine.ForwardSpeed }}</span></div>
                        <div><span class="label">MoveSpeed:</span> <span class="value">{{ currentMachine.MoveSpeed }}</span></div>
                        <div><span class="label">BzdSpeed:</span> <span class="value">{{ currentMachine.BzdSpeed }}</span></div>
                        <div><span class="label">Boost Capacity:</span> <span class="value">{{ currentMachine.BoostCapacity }}</span></div>
                        <div><span class="label">Boost Recovery:</span> <span class="value">{{ currentMachine.BoostRecoverySpeed }}</span></div>
                        <div><span class="label">Boost Use:</span> <span class="value">{{ currentMachine.BoostConsumption }}</span></div>
                        <div><span class="label">Radar:</span> <span class="value">{{ currentMachine.RadarRange }}</span></div>
                        <div><span class="label">Respawn:</span> <span class="value">{{ currentMachine.RespawnTimeSeconds }}s</span></div>
                        <div><span class="label">TrackSpd:</span> <span class="value">{{ currentMachine.TrackSpeed }}</span></div>
                        <div><span class="label">TrackAcc:</span> <span class="value">{{ currentMachine.TrackAcceleration?.toFixed(2) }}</span></div>
                        <div><span class="label">Atkspd Lv:</span> <span class="value">{{ currentMachine.AttackSpeedLevel }}</span></div>
                    </div>
                    <!-- Shield Info -->
                    <div class="grid grid-cols-3 gap-2 text-xs mt-2 pt-2 border-t border-white/10">
                        <div><span class="label">Shield Type:</span> <span class="value">{{ currentMachine.ShieldType || '--' }}</span></div>
                        <div><span class="label">Shield Dir:</span> <span class="value">🛡️ {{ currentMachine.ShieldDirection || '--' }}</span></div>
                        <div><span class="label">Shield %:</span> <span class="value">{{ currentMachine.ShieldDeductionPercentage ? currentMachine.ShieldDeductionPercentage + '%' : '--' }}</span></div>
                    </div>
                </div>

                <!-- Skills Section (Collapsible) -->
                <div class="info-card">
                    <div class="section-header" @click="skillsCollapsed = !skillsCollapsed">
                        <h3 class="section-title mb-0">Skills ({{ skills.length }})</h3>
                        <span class="collapse-icon">{{ skillsCollapsed ? '▼' : '▲' }}</span>
                    </div>
                    <div v-if="!skillsCollapsed" class="flex flex-col gap-2 mt-2 max-h-48 overflow-y-auto">
                        <SkillCard v-for="(skill, idx) in skills" :key="idx" :skill="skill" :index="idx + 1" />
                        <div v-if="skills.length === 0" class="text-gray-500 text-xs">No skill data</div>
                    </div>
                </div>

                <!-- Weapons Section (Collapsible) -->
                <div class="info-card">
                    <div class="section-header" @click="weaponsCollapsed = !weaponsCollapsed">
                        <h3 class="section-title mb-0">Weapons ({{ weapons.length }})</h3>
                        <span class="collapse-icon">{{ weaponsCollapsed ? '▼' : '▲' }}</span>
                    </div>
                    <div v-if="!weaponsCollapsed" class="flex flex-col gap-2 mt-2 max-h-60 overflow-y-auto">
                        <WeaponCard v-for="(weapon, idx) in weapons" :key="idx" :weapon="weapon" :index="idx + 1" :isSpecial="idx === 3" />
                        <div v-if="weapons.length === 0" class="text-gray-500 text-xs">No weapon data</div>
                    </div>
                </div>
            </div>
        </div>
    </div>
</template>

<style scoped>
.info-card { @apply bg-bg-panel border border-neon-blue/50 p-3 shadow-lg transition-all duration-300; }
.info-card:hover { @apply border-neon-cyan/60; }

.section-title { @apply text-neon-cyan text-sm font-semibold mb-2; }
.section-header { @apply flex items-center justify-between cursor-pointer select-none; }
.collapse-icon { @apply text-gray-500 text-xs transition-transform; }

.label { @apply text-gray-500; }
.value { @apply text-white ml-1; }
.value-gold { @apply text-gundam-gold ml-1 font-bold; }
.value-blue { @apply text-neon-blue ml-1; }

.toggle-btn {
    @apply px-2 py-0.5 text-xs border rounded opacity-50 transition-all;
    border-color: var(--btn-color);
    color: var(--btn-color);
}
.toggle-btn.active {
    @apply opacity-100;
    background: color-mix(in srgb, var(--btn-color) 20%, transparent);
    box-shadow: 0 0 6px var(--btn-color);
}

.transform-btn {
    @apply px-3 py-1 text-xs font-semibold border border-orange-500 text-orange-400 rounded transition-all;
}
.transform-btn:hover { @apply bg-orange-500/20; }
.transform-btn.active { @apply bg-orange-500 text-black; }
</style>
