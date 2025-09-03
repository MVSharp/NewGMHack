//using InjectDotnet;

﻿using InjectDotnet;
using InjectDotnet.Debug;
using System.Diagnostics;
using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
var target = Process.GetProcessesByName("GOnline").FirstOrDefault();
while (target == null)
{
    target = Process.GetProcessesByName("GOnline").FirstOrDefault();
    Console.WriteLine("Waiting inject");
    await Task.Delay(2000);
}

var arg = new Argument
{
    Title = target.WriteMemory("Injected Form"),
    Text =
        target.WriteMemory($"This form has been injected into {target.MainModule?.FileName} and is running in its memory space"),
    //Picture = target.WriteMemory(picBytes),
    //pic_sz = picBytes.Length
};
var t = target.Inject(
                      "NewGMHack.Stub.runtimeconfig.json",
                      "NewGMHack.Stub.dll",
                      "NewGMHack.Stub.Entry, NewGMHack.Stub, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                      "Bootstrap",
                      arg, true);
if (t == 0x128)
{
    Console.WriteLine("injected sucessfully");
}

else
{
    Console.WriteLine("fucked , it failed,we r fucked up");
}

Console.WriteLine(t);

// target.First().iN
//target.First().Inject();
struct Argument
{
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    public IntPtr Title;
    public IntPtr Text;
    //public IntPtr Picture;
    //public int pic_sz;
#pragma warning restore CS0649
}