using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NewGMHack.Stub.Services
{

[StructLayout(LayoutKind.Sequential)]
public struct DIMOUSESTATE
{
    public int lX; // X-axis movement
    public int lY; // Y-axis movement
    public int lZ; // Wheel movement

    public byte rgbButtons0; // Left button
    public byte rgbButtons1; // Right button
    public byte rgbButtons2; // Middle button
    public byte rgbButtons3;
    public byte rgbButtons4;
    public byte rgbButtons5;
    public byte rgbButtons6;
    public byte rgbButtons7;
}
    
public class InputStateTracker
{
    public byte[] Keyboard = new byte[256];
    public DIMOUSESTATE Mouse;

    public bool IsKeyboardIdle => Keyboard.All(b => b == 0);
    public bool IsMouseIdle => Mouse.rgbButtons0 == 0 && Mouse.rgbButtons1 == 0 && Mouse.rgbButtons2 == 0;

    public void Update(DeviceType type, int size, IntPtr ptr)
    {
        if (type == DeviceType.Keyboard && size == 256)
            Marshal.Copy(ptr, Keyboard, 0, 256);
        else if (type == DeviceType.Mouse && size == Marshal.SizeOf<DIMOUSESTATE>())
            Mouse = Marshal.PtrToStructure<DIMOUSESTATE>(ptr);
    }

    public bool IsLeftDown => (Mouse.rgbButtons0 & 0x80) != 0;
    public bool IsRightDown => (Mouse.rgbButtons1 & 0x80) != 0;
}
}
