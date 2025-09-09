using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using InjectDotnet.NativeHelper;
using Microsoft.Extensions.Logging;
using NewGMHack.Stub.PacketStructs;
using NewGMHack.Stub.PacketStructs.Send;
using NewGMHack.Stub.Services;
using Reloaded.Memory.Extensions;
using ZLinq;
using ZLogger;

namespace NewGMHack.Stub;

public sealed class WinsockHookManager(
    ILogger<WinsockHookManager> logger,
    Channel<PacketContext>      channel,
    SelfInformation             self,
    IBuffSplitter               splitter,
    PacketDataModifier          modifier
    )
{
    private SendDelegate?     _originalSend;
    private RecvDelegate?     _originalRecv;
    private SendToDelegate?   _originalSendTo;
    private RecvFromDelegate? _originalRecvFrom;

    private INativeHook?      _sendHook;
    private INativeHook?      _recvHook;
    private INativeHook?      _sendToHook;
    private INativeHook?      _recvFromHook;
    private List<INativeHook> Hooks = new(4);

    #region Dont Remove prevent GC

    private SendDelegate?     _sendHookDelegate;
    private RecvDelegate?     _recvHookDelegate;
    private SendToDelegate?   _sendToHookDelegate;
    private RecvFromDelegate? _recvFromHookDelegate;

    #endregion

    public void HookAll()
    {
        logger.ZLogInformation($"start hook");
        var currentProc = Process.GetCurrentProcess();
        var ws2_32      = currentProc.GetModulesByName("ws2_32").First();
        _sendHookDelegate     = new(SendHook);
        _recvFromHookDelegate = new(RecvFromHook);
        _sendToHookDelegate   = new(SendToHook);
        _recvHookDelegate     = new(RecvHook);
        //prevent GC
        HookFunction(ws2_32, "send",     _sendHookDelegate,     out _sendHook,     out _originalSend);
        HookFunction(ws2_32, "recv",     _recvHookDelegate,     out _recvHook,     out _originalRecv);
        HookFunction(ws2_32, "sendto",   _sendToHookDelegate,   out _sendToHook,   out _originalSendTo);
        HookFunction(ws2_32, "recvfrom", _recvFromHookDelegate, out _recvFromHook, out _originalRecvFrom);
        Hooks.AddRange([_sendHook, _recvFromHook, _recvHook, _sendToHook]);
    }

    public void UnHookAll()
    {
        foreach (var hook in Hooks)
        {
            hook.Dispose();
        }
    }

    private void HookFunction<T>(ProcessModule    module,       string functionName, T hookDelegate,
                                 out INativeHook? hookInstance, out T? originalDelegate) where T : Delegate
    {
        hookInstance     = null;
        originalDelegate = null;

        logger.ZLogInformation($"Hooking: {functionName}");

        if (module.GetExportByName(functionName) is not { } export)
        {
            logger.ZLogError($"Export not found: {functionName}");
            return;
        }

        logger.ZLogInformation($"Found export: {export.FunctionName} | RVA {export.FunctionRVA}");

        var hookResult = export.Hook(hookDelegate);
        if (hookResult is not INativeHook hook || !hookResult.IsHooked)
        {
            logger.ZLogError($"Failed to hook: {functionName}");
            return;
        }

        hookInstance     = hook;
        originalDelegate = Marshal.GetDelegateForFunctionPointer<T>(hook.OriginalFunction);

        logger.ZLogInformation($"Hooked {functionName} successfully. Hook ptr: {hook.HookFunction}, Original ptr: {hook.OriginalFunction}");
    }

    private unsafe int SendHook(IntPtr socket, IntPtr buffer, int length, int flags)
    {
        try
        {
            Span<byte> data = new((void*)buffer, length);
            var modified = modifier.TryModifySendData(data);
            if (modified != null)
            {
                try
                {
                    fixed (byte* ptr = modified)
                    {
                        return _originalSend!(socket, (IntPtr)ptr, modified.Length, flags);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(modified, clearArray: true);
                }
            }
        }
        catch (Exception ex)
        {
            logger.ZLogError($"SendHook error: {ex.Message}");
        }
        return _originalSend!(socket, buffer, length, flags);
    }

    private unsafe int RecvHook(IntPtr socket, IntPtr buffer, int length, int flags)
    {
        try
        {
            var receivedLength = _originalRecv!(socket, buffer, length, flags);
            //return receivedLength;
            // if (receivedLength == -1)
            // {
            //     logger.ZLogInformation($"recv returned invalid length: {receivedLength}");
            //     return receivedLength;
            // }
            if (receivedLength > 6)
            {
                Span<byte> data = new Span<byte>((void*)buffer, receivedLength);
                //logger.ZLogInformation($"new recv hook PersonInfo: span length {data.Length} | {BitConverter.ToString(data.ToArray())} | hook len {receivedLength}");
                channel.Writer.TryWrite(new PacketContext(socket, data.ToArray()));
            }

            return receivedLength;
        }
        catch
        {
        }

        return _originalRecv!(socket, buffer, length, flags);
    }

    private unsafe int SendToHook(IntPtr socket, IntPtr buffer, int length, int flags, IntPtr to, int tolen)
    {
        try
        {
            Span<byte> data = new((void*)buffer, length);
            var modified = modifier.TryModifySendToData(data);
            if (modified != null)
            {
                try
                {
                    fixed (byte* ptr = modified)
                    {
                        return _originalSendTo!(socket, (IntPtr)ptr, modified.Length, flags, to, tolen);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(modified, clearArray: true);
                }
            }
        }
        catch (Exception ex)
        {
            logger.ZLogError($"SendToHook error: {ex.Message}");
        }
        return _originalSendTo!(socket, buffer, length, flags, to, tolen);
    }

    private unsafe int RecvFromHook(IntPtr socket, IntPtr buffer, int length, int flags, IntPtr from, ref int fromlen)
    {
        try
        {
            int receivedLength = _originalRecvFrom!(socket, buffer, length, flags, from, ref fromlen);
            // if (receivedLength ==-1)
            // {
            //     logger.ZLogInformation($"recvfrom returned invalid length: {receivedLength}");
            // }
            if (receivedLength > 4)
            {
                //Span<byte> data = new Span<byte>((void*)buffer, receivedLength);
                //var        hex  = BitConverter.ToString(data.ToArray()).Replace("-", " ");
                //    logger.ZLogInformation($"Recv from : {hex}");
            }

            return receivedLength;
        }
        catch
        {
        }

        return _originalRecvFrom!(socket, buffer, length, flags, from, ref fromlen);
    }

    public unsafe void SendPacket(IntPtr socket, ReadOnlySpan<byte> data, int flags = 0)
    {
        try
        {
            fixed (byte* ptr = data)
            {
                IntPtr buffer = (IntPtr)ptr;
                this.OriginalSend(socket, buffer, data.Length, flags);
            }
        }
        catch
        {
        }
    }

    public SendDelegate OriginalSend => _originalSend!;

    // Delegates
    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
    public delegate int SendDelegate(IntPtr socket, IntPtr buffer, int length, int flags);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
    private delegate int RecvDelegate(IntPtr socket, IntPtr buffer, int length, int flags);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
    private delegate int SendToDelegate(IntPtr socket, IntPtr buffer, int length, int flags, IntPtr to, int tolen);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
    private delegate int RecvFromDelegate(IntPtr  socket, IntPtr buffer, int length, int flags, IntPtr from,
                                          ref int fromlen);
}