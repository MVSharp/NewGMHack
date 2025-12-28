//using System.Buffers;
//using System.Diagnostics;
//using System.Runtime.InteropServices;
//using System.Threading.Channels;
//using InjectDotnet.NativeHelper;
//using Microsoft.Extensions.Logging;
//using NewGMHack.Stub.PacketStructs;
//using NewGMHack.Stub.PacketStructs.Send;
//using NewGMHack.Stub.Services;
//using Reloaded.Memory.Extensions;
//using ZLinq;
//using ZLogger;

//namespace NewGMHack.Stub.Hooks;

//public sealed class WinsockHookManager(
//    ILogger<WinsockHookManager> logger,
//    Channel<PacketContext>      channel,
//    SelfInformation             self,
//    IBuffSplitter               splitter,
//    PacketDataModifier          modifier
//    ) : IHookManager
//{
//    private SendDelegate?     _originalSend;
//    private RecvDelegate?     _originalRecv;
//    private SendToDelegate?   _originalSendTo;
//    private RecvFromDelegate? _originalRecvFrom;
//    private IntPtr _lastSocket;
//    private INativeHook?      _sendHook;
//    private INativeHook?      _recvHook;
//    private INativeHook?      _sendToHook;
//    private INativeHook?      _recvFromHook;
//    private List<INativeHook> Hooks = new(4);

//    #region Dont Remove prevent GC

//    private SendDelegate?     _sendHookDelegate;
//    private RecvDelegate?     _recvHookDelegate;
//    private SendToDelegate?   _sendToHookDelegate;
//    private RecvFromDelegate? _recvFromHookDelegate;

//    #endregion

//    public void HookAll()
//    {
//        logger.ZLogInformation($"start hook");
//        var currentProc = Process.GetCurrentProcess();
//        var ws2_32      = currentProc.GetModulesByName("ws2_32").First();
//        _sendHookDelegate     = new(SendHook);
//        _recvFromHookDelegate = new(RecvFromHook);
//        _sendToHookDelegate   = new(SendToHook);
//        _recvHookDelegate     = new(RecvHook);
//        //prevent GC
//        HookFunction(ws2_32, "send",     _sendHookDelegate,     out _sendHook,     out _originalSend);
//        HookFunction(ws2_32, "recv",     _recvHookDelegate,     out _recvHook,     out _originalRecv);
//        HookFunction(ws2_32, "sendto",   _sendToHookDelegate,   out _sendToHook,   out _originalSendTo);
//        HookFunction(ws2_32, "recvfrom", _recvFromHookDelegate, out _recvFromHook, out _originalRecvFrom);
//        Hooks.AddRange([_sendHook, _recvFromHook, _recvHook, _sendToHook]);
//    }

//    public void UnHookAll()
//    {
//        foreach (var hook in Hooks)
//        {
//            hook.Dispose();
//        }
//    }

//    private void HookFunction<T>(ProcessModule    module,       string functionName, T hookDelegate,
//                                 out INativeHook? hookInstance, out T? originalDelegate) where T : Delegate
//    {
//        hookInstance     = null;
//        originalDelegate = null;

//        logger.ZLogInformation($"Hooking: {functionName}");

//        if (module.GetExportByName(functionName) is not { } export)
//        {
//            logger.ZLogError($"Export not found: {functionName}");
//            return;
//        }

//        logger.ZLogInformation($"Found export: {export.FunctionName} | RVA {export.FunctionRVA}");

//        var hookResult = export.Hook(hookDelegate);
//        if (hookResult is not INativeHook hook || !hookResult.IsHooked)
//        {
//            logger.ZLogError($"Failed to hook: {functionName}");
//            return;
//        }

//        hookInstance     = hook;
//        originalDelegate = Marshal.GetDelegateForFunctionPointer<T>(hook.OriginalFunction);

//        logger.ZLogInformation($"Hooked {functionName} successfully. Hook ptr: {hook.HookFunction}, Original ptr: {hook.OriginalFunction}");
//    }

//   private unsafe int SendHook(nint socket, nint buffer, int length, int flags)
//{
//        _lastSocket = socket;
//    try
//    {
//        Span<byte> data = new((void*)buffer, length);
//            //TODO refractor later
//        if (data[4] == 0x3D && data[5] ==0x06) // transofrm
//        {
//           byte[] varA = new byte[] { data[10], data[11], data[12], data[13] };
//           byte[] varB = new byte[] { data[14], data[15], data[16], data[17] }; 
//            byte[] newArray = new byte[] {
//                0x0F, 0x00, 0xF0, 0x03, 0x3F, 0x06,
//                varA[0], varA[1], varA[2], varA[3],
//                varA[0], varA[1], varA[2], varA[3],
//                varB[0], varB[1], varB[2], varB[3],
//                0x00
//            };
//                RecvPacket(socket, newArray, flags);
//        }
//            var extras = modifier.TryHandleExtraSendData(data);
//            if(extras.Count > 0)

//            {
//                foreach (var extra in extras)
//                {

//                    fixed (byte* ptr = extra)
//                    {
//                        _originalSend!(socket, (nint)ptr, extra.Length, flags);
//                    }
//                }
//                return length;
//            }
//            var modified = modifier.TryModifySendData(data);
//            //case 2129: // send funnel 

//        if (modified != null)
//        {
//            fixed (byte* ptr = modified)
//            {
//                return _originalSend!(socket, (nint)ptr, modified.Length, flags);
//            }
//        }
//    }
//    catch (Exception ex)
//    {
//        logger.ZLogError($"SendHook error: {ex.Message}");
//    }

//    return _originalSend!(socket, buffer, length, flags);
//}    private unsafe int RecvHook(nint socket, nint buffer, int length, int flags)
//    {
//        try
//        {
//            var receivedLength = _originalRecv!(socket, buffer, length, flags);
//            //return receivedLength;
//            // if (receivedLength == -1)
//            // {
//            //     logger.ZLogInformation($"recv returned invalid length: {receivedLength}");
//            //     return receivedLength;
//            // }
//            if (receivedLength > 6)
//            {
//                Span<byte> data = new Span<byte>((void*)buffer, receivedLength);
//                //logger.ZLogInformation($"new recv hook PersonInfo: span length {data.Length} | {BitConverter.ToString(data.ToArray())} | hook len {receivedLength}");
//                channel.Writer.TryWrite(new PacketContext(socket, data.ToArray()));
//            }

//            return receivedLength;
//        }
//        catch
//        {
//        }

//        return _originalRecv!(socket, buffer, length, flags);
//    }

//private unsafe int SendToHook(nint socket, nint buffer, int length, int flags, nint to, int tolen)
//{
//    try
//    {
//            Span<byte> data = new((void*)buffer, length);
//            //    if (data[4] == 0x6D && data[5] ==0x27)
//            //    {

//            //        fixed (byte* ptr = data)
//            //        {
//            //            for (int i = 0; i < 100; i++)
//            //            {
//            //                _originalSendTo!(socket, (nint)ptr, data.Length, flags, to, tolen);
//            //            }
//            //        }
//            //    }
//            var modified = modifier.TryModifySendToData(data);
//        if (modified != null)
//        {
//            fixed (byte* ptr = modified)
//            {
//                return _originalSendTo!(socket, (nint)ptr, modified.Length, flags, to, tolen);
//            }
//        }
//    }
//    catch (Exception ex)
//    {
//        logger.ZLogError($"SendToHook error: {ex.Message}");
//    }

//    return _originalSendTo!(socket, buffer, length, flags, to, tolen);
//}

//private unsafe int RecvFromHook(nint socket, nint buffer, int length, int flags, nint from, ref int fromlen)
//{
//    try
//    {
//        int receivedLength = _originalRecvFrom!(socket, buffer, length, flags, from, ref fromlen);

//        if (receivedLength > 4)
//        {
//            Span<byte> data = new((void*)buffer, receivedLength);
//            var modified = modifier.TryModifyRecvFromData(data); // Use correct method

//            if (modified != null && modified.Length <= receivedLength)
//            {
//                fixed (byte* ptr = modified)
//                {
//                    for (int i = 0; i < modified.Length; i++)
//                    {
//                        ((byte*)buffer)[i] = ptr[i]; // Write back to original buffer
//                    }
//                }
//            }
//        }

//        return receivedLength;
//    }
//    catch
//    {
//        // Optional: log or handle error
//    }

//    return _originalRecvFrom!(socket, buffer, length, flags, from, ref fromlen);
//}
//    public unsafe void RecvPacket(nint socket , ReadOnlySpan<byte> data , int flags =0)
//    {

//        try
//        {
//            fixed (byte* ptr = data)
//            {
//                nint buffer = (nint)ptr;
//                OriginalRecv(socket, buffer, data.Length, flags);
//            }
//        }
//        catch
//        {
//        }
//    }
//    public unsafe void SendPacket(nint socket, ReadOnlySpan<byte> data, int flags = 0)
//    {
//        try
//        {
//            if(socket == 0)
//            {
//                socket = _lastSocket;
//            }
//            fixed (byte* ptr = data)
//            {
//                nint buffer = (nint)ptr;
//                OriginalSend(socket, buffer, data.Length, flags);
//            }
//        }
//        catch
//        {
//        }
//    }

//    public SendDelegate OriginalSend => _originalSend!;

//    public RecvDelegate OriginalRecv => _originalRecv!;

//    // Delegates
//    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
//    public delegate int SendDelegate(nint socket, nint buffer, int length, int flags);

//    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
//    public delegate int RecvDelegate(nint socket, nint buffer, int length, int flags);

//    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
//    private delegate int SendToDelegate(nint socket, nint buffer, int length, int flags, nint to, int tolen);

//    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
//    private delegate int RecvFromDelegate(nint socket, nint buffer, int length, int flags, nint from,
//                                          ref int fromlen);
//}

using System.Buffers;
using InjectDotnet.NativeHelper;
using Microsoft.Extensions.Logging;
using NewGMHack.Stub;
using NewGMHack.Stub.Hooks;
using NewGMHack.Stub.Services;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X86;
using Squalr.Engine.Utils.Extensions;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using ZLogger;

public sealed class WinsockHookManager(
    ILogger<WinsockHookManager> logger,
    Channel<PacketContext> channel,
    SelfInformation self,
    IBuffSplitter splitter,
    PacketDataModifier modifier,
    IReloadedHooks hooksEngine // Injected Reloaded.Hooks engine
) : IHookManager
{
    private IHook<SendDelegate>? _sendHook;
    private IHook<RecvDelegate>? _recvHook;
    private IHook<SendToDelegate>? _sendToHook;
    private IHook<RecvFromDelegate>? _recvFromHook;

    private SendDelegate? _originalSend;
    private RecvDelegate? _originalRecv;
    private SendToDelegate? _originalSendTo;
    private RecvFromDelegate? _originalRecvFrom;

    public static IntPtr _lastSocket;

    public void HookAll()
    {
        logger.LogInformation($"Initializing Winsock hooks using Reloaded.Hooks");

        HookFunction("ws2_32.dll", "send", new SendDelegate(SendHook), out _sendHook, out _originalSend);
        HookFunction("ws2_32.dll", "recv", new RecvDelegate(RecvHook), out _recvHook, out _originalRecv);
        HookFunction("ws2_32.dll", "sendto", new SendToDelegate(SendToHook), out _sendToHook, out _originalSendTo);
        HookFunction("ws2_32.dll", "recvfrom", new RecvFromDelegate(RecvFromHook), out _recvFromHook, out _originalRecvFrom);

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

private void HookFunction<T>(string dllName, string functionName, T hookDelegate, out IHook<T>? hook, out T? original) where T : Delegate
{
    hook = null;
    original = null;

    try
    {

            var functionPtr = NativeLibrary.GetExport(NativeLibrary.Load(dllName), functionName);
            hook = hooksEngine.CreateHook(hookDelegate, functionPtr);
            hook.Activate();
        hook.Enable();

        logger.LogInformation($"Trampoline: {hook.PrintDebugTag()}");
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

private bool IsLocalLoopbackPort(nint socket, int portStart , int portEnd)
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
    private const uint LoopbackMask = 0x000000FF; // Check first byte (little endian: low byte is first)
    private const uint LoopbackValue = 0x7F; // 127
    
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
        uint firstOctet = ip & 0xFF; // First byte (lowest address)
        if (firstOctet == 127) return true; // Loopback
        if (firstOctet == 10) return true;
        if (firstOctet == 172) return true; // Broad check for 172.x
        if (firstOctet == 192) // Check 192.168
        {
             uint secondOctet = (ip >> 8) & 0xFF;
             if (secondOctet == 168) return true;
        }
        return false;
    }
    
    private unsafe int SendHook(nint socket, nint buffer, int length, int flags)
    {
        // Avoid string allocations! Use cached structs methods or direct check.
        // Logic: 
        // 1. Check if Local is 127.x.x.x AND Port 40000-65535
        // 2. Check if Remote is 127.x.x.x AND Port 4000-7000
        
        // Optimize: we really only need to block/allow specific traffic.
        // Original logic: "if !isValid return original".
        
        SOCKADDR_IN localAddr = new SOCKADDR_IN();
        int addrSize = Marshal.SizeOf(localAddr);
        bool isLocalValid = false;
        
        if (getsockname(socket, ref localAddr, ref addrSize) == 0)
        {
             // Check IP starts with 127.
             if ((localAddr.sin_addr & 0xFF) == 127) 
             {
                 ushort port = ntohs(localAddr.sin_port);
                 if (port >= 40000 && port <= 65535) isLocalValid = true;
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
                 ushort port = ntohs(remoteAddr.sin_port);
                 if (port >= 4000 && port <= 7000) isRemoteValid = true;
             }
        }

        if (!isRemoteValid) return _originalSend!(socket, buffer, length, flags);

        // Passed checks.
        
        if(length <= 6) 
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
            // Optimize: Check remote IP for private range directly
            SOCKADDR_IN remoteAddr = new SOCKADDR_IN();
            int addrSize = Marshal.SizeOf(remoteAddr);
            
            if (getpeername(socket, ref remoteAddr, ref addrSize) == 0)
            {
                if (IsPrivateIp(remoteAddr.sin_addr))
                {
                     return _originalRecv!(socket, buffer, length, flags);
                }
            }


            int receivedLength = _originalRecv!(socket, buffer, length, flags);
            if (receivedLength < 15) return receivedLength;
            //if (receivedLength > 6)
            //{
            Span<byte> data   = new((void*)buffer, receivedLength);
            //if (data[2] == 0xF0 && data[3] == 0x03)
            //{
            var skipped = data.Slice(15);
            if (skipped.Length < 5) return receivedLength;
            if (skipped[2] == 0xf0 && skipped[3] == 0x03)
            {

                if (self.ClientConfig.Features.IsFeatureEnable(FeatureName.Debug))
                {
                    logger.ZLogInformation($"[RECV=SKIPPED|{skipped.Length}]{BitConverter.ToString(skipped.ToArray())}");
                }

                var isSucess = channel.Writer.TryWrite(new PacketContext(_lastSocket, skipped.ToArray()));
                if (!isSucess)
                {
                    logger.ZLogCritical($"[RECV]channel is full , packet is missing");
                }
                else
                {
                    //logger.ZLogInformation($"written to packet services : {BitConverter.ToString(skipped.ToArray())}");
                }
            }
                //if (data[2]==0xf0 && data[3] == 0x03)
                //{
                //    if (self.ClientConfig.Features.IsFeatureEnable(FeatureName.Debug))
                //    {
                //        logger.ZLogInformation($"[RECV]{BitConverter.ToString(data.ToArray())}");
                //    }

                //    channel.Writer.TryWrite(new PacketContext(socket, data.ToArray()));
                //}
                //else
                //{

                //    if (self.ClientConfig.Features.IsFeatureEnable(FeatureName.Debug))
                //    {
                //        logger.ZLogInformation($"[RECV-skipped]{BitConverter.ToString(data.Slice(15).ToArray())}");
                //    }
                //}

                //}
                //}
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
                Span<byte> data = new((void*)buffer, receivedLength);
                var modified = modifier.TryModifyRecvFromData(data);
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
[DllImport("ws2_32.dll", SetLastError = true)] private static extern int getsockname( nint s, ref SOCKADDR_IN name, ref int namelen);
[DllImport("ws2_32.dll")] private static extern ushort ntohs(ushort netshort);
[DllImport("ws2_32.dll", SetLastError = true)] private static extern int getpeername(nint s, ref SOCKADDR_IN addr, ref int addrlen);
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
