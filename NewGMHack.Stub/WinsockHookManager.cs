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
using ZLogger;

namespace NewGMHack.Stub;

public sealed class WinsockHookManager(ILogger<WinsockHookManager> logger, Channel<PacketContext> channel,SelfInformation self,IBuffSplitter splitter)
{
    private SendDelegate?     _originalSend;
    private RecvDelegate?     _originalRecv;
    private SendToDelegate?   _originalSendTo;
    private RecvFromDelegate? _originalRecvFrom;

    private INativeHook? _sendHook;
    private INativeHook? _recvHook;
    private INativeHook? _sendToHook;
    private INativeHook? _recvFromHook;

    public void HookAll()
    {
        logger.ZLogInformation($"start hook");
        var currentProc = Process.GetCurrentProcess();
        var ws2_32      = currentProc.GetModulesByName("ws2_32").First();

        HookFunction(ws2_32, "send",   new SendDelegate(SendHook),     out _sendHook,   out _originalSend);
        HookFunction(ws2_32, "recv",   new RecvDelegate(RecvHook),     out _recvHook,   out _originalRecv);
        HookFunction(ws2_32, "sendto", new SendToDelegate(SendToHook), out _sendToHook, out _originalSendTo);
        HookFunction(ws2_32, "recvfrom", new RecvFromDelegate(RecvFromHook), out _recvFromHook,
                     out _originalRecvFrom);
    }

    private void HookFunction<T>(ProcessModule    module,       string functionName, T hookDelegate,
                                 out INativeHook? hookInstance, out T? originalDelegate) where T : Delegate
    {
        hookInstance     = null;
        originalDelegate = null;

        logger.ZLogInformation($"Hooking: {functionName}");

        if (module.GetExportByName(functionName) is not NativeExport export)
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

    // Hooked implementations
    private unsafe int SendHook(IntPtr socket, IntPtr buffer, int length, int flags)
    {
        //Span<byte> data = new Span<byte>((void*)buffer, length);
        // Modify data if needed
        // if (length > 4)
        // {

        // Span<byte> data = new Span<byte>((void*)buffer, length);
        // if ((data[0] == 0xA3 && data[1] == 0x00) || (data[0]==0xA6 && data[1] == 0x00))
        // {
        //
        // var        hex  = BitConverter.ToString(data.ToArray()).Replace("-", " ");
        //     //logger.ZLogInformation($"Send : {hex}");
        // }
        // }
        // //logger.ZLogInformation($"send hook PersonInfo: span length {data.Length} | hook len {length}");
        // if 02F5 , F502 (1522) then block if misison
        if (self.ClientConfig.IsMissionBomb )
        {
            Span<byte> data = new Span<byte>((void*)buffer, length);
            if (data.Length > 4)
            {
                var result = splitter.Split(data).FirstOrDefault();
                if (result != null)
                {
                    //TODO refractor later
                    var method = result.Method;
                    switch (method)
                    {
                        case 1335: // normal damage 
                            var raw = new byte[]{0x00,0x00,0x00,0x00}.AsSpan().CombineWith( result.MethodBody.AsSpan());
                            var attack = raw.ReadStruct<Attack1335>();
                            var targets = raw.SliceAfter<Attack1335>().CastTo<TargetData>();
                            if (attack.PlayerId == self.PersonInfo.PersonId)
                            {
                                logger.ZLogInformation($"Attacked By me , now motify {result.Method}");
                                for (var index = 0; index < targets.Length; index++)
                                {
                                    targets[index].Damage = ushort.MaxValue;
                                }
                            }
                            else
                            {
                                logger.ZLogInformation($"1335 not attacked by me {attack.PlayerId} | {attack.PlayerId2}");
                            }
                                var attackBytes = attack.ToByteArray().AsSpan();
                            var targetsBytes = targets.AsByteSpan();
                            var combined = attackBytes.CombineWith(targetsBytes);
                            fixed (byte* ptr = combined)
                            {
                                IntPtr ptrBuffer = (IntPtr)ptr;
                                return _originalSend!(socket, ptrBuffer, attackBytes.Length + targetsBytes.Length, flags);
                            }
                            break;
                        case 1486: // item , bucket damage

                            var raw1 = new byte[]{0x00,0x00,0x00,0x00}.AsSpan().CombineWith( result.MethodBody.AsSpan());
                            var attack1 = raw1.ReadStruct<Attack1486>();
                            var targets1 = raw1.SliceAfter<Attack1486>().CastTo<TargetData>();
                            if (attack1.PlayerId == self.PersonInfo.PersonId)
                            {
                                logger.ZLogInformation($"Attacked By me , now motify {result.Method}");
                                for (var index = 0; index < targets1.Length; index++)
                                {
                                    targets1[index].Damage = ushort.MaxValue;
                                }
                            }
                            else
                            {
                                logger.ZLogInformation($"1486 not attacked by me {attack1.PlayerId} | {attack1.PlayerId2}");
                            }
                                var attackBytes1 = attack1.ToByteArray().AsSpan();
                            var targetsBytes1 = targets1.AsByteSpan();
                            var combined1 = attackBytes1.CombineWith(targetsBytes1);
                            fixed (byte* ptr = combined1)
                            {
                                IntPtr ptrBuffer = (IntPtr)ptr;
                                return _originalSend!(socket, ptrBuffer, attackBytes1.Length + targetsBytes1.Length, flags);
                            }
                            break;
                    }
                }
            }
        }
        return _originalSend!(socket, buffer, length, flags);
    }

    private unsafe int RecvHook(IntPtr socket, IntPtr buffer, int length, int flags)
    {
        var receivedLength = _originalRecv!(socket, buffer, length, flags);
//return receivedLength;
        // if (receivedLength == -1)
        // {
        //     logger.ZLogInformation($"recv returned invalid length: {receivedLength}");
        //     return receivedLength;
        // }
        if (receivedLength > 4)
        {
            Span<byte> data = new Span<byte>((void*)buffer, receivedLength);
            //logger.ZLogInformation($"new recv hook PersonInfo: span length {data.Length} | hook len {receivedLength}");
            channel.Writer.TryWrite(new PacketContext(socket, data.ToArray()));
        }

        return receivedLength;
    }

    private unsafe int SendToHook(IntPtr socket, IntPtr buffer, int length, int flags, IntPtr to, int tolen)
    {
        //Span<byte> data = new Span<byte>((void*)buffer, length);

        //logger.ZLogInformation($"send to hook PersonInfo: span length {data.Length} | hook len {length}");
        // Modify data if needed
        return _originalSendTo!(socket, buffer, length, flags, to, tolen);
    }

    private unsafe int RecvFromHook(IntPtr socket, IntPtr buffer, int length, int flags, IntPtr from, ref int fromlen)
    {
        int receivedLength = _originalRecvFrom!(socket, buffer, length, flags, from, ref fromlen);
        // if (receivedLength ==-1)
        // {
        //     logger.ZLogInformation($"recvfrom returned invalid length: {receivedLength}");
        // }
        if (receivedLength > 4)
        {
            Span<byte> data = new Span<byte>((void*)buffer, receivedLength);
            //var        hex  = BitConverter.ToString(data.ToArray()).Replace("-", " ");
            //    logger.ZLogInformation($"Recv from : {hex}");
        }

        return receivedLength;
    }

    public unsafe void SendPacket(IntPtr socket, ReadOnlySpan<byte> data, int flags = 0)
    {
        fixed (byte* ptr = data)
        {
            IntPtr buffer = (IntPtr)ptr;
            this.OriginalSend(socket, buffer, data.Length, flags);
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