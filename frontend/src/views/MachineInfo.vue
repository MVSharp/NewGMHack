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
    if (showTransform.value && machineBaseInfo.value?.transformedMachine) {
        return machineBaseInfo.value.transformedMachine
    }
    return machineBaseInfo.value
})

// Computed hex values from MachineBaseInfo + OC points
const hexStats = computed(() => {
    const base = currentMachine.value
    const ocBase = machineInfo.value?.machineModel?.ocBasePoints
    const ocBonus = machineInfo.value?.machineModel?.ocBonusPoints
    const specialAtk = base?.specialAttack

    return {
        base: {
            attack: base?.attack ?? 0,
            agility: (base?.agility ?? 0) * 2,
            hp: (base?.hp ?? 0) * 0.01,
            defense: base?.defense ?? 0,
            sp: (specialAtk?.weaponDamage ?? 0) * 0.01,
            speed: (base?.forwardSpeed ?? 0) * 2
        },
        ocBase: {
            attack: ocBase?.attack ?? 0,
            agility: ocBase?.agility ?? 0,
            hp: ocBase?.hp ?? 0,
            defense: ocBase?.defense ?? 0,
            sp: ocBase?.special ?? 0,
            speed: ocBase?.speed ?? 0
        },
        ocBonus: {
            attack: ocBonus?.attack ?? 0,
            agility: ocBonus?.agility ?? 0,
            hp: ocBonus?.hp ?? 0,
            defense: ocBonus?.defense ?? 0,
            sp: ocBonus?.special ?? 0,
            speed: ocBonus?.speed ?? 0
        }
    }
})

const machineModel = computed(() => machineInfo.value?.machineModel)
const machineBaseInfo = computed(() => machineInfo.value?.machineBaseInfo)
const hasTransform = computed(() => machineBaseInfo.value?.hasTransform ?? false)

const skills = computed(() => [
    currentMachine.value?.skill1Info,
    currentMachine.value?.skill2Info
].filter(Boolean))

const weapons = computed(() => [
    currentMachine.value?.weapon1Info,
    currentMachine.value?.weapon2Info,
    currentMachine.value?.weapon3Info,
    currentMachine.value?.specialAttack
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
                        <div><span class="label">ID:</span> <span class="value">{{ currentMachine?.machineId ?? '--' }}</span></div>
                        <div><span class="label">Rank:</span> <span class="value-gold">{{ currentMachine?.rank ?? '--' }}</span></div>
                        <div><span class="label">Quality:</span> <span class="value">{{ '⭐'.repeat(currentMachine?.quality || 0) }} ({{ currentMachine?.quality ?? 0 }})</span></div>
                        <div class="col-span-2"><span class="label">CN:</span> <span class="value">{{ currentMachine?.chineseName || '--' }}</span></div>
                        <div><span class="label">Rarity:</span> <span class="value">{{ '⭐'.repeat(currentMachine?.rarity || 0) }} ({{ currentMachine?.rarity ?? 0 }})</span></div>
                        <div class="col-span-2"><span class="label">EN:</span> <span class="value">{{ currentMachine?.englishName || '--' }}</span></div>
                        <div><span class="label">Combat:</span> <span class="value-blue">{{ currentMachine?.combatType ?? '--' }}</span></div>
                    </div>
                </div>

                <!-- User Machine Data -->
                <div class="info-card" v-if="machineModel">
                    <h3 class="section-title">User Machine Data</h3>
                    <div class="grid grid-cols-3 gap-2 text-xs">
                        <div><span class="label">Slot:</span> <span class="value">{{ machineModel.slot }}</span></div>
                        <div><span class="label">Level:</span> <span class="value">{{ machineModel.level }}</span></div>
                        <div><span class="label">OC Max:</span> <span class="value">{{ machineModel.ocMaxLevel ?? '--' }}</span></div>
                        <div><span class="label">Battles:</span> <span class="value">{{ machineModel.battleCount }}</span></div>
                        <div><span class="label">Battery:</span> <span class="value">{{ machineModel.batteryPercent?.toFixed(1) }}%</span></div>
                        <div><span class="label">Locked:</span> <span :class="machineModel.isLocked ? 'text-red-400' : 'text-green-400'">{{ machineModel.isLocked ? 'Yes' : 'No' }}</span></div>
                        <div><span class="label">EXP:</span> <span class="value">{{ machineModel.currentExp ?? '--' }}</span></div>
                        <div><span class="label">Polish:</span> <span class="value">{{ machineModel.brushPolish ?? '--' }}</span></div>
                        <div><span class="label">Extra Parts:</span> <span class="value">{{ machineModel.extraSkillParts ?? '--' }}</span></div>
                        <div class="col-span-3"><span class="label">Buy In Time:</span> <span class="value">{{ machineModel.buyInTime ? new Date(machineModel.buyInTime).toLocaleString() : '--' }}</span></div>
                    </div>
                    <!-- Colors -->
                    <div class="mt-3" v-if="machineModel.colors">
                        <span class="label text-xs">Colors:</span>
                        <ColorStrip :colors="machineModel.colors" class="mt-1" />
                    </div>
                    <!-- OC Parts -->
                    <div class="grid grid-cols-2 gap-3 mt-3">
                        <div><span class="label text-xs">OC Set 1:</span><OcPartsDisplay :parts="machineModel.oc1Parts" class="mt-1" /></div>
                        <div><span class="label text-xs">OC Set 2:</span><OcPartsDisplay :parts="machineModel.oc2Parts" class="mt-1" /></div>
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
                        <div><span class="label">HP:</span> <span class="value">{{ currentMachine.hp }}</span></div>
                        <div><span class="label">Shield:</span> <span class="value">{{ currentMachine.shieldHP }}</span></div>
                        <div><span class="label">Attack:</span> <span class="value">{{ currentMachine.attack }}</span></div>
                        <div><span class="label">Defense:</span> <span class="value">{{ currentMachine.defense }}</span></div>
                        <div><span class="label">Agility:</span> <span class="value">{{ currentMachine.agility }}</span></div>
                        <div><span class="label">FwdSpeed:</span> <span class="value">{{ currentMachine.forwardSpeed }}</span></div>
                        <div><span class="label">MoveSpeed:</span> <span class="value">{{ currentMachine.moveSpeed }}</span></div>
                        <div><span class="label">BzdSpeed:</span> <span class="value">{{ currentMachine.bzdSpeed }}</span></div>
                        <div><span class="label">Boost Capacity:</span> <span class="value">{{ currentMachine.boostCapacity }}</span></div>
                        <div><span class="label">Boost Recovery:</span> <span class="value">{{ currentMachine.boostRecoverySpeed }}</span></div>
                        <div><span class="label">Boost Use:</span> <span class="value">{{ currentMachine.boostConsumption }}</span></div>
                        <div><span class="label">Radar:</span> <span class="value">{{ currentMachine.radarRange }}</span></div>
                        <div><span class="label">Respawn:</span> <span class="value">{{ currentMachine.respawnTimeSeconds }}s</span></div>
                        <div><span class="label">TrackSpd:</span> <span class="value">{{ currentMachine.trackSpeed }}</span></div>
                        <div><span class="label">TrackAcc:</span> <span class="value">{{ currentMachine.trackAcceleration?.toFixed(2) }}</span></div>
                        <div><span class="label">Atkspd Lv:</span> <span class="value">{{ currentMachine.attackSpeedLevel }}</span></div>
                    </div>
                    <!-- Shield Info -->
                    <div class="grid grid-cols-3 gap-2 text-xs mt-2 pt-2 border-t border-white/10">
                        <div><span class="label">Shield Type:</span> <span class="value">{{ currentMachine.shieldType || '--' }}</span></div>
                        <div><span class="label">Shield Dir:</span> <span class="value">🛡️ {{ currentMachine.shieldDirection || '--' }}</span></div>
                        <div><span class="label">Shield %:</span> <span class="value">{{ currentMachine.shieldDeductionPercentage ? currentMachine.shieldDeductionPercentage + '%' : '--' }}</span></div>
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
