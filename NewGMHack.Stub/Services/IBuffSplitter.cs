using NewGMHack.Stub.PacketStructs;

namespace NewGMHack.Stub.Services;

public interface IBuffSplitter
{
    List<PacketSegment> Split(ReadOnlySpan<byte> input);
}