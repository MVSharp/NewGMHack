using System.Runtime.InteropServices;

namespace NewGMHack.Stub.PacketStructs;

/// <summary>
/// Argument struct for injection.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Argument
{
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    public IntPtr Title;
    public IntPtr Text;
    public IntPtr ChannelName;
#pragma warning restore CS0649
}

