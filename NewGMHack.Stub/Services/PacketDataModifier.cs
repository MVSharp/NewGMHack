using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NewGMHack.CommunicationModel.PacketStructs.Send;
using NewGMHack.Stub;
using NewGMHack.Stub.PacketStructs;
using NewGMHack.Stub.PacketStructs.Send;
using NewGMHack.Stub.Services;
using ZLinq;
using ZLogger;

public sealed class PacketDataModifier
{
    private readonly SelfInformation _self;
    private readonly IBuffSplitter _splitter;
    private readonly ILogger<PacketDataModifier> _logger;

    public PacketDataModifier(SelfInformation self, IBuffSplitter splitter, ILogger<PacketDataModifier> logger = null)
    {
        _self = self;
        _splitter = splitter;
        _logger = logger;
    }

public static float DecodePosition(byte high, byte low)
{
  ushort raw = (ushort)((high << 8) | low);

    // Mask to extract lower 14 bits
    short value = (short)(raw & 0x3FFF);

    // If sign bit (bit 13) is set, convert to negative
    if ((value & 0x2000) != 0)
    {
        value -= 0x4000;
    }

    return value;
 }
    public List<byte[]> TryHandleExtraSendData(ReadOnlySpan<byte> data)
    {
        try
        {
            // Check packet length and opcode 0x0851 (2129)
            // Little endian: 0x51 0x08
            if (data.Length >= Unsafe.SizeOf<SendFunnel2129>() && data[4] == 0x51 && data[5] == 0x08)
            {
                // Zero-copy read
                ref readonly var sendFunnel = ref MemoryMarshal.AsRef<SendFunnel2129>(data);
                
                _logger?.ZLogInformation($" raw funnel struct:{sendFunnel.PlayerId} {sendFunnel.TargetId} {sendFunnel.Count}");
                
                if (_self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsAutoFunnel) && _self.PersonInfo.PersonId == sendFunnel.PlayerId)
                {
                    if (sendFunnel.Count >= 9) return [];
                    
                    var result = new List<byte[]>(10); // Pre-allocate list capacity
                    
                    // Stack allocated struct for modification
                    SendFunnel2129 structs = sendFunnel;
                    
                    for (int i = 0; i <= 9; i++)
                    {
                        structs.Count = (byte)i;
                        
                        // Serialize directly to byte array
                        byte[] b = new byte[Unsafe.SizeOf<SendFunnel2129>()];
                        MemoryMarshal.Write(b, in structs);
                        
                        _logger?.ZLogInformation($"funnel:{Convert.ToHexString(b)}");
                        result.Add(b);
                    }
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.ZLogInformation($"{ex.Message} {ex.StackTrace}");
        }
        return [];
    }

    public byte[]? TryModifySendData(ReadOnlySpan<byte> data)
    {
        if (data.Length <= 6) return null;

        // Opcode optimization: Check if it's 1868 (0x074C) -> 4C 07
        bool isOpcode1868 = data[4] == 0x4C && data[5] == 0x07;
        
        if (!isOpcode1868) return null;

        // Optimize: Don't split if we just need the first packet's method
        // Logic from original code: var method = result.Method;
        
        if (!_self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsMissionBomb) &&
            !_self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsPlayerBomb))
        {
            return null;
        }

        // We know it's 1868, so we can cast it safely if length permits
        if (data.Length < Unsafe.SizeOf<Attack1335>()) return null;

        // Zero-copy read of the packet header
        ref readonly var attack = ref MemoryMarshal.AsRef<Attack1335>(data);

        // Get actual packet length from header to handle coalesced packets correctly
        ushort packetLength = attack.Length; 
        if (data.Length < packetLength) return null; // Incomplete packet

        // Get targets slice based on ACTUAL packet length
        // Targets start after header, and end at packetLength
        int headerSize = Unsafe.SizeOf<Attack1335>();
        int targetDataSize = packetLength - headerSize; 
        
        // Ensure non-negative size (though packetLength should be valid)
        if (targetDataSize < 0) return null;

        // Slice safely
        var targetsSpan = data.Slice(headerSize, targetDataSize);
        var targets = MemoryMarshal.Cast<byte, TargetData>(targetsSpan);

        if (_self.Teammales.Contains(attack.PlayerId))
        {
            // Logic seems to be: if teammate, do nothing (assignment to self)
            // Original: attack.PlayerId = attack.PlayerId; ...
            // So we return null if no changes needed usually, but here we might need 
            // to modify it if we were supposed to return a modified buffer.
            // However, looking at the code, if teammate, it just assigns self to self.
            // If the intention is to NOT modify, we should return null.
            // BUT, the original code returned a modified buffer even if it was just copying?
            // "var modified = new byte[...]" happened at the end unconditionally in the case block.
            // Let's assume we proceed to creating the modified packet regardless.
        }
        else if (attack.PlayerId != _self.PersonInfo.PersonId)
        {
            // It's not me, and not a teammate
            var tmpId = attack.PlayerId;
            
            // Create a modified copy of the header
            Attack1335 modifiedAttack = attack;
            modifiedAttack.PlayerId = _self.PersonInfo.PersonId;
            modifiedAttack.WeaponId = _self.PersonInfo.Weapon2;
            modifiedAttack.WeaponSlot = 1;

            // Prepare modified packet buffer
            // Header + Targets + 1 byte terminator (from original logic: combined with [0x00])
            // Wait, does packetLength include terminator? 
            // If original code did: targetsBytes.CombineWith([0x00]) -> implies terminator is ADDED?
            // "var modified = new byte[attackBytes.Length + targetsBytes.Length]"
            // targetsBytes in original was "targets.AsByteSpan().CombineWith([0x00])"
            // So modified len = Header + Targets + 1.
            // The original result.MethodBody probably stripped the header.
            // Let's assume the new packet needs to be: Header + Targets + 0x00.
            
            int packetSize = Unsafe.SizeOf<Attack1335>() + targetsSpan.Length + 1;
            var modifiedPacket = new byte[packetSize];
            
            // Write modified header
            MemoryMarshal.Write(modifiedPacket, in modifiedAttack);
            
            // Write modified targets
            var modifiedTargetsSpan = modifiedPacket.AsSpan(Unsafe.SizeOf<Attack1335>(), targetsSpan.Length);
            var modifiedTargets = MemoryMarshal.Cast<byte, TargetData>(modifiedTargetsSpan);
            
            for (int i = 0; i < targets.Length; i++)
            {
                modifiedTargets[i] = targets[i]; // Copy original target data
                modifiedTargets[i].TargetId = tmpId; 
                
                if (modifiedTargets[i].TargetId != 0)
                {
                    modifiedTargets[i].Damage = ushort.MaxValue;
                }
            }
            
            // Write terminator
            modifiedPacket[packetSize - 1] = 0x00;
            
            _logger?.ZLogInformation($"Modified 1335: {BitConverter.ToString(modifiedPacket)}");
            return modifiedPacket;
        }

        // If we fell through (Teammate or Self), we still might need to apply the damage mod?
        // Original code: LOOP over targets and apply damage mod appears OUTSIDE the else if
        // Yes, lines 155-160 of original code apply to everyone!
        
        {
            // Create copy of header
            Attack1335 modifiedAttack = attack;
            
            // Packet size
            int packetSize = Unsafe.SizeOf<Attack1335>() + targetsSpan.Length + 1;
            byte[] modifiedPacket = new byte[packetSize];
            
            MemoryMarshal.Write(modifiedPacket, in modifiedAttack);
            
            var modifiedTargetsSpan = modifiedPacket.AsSpan(Unsafe.SizeOf<Attack1335>(), targetsSpan.Length);
            var modifiedTargets = MemoryMarshal.Cast<byte, TargetData>(modifiedTargetsSpan);
            
            for (int i = 0; i < targets.Length; i++)
            {
                modifiedTargets[i] = targets[i]; // Copy
                if (modifiedTargets[i].TargetId != 0)
                {
                    modifiedTargets[i].Damage = ushort.MaxValue;
                }
            }
             // Write terminator
            modifiedPacket[packetSize - 1] = 0x00;

            _logger?.ZLogInformation($"Modified 1335: {BitConverter.ToString(modifiedPacket)}");
            return modifiedPacket;
        }
    }
    public static string GetBitString(byte[] data)
    {
        BitArray bits = new BitArray(data);
        char[] result = new char[bits.Length];

        for (int i = 0; i < bits.Length; i++)
        {
            result[i] = bits[i] ? '1' : '0';
        }

        return new string(result);
    }

    public float DecodePackedPosition(byte low, byte high)
    {
        ushort raw = (ushort)((high << 8) | low); // Little-endian
        short signed = unchecked((short)raw);     // Interpret as signed

        return signed / 8.0f; // 2^3 = 8 → divide to get float
    }

    public static void WriteUShortToSpan(Span<byte> target, int offset, ushort value)
    {
        if (target.Length < offset + 2)
            throw new ArgumentOutOfRangeException(nameof(offset), "Not enough space in target span to write ushort.");

        byte[] bytes = BitConverter.GetBytes(value); // Little-endian by default
        target[offset] = bytes[0];
        target[offset + 1] = bytes[1];
    }
    public byte[]? TryModifyRecvFromData(ReadOnlySpan<byte> data)
    {

        if (data.Length <= 6) return null;

        if ( (data[4] == 0x6A && data[5] == 0x27))
        {
            if (_self.ClientConfig.Features.IsFeatureEnable(FeatureName.SuckStarOverChina))
            {
                var modified = data.ToArray(); // make a writable copy

                //WriteUShortToSpan(modified, 19, (ushort)_self.PersonInfo.X);
                //WriteUShortToSpan(modified, 21, (ushort)_self.PersonInfo.Y);
                //WriteUShortToSpan(modified, 23, (ushort)_self.PersonInfo.Z);
                for(int i = 19; i <= 24; i ++)
                {
                    modified[i] = TempLocationBytes[i-19];
                }
                return modified;
            }
        }
        else if (data[4] == 0x6D && data[5] ==0x27 )
        {
            //if(_self.ClientConfig.Features.IsFeatureEnable(FeatureName.SuckStarOverChina))
            //{

              var r = DoProcessFromMultiples(data);
            if (!r.isModified) return null;
            return r.output;
            //}
        }
            return null;
    }
    private byte[] TempLocationBytes = new byte[6];

public static bool ContainsLocationBytesSeq(ReadOnlySpan<byte> span)
{
    byte[] sequence = new byte[] { 0x6A, 0x27, 0x02, 0x00 };

    for (int i = 0; i <= span.Length - sequence.Length; i++)
    {
        if (span.Slice(i, sequence.Length).SequenceEqual(sequence))
        {
            return true;
        }
    }

    return false;
}
    public byte[]? TryModifySendToData(ReadOnlySpan<byte> data)
    {
        if (data.Length <= 6) return null;

        if ( (data[4] == 0x6A && data[5] == 0x27) )
        {
            TempLocationBytes[0] = data[19];
            TempLocationBytes[1] = data[20];
            TempLocationBytes[2] = data[21];
            TempLocationBytes[3] = data[22];
            TempLocationBytes[4] = data[23];
            TempLocationBytes[5] = data[24];
            
            if (!_self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsIllusion)) return null;

            var modified = new byte[data.Length];
            data.CopyTo(modified);
            modified[19] = modified[20] = modified[21] = modified[22] = modified[23] = modified[24] = 0xFF;
            
            return modified;
        }
        else if (data[4] == 0x6D && data[5] ==0x27)
        {
            var r = DoProcessFromMultiples(data,true);
            if (!r.isModified) return null;
            return r.output;
        }
        else if (data[4] == 0x55 && data[5] == 0x27)
        {
            if (!_self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsIllusion)) return null;

            var modified = new byte[data.Length];
            data.CopyTo(modified);
            for (int i = 16; i <= 23; i++)
                modified[i] = 0xFF;

            return modified;
        }

        return null;
    }

    (byte[] output, bool isModified) DoProcessFromMultiples(ReadOnlySpan<byte> data , bool isSend = false)
    {
        if (data.Length < 8 || data[4] != 0x6D || data[5] != 0x27)
        {
            return (null, false);
        }

        // Allocate one buffer for the result
        byte[] buffer = new byte[data.Length];
        data.CopyTo(buffer);
        Span<byte> bufferSpan = buffer;

        byte total = bufferSpan[6];
        int offset = 8;
        bool modified = false;

        for (int i = 0; i < total; i++)
        {
            if (offset >= bufferSpan.Length) break;

            byte size = bufferSpan[offset];
            offset++;

            if (offset + size > bufferSpan.Length) break;

            // In-place modification of the entry within the buffer
            Span<byte> entry = bufferSpan.Slice(offset, size);
            offset += size;

            ProcessEntry(entry, ref modified, isSend);
        }

        if (!modified) return (null, false);

        return (buffer, true);
    }

    void ProcessEntry(Span<byte> entry, ref bool modified, bool isSend)
    {
        if (entry.Length >= 2 && entry[0] == 0x6A && entry[1] == 0x27 && entry.Length >= 6)
        {
            if (isSend)
            {
                entry[^6..].CopyTo(TempLocationBytes);
            }        

            if (!_self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsIllusion)) return;
            if (!_self.ClientConfig.Features.IsFeatureEnable(FeatureName.SuckStarOverChina)) return;
            
            TempLocationBytes.CopyTo(entry[^6..]);
            modified = true;
        }
    }
}
