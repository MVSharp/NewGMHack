using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InjectDotnet;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NewGMHack.CommunicationModel.IPC.Requests;
using NewGmHack.GUI.Abstracts;
using NewGmHack.GUI.Services;
using NewGmHack.GUI.Views;
using ObservableCollections;
using ZLinq;
using ZLogger;
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
        private readonly ILogger<MainViewModel> _logger;

        public MainViewModel( RemoteHandler master,
                              IFeatureHandler featureHandler,
                              PersonInfoUserControlsViewModel personInfoVm,
                              IWebServerStatus webServerStatus,
                              IDialogCoordinator              dialogCoordinator,
                              ILogger<MainViewModel> logger)
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
            _logger = logger;
            _logger.ZLogInformation($"MainViewModel initialized");
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

        private bool   _isInjecting = false;
        private string _processPath = "";
        private async Task InjectInternal(bool showDialogs)
        {
            if (_isInjecting || IsConnected)
            {
                _logger.ZLogDebug($"InjectInternal skipped: _isInjecting={_isInjecting}, IsConnected={IsConnected}");
                return;
            }

            try
            {
                _isInjecting = true;
                _logger.ZLogInformation($"Starting injection process (showDialogs={showDialogs})");
                
                string processName = Encoding.UTF8.GetString(Convert.FromBase64String("R09ubGluZQ=="));
                _logger.ZLogDebug($"Searching for process: {processName}");
                
                var target = Process.GetProcessesByName(processName).FirstOrDefault(x=>!x.MainWindowTitle.Contains("GM_HACK"));
                if (target == null)
                {
                    if (!string.IsNullOrEmpty(_processPath))
                    {

                        var psi = new ProcessStartInfo
                        {
                            FileName = _processPath,
                            Arguments = "127.2.52.234 5001"
                        };
                        Process.Start(psi);
                    }
                }
                // Allow some retries but maybe not infinite blocking if we want to be safe? 
                // Original code loops forever. We'll stick to that to match behavior.
                while (target == null)
                {
                    target = Process.GetProcessesByName(processName).FirstOrDefault(x=>!x.MainWindowTitle.Contains("GM_HACK"));
                    _logger.ZLogDebug($"Process not found, waiting 2 seconds...");
                    await Task.Delay(2000);
                    // If we want to cancel?
                }

                _processPath = target.MainModule.FileName;
                _logger.ZLogInformation($"Target process found: PID={target.Id}, Name={target.ProcessName}");

                // Allocate buffer for channel name (Stub will write to this)
                // We use WriteMemory to allocate and initialize with zeros
                var chan     = $"Sdhook_{target.Id}";
                _logger.ZLogInformation($"channel key : {chan}");
                _handler.Connect(chan);
                var t = await Task.Run(() =>
                {
                    // Log WriteMemory returns for debugging
                    var titlePtr = target.WriteMemory("Injected Form");
                    var textPtr = target.WriteMemory($"This form has been injected into {target.MainModule?.FileName} and is running in its memory space");
                    var channelPtr = target.WriteMemory(chan);

                    _logger.ZLogInformation($"[INJECTION DEBUG] WriteMemory returns - Title: 0x{titlePtr:X}, Text: 0x{textPtr:X}, Channel: 0x{channelPtr:X}");

                    var arg = new NewGMHack.Stub.PacketStructs.Argument
                    {
                        Title = titlePtr,
                        Text = textPtr,
                        ChannelName = channelPtr
                    };

                    _logger.ZLogInformation($"[INJECTION DEBUG] Argument struct - Title: 0x{arg.Title:X}, Text: 0x{arg.Text:X}, ChannelName: 0x{arg.ChannelName:X}");
                    _logger.ZLogInformation($"[INJECTION DEBUG] Argument struct size: {System.Runtime.InteropServices.Marshal.SizeOf(arg)} bytes");


                    // Read Stub DLL version dynamically
                    var stubDllPath = "NewGMHack.Stub.dll";

                    var stubVersion = GetAssemblyVersion(stubDllPath);
                    var assemblyQualifiedName = $"NewGMHack.Stub.Entry, NewGMHack.Stub, Version={stubVersion}, Culture=neutral, PublicKeyToken=null";
                    
                    _logger.ZLogInformation($"Injecting with stubDllPath={stubDllPath}, stubVersion={stubVersion}");
                    _logger.ZLogDebug($"AssemblyQualifiedName: {assemblyQualifiedName}");
                    
                    return target.Inject(
                                          "NewGMHack.Stub.runtimeconfig.json",
                                          stubDllPath,
                                          assemblyQualifiedName,
                                          "Bootstrap",
                                          arg, true);
                });

                _logger.ZLogInformation($"Injection returned result code: 0x{t:X}");

                if (t == 0x128)
                {
                    // Read back the channel name from the buffer
                    
                    // Helper to clean string

                        
                        _logger.ZLogInformation($"Waiting for connection...");
                        while (!IsConnected)
                        {
                            await Task.Delay(1000);
                        }
                        _logger.ZLogInformation($"Connection established");
                        await _featureHandler.Refresh();
                        
                        if(showDialogs)
                            await _dialogCoordinator.ShowMessageAsync(this, "Injected", "Injected");
                }
                else
                {
                    _logger.ZLogError($"Injection failed with result code: 0x{t:X}");
                    if(showDialogs)
                        await _dialogCoordinator.ShowMessageAsync(this, "failed to inject", "failed to inject");
                }

            }
            catch(Exception ex)
            {
                _logger.ZLogError(ex, $"Exception during injection: {ex.Message}");
                if(showDialogs)
                    await _dialogCoordinator.ShowMessageAsync(this, "failed to inject", $"{ex.Message}|{ex.StackTrace}");
            }
            finally
            {
                _isInjecting = false;
                _logger.ZLogDebug($"InjectInternal completed, _isInjecting reset to false");
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