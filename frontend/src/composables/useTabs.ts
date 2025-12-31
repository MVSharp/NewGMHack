import { ref } from 'vue'

export const TabNames = {
    Dashboard: 'Dashboard',
    Features: 'Features',
    Pilot: 'Pilot',
    Lobby: 'Lobby',
    MachineInfo: 'MachineInfo'
} as const

export type TabName = typeof TabNames[keyof typeof TabNames]

// Shared state (singleton)
const currentTab = ref<TabName>(TabNames.Dashboard)

export function useTabs() {
    function setTab(tab: TabName) {
        currentTab.value = tab
    }

    return {
        currentTab,
        setTab,
        TabNames
    }
}
