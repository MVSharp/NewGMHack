using System.Buffers;
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
    private readonly ILogger<PacketDataModifier> _logger;  // Optional, inject if needed for modifier-specific logs

    public PacketDataModifier(SelfInformation self, IBuffSplitter splitter, ILogger<PacketDataModifier> logger = null)
    {
        _self = self;
        _splitter = splitter;
        _logger = logger;
    }

    /// <summary>
    /// Attempts to modify send data. Returns a new byte array if modified, or null if no change.
    /// </summary>
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
        byte[] modified;

        switch (method)
        {
            case 1335:  // Normal damage
                var attack = raw.ReadStruct<Attack1335>();
                var targets = raw.SliceAfter<Attack1335>().CastTo<TargetData>();
                if (attack.PlayerId != _self.PersonInfo.PersonId) return null;

                for (var i = 0; i < targets.Length; i++)
                {
                    targets[i].Damage = ushort.MaxValue;
                }

                modified = ArrayPool<byte>.Shared.Rent(attack.ToByteArray().Length + targets.AsByteSpan().Length);
                try
                {
                    attack.ToByteArray().AsSpan().CopyTo(modified);
                    targets.AsByteSpan().CopyTo(modified.AsSpan(attack.ToByteArray().Length));
                    _logger?.ZLogInformation($"{BitConverter.ToString(modified.AsSpan(0, modified.Length).ToArray())}");
                    return modified;
                }
                finally
                {
                    // Caller must return the array to pool after use
                }

            case 1486:  // Item/bucket damage
                var attack1 = raw.ReadStruct<Attack1486>();
                var targets1 = raw.SliceAfter<Attack1486>().CastTo<TargetData>();
                if (attack1.PlayerId != _self.PersonInfo.PersonId) return null;

                for (var i = 0; i < targets1.Length; i++)
                {
                    targets1[i].Damage = ushort.MaxValue;
                }

                modified = ArrayPool<byte>.Shared.Rent(attack1.ToByteArray().Length + targets1.AsByteSpan().Length);
                try
                {
                    attack1.ToByteArray().AsSpan().CopyTo(modified);
                    targets1.AsByteSpan().CopyTo(modified.AsSpan(attack1.ToByteArray().Length));
                    _logger?.ZLogInformation($"{BitConverter.ToString(modified.AsSpan(0, modified.Length).ToArray())}");
                    return modified;
                }
                finally
                {
                    // Caller must return to pool
                }

            case 1538:
                var buf = result.MethodBody.ToArray();  // Copy to modify
                buf[46] = 0xFF;
                buf[47] = 0xFF;
                modified = ArrayPool<byte>.Shared.Rent(data[0..6].Length + buf.Length);
                try
                {
                    data[0..6].CopyTo(modified);
                    buf.CopyTo(modified.AsSpan(data[0..6].Length));
                    return modified;
                }
                finally
                {
                    // Caller must return to pool
                }

            default:
                return null;
        }
    }

    /// <summary>
    /// Attempts to modify sendto data. Returns a new byte array if modified, or null if no change.
    /// </summary>
    public byte[]? TryModifySendToData(ReadOnlySpan<byte> data)
    {
        if (data.Length <= 6) return null;

        if (data[4] == 0x6A && data[5] == 0x27)
        {
            _self.PersonInfo.X = BitConverter.ToInt16([data[20], data[19]], 0);
            _self.PersonInfo.Y = BitConverter.ToInt16([data[22], data[21]], 0);
            _self.PersonInfo.Z = BitConverter.ToInt16([data[24], data[23]], 0);

            if (!_self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsIllusion)) return null;

            var modified = ArrayPool<byte>.Shared.Rent(data.Length);
            data.CopyTo(modified);
            modified[19] = 0xFF;
            modified[20] = 0xFF;
            modified[21] = 0xFF;
            modified[22] = 0xFF;
            modified[23] = 0xFF;
            modified[24] = 0xFF;
            return modified;  // Caller returns to pool
        }
        else if (data[4] == 0x55 && data[5] == 0x27)
        {
            var modified = ArrayPool<byte>.Shared.Rent(data.Length);
            data.CopyTo(modified);
            for (int i = 16; i <= 23; i++)
            {
                modified[i] = 0xFF;
            }
            return modified;  // Caller returns to pool
        }

        return null;
    }
}