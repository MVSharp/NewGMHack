using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NewGMHack.Stub.Services;

namespace NewGMHack.Stub.PacketStructs;

/// <summary>
/// Zero-allocation enumerator for method packets.
/// </summary>
public ref struct MethodPacketEnumerator
{
    private ReadOnlySpan<byte> _remaining;
    private MethodPacket _current;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal MethodPacketEnumerator(ReadOnlySpan<byte> input)
    {
        _remaining = input;
        _current = default;
    }

    public MethodPacketEnumerator GetEnumerator() => this;

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool MoveNext()
    {
        if (_remaining.Length < 6) return false; // Min: length(2) + F0-03(2) + method(2)

        // Find separator using SIMD-optimized search
        int sepIdx = BuffSplitter.FindSeparatorStatic(_remaining);
        if (sepIdx < 0) return false;

        // Validate header
        if (sepIdx < 2) return false;

        // Read header using MemoryMarshal
        short length = MemoryMarshal.Read<short>(_remaining.Slice(sepIdx));
        short method = MemoryMarshal.Read<short>(_remaining.Slice(sepIdx + 2));

        // Find body extent
        int bodyStart = sepIdx + 4; // Skip: 2 bytes before sep + sep itself
        var bodySpan = _remaining.Slice(bodyStart);

        int nextSep = BuffSplitter.FindSeparatorStatic(bodySpan);
        ReadOnlySpan<byte> body;

        if (nextSep < 0)
        {
            // Rest is body
            body = bodySpan;
            _remaining = default;
        }
        else
        {
            // Body ends 2 bytes before next separator
            int bodyLen = Math.Max(0, nextSep - 2);
            body = bodySpan.Slice(0, bodyLen);
            _remaining = bodySpan.Slice(bodyLen);
        }

        _current = new MethodPacket(length, method, body);
        return true;
    }

    public readonly MethodPacket Current => _current;
}
