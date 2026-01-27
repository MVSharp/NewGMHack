using System.Buffers;
using InjectDotnet.NativeHelper;
using Microsoft.Extensions.Logging;
using NewGMHack.Stub;
using NewGMHack.Stub.Hooks;
using NewGMHack.Stub.Services;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X86;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using ZLogger;

public sealed class WinsockHookManager(
    ILogger<WinsockHookManager> logger,
    Channel<PacketContext>      channel,
    SelfInformation             self,
    IBuffSplitter               splitter,
    PacketDataModifier          modifier,
    IReloadedHooks              hooksEngine,
    IPacketAccumulator          accumulator
) : IHookManager
{
    private IHook<SendDelegate>?     _sendHook;
    private IHook<RecvDelegate>?     _recvHook;
    private IHook<SendToDelegate>?   _sendToHook;
    private IHook<RecvFromDelegate>? _recvFromHook;

    private SendDelegate?     _originalSend;
    private RecvDelegate?     _originalRecv;
    private SendToDelegate?   _originalSendTo;
    private RecvFromDelegate? _originalRecvFrom;

    public static IntPtr _lastSocket;

    /// <summary>
    /// Socket for injecting fake recv via send (local 4000-7000, remote 10000-65535)
    /// </summary>
    public static IntPtr _loopbackRecvSocket;

    public void HookAll()
    {
        logger.LogInformation($"Initializing Winsock hooks using Reloaded.Hooks");

        HookFunction("ws2_32.dll", "send",   new SendDelegate(SendHook),     out _sendHook,   out _originalSend);
        HookFunction("ws2_32.dll", "recv",   new RecvDelegate(RecvHook),     out _recvHook,   out _originalRecv);
        HookFunction("ws2_32.dll", "sendto", new SendToDelegate(SendToHook), out _sendToHook, out _originalSendTo);
        HookFunction("ws2_32.dll", "recvfrom", new RecvFromDelegate(RecvFromHook), out _recvFromHook,
                     out _originalRecvFrom);

        logger.LogInformation($"Winsock hooks installed: send, recv, sendto, recvfrom");
    }

    public void UnHookAll()
    {
        _sendHook?.Disable();
        _recvHook?.Disable();
        _sendToHook?.Disable();
        _recvFromHook?.Disable();
        logger.LogInformation($"Winsock hooks disabled");
    }

    private void HookFunction<T>(string dllName, string functionName, T hookDelegate, out IHook<T>? hook,
                                 out T? original) where T : Delegate
    {
        hook     = null;
        original = null;

        try
        {
            var functionPtr = NativeLibrary.GetExport(NativeLibrary.Load(dllName), functionName);
            hook = hooksEngine.CreateHook(hookDelegate, functionPtr);
            hook.Activate();
            hook.Enable();

            original = hook.OriginalFunction;
            logger.LogInformation($"Activated: {hook.IsHookActivated}| Enabled:{hook.IsHookEnabled} |Hooked {functionName} from {dllName} at 0x{functionPtr:X}");
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to hook {functionName}: {ex}");
        }
    }

    public static (string ip, int port)? GetRemoteAddress(nint socket)
    {
        SOCKADDR_IN addr = new SOCKADDR_IN { sin_zero = new byte[8] };
        int         size = Marshal.SizeOf(addr);
        if (getpeername(socket, ref addr, ref size) == 0)
        {
            byte[] ipBytes = BitConverter.GetBytes(addr.sin_addr);
            string ip      = string.Join(".", ipBytes);
            int    port    = ntohs(addr.sin_port);
            return (ip, port);
        }

        return null;
    }

    private bool IsLocalLoopback(nint socket, string startWith = "127.")
    {
        SOCKADDR_IN addr = new SOCKADDR_IN();
        addr.sin_zero = new byte[8];
        int size = Marshal.SizeOf(addr);
        if (getsockname(socket, ref addr, ref size) == 0)
        {
            byte[] ipBytes = BitConverter.GetBytes(addr.sin_addr);
            string ip      = string.Join(".", ipBytes);

            return ip.StartsWith(startWith);
        }

        return false;
    }

    private bool IsLocalLoopbackPort(nint socket, int portStart, int portEnd)
    {
        SOCKADDR_IN addr = new SOCKADDR_IN();
        addr.sin_zero = new byte[8];
        int size = Marshal.SizeOf(addr);
        if (getsockname(socket, ref addr, ref size) == 0)
        {
            ushort port = ntohs(addr.sin_port);
            return port >= portStart && port <= portEnd;
        }

        return false;
    }

    public static (string ip, int port)? GetLocalAddress(nint socket)
    {
        SOCKADDR_IN addr = new SOCKADDR_IN { sin_zero = new byte[8] };
        int         size = Marshal.SizeOf(addr);
        if (getsockname(socket, ref addr, ref size) == 0)
        {
            byte[] ipBytes = BitConverter.GetBytes(addr.sin_addr);
            string ip      = string.Join(".", ipBytes);
            int    port    = ntohs(addr.sin_port);
            return (ip, port);
        }

        return null;
    }

    //private unsafe int SendHook(nint socket, nint buffer, int length, int flags)
    //{
    // Optimized: Constants needed for checking without strings
    private const uint LoopbackMask  = 0x000000FF; // Check first byte (little endian: low byte is first)
    private const uint LoopbackValue = 0x7F;       // 127

    // Private ranges: 
    // 10.0.0.0/8     -> (ip & 0xFF) == 10
    // 172.16.0.0/12  -> (ip & 0xFF) == 172 && ... (simplification: just check 172)
    // 192.168.0.0/16 -> (ip & 0xFF) == 192 && ((ip >> 8) & 0xFF) == 168
    // Note: sin_addr is Network Byte Order (Big Endian). 
    // BUT we read it as uint on Little Endian. 
    // Byte 0 (lowest address) is the first octet.
    // So 'ip & 0xFF' gives the first octet.

    private bool IsPrivateIp(uint ip)
    {
        uint firstOctet = ip & 0xFF;        // First byte (lowest address)
        if (firstOctet == 127) return true; // Loopback
        if (firstOctet == 10) return true;
        if (firstOctet == 172) return true; // Broad check for 172.x
        if (firstOctet == 192)              // Check 192.168
        {
            uint secondOctet = (ip >> 8) & 0xFF;
            if (secondOctet == 168) return true;
        }

        return false;
    }

    private unsafe int SendHook(nint socket, nint buffer, int length, int flags)
    {
        // First check if this is the loopback recv socket (local 4000-7000, remote 10000-65535)
        // Save it for injecting fake recv data
        //SOCKADDR_IN checkLocalAddr = new SOCKADDR_IN();
        //int         checkAddrSize  = Marshal.SizeOf(checkLocalAddr);
        //if (getsockname(socket, ref checkLocalAddr, ref checkAddrSize) == 0)
        //{
        //    if ((checkLocalAddr.sin_addr & 0xFF) == 127)
        //    {
        //        ushort localPort = ntohs(checkLocalAddr.sin_port);
        //        if (localPort >= 4000 && localPort <= 7000)
        //        {
        //            SOCKADDR_IN checkRemoteAddr = new SOCKADDR_IN();
        //            checkAddrSize = Marshal.SizeOf(checkRemoteAddr);
        //            if (getpeername(socket, ref checkRemoteAddr, ref checkAddrSize) == 0)
        //            {
        //                if ((checkRemoteAddr.sin_addr & 0xFF) == 127)
        //                {
        //                    ushort remotePort = ntohs(checkRemoteAddr.sin_port);
        //                    if (remotePort >= 10000 && remotePort <= 65535)
        //                    {
        //                        _loopbackRecvSocket = socket;
        //                        return _originalSend!(socket, buffer, length, flags);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}

        // Avoid string allocations! Use cached structs methods or direct check.
        // Logic: 
        // 1. Check if Local is 127.x.x.x AND Port 40000-65535
        // 2. Check if Remote is 127.x.x.x AND Port 4000-7000

        // Optimize: we really only need to block/allow specific traffic.
        // Original logic: "if !isValid return original".

        SOCKADDR_IN localAddr    = new SOCKADDR_IN();
        int         addrSize     = Marshal.SizeOf(localAddr);
        bool        isLocalValid = false;

        if (getsockname(socket, ref localAddr, ref addrSize) == 0)
        {
            // Check IP starts with 127.
            if ((localAddr.sin_addr & 0xFF) == 127)
            {
                ushort port                                      = ntohs(localAddr.sin_port);
                if (port >= 10000 && port <= 65535) isLocalValid = true;
            }
        }

        if (!isLocalValid) return _originalSend!(socket, buffer, length, flags);

        SOCKADDR_IN remoteAddr = new SOCKADDR_IN();
        addrSize = Marshal.SizeOf(remoteAddr);
        bool isRemoteValid = false;

        if (getpeername(socket, ref remoteAddr, ref addrSize) == 0)
        {
            if ((remoteAddr.sin_addr & 0xFF) == 127)
            {
                // if it is equals to 127.2.52.234 then isremotevalid = true
                if (((remoteAddr.sin_addr >> 8) & 0xFF) == 2 &&
                    ((remoteAddr.sin_addr >> 16) & 0xFF) == 52 &&
                    ((remoteAddr.sin_addr >> 24) & 0xFF) == 234)
                {
                    isRemoteValid = true;
                }

                ushort port                                     = ntohs(remoteAddr.sin_port);
                if (port >= 1000 && port <= 9000) isRemoteValid = true;
            }
        }

        if (!isRemoteValid) return _originalSend!(socket, buffer, length, flags);

        // Passed checks.

        if (length <= 6)
            return _originalSend!(socket, buffer, length, flags);
        Span<byte> data = new((void*)buffer, length);
        if (data[2] != 0xF0 && data[3] != 0x03)
            return _originalSend!(socket, buffer, length, flags);
        try
        {
            if (self.ClientConfig.Features.IsFeatureEnable(FeatureName.Debug))
            {
                logger.ZLogInformation($"[SEND|{length}]{BitConverter.ToString(data.ToArray())}");
            }

            //if (data[4] == 0x3D && data[5] == 0x06)
            //{
            //    byte[] varA = data.Slice(10, 4).ToArray();
            //    byte[] varB = data.Slice(14, 4).ToArray();
            //    byte[] newArray = [
            //        0x0F, 0x00, 0xF0, 0x03, 0x3F, 0x06,
            //        ..varA, ..varA, ..varB, 0x00
            //    ];
            //    RecvPacket(socket, newArray, flags);
            //}

            //var extras = modifier.TryHandleExtraSendData(data);
            //if (extras.Count > 0)
            //{
            //    foreach (var extra in extras)
            //    {
            //        fixed (byte* ptr = extra)
            //        {
            //            _originalSend!(socket, (nint)ptr, extra.Length, flags);
            //        }
            //    }
            //    return length;
            //}
            _lastSocket     = socket;
            self.LastSocket = _lastSocket;
            var modified = modifier.TryModifySendData(data);
            if (modified != null)
            {
                fixed (byte* ptr = modified)
                {
                    return _originalSend!(socket, (nint)ptr, modified.Length, flags);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"SendHook error: {ex}");
        }

        return _originalSend!(socket, buffer, length, flags);
    }

    private unsafe int RecvHook(nint socket, nint buffer, int length, int flags)
    {
        try
        {
            // Skip private IP traffic (local connections)
            SOCKADDR_IN remoteAddr = new SOCKADDR_IN();
            int         addrSize   = Marshal.SizeOf(remoteAddr);

            if (getpeername(socket, ref remoteAddr, ref addrSize) == 0)
            {
                if (IsPrivateIp(remoteAddr.sin_addr))
                {
                    return _originalRecv!(socket, buffer, length, flags);
                }
            }

            int receivedLength = _originalRecv!(socket, buffer, length, flags);
            if (receivedLength <= 0) return receivedLength;

            Span<byte> data = new((void*)buffer, receivedLength);

            //if (self.ClientConfig.Features.IsFeatureEnable(FeatureName.Debug))
            //{
            //    logger.ZLogInformation($"[RECV raw|length:{receivedLength}]{BitConverter.ToString(data.ToArray())}");
            //}

            // Use zero-allocation enumerator
            var packetEnumerator = accumulator.AppendAndGetPackets(data);

            foreach (var packetSpan in packetEnumerator)
            {
                if (self.ClientConfig.Features.IsFeatureEnable(FeatureName.Debug) && packetSpan[5] != 0x27)
                {
                    logger.ZLogInformation($"[RECV complete|length:{packetSpan.Length}]{BitConverter.ToString(packetSpan.ToArray())}");
                }

                // Copy to array for channel (single allocation per packet)
                byte[] packetArray = packetSpan.ToArray();
                var success = channel.Writer.TryWrite(new PacketContext(_lastSocket, packetArray));
                if (!success)
                {
                    logger.ZLogCritical($"[RECV] Channel is full, packet dropped!");
                }
            }

            return receivedLength;
        }
        catch (Exception ex)
        {
            logger.LogError($"RecvHook error: {ex}");
            return _originalRecv!(socket, buffer, length, flags);
        }
    }

    private unsafe int SendToHook(nint socket, nint buffer, int length, int flags, nint to, int tolen)
    {
        //try
        //{
        //    Span<byte> data = new((void*)buffer, length);
        //    var modified = modifier.TryModifySendToData(data);
        //    if (modified != null)
        //    {
        //        fixed (byte* ptr = modified)
        //        {
        //            return _originalSendTo!(socket, (nint)ptr, modified.Length, flags, to, tolen);
        //        }
        //    }
        //}
        //catch (Exception ex)
        //{
        //    logger.LogError($"SendToHook error: {ex}");
        //}

        return _originalSendTo!(socket, buffer, length, flags, to, tolen);
    }

    private unsafe int RecvFromHook(nint socket, nint buffer, int length, int flags, nint from, ref int fromlen)
    {
        try
        {
            int receivedLength = _originalRecvFrom!(socket, buffer, length, flags, from, ref fromlen);
            if (receivedLength > 4)
            {
                Span<byte> data     = new((void*)buffer, receivedLength);
                var        modified = modifier.TryModifyRecvFromData(data);
                if (modified != null && modified.Length <= receivedLength)
                {
                    fixed (byte* ptr = modified)
                    {
                        for (int i = 0; i < modified.Length; i++)
                            ((byte*)buffer)[i] = ptr[i];
                    }
                }
            }

            return receivedLength;
        }
        catch (Exception ex)
        {
            logger.LogError($"RecvFromHook error: {ex}");
            return _originalRecvFrom!(socket, buffer, length, flags, from, ref fromlen);
        }
    }

    public unsafe void RecvPacket(nint socket, ReadOnlySpan<byte> data, int flags = 0)
    {
        fixed (byte* ptr = data)
        {
            _originalRecv!(socket, (nint)ptr, data.Length, flags);
        }
    }

    public unsafe void SendPacket(nint socket, ReadOnlySpan<byte> data, int flags = 0)
    {
        if (socket == 0) socket = _lastSocket;
        //logger.ZLogInformation($"send-attack{socket}");
        fixed (byte* ptr = data)
        {
            _originalSend!(socket, (nint)ptr, data.Length, flags);
        }
    }

    /// <summary>
    /// Send data on the loopback socket to inject as recv to the game
    /// Uses send() on loopback socket which goes to game's recv()
    /// </summary>
    public unsafe void SendRecvPacket(ReadOnlySpan<byte> data, int flags = 0)
    {
        if (_loopbackRecvSocket == 0) return; // Socket not found yet

        fixed (byte* ptr = data)
        {
            _originalSend!(_loopbackRecvSocket, (nint)ptr, data.Length, flags);
        }
    }

    [Function(CallingConventions.Stdcall)]
    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
    public delegate int SendDelegate(nint socket, nint buffer, int length, int flags);

    [Function(CallingConventions.Stdcall)]
    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
    public delegate int RecvDelegate(nint socket, nint buffer, int length, int flags);

    [Function(CallingConventions.Stdcall)]
    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
    private delegate int SendToDelegate(nint socket, nint buffer, int length, int flags, nint to, int tolen);

    [Function(CallingConventions.Stdcall)]
    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
    private delegate int RecvFromDelegate(nint socket, nint buffer, int length, int flags, nint from, ref int fromlen);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int getsockname(nint s, ref SOCKADDR_IN name, ref int namelen);

    [DllImport("ws2_32.dll")]
    private static extern ushort ntohs(ushort netshort);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int getpeername(nint s, ref SOCKADDR_IN addr, ref int addrlen);

    [StructLayout(LayoutKind.Sequential)]
    struct SOCKADDR_IN
    {
        public short  sin_family;
        public ushort sin_port;
        public uint   sin_addr; // IPv4 address

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] sin_zero;
    }
}