using System.Runtime.CompilerServices;

namespace NewGMHack.Stub.PacketStructs;

/// <summary>
/// Zero-allocation representation of a method packet.
/// MethodBody is a ReadOnlySpan pointing into original buffer.
/// </summary>
public readonly ref struct MethodPacket
{
    public readonly short Length;
    public readonly short Method;
    public readonly ReadOnlySpan<byte> MethodBody;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MethodPacket(short length, short method, ReadOnlySpan<byte> methodBody)
    {
        Length = length;
        Method = method;
        MethodBody = methodBody;
    }
}
