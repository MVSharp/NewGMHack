using System.Runtime.CompilerServices;

namespace NewGMHack.Stub.PacketStructs;

/// <summary>
/// Zero-allocation enumerator for complete packets from PacketAccumulator.
/// Yields ReadOnlySpan<byte> pointing into accumulator's buffer (snapshot before compaction).
/// </summary>
/// <remarks>
/// <para>Performance-critical enumerator optimized for 10k+ packets/second throughput.</para>
/// <para><strong>IMPORTANT:</strong> Caller must follow standard enumerator pattern:
/// <code>
/// foreach (ReadOnlySpan<byte> packet in enumerator) { ... }
/// // OR manual pattern:
/// while (enumerator.MoveNext()) {
///     ReadOnlySpan<byte> packet = enumerator.Current; // Safe here
/// }
/// </code>
/// </para>
/// <para><strong>WARNING:</strong> Accessing <see cref="Current"/> before first <see cref="MoveNext()"/> call,
/// or after <see cref="MoveNext()"/> returns false, will cause <see cref="IndexOutOfRangeException"/>.
/// This design choice eliminates bounds-checking overhead in the hot path.</para>
/// </remarks>
public ref struct PacketRefEnumerator
{
    private readonly ReadOnlySpan<byte> _completePacketsSpan;
    private readonly int[] _offsets;
    private readonly int _count;
    private int _index;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PacketRefEnumerator(ReadOnlySpan<byte> completePacketsSpan, int[] offsets, int count)
    {
        _completePacketsSpan = completePacketsSpan;
        _offsets = offsets;
        _count = count;
        _index = -1;
    }

    public PacketRefEnumerator GetEnumerator() => this;

    /// <summary>
    /// Advances the enumerator to the next packet.
    /// </summary>
    /// <returns>true if there is another packet; false if enumeration is complete.</returns>
    /// <remarks>
    /// Must be called before accessing <see cref="Current"/>.
    /// Returns false when _index reaches _count, signaling end of enumeration.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool MoveNext()
    {
        _index++;
        return _index < _count;
    }

    /// <summary>
    /// Gets the current packet as a ReadOnlySpan pointing into the buffer snapshot.
    /// </summary>
    /// <exception cref="IndexOutOfRangeException">
    /// Thrown if accessed before first MoveNext() call or after MoveNext() returns false.
    /// This is intentional for performance - bounds checking is omitted from the hot path.
    /// </exception>
    /// <remarks>
    /// Always call MoveNext() and verify it returns true before accessing this property.
    /// The span returned is valid only for the lifetime of this enumerator.
    /// </remarks>
    public readonly ReadOnlySpan<byte> Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            int start = _offsets[_index * 2];
            int end = _offsets[_index * 2 + 1];
            return _completePacketsSpan.Slice(start, end - start);
        }
    }
}
