using NewGMHack.Stub.PacketStructs;

namespace NewGMHack.Stub.Services;

public interface IBuffSplitter
{
    List<PacketSegment> Split(ReadOnlySpan<byte> input);

    /// <summary>
    /// Returns zero-allocation enumerator over method packets.
    /// </summary>
    MethodPacketEnumerator EnumeratePackets(ReadOnlySpan<byte> input);
}