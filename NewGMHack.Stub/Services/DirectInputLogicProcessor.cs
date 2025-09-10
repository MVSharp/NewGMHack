using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SharpDX.DirectInput;

public class DirectInputLogicProcessor
{
    private const int DIK_1 = 0x02;
    private const int DIK_2 = 0x03;
    private const int DIK_3 = 0x04;

    private static bool leftWasDown = false;
    private static bool rightWasDown = false;
    private static bool leftJustReleased = false;

    private static int lastKeypad = 1;
    private static bool isSwitching = false;
    private static readonly Queue<InjectedKey> switchQueue = new();
    private static readonly List<InjectedKey> activeInjectedKeys = new();
    // fine turn these two value for different behaviour too fast sometimes the game is not rendering it so we may casue "ghost key" or miss
    private const int InjectHoldFrames = 12;
    private const int InjectGapFrames = 20;

    private class InjectedKey
    {
        public int Code;
        public bool IsDown;
        public int FramesLeft;
    }

    public void Process(DeviceType deviceType, int size, IntPtr dataPtr)
    {
        if (deviceType == DeviceType.Keyboard && size == 256)
            ProcessKeyboard(dataPtr);
        else if (deviceType == DeviceType.Mouse && size == Marshal.SizeOf<DIMOUSESTATE>())
            ProcessMouse(dataPtr);
    }

    public void ZeroMemory(IntPtr ptr, int size)
    {
        if (ptr == IntPtr.Zero || size <= 0) return;
        Span<byte> zero = stackalloc byte[size];
        Marshal.Copy(zero.ToArray(), 0, ptr, size);
    }

    private void ProcessMouse(IntPtr dataPtr)
    {
        DIMOUSESTATE state = Marshal.PtrToStructure<DIMOUSESTATE>(dataPtr);

        bool leftDown = (state.rgbButtons0 & 0x80) != 0;
        bool rightDown = (state.rgbButtons1 & 0x80) != 0;

        leftJustReleased = leftWasDown && !leftDown;

        if (leftDown && !rightDown)
            state.rgbButtons1 = 0x80;
        else if (!leftDown && !rightWasDown)
            state.rgbButtons1 = 0x00;

        rightWasDown = rightDown;
        leftWasDown = leftDown;

        Marshal.StructureToPtr(state, dataPtr, false);
    }

    private void ProcessKeyboard(IntPtr dataPtr)
    {
        byte[] keys = new byte[256];
        Marshal.Copy(dataPtr, keys, 0, 256);

        // Detect key press to update lastKeypad
        if (keys[DIK_1] == 0x80) lastKeypad = 1;
        else if (keys[DIK_2] == 0x80) lastKeypad = 2;
        else if (keys[DIK_3] == 0x80) lastKeypad = 3;

        // Trigger switch sequence after left click release
        if (leftJustReleased && !isSwitching)
        {
            switchQueue.Clear();
            switch (lastKeypad)
            {
                case 1:
                    EnqueueKey(DIK_2, true);
                    EnqueueKey(DIK_2, false);
                    EnqueueDelay();
                    EnqueueKey(DIK_1, true);
                    EnqueueKey(DIK_1, false);
                    break;
                case 2:
                    EnqueueKey(DIK_1, true);
                    EnqueueKey(DIK_1, false);
                    EnqueueDelay();
                    EnqueueKey(DIK_2, true);
                    EnqueueKey(DIK_2, false);
                    break;
                case 3:
                    EnqueueKey(DIK_2, true);
                    EnqueueKey(DIK_2, false);
                    EnqueueDelay();
                    EnqueueKey(DIK_3, true);
                    EnqueueKey(DIK_3, false);
                    break;
            }
            isSwitching = true;
        }

        // Process switch queue
        if (isSwitching && switchQueue.Count > 0)
        {
            var next = switchQueue.Dequeue();

            if (next.Code >= 0)
            {
                activeInjectedKeys.Add(new InjectedKey
                {
                    Code = next.Code,
                    IsDown = next.IsDown,
                    FramesLeft = next.FramesLeft
                });
            }

            if (switchQueue.Count == 0)
                isSwitching = false;
        }

        // Apply injected keys
        foreach (var key in activeInjectedKeys.ToArray())
        {
            keys[key.Code] = key.IsDown ? (byte)0x80 : (byte)0x00;
            key.FramesLeft--;
            if (key.FramesLeft <= 0)
                activeInjectedKeys.Remove(key);
        }

        leftJustReleased = false;
        Marshal.Copy(keys, 0, dataPtr, 256);
    }

    private void EnqueueKey(int dikCode, bool isDown)
    {
        switchQueue.Enqueue(new InjectedKey
        {
            Code = dikCode,
            IsDown = isDown,
            FramesLeft = InjectHoldFrames
        });
    }

    private void EnqueueDelay()
    {
        switchQueue.Enqueue(new InjectedKey
        {
            Code = -1, // No key
            IsDown = false,
            FramesLeft = InjectGapFrames
        });
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DIMOUSESTATE
    {
        public int lX;
        public int lY;
        public int lZ;
        public byte rgbButtons0;
        public byte rgbButtons1;
        public byte rgbButtons2;
        public byte rgbButtons3;
        public byte rgbButtons4;
        public byte rgbButtons5;
        public byte rgbButtons6;
        public byte rgbButtons7;
    }
}
