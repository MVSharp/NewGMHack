using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NewGMHack.Stub.PacketStructs;

namespace NewGMHack.Stub.Services;

/// <summary>
/// High-performance TCP packet accumulator using .NET 10 APIs.
/// Reassembles fragmented recv data based on length prefix.
/// Packet format: [Length:uint16] [F0-03:2 bytes] [Method:uint16] [Body:variable]
/// </summary>
public sealed class PacketAccumulator : IPacketAccumulator
{
    private const int MinPacketSize = 6;       // Minimum: length(2) + separator(2) + method(2)
    private const byte SeparatorByte1 = 0xF0;
    private const byte SeparatorByte2 = 0x03;

    // .NET 10: SearchValues for SIMD-optimized byte search
    private static readonly SearchValues<byte> SeparatorSearch = SearchValues.Create([SeparatorByte1]);

    private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Shared;
    private static readonly ArrayPool<int> OffsetPool = ArrayPool<int>.Shared;
    private const int MaxPacketsPerRecv = 32; // Offset buffer size = MaxPacketsPerRecv * 2

    // Use ArrayPool for buffer management to reduce GC pressure
    private byte[] _buffer;
    private int _position;
    private readonly Lock _lock = new();  // .NET 10: Lock type for better performance than object

    public PacketAccumulator(int initialCapacity = 8192)
    {
        _buffer = BufferPool.Rent(initialCapacity);
        _position = 0;
    }

    /// <summary>
    /// Appends raw recv data (after skipping protocol header) and extracts complete packets.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public List<byte[]> AppendAndExtract(ReadOnlySpan<byte> rawRecvData)
    {
        if (rawRecvData.IsEmpty)
            return [];

        lock (_lock)
        {
            EnsureCapacity(rawRecvData.Length);
            rawRecvData.CopyTo(_buffer.AsSpan(_position));
            _position += rawRecvData.Length;

            return ExtractCompletePackets();
        }
    }

    /// <summary>
    /// Appends raw recv data (after skipping protocol header) and extracts complete packets.
    /// Zero-allocation version using ArrayPool and ref struct enumerator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public PacketRefEnumerator AppendAndGetPackets(ReadOnlySpan<byte> rawRecvData)
    {
        if (rawRecvData.IsEmpty)
            return default;

        lock (_lock)
        {
            EnsureCapacity(rawRecvData.Length);
            rawRecvData.CopyTo(_buffer.AsSpan(_position));
            _position += rawRecvData.Length;

            return ExtractCompletePacketsV2();
        }
    }

    /// <summary>
    /// Extracts all complete packets from the buffer based on length prefix.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private List<byte[]> ExtractCompletePackets()
    {
        List<byte[]>? results = null;
        var bufferSpan = _buffer.AsSpan(0, _position);
        
        // HEURISTIC: per user request, if we have exactly ONE separator, take the whole buffer
        // This handles cases where Length header is unreliable (e.g. 1093 vs 1097)
        int firstSep = FindSeparator(bufferSpan);
        if (firstSep >= 2)
        {
             // Check for a second separator
             int searchNext = firstSep + 2;
             bool hasSecond = false;
             if (searchNext < _position)
             {
                 if (FindSeparator(bufferSpan[searchNext..]) >= 0) hasSecond = true;
             }
             //WARNING this case happen because when header length is in fact incorrect , cuz the server maintainers suck as fuck , forgot update the packet length
             if (!hasSecond)
             {
                 // Single packet case - Consume ALL
                 int packetStart = firstSep - 2;
                 int packetLen = _position - packetStart;
                 
                 byte[] packet = GC.AllocateUninitializedArray<byte>(packetLen);
                 bufferSpan.Slice(packetStart, packetLen).CopyTo(packet);
                 
                 _position = 0; // Fully consumed
                 return [packet];
             }
        }

        int consumed = 0;

        while (consumed < _position)
        {
            var remaining = bufferSpan[consumed..];

            // Find next F0-03 separator using SIMD search
            int separatorOffset = FindSeparator(remaining);
            if (separatorOffset < 0)
                break;

            // Need at least 2 bytes before separator for length prefix
            if (separatorOffset < 2)
            {
                consumed++;
                continue;
            }

            // Read length prefix using MemoryMarshal for zero-copy
            int lengthPrefixOffset = separatorOffset - 2;
            ushort packetLength = MemoryMarshal.Read<ushort>(remaining[lengthPrefixOffset..]);

            // Validate length
            if (packetLength < MinPacketSize || packetLength > 65535)
            {
                consumed += separatorOffset + 2;
                continue;
            }

            // Calculate full packet bounds
            int packetStart = consumed + lengthPrefixOffset;
            int packetEnd = packetStart + packetLength;

            if (packetEnd > _position)
                break; // Incomplete packet

            // Extract complete packet
            results ??= new(4);
            byte[] packet = GC.AllocateUninitializedArray<byte>(packetLength);
            bufferSpan.Slice(packetStart, packetLength).CopyTo(packet);
            results.Add(packet);

            consumed = packetEnd;
        }

        // Compact buffer
        if (consumed > 0)
        {
            if (consumed < _position)
            {
                _buffer.AsSpan(consumed, _position - consumed).CopyTo(_buffer);
                _position -= consumed;
            }
            else
            {
                _position = 0;
            }
        }

        return results ?? [];
    }

    /// <summary>
    /// Extracts all complete packets from the buffer based on length prefix.
    /// Zero-allocation version using offset tracking and ref struct enumerator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private PacketRefEnumerator ExtractCompletePacketsV2()
    {
        // Rent offset buffer
        var offsets = OffsetPool.Rent(MaxPacketsPerRecv * 2);
        int packetCount = 0;

        var bufferSpan = _buffer.AsSpan(0, _position);

        // HEURISTIC: Single separator case (identical logic to original)
        int firstSep = FindSeparator(bufferSpan);
        if (firstSep >= 2)
        {
            int searchNext = firstSep + 2;
            bool hasSecond = false;
            if (searchNext < _position)
            {
                if (FindSeparator(bufferSpan[searchNext..]) >= 0) hasSecond = true;
            }

            if (!hasSecond)
            {
                // Single complete packet - consume ALL
                int packetStart = firstSep - 2;
                offsets[0] = packetStart;
                offsets[1] = _position;
                packetCount = 1;

                // Create snapshot BEFORE clearing buffer
                var snapshot = new PacketRefEnumerator(bufferSpan, offsets, 1);
                _position = 0;

                // Note: offsets rented but will be "leaked" - this is OK because
                // enumerator is consumed immediately in RecvHook before next call
                return snapshot;
            }
        }

        int consumed = 0;

        while (consumed < _position && packetCount < MaxPacketsPerRecv)
        {
            var remaining = bufferSpan[consumed..];
            int separatorOffset = FindSeparator(remaining);
            if (separatorOffset < 0) break;
            if (separatorOffset < 2) { consumed++; continue; }

            int lengthPrefixOffset = separatorOffset - 2;
            ushort packetLength = MemoryMarshal.Read<ushort>(remaining[lengthPrefixOffset..]);

            if (packetLength < MinPacketSize || packetLength > 65535)
            {
                consumed += separatorOffset + 2;
                continue;
            }

            int packetStart = consumed + lengthPrefixOffset;
            int packetEnd = packetStart + packetLength;

            if (packetEnd > _position) break; // Incomplete packet

            // Store offset
            int idx = packetCount * 2;
            offsets[idx] = packetStart;
            offsets[idx + 1] = packetEnd;
            packetCount++;

            consumed = packetEnd;
        }

        // Create snapshot BEFORE compacting
        var result = new PacketRefEnumerator(bufferSpan, offsets, packetCount);

        // Compact buffer (identical logic to original)
        if (consumed > 0)
        {
            if (consumed < _position)
            {
                _buffer.AsSpan(consumed, _position - consumed).CopyTo(_buffer);
                _position -= consumed;
            }
            else
            {
                _position = 0;
            }
        }

        return result;
    }

    /// <summary>
    /// Finds F0-03 separator using SIMD-optimized SearchValues.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindSeparator(ReadOnlySpan<byte> span)
    {
        int searchStart = 0;
        while (searchStart < span.Length - 1)
        {
            // .NET 10: IndexOfAny with SearchValues uses SIMD/AVX2/AVX-512
            int idx = span[searchStart..].IndexOfAny(SeparatorSearch);
            if (idx < 0) return -1;

            int actualIndex = searchStart + idx;
            if (actualIndex + 1 < span.Length && span[actualIndex + 1] == SeparatorByte2)
                return actualIndex;

            searchStart = actualIndex + 1;
        }
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int additionalBytes)
    {
        int required = _position + additionalBytes;
        if (required > _buffer.Length)
        {
            int newSize = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(_buffer.Length * 2, required));

            // Return old buffer to pool
            var oldBuffer = _buffer;
            _buffer = BufferPool.Rent(newSize);
            oldBuffer.AsSpan(0, _position).CopyTo(_buffer);
            BufferPool.Return(oldBuffer);
        }
    }

    public void Clear()
    {
        lock (_lock) { _position = 0; }
    }

    public int BufferedLength => _position;
}
