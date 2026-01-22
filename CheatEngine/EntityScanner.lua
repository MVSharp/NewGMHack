--[[
  Entity Scanner Lua Script for Cheat Engine
  
  Purpose: Find and display entity list for offset/base address verification
  Target: x86 Process
  
  Usage:
  1. Attach Cheat Engine to the target process FIRST
  2. Execute this script (Ctrl+Alt+L or Table -> Show Cheat Table Lua Script)
  3. Run scanEntities() to find all entities
  4. Run scanSelf() to find your own entity
  5. Run findBaseAddress() to verify current base offset
]] -- ============================================================================
-- CONFIGURATION - Update these offsets when the game updates
-- ============================================================================
local CONFIG = {
    -- Base offset from module base
    BaseOffset = 0x012E606C,

    -- Pointer chain offsets to reach entity list
    PointerChainOffsets = {0x40, 0x0, 0x8},

    -- Entity structure offsets
    Offsets = {
        EntityId = 0x04,
        PositionPtr = 0x30,
        CurrentHp = 0x34,
        MaxHp = 0x38,
        Team = 0x2DC2,
        Type = 0x2FE0
    },

    -- Position offsets (from PositionPtr)
    PositionOffsets = {
        X = 0x88,
        Y = 0x8C,
        Z = 0x90
    },

    -- MySelf offset from first pointer
    MySelfOffset = 0x60,

    -- Scan settings
    MaxEntities = 12,
    ScanLimit = 100,
    MaxValidHp = 600000
}

-- ============================================================================
-- UTILITY FUNCTIONS
-- ============================================================================

local function log(message)
    print(string.format("[Scanner] %s", message))
end

local function logEntityDetailed(index, entity)
    log(string.format("=== Entity [%d] ===", index))
    log(string.format("  Entity Struct Address  : 0x%08X", entity.address or 0))
    log(string.format("  Position Pointer Addr  : 0x%08X", entity.posPtrAddress or 0))
    log(string.format("  Entity ID              : %d", entity.id or 0))
    log(string.format("  HP                     : %d / %d", entity.currentHp or 0, entity.maxHp or 0))
    log(string.format("  Position (X, Y, Z)     : %.2f, %.2f, %.2f", entity.x or 0, entity.y or 0, entity.z or 0))
    log("  --- Address Details ---")
    log(string.format("  CurrentHP Address      : 0x%08X", (entity.address or 0) + CONFIG.Offsets.CurrentHp))
    log(string.format("  MaxHP Address          : 0x%08X", (entity.address or 0) + CONFIG.Offsets.MaxHp))
    log(string.format("  X Position Address     : 0x%08X", (entity.posPtrAddress or 0) + CONFIG.PositionOffsets.X))
    log(string.format("  Y Position Address     : 0x%08X", (entity.posPtrAddress or 0) + CONFIG.PositionOffsets.Y))
    log(string.format("  Z Position Address     : 0x%08X", (entity.posPtrAddress or 0) + CONFIG.PositionOffsets.Z))
    log(string.format("  Team Offset Address    : 0x%08X", (entity.address or 0) + CONFIG.Offsets.Team))
    log(string.format("  Type Offset Address    : 0x%08X", (entity.address or 0) + CONFIG.Offsets.Type))
end

local function logEntityCompact(index, entity)
    log(string.format("  [%d] Addr: 0x%08X | PosPtr: 0x%08X | ID: %d | HP: %d/%d | Pos: (%.1f, %.1f, %.1f)", index,
        entity.address or 0, entity.posPtrAddress or 0, entity.id or 0, entity.currentHp or 0, entity.maxHp or 0,
        entity.x or 0, entity.y or 0, entity.z or 0))
end

local function isValidAddress(address)
    if address == nil then
        return false
    end
    if address < 0x10000 then
        return false
    end
    if address >= 0x80000000 then
        return false
    end
    return true
end

local function safeReadInteger(address)
    if not isValidAddress(address) then
        return nil
    end
    local value = readInteger(address)
    return value
end

local function safeReadFloat(address)
    if not isValidAddress(address) then
        return nil
    end
    local value = readFloat(address)
    return value
end

local function readPointerChain(baseAddr, offsets)
    local addr = baseAddr
    for i, offset in ipairs(offsets) do
        local ptr = safeReadInteger(addr)
        if ptr == nil or ptr == 0 then
            return 0
        end
        addr = ptr + offset
    end
    return addr
end

-- ============================================================================
-- MODULE BASE ADDRESS (Auto-detect from attached process)
-- ============================================================================

local function getMainModuleBase()
    local pid = getOpenedProcessID()
    if pid == 0 then
        log("ERROR: No process attached. Please attach to the target process first!")
        return 0, nil
    end

    local moduleList = enumModules(pid)
    if moduleList == nil or #moduleList == 0 then
        log("ERROR: Could not enumerate modules")
        return 0, nil
    end

    -- The first module is usually the main executable
    local mainModule = moduleList[1]
    log(string.format("Auto-detected main module: %s at 0x%08X", mainModule.Name, mainModule.Address))
    return mainModule.Address, mainModule.Name
end

-- ============================================================================
-- ENTITY READING
-- ============================================================================

local function readEntityData(entityStruct)
    if not isValidAddress(entityStruct) then
        return nil
    end

    local entity = {}
    entity.address = entityStruct

    -- Read entity ID
    local entityId = safeReadInteger(entityStruct + CONFIG.Offsets.EntityId)
    entity.id = entityId or 0

    -- Read HP values
    local hp = safeReadInteger(entityStruct + CONFIG.Offsets.CurrentHp)
    local maxHp = safeReadInteger(entityStruct + CONFIG.Offsets.MaxHp)

    if hp == nil or maxHp == nil then
        return nil
    end

    -- Validate HP range
    if hp < 0 or hp > CONFIG.MaxValidHp then
        return nil
    end
    if maxHp < 0 or maxHp > CONFIG.MaxValidHp then
        return nil
    end

    entity.currentHp = hp
    entity.maxHp = maxHp

    -- Read position pointer
    local posPtr = safeReadInteger(entityStruct + CONFIG.Offsets.PositionPtr)
    if posPtr == nil or posPtr == 0 then
        return nil
    end
    entity.posPtrAddress = posPtr

    -- Read position
    local x = safeReadFloat(posPtr + CONFIG.PositionOffsets.X)
    local y = safeReadFloat(posPtr + CONFIG.PositionOffsets.Y)
    local z = safeReadFloat(posPtr + CONFIG.PositionOffsets.Z)

    if x == nil or y == nil or z == nil then
        return nil
    end

    entity.x = x
    entity.y = y + 50 -- Match the +50 offset from original code
    entity.z = z

    return entity
end

-- ============================================================================
-- SCAN FUNCTIONS
-- ============================================================================

-- Set detailed = true for full address breakdown, false for compact output
function scanEntities(detailed)
    if detailed == nil then
        detailed = true
    end

    log("========================================")
    log("         SCANNING ENTITIES")
    log("========================================")

    local moduleBase, moduleName = getMainModuleBase()
    if moduleBase == 0 then
        return {}
    end
    log(string.format("Module Base: 0x%08X", moduleBase))

    local baseAddr = moduleBase + CONFIG.BaseOffset
    log(string.format("Base Address (Module + 0x%08X): 0x%08X", CONFIG.BaseOffset, baseAddr))

    local entityAddr = readPointerChain(baseAddr, CONFIG.PointerChainOffsets)
    if entityAddr == 0 then
        log("ERROR: Could not resolve pointer chain")
        return {}
    end
    log(string.format("Entity List Address: 0x%08X", entityAddr))

    -- Read list head (at entityAddr - 0x8)
    local listHead = safeReadInteger(entityAddr - 0x8)
    if listHead == nil or listHead == 0 then
        log("ERROR: List head is null")
        return {}
    end
    log(string.format("List Head Address: 0x%08X", listHead))
    log("----------------------------------------")

    local entities = {}
    local visited = {}
    local current = listHead
    local scannedCount = 0

    while current ~= 0 and visited[current] == nil and scannedCount < CONFIG.ScanLimit do
        visited[current] = true
        scannedCount = scannedCount + 1

        -- Read data address at current + 0x8
        local dataAddr = safeReadInteger(current + 0x8)
        if dataAddr ~= nil and dataAddr ~= 0 then
            local entity = readEntityData(dataAddr)
            if entity ~= nil then
                entity.nodeAddress = current -- Store the linked list node address too
                table.insert(entities, entity)
            end
        end

        -- Move to next node
        local nextNode = safeReadInteger(current)
        current = nextNode or 0
    end

    log(string.format("Found %d valid entities (scanned %d nodes)", #entities, scannedCount))
    log("========================================")

    for i, entity in ipairs(entities) do
        if detailed then
            logEntityDetailed(i, entity)
        else
            logEntityCompact(i, entity)
        end
    end

    return entities
end

function scanSelf(detailed)
    if detailed == nil then
        detailed = true
    end

    log("========================================")
    log("           SCANNING SELF")
    log("========================================")

    local moduleBase, moduleName = getMainModuleBase()
    if moduleBase == 0 then
        return nil
    end

    local pointerBase = moduleBase + CONFIG.BaseOffset
    log(string.format("Pointer Base: 0x%08X", pointerBase))

    local firstPtr = safeReadInteger(pointerBase)
    if firstPtr == nil or firstPtr == 0 then
        log("ERROR: First pointer is null")
        return nil
    end
    log(string.format("First Pointer: 0x%08X", firstPtr))

    local entityStruct = safeReadInteger(firstPtr + CONFIG.MySelfOffset)
    if entityStruct == nil or entityStruct == 0 then
        log("ERROR: Entity struct pointer is null")
        return nil
    end
    log(string.format("Self Entity Struct Address: 0x%08X", entityStruct))
    log("----------------------------------------")

    local entity = readEntityData(entityStruct)
    if entity == nil then
        log("ERROR: Could not read self entity data")
        return nil
    end

    if detailed then
        logEntityDetailed(0, entity)
    else
        logEntityCompact(0, entity)
    end

    return entity
end

-- ============================================================================
-- OFFSET VERIFICATION FUNCTIONS
-- ============================================================================

function findBaseAddress()
    log("========================================")
    log("      VERIFYING BASE ADDRESS")
    log("========================================")

    local moduleBase, moduleName = getMainModuleBase()
    if moduleBase == 0 then
        return
    end

    log(string.format("Module Base: 0x%08X", moduleBase))
    log(string.format("Current BaseOffset: 0x%08X", CONFIG.BaseOffset))
    log(string.format("Current Full Address: 0x%08X", moduleBase + CONFIG.BaseOffset))

    -- Verify current offset still works
    local testAddr = moduleBase + CONFIG.BaseOffset
    local testPtr = safeReadInteger(testAddr)
    if testPtr ~= nil and testPtr ~= 0 then
        log(string.format("Status: VALID - First pointer: 0x%08X", testPtr))
    else
        log("Status: INVALID - Offset returns null pointer")
    end
end

function dumpPointerChain()
    log("========================================")
    log("      DUMPING POINTER CHAIN")
    log("========================================")

    local moduleBase, moduleName = getMainModuleBase()
    if moduleBase == 0 then
        return
    end

    log(string.format("Step 0: Module Base = 0x%08X", moduleBase))

    local baseAddr = moduleBase + CONFIG.BaseOffset
    log(string.format("Step 1: Base + 0x%08X = 0x%08X", CONFIG.BaseOffset, baseAddr))

    local ptr1 = safeReadInteger(baseAddr)
    if ptr1 == nil or ptr1 == 0 then
        log("ERROR: First dereference failed")
        return
    end
    log(string.format("Step 2: [0x%08X] = 0x%08X", baseAddr, ptr1))

    local addr = ptr1
    for i, offset in ipairs(CONFIG.PointerChainOffsets) do
        local nextAddr = addr + offset
        log(string.format("Step %d: 0x%08X + 0x%X = 0x%08X", i + 2, addr, offset, nextAddr))

        local nextPtr = safeReadInteger(nextAddr)
        if nextPtr == nil or nextPtr == 0 then
            log(string.format("Step %d: [0x%08X] = NULL (chain ends here)", i + 2, nextAddr))
            return
        end
        log(string.format("Step %d: [0x%08X] = 0x%08X", i + 2, nextAddr, nextPtr))
        addr = nextPtr
    end

    log(string.format("Final Entity List Address: 0x%08X", addr))

    -- Also show list head
    local listHead = safeReadInteger(addr - 0x8)
    if listHead ~= nil and listHead ~= 0 then
        log(string.format("List Head (addr - 0x8): 0x%08X", listHead))
    end
end

function showCurrentConfig()
    log("========================================")
    log("      CURRENT CONFIGURATION")
    log("========================================")
    log(string.format("BaseOffset: 0x%08X", CONFIG.BaseOffset))
    log("PointerChainOffsets: " .. table.concat((function()
        local t = {}
        for _, v in ipairs(CONFIG.PointerChainOffsets) do
            table.insert(t, string.format("0x%X", v))
        end
        return t
    end)(), ", "))
    log("Entity Offsets:")
    for name, offset in pairs(CONFIG.Offsets) do
        log(string.format("  %s: 0x%04X", name, offset))
    end
    log("Position Offsets:")
    for name, offset in pairs(CONFIG.PositionOffsets) do
        log(string.format("  %s: 0x%04X", name, offset))
    end
    log(string.format("MySelfOffset: 0x%04X", CONFIG.MySelfOffset))
end

-- ============================================================================
-- MAIN ENTRY
-- ============================================================================

log("========================================")
log("    Entity Scanner Script Loaded!")
log("========================================")
log("Available commands:")
log("  scanEntities()       - Scan all entities (detailed)")
log("  scanEntities(false)  - Scan all entities (compact)")
log("  scanSelf()           - Scan your entity (detailed)")
log("  scanSelf(false)      - Scan your entity (compact)")
log("  findBaseAddress()    - Verify current base offset")
log("  dumpPointerChain()   - Show full pointer chain")
log("  showCurrentConfig()  - Display offset configuration")
log("")
log("NOTE: Attach to the target process FIRST before running scan functions!")
