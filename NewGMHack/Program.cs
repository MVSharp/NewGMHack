using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Squalr.Engine.Logging;
using Squalr.Engine.Memory;
using Squalr.Engine.OS;

namespace EntityDumper
{
    class Program
    {
        private static readonly int BaseOffset = 0x5C1FEC;
        private static readonly int[] Offsets = { 0x50, 0x4, 0x8 };
        private static readonly int HpOffset = 0x34;
        private static readonly int PosPtrOffset = 0x30;
        private static readonly int[] XyzOffsets = { 0x88, 0x8C, 0x90 };
        private static readonly int TeamOffset = 0x2DC2;
        private static readonly int TypeOffset = 0x2FE0;

        static void Main(string[] args)
        {
            // Subscribe to Squalr logs for error output

            string ModuleName = Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ==")) + ".exe";
            Logger.Subscribe(new EngineLogEvents());

            // Attach to process
            IEnumerable<Process> processes = Processes.Default.GetProcesses();
            Process process = processes.FirstOrDefault(p => p.ProcessName.Equals(ModuleName.Replace(".exe", ""), StringComparison.OrdinalIgnoreCase));
            if (process == null)
            {
                Console.WriteLine($"Error: Could not find process {ModuleName}");
                return;
            }
            Processes.Default.OpenedProcess = process;

            // Get module base address
            uint moduleBase = GetModuleBaseAddress(ModuleName);


            uint managerAddr = checked(moduleBase + (uint)0x5C1FE4);
            if (!TryReadUInt(managerAddr, out uint manager) || manager == 0) 
            {
                Debugger.Break();
            }

Console.WriteLine($"managerAddr = 0x{managerAddr:X8}, manager = 0x{manager:X8}");
if (!TryReadUInt(managerAddr + 0x08, out uint entityHandle) || entityHandle == 0)
            {
                Debugger.Break();
            }
Console.WriteLine($"rawHandle = 0x{entityHandle:X8}");
if (!TryReadUInt(entityHandle + 0x70, out uint entityStruct) || entityStruct == 0)
            {
                Debugger.Break();
            }
            if (!TryReadInt(entityStruct + 0x34, out int myhp) || myhp < 0 || myhp > 30000)
            {
                Debugger.Break();
            }
            if (moduleBase == 0)
            {
                Console.WriteLine($"Error: Could not find module {ModuleName}");
                return;
            }
            uint baseAddr;
            try
            {
                baseAddr = checked(moduleBase + (uint)BaseOffset);
            }
            catch (OverflowException ex)
            {
                Console.WriteLine($"Error: Overflow calculating base address 0x{moduleBase:X} + 0x{BaseOffset:X}: {ex.Message}");
                return;
            }

            // Follow pointer chain to first entity
            uint entityAddr = ReadPointerChain(baseAddr, Offsets);
            if (entityAddr == 0)
            {
                Console.WriteLine($"Error: Invalid pointer chain at 0x{baseAddr:X}");
                Console.WriteLine("Verify base address and offsets in Cheat Engine.");
                return;
            }

            // Find list head (assuming entity is at data+0x8)
            bool success;
            uint listHead;
            try
            {
                listHead = Reader.Default.Read<uint>(checked(entityAddr - 0x8), out success);
            }
            catch (OverflowException ex)
            {
                Console.WriteLine($"Error: Overflow calculating list head address 0x{entityAddr:X} - 0x8: {ex.Message}");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading list head at 0x{(entityAddr - 0x8):X}: {ex.Message}");
                return;
            }
            if (!success || listHead == 0)
            {
                Console.WriteLine("Error: Could not find list head");
                return;
            }

            // Iterate linked list
            HashSet<uint> visited = new HashSet<uint>();
            int count = 0;
            uint current = listHead;
            while (current != 0 && !visited.Contains(current) && count < 100) // Safety limit
            {
                visited.Add(current);
                count++;

                // Read entity data pointer
                uint dataAddr;
                try
                {
                    dataAddr = Reader.Default.Read<uint>(checked(current + 0x8), out success);
                }
                catch (OverflowException ex)
                {
                    Console.WriteLine($"Entity {count}: Overflow calculating data pointer at 0x{current:X} + 0x8: {ex.Message}");
                    continue;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Entity {count}: Error reading data pointer at 0x{(current + 0x8):X}: {ex.Message}");
                    continue;
                }
                if (success && dataAddr != 0)
                {
                    // Read HP (try int, then float)
                    int hpInt = 0;
                    float hpFloat = 0;
                    try
                    {
                        hpInt = Reader.Default.Read<Int32>(checked(dataAddr + (uint)HpOffset), out success);
                        hpFloat = success ? Reader.Default.Read<Single>(checked(dataAddr + (uint)HpOffset), out success) : 0;
                    }
                    catch (OverflowException ex)
                    {
                        Console.WriteLine($"Entity {count}: Overflow reading HP at 0x{dataAddr:X} + 0x{HpOffset:X}: {ex.Message}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Entity {count}: Error reading HP at 0x{(dataAddr + (uint)HpOffset):X}: {ex.Message}");
                        continue;
                    }
                    string hp = hpInt >= 0 && hpInt <= 10000 ? hpInt.ToString() : hpFloat.ToString("F2");

                    // Read position pointer
                    uint posPtr;
                    try
                    {
                        posPtr = Reader.Default.Read<uint>(checked(dataAddr + (uint)PosPtrOffset), out success);
                    }
                    catch (OverflowException ex)
                    {
                        Console.WriteLine($"Entity {count}: Overflow reading position pointer at 0x{dataAddr:X} + 0x{PosPtrOffset:X}: {ex.Message}");
                        posPtr = 0;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Entity {count}: Error reading position pointer at 0x{(dataAddr + (uint)PosPtrOffset):X}: {ex.Message}");
                        posPtr = 0;
                    }
                    float x = 0, y = 0, z = 0;
                    if (success && posPtr != 0)
                    {
                        try
                        {
                            x = Reader.Default.Read<Single>(checked(posPtr + (uint)XyzOffsets[0]), out success);
                            y = success ? Reader.Default.Read<Single>(checked(posPtr + (uint)XyzOffsets[1]), out success) : 0;
                            z = success ? Reader.Default.Read<Single>(checked(posPtr + (uint)XyzOffsets[2]), out success) : 0;
                        }
                        catch (OverflowException ex)
                        {
                            Console.WriteLine($"Entity {count}: Overflow reading coordinates at 0x{posPtr:X} + 0x{XyzOffsets[0]:X}: {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Entity {count}: Error reading coordinates at 0x{(posPtr + (uint)XyzOffsets[0]):X}: {ex.Message}");
                        }
                    }

                    // Read team and type
                    byte team = 0;
                    int typeValue = 0;
                    try
                    {
                        team = Reader.Default.Read<Byte>(checked(dataAddr + (uint)TeamOffset), out success);
                        typeValue = success ? Reader.Default.Read<Int32>(checked(dataAddr + (uint)TypeOffset), out success) : 0;
                    }
                    catch (OverflowException ex)
                    {
                        Console.WriteLine($"Entity {count}: Overflow reading team/type at 0x{dataAddr:X} + 0x{TeamOffset:X}: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Entity {count}: Error reading team/type at 0x{(dataAddr + (uint)TeamOffset):X}: {ex.Message}");
                    }

                    // Skip invalid entities (e.g., Entity 1)
                    if (hpInt > 1000 && hpFloat == 0 || (x == 0 && y == 0 && z == 0))
                    {
                        Console.WriteLine($"Entity {count}: Skipped (invalid data), Address = 0x{dataAddr:X}");
                    }
                    else
                    {
                        Console.WriteLine($"Entity {count}: HP = {hp}, Pos = ({x:F2}, {y:F2}, {z:F2}), Team = {team}, Type = {typeValue}, Address = 0x{dataAddr:X}");
                    }
                }
                else
                {
                    Console.WriteLine($"Entity {count}: Skipped (invalid data pointer), Node = 0x{current:X}");
                }

                // Move to next node
                try
                {
                    current = Reader.Default.Read<uint>(current, out success);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Entity {count}: Error reading next pointer at 0x{current:X}: {ex.Message}");
                    break;
                }
                if (!success)
                {
                    Console.WriteLine($"Entity {count}: Failed to read next pointer at 0x{current:X}");
                    break;
                }
            }

            Console.WriteLine($"Dumped {count} entities");
        }

        private static uint GetModuleBaseAddress(string moduleName)
        {
            try
            {
                var modules = Query.Default.GetModules();
                var module = modules.FirstOrDefault(m => m.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
                return module != null ? (uint)module.BaseAddress : 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting module base: {ex.Message}");
                return 0;
            }
        }
private static bool TryReadInt(uint address, out int value)
{
    bool success;
    value = Reader.Default.Read<int>((ulong)address, out success);
    return success;
}

private static bool TryReadUInt(uint address, out uint value)
{
    bool success;
    value = Reader.Default.Read<uint>((ulong)address, out success);
    return success;
}
        private static uint ReadPointerChain(uint baseAddr, int[] offsets)
        {
            uint addr = baseAddr;
            bool success;
            foreach (int offset in offsets)
            {
                try
                {
                    addr = Reader.Default.Read<uint>(addr, out success);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading pointer at 0x{addr:X}: {ex.Message}");
                    return 0;
                }
                if (!success || addr == 0)
                {
                    Console.WriteLine($"Invalid pointer read at 0x{addr:X}");
                    return 0;
                }
                try
                {
                    addr = checked(addr + (uint)offset);
                }
                catch (OverflowException ex)
                {
                    Console.WriteLine($"Overflow in pointer chain at 0x{addr:X} + 0x{offset:X}: {ex.Message}");
                    return 0;
                }
            }
            return addr;
        }
    }

    public class EngineLogEvents : ILoggerObserver
    {
        public void OnLogEvent(LogLevel logLevel, string message, string innerMessage)
        {
            Console.WriteLine($"[Squalr Log {logLevel}]: {message}");
            if (!string.IsNullOrEmpty(innerMessage))
            {
                Console.WriteLine($"[Inner]: {innerMessage}");
            }
        }
    }
}
