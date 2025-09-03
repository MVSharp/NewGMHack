using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InjectDotnet;
// using MessagePipe;
// using NewGMHack.CommunicationModel.IPC;

namespace NewGmHack.GUI.ViewModels
{
    public partial class MainViewModel(RemoteHandler remoteHandler) : ObservableObject
    {
        [RelayCommand]
        private async Task Inject()
        {
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
                var r = await remoteHandler.AskForHealth();
                if (r)
                {
                    Console.WriteLine("injected sucessfully");
                }
            }

            else
            {
                Console.WriteLine("fucked , it failed,we r fucked up");
            }

        }
    }
}