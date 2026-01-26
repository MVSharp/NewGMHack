using System.Runtime.CompilerServices;

namespace NewGMHack.Stub.PacketStructs;

/// <summary>
/// Zero-allocation enumerator for complete packets from PacketAccumulator.
/// Yields ReadOnlySpan<byte> pointing into accumulator's buffer (snapshot before compaction).
/// </summary>
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

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool MoveNext()
    {
        _index++;
        return _index < _count;
    }

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
