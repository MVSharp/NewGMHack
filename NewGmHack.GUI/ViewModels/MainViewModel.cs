using System.Diagnostics;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InjectDotnet;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using NewGMHack.CommunicationModel.IPC.Requests;
using NewGmHack.GUI.Abstracts;
using NewGmHack.GUI.Services;
using NewGmHack.GUI.Views;
using ObservableCollections;
using ZLinq;
using System.Text;
using System.Reflection;

// using MessagePipe;
// using NewGMHack.CommunicationModel.IPC;

namespace NewGmHack.GUI.ViewModels
{
    public partial class MainViewModel : ObservableObject, IHealthCheckHandler
    {
        [ObservableProperty] private bool _isConnected = false;
        [ObservableProperty] private bool _isRandomLocation = false;
        ObservableList<TabItem> tabslist;
        [ObservableProperty] private NotifyCollectionChangedSynchronizedViewList<TabItem> _tabs;
        private readonly             RemoteHandler _handler;
        private readonly             IDialogCoordinator _dialogCoordinator;
        private readonly IFeatureHandler _featureHandler;
        private readonly PersonInfoUserControlsViewModel _personInfoVm;
        private readonly IWebServerStatus _webServerStatus;

        public MainViewModel( RemoteHandler master,
                              IFeatureHandler featureHandler,
                              PersonInfoUserControlsViewModel personInfoVm,
                              IWebServerStatus webServerStatus,
                              IDialogCoordinator              dialogCoordinator)
        {
            tabslist = [];
            Tabs = tabslist.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);
            AddTab<PersonInfoView, PersonInfoUserControlsViewModel>("Person Info");
            AddTab<RoommatesView, RoommatesViewModel>("RoommatesView");
            AddTab<FeaturesView, FeaturesViewModel>("Features");
            _handler           = master;
            _dialogCoordinator = dialogCoordinator;
            _featureHandler = featureHandler;
            _personInfoVm = personInfoVm;
            _webServerStatus = webServerStatus;
        }

        [RelayCommand]
        private void OpenWebReport()
        {
            try
            {
                var pid = _personInfoVm.PersonInfo?.PersonId ?? 0;
                var url = _webServerStatus.BaseUrl; // Dynamic URL
                
                if (pid != 0)
                {
                    url += $"?pid={pid}";
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to open browser: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task Deattach()
        {
            await _handler.DeattachRequest();
        }

        [RelayCommand]
        private async Task Inject()
        {
            await InjectInternal(true);
        }

        public async Task<string> InjectFromWeb()
        {
            if (IsConnected) return "Already Connected";
            
            // Fire and forget the injection process
            _ = InjectInternal(false); 
            return "Injection Started";
        }

        public async Task DeattachFromWeb()
        {
            await _handler.DeattachRequest();
        }

        private bool _isInjecting = false;

        private async Task InjectInternal(bool showDialogs)
        {
            if (_isInjecting || IsConnected) return;

            try
            {
                _isInjecting = true;
                string processName = Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ=="));
                var target = Process.GetProcessesByName(processName).FirstOrDefault();
                
                // Allow some retries but maybe not infinite blocking if we want to be safe? 
                // Original code loops forever. We'll stick to that to match behavior.
                while (target == null)
                {
                    target = Process.GetProcessesByName(processName).FirstOrDefault();
                    Debug.WriteLine("Waiting inject...");
                    await Task.Delay(2000);
                    // If we want to cancel?
                }

                var t = await Task.Run(() =>
                {
                    var arg = new Argument
                    {
                        Title = target.WriteMemory("Injected Form"),
                        Text =
                            target.WriteMemory($"This form has been injected into {target.MainModule?.FileName} and is running in its memory space"),
                    };
                    
                    // Read Stub DLL version dynamically
                    var stubDllPath = "NewGMHack.Stub.dll";
                    var stubVersion = GetAssemblyVersion(stubDllPath);
                    var assemblyQualifiedName = $"NewGMHack.Stub.Entry, NewGMHack.Stub, Version={stubVersion}, Culture=neutral, PublicKeyToken=null";
                    
                    return target.Inject(
                                          "NewGMHack.Stub.runtimeconfig.json",
                                          stubDllPath,
                                          assemblyQualifiedName,
                                          "Bootstrap",
                                          arg, true);
                });

                if (t == 0x128)
                {
                    Console.WriteLine("injected sucessfully");
                    while (!IsConnected)
                    {
                        await Task.Delay(1000);
                    }
                    await _featureHandler.Refresh();
                    
                    if(showDialogs)
                        await _dialogCoordinator.ShowMessageAsync(this, "Injected", "Injected");
                }
                else
                {
                    Console.WriteLine("Injected failed");
                    if(showDialogs)
                        await _dialogCoordinator.ShowMessageAsync(this, "failed to inject", "failed to inject");
                }

            }
            catch(Exception ex)
            {
                if(showDialogs)
                    await _dialogCoordinator.ShowMessageAsync(this, "failed to inject", $"{ex.Message}|{ex.StackTrace}");
            }
            finally
            {
                _isInjecting = false;
            }
        }

        /// <inheritdoc />
        public void SetHealthStatus(bool isConnected)
        {
            if (!isConnected)
            {
                _featureHandler.Clear();
            }

            IsConnected = isConnected;
        }

        public void AddTab<TView, TViewModel>(string header)
            where TView : UserControl
            where TViewModel : class
        {
            var view      = App.Services.GetRequiredService<TView>();
            var viewModel = App.Services.GetRequiredService<TViewModel>();
            view.DataContext = viewModel;

            var tab = new TabItem
            {
                Header  = header,
                Content = view
            };

            tabslist.Add(tab);
        }

//#pragma warning disable
        [RelayCommand]
        public Task OnLoaded()
        {
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Read the version from a DLL file without loading it into the current AppDomain
        /// </summary>
        private static string GetAssemblyVersion(string dllPath)
        {
            try
            {
                // Use AssemblyName to read version without loading the assembly
                var assemblyName = AssemblyName.GetAssemblyName(dllPath);
                return assemblyName.Version?.ToString() ?? "1.0.0.0";
            }
            catch
            {
                // Fallback if we can't read the version
                return "1.0.0.0";
            }
        }
    }
}