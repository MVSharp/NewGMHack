using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NewGMHack.Stub;
using NewGMHack.Stub.PacketStructs;
using NewGMHack.Stub.PacketStructs.Send;
using NewGMHack.Stub.Services;
using Reloaded.Memory.Extensions;
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

    public byte[]? TryModifySendData(ReadOnlySpan<byte> data)
    {
        if (data.Length <= 6) return null;

        if (!_self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsMissionBomb) &&
            !_self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsPlayerBomb))
        {
            return null;
        }

        var result = _splitter.Split(data).AsValueEnumerable().FirstOrDefault();
        if (result == null) return null;

        var method = result.Method;
        var raw = data[..6].CombineWith(result.MethodBody.AsSpan());

        switch (method)
        {
            case 1335:
            {
                var attack = raw.ReadStruct<Attack1335>();
                var targets = raw.SliceAfter<Attack1335>().CastTo<TargetData>();
                if (attack.PlayerId != _self.PersonInfo.PersonId) return null;

                for (int i = 0; i < targets.Length; i++)
                    targets[i].Damage = ushort.MaxValue;

                var attackBytes = attack.ToByteArray();
                var targetsBytes = targets.AsByteSpan();
                var modified = new byte[attackBytes.Length + targetsBytes.Length];

                attackBytes.CopyTo(modified.AsSpan(0, attackBytes.Length));
                targetsBytes.CopyTo(modified.AsSpan(attackBytes.Length));

                    _logger?.ZLogInformation($"Modified 1335: {BitConverter.ToString(modified)}");
                    return modified;
            }

            case 1486:
            {
                var attack = raw.ReadStruct<Attack1486>();
                var targets = raw.SliceAfter<Attack1486>().CastTo<TargetData>();
                if (attack.PlayerId != _self.PersonInfo.PersonId) return null;

                for (int i = 0; i < targets.Length; i++)
                    targets[i].Damage = ushort.MaxValue;

                var attackBytes = attack.ToByteArray();
                var targetsBytes = targets.AsByteSpan();
                var modified = new byte[attackBytes.Length + targetsBytes.Length];

                attackBytes.CopyTo(modified.AsSpan(0, attackBytes.Length));
                targetsBytes.CopyTo(modified.AsSpan(attackBytes.Length));

                _logger?.ZLogInformation($"Modified 1486: {BitConverter.ToString(modified)}");
                return modified;
            }

            case 1538:
            {
                var buf = result.MethodBody.ToArray();
                buf[46] = 0xFF;
                buf[47] = 0xFF;

                var modified = new byte[6 + buf.Length];
                data[0..6].CopyTo(modified.AsSpan(0, 6));
                buf.CopyTo(modified.AsSpan(6));

                _logger?.ZLogInformation($"Modified 1538: {BitConverter.ToString(modified)}");
                return modified;
            }
            default:
                return null;
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
        else if (data[4] == 0x6D && data[5] ==0x27)
        {
              var r = DoProcessFromMultiples(data);
            if (!r.isModified) return null;
            return r.output;
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
            //_self.PersonInfo.X = DecodePackedPosition(data[19], data[20] ) /10;
            //_self.PersonInfo.Y = DecodePackedPosition(data[21], data[22])  /10;
            //_self.PersonInfo.Z = DecodePackedPosition(data[23], data[24] ) /10;
            var buf = data.ToArray();
            ushort rawX = (ushort)((data[20] << 8) | data[19]);
            ushort rawY = (ushort)((data[22] << 8) | data[21]);
            ushort rawZ = (ushort)((data[24] << 8) | data[23]);

            // Split into bitfields: upper 6 bits = coarse, lower 10 bits = fine
            int coarseX = (rawX >> 10) & 0x3F;
            int fineX   = rawX & 0x03FF;

            int coarseY = (rawY >> 10) & 0x3F;
            int fineY   = rawY & 0x03FF;

            int coarseZ = (rawZ >> 10) & 0x3F;
            int fineZ   = rawZ & 0x03FF;

            // Apply scale factors — tweak these based on your world units
            float coarseScale = 128.0f;   // each coarse unit = 128 world units
            float fineScale   = 0.125f;   // each fine unit = 0.125 world units

            // Final decoded positions
            _self.PersonInfo.X = coarseX * coarseScale + fineX * fineScale;
            _self.PersonInfo.Y = coarseY * coarseScale + fineY * fineScale;
            _self.PersonInfo.Z = coarseZ * coarseScale + fineZ * fineScale;       
            TempLocationBytes[0] = data[19];
            TempLocationBytes[1] = data[20];
            TempLocationBytes[2] = data[21];
            TempLocationBytes[3] = data[22];
            TempLocationBytes[4] = data[23];
            TempLocationBytes[5] = data[24];
            //_logger.ZLogInformation(
            //    $"x: 0x{data[20]:X2} 0x{data[19]:X2} | bits: {GetBitString(new byte[] { data[20], data[19] })}\n" +
            //    $"y: 0x{data[22]:X2} 0x{data[21]:X2} | bits: {GetBitString(new byte[] { data[22], data[21] })}\n" +
            //    $"z: 0x{data[24]:X2} 0x{data[23]:X2} | bits: {GetBitString(new byte[] { data[24], data[23] })}"
            //);
            if (!_self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsIllusion)) return null;

           // _logger?.ZLogInformation($"Update Location");

            var modified = data.ToArray();
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

            var modified = data.ToArray();
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
        return (data.ToArray(), false);
    }

    byte total = data[6];
    int offset = 8;
    List<byte> output = new List<byte>();
    bool modified = false;

    output.AddRange(data.Slice(0, offset).ToArray());

    for (int i = 0; i < total; i++)
    {
        if (offset >= data.Length)
        {
            break;
        }

        byte size = data[offset];
        offset++;

        if (offset + size > data.Length)
        {
            break;
        }

        Span<byte> entry = data.Slice(offset, size).ToArray();
        offset += size;

        var processed = ProcessEntry(entry, ref modified,isSend);
        output.Add((byte)processed.Length);
        output.AddRange(processed);
    }

    return (output.ToArray(), modified);
}

Span<byte> ProcessEntry(Span<byte> entry, ref bool modified,bool isSend)
{
    if (entry.Length >= 2 && entry[0] == 0x6A && entry[1] == 0x27 && entry.Length >= 6)
    {
            if (isSend)
            {
                entry[^6..].CopyTo(TempLocationBytes);
            }        
        TempLocationBytes.CopyTo(entry[^6..]);
        modified = true;
    }
    return entry;
}
}
