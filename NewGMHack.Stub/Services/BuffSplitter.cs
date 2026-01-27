using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NewGMHack.Stub.PacketStructs;
using ZLogger;

namespace NewGMHack.Stub.Services;

/// <summary>
/// High-performance buffer splitter for parsing packet segments.
/// Leverages .NET 10 APIs: SearchValues for SIMD search, MemoryMarshal for zero-copy reads.
/// </summary>
public sealed class BuffSplitter(ILogger<BuffSplitter> logger) : IBuffSplitter
{
    // .NET 10: SearchValues provides SIMD-vectorized multi-byte pattern search
    private static readonly SearchValues<byte> SeparatorSearch = SearchValues.Create([0xF0]);
    private static ReadOnlySpan<byte> Separator => [0xF0, 0x03];
    
    private const int HeaderSize = 4; // 2 bytes length + 2 bytes method
    private const int FooterSize = 2; // 2 bytes before next separator belong to current segment

    /// <summary>
    /// Splits the input buffer into packet segments based on the 0xF0 0x03 separator.
    /// Returns a list for interface compatibility. For zero-allocation scenarios, use <see cref="Enumerate"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public List<PacketSegment> Split(ReadOnlySpan<byte> input)
    {
        // Fast path: empty or too small input
        if (input.Length < HeaderSize)
        {
            return [];
        }

        List<PacketSegment>? segments = null;

        try
        {
            foreach (var rawSegment in Enumerate(input))
            {
                segments ??= new List<PacketSegment>(4);
                segments.Add(new PacketSegment(
                    rawSegment.Length,
                    rawSegment.Method,
                    rawSegment.Body.ToArray()
                ));
            }

            return segments ?? [];
        }
        catch (Exception ex)
        {
            logger.ZLogWarning($"BuffSplitter error: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Zero-allocation enumeration of packet segments.
    /// Use this for hot paths where you don't need to store the segments.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PacketEnumerator Enumerate(ReadOnlySpan<byte> input) => new(input);

    /// <summary>
    /// Zero-allocation enumeration of method packets.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MethodPacketEnumerator EnumeratePackets(ReadOnlySpan<byte> input) => new(input);

    /// <summary>
    /// Finds the separator (0xF0 0x03) using .NET 10 SIMD-optimized SearchValues.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindSeparator(ReadOnlySpan<byte> span)
    {
        int searchStart = 0;

        while (searchStart < span.Length)
        {
            // .NET 10: IndexOfAny with SearchValues uses SIMD/AVX2/AVX-512
            int idx = span[searchStart..].IndexOfAny(SeparatorSearch);
            if (idx < 0) return -1;

            int actualIndex = searchStart + idx;

            // Verify the second byte of separator
            if (actualIndex + 1 < span.Length && span[actualIndex + 1] == 0x03)
            {
                return actualIndex;
            }

            searchStart = actualIndex + 1;
        }

        return -1;
    }

    /// <summary>
    /// Public static version for MethodPacketEnumerator to use.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FindSeparatorStatic(ReadOnlySpan<byte> span)
    {
        return FindSeparator(span);
    }

    /// <summary>
    /// A ref struct enumerator for zero-allocation segment iteration.
    /// Compatible with foreach via duck typing.
    /// </summary>
    public ref struct PacketEnumerator
    {
        private ReadOnlySpan<byte> _remaining;
        private RawPacketSegment _current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PacketEnumerator(ReadOnlySpan<byte> input)
        {
            _remaining = input;
            _current = default;
        }

        public readonly RawPacketSegment Current => _current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly PacketEnumerator GetEnumerator() => this;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public bool MoveNext()
        {
            if (_remaining.Length < HeaderSize)
                return false;

            // Find separator using SIMD-optimized search
            int separatorOffset = FindSeparator(_remaining);
            if (separatorOffset < 0)
                return false;

            // Validate header space
            if (separatorOffset + HeaderSize > _remaining.Length)
                return false;

            // .NET 10: MemoryMarshal.Read for zero-copy struct reads
            // Read length and method as a single ushort pair for efficiency
            short length = MemoryMarshal.Read<short>(_remaining[separatorOffset..]);
            short method = MemoryMarshal.Read<short>(_remaining[(separatorOffset + 2)..]);

            int bodyStart = separatorOffset + HeaderSize;
            var bodySpan = _remaining[bodyStart..];

            // Find next separator
            int nextSeparatorOffset = FindSeparator(bodySpan);

            ReadOnlySpan<byte> body;
            if (nextSeparatorOffset < 0)
            {
                // No more separators - rest is body
                body = bodySpan;
                _remaining = [];
            }
            else
            {
                // Body ends 2 bytes before next separator
                int bodyLength = Math.Max(0, nextSeparatorOffset - FooterSize);
                body = bodySpan[..bodyLength];
                _remaining = bodySpan[bodyLength..];
            }

            _current = new RawPacketSegment(length, method, body);
            return true;
        }
    }

    /// <summary>
    /// A zero-allocation packet segment representation using ReadOnlySpan.
    /// </summary>
    public readonly ref struct RawPacketSegment
    {
        public readonly short Length;
        public readonly short Method;
        public readonly ReadOnlySpan<byte> Body;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RawPacketSegment(short length, short method, ReadOnlySpan<byte> body)
        {
            Length = length;
            Method = method;
            Body = body;
        }

        /// <summary>
        /// Creates a heap-allocated PacketSegment from this ref struct.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PacketSegment ToPacketSegment() => new(Length, Method, Body.ToArray());
    }
}