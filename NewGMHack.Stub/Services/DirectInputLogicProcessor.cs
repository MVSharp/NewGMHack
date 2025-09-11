using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NewGMHack.Stub;
using SharpDX.DirectInput;

public class DirectInputLogicProcessor(SelfInformation self)
{
    private const int DIK_1 = 0x02;
    private const int DIK_2 = 0x03;
    private const int DIK_3 = 0x04;

    private static bool leftWasDown = false;
    private static bool rightWasDown = false;
    private static bool leftJustReleased = false;

    private static int lastKeypad = 1;
    private static bool isSwitching = false;

    private static readonly Stopwatch timer = Stopwatch.StartNew();
    private static readonly List<ScheduledKeyEvent> scheduledEvents = new();

    // Base durations
    private const int KeyDownDurationMs = 10;
    private const int KeyUpDurationMs = 10;

    // Step-specific delays
    private const int DelaySwitchToMs = 200;   // Delay before switch-back
    private const int DelaySwitchBackMs = 100;  // Shorter delay for return

// DirectInput key codes (partial list)
private const int DIK_ESCAPE = 0x01;
private const int DIK_F5 = 0x3F;
    private class ScheduledKeyEvent
    {
        public int Code;
        public bool IsDown;
        public long TriggerTimeMs;
        public bool Fired;
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

    if (self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsAimSupport))
        HandleAimSupport(keys);

    if (self.ClientConfig.Features.IsFeatureEnable(FeatureName.IsAutoReady) && !self.ClientConfig.IsInGame && !isSwitching)
        HandleAutoReady();

    InjectScheduledKeys(keys);
    Marshal.Copy(keys, 0, dataPtr, 256);
}

private void HandleAimSupport(byte[] keys)
{
    if (keys[DIK_1] == 0x80) lastKeypad = 1;
    else if (keys[DIK_2] == 0x80) lastKeypad = 2;
    else if (keys[DIK_3] == 0x80) lastKeypad = 3;

    if (leftJustReleased && !isSwitching)
    {
        scheduledEvents.Clear();
        long baseTime = timer.ElapsedMilliseconds;

        switch (lastKeypad)
        {
            case 1:
                ScheduleKeyPress(DIK_2, 0, DelaySwitchToMs);
                ScheduleKeyPress(DIK_1, 0, DelaySwitchBackMs);
                break;
            case 2:
                ScheduleKeyPress(DIK_1, 0, DelaySwitchToMs);
                ScheduleKeyPress(DIK_2, 0, DelaySwitchBackMs);
                break;
            case 3:
                ScheduleKeyPress(DIK_2, 0, DelaySwitchToMs);
                ScheduleKeyPress(DIK_3, 0, DelaySwitchBackMs);
                break;
        }

        isSwitching = true;
    }

    leftJustReleased = false;
}

private void ScheduleKeyPress(int code, int downDelay, int upDelay)
{
    long baseTime = timer.ElapsedMilliseconds;

    scheduledEvents.Add(new ScheduledKeyEvent
    {
        Code = code,
        IsDown = true,
        TriggerTimeMs = baseTime + downDelay,
        Fired = false
    });
    scheduledEvents.Add(new ScheduledKeyEvent
    {
        Code = code,
        IsDown = false,
        TriggerTimeMs = baseTime + downDelay + KeyDownDurationMs + upDelay,
        Fired = false
    });
}

private void InjectScheduledKeys(byte[] keys)
{
    long now = timer.ElapsedMilliseconds;
    foreach (var evt in scheduledEvents.ToArray())
    {
        if (!evt.Fired && now >= evt.TriggerTimeMs)
        {
            keys[evt.Code] = evt.IsDown ? (byte)0x80 : (byte)0x00;
            evt.Fired = true;
        }
    }

    scheduledEvents.RemoveAll(e => e.Fired);
    if (scheduledEvents.Count == 0)
        isSwitching = false;
}
private void HandleAutoReady()
{
    scheduledEvents.Clear();
    long baseTime = timer.ElapsedMilliseconds;

    ScheduleKeyPress(DIK_F5, 0, 50);
    ScheduleKeyPress(DIK_ESCAPE, 100, 50);

    isSwitching = true;
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
