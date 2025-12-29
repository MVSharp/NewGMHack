using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using Dapper;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using NewGMHack.CommunicationModel.IPC.Requests;
using NewGMHack.CommunicationModel.IPC.Responses;
using NewGMHack.CommunicationModel.Models;
using NewGmHack.GUI; 
using NewGmHack.GUI.ViewModels; 
using NewGMHack.Stub; // For ClientConfig
using System.Reflection;
using System.Linq;


namespace NewGmHack.GUI.Services
{
    public partial class WebHostService : BackgroundService
    {
        private readonly ILogger<WebHostService> _logger;
        private readonly System.Threading.Channels.Channel<RewardNotification> _channel;
        private readonly System.Threading.Channels.Channel<WebMessage> _webChannel;
        private readonly IWebServerStatus _webServerStatus;
        private readonly RemoteHandler _remoteHandler;
        private readonly MainViewModel _mainViewModel; 
        private IHost? _webHost;
        private readonly List<HackFeatures> _offlineFeatures = new ClientConfig().Features;


        public WebHostService(ILogger<WebHostService> logger, 
                              System.Threading.Channels.Channel<RewardNotification> channel,
                              System.Threading.Channels.Channel<WebMessage> webChannel,
                              IWebServerStatus webServerStatus,
                              RemoteHandler remoteHandler,
                              MainViewModel mainViewModel) // Added
        {
            _logger = logger;
            _channel = channel;
            _webChannel = webChannel;
            _webServerStatus = webServerStatus;
            _remoteHandler = remoteHandler;
            _mainViewModel = mainViewModel;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NewGMHack");
                var dbPath = Path.Combine(folder, "rewards.db");
                var connString = $"Data Source={dbPath};Mode=ReadOnly";

                // Dynamic Port Detection (Start at 5000)
                int port = GetAvailablePort(5000);
                string url = $"http://localhost:{port}";
                
                // Update shared status so MainWindow knows the correct URL
                _webServerStatus.BaseUrl = url;

                _webHost = Host.CreateDefaultBuilder()
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseContentRoot(AppDomain.CurrentDomain.BaseDirectory); // Ensure we look in EXE dir
                        webBuilder.UseUrls(url);
                        webBuilder.ConfigureServices(services =>
                        {
                            services.AddCors();
                            services.AddSignalR();
                            services.AddSingleton(new DbConfig { ConnectionString = connString });
                            // Pass the outer channel to inner container
                            services.AddSingleton(_channel);
                            services.AddSingleton(_webChannel);
                            // Pass RemoteHandler
                            services.AddSingleton(_remoteHandler);
                            services.AddHostedService<RewardBroadcaster>();
                            services.AddHostedService<WebBroadcaster>();
                        });
                        webBuilder.Configure(app =>
                        {
                            app.UseCors(x => x.AllowAnyHeader().AllowAnyMethod().AllowCredentials().SetIsOriginAllowed(_ => true));
                            app.UseDefaultFiles();
                            app.UseStaticFiles();
                            app.UseRouting();

                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapHub<RewardHub>("/rewardHub");
                                
                                // API: Get Player Stats Summary
                                endpoints.MapGet("/api/stats/{playerId}", async (HttpContext context, uint playerId, DbConfig db) =>
                                {
                                    using var conn = new SqliteConnection(db.ConnectionString);
                                    
                                    // Total Stats
                                    var sql = @"
                                        SELECT 
                                            COUNT(*) as Matches,
                                            SUM(ifnull(Points,0)) as TotalPoints,
                                            SUM(ifnull(Kills,0)) as TotalKills,
                                            SUM(ifnull(Deaths,0)) as TotalDeaths,
                                            SUM(ifnull(Supports,0)) as TotalSupports,
                                            MAX(Points) as MaxPoints,
                                            SUM(ifnull(GBGain,0)) as TotalGBGain,
                                            SUM(ifnull(MachineAddedExp,0)) as TotalMachineExp,
                                            SUM(ifnull(Bonus1,0) + ifnull(Bonus2,0) + ifnull(Bonus3,0) + ifnull(Bonus4,0) + ifnull(Bonus5,0) + ifnull(Bonus6,0) + ifnull(Bonus7,0) + ifnull(Bonus8,0)) as TotalBonusGB,
                                            MIN(CreatedAtUtc) as FirstSortieDate,
                                            MAX(CreatedAtUtc) as LastSortieDate
                                        FROM MatchRewards 
                                        WHERE PlayerId = @PlayerId";
                                    var stats = await conn.QuerySingleOrDefaultAsync(sql, new { PlayerId = playerId });
                                    
                                    // Hourly Stats (Last 60 mins)
                                    var sqlHourly = @"
                                        SELECT 
                                            COUNT(*) as MatchesLastHour,
                                            SUM(GBGain) as GBGainLastHour,
                                            SUM(MachineAddedExp) as MachineExpLastHour,
                                            SUM(ifnull(Bonus1,0) + ifnull(Bonus2,0) + ifnull(Bonus3,0) + ifnull(Bonus4,0) + ifnull(Bonus5,0) + ifnull(Bonus6,0) + ifnull(Bonus7,0) + ifnull(Bonus8,0)) as BonusLastHour
                                        FROM MatchRewards
                                        WHERE PlayerId = @PlayerId AND datetime(CreatedAtUtc) >= datetime('now', '-1 hour')";
                                    var hourly = await conn.QuerySingleOrDefaultAsync(sqlHourly, new { PlayerId = playerId });

                                    // Today Stats (Since midnight UTC)
                                    var sqlToday = @"
                                        SELECT 
                                            COUNT(*) as MatchesToday,
                                            SUM(GBGain) as GBGainToday,
                                            SUM(MachineAddedExp) as MachineExpToday,
                                            SUM(ifnull(Bonus1,0) + ifnull(Bonus2,0) + ifnull(Bonus3,0) + ifnull(Bonus4,0) + ifnull(Bonus5,0) + ifnull(Bonus6,0) + ifnull(Bonus7,0) + ifnull(Bonus8,0)) as BonusToday
                                        FROM MatchRewards
                                        WHERE PlayerId = @PlayerId AND datetime(CreatedAtUtc) >= date('now')";
                                    var today = await conn.QuerySingleOrDefaultAsync(sqlToday, new { PlayerId = playerId });

                                    await context.Response.WriteAsJsonAsync(new { stats, hourly, today }, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = null });
                                });

                                endpoints.MapGet("/api/history/{playerId}", async (HttpContext context, uint playerId, DbConfig db) =>
                                {
                                    using var conn = new SqliteConnection(db.ConnectionString);
                                    var sql = @"
                                        SELECT *, 
                                        (ifnull(Bonus1,0) + ifnull(Bonus2,0) + ifnull(Bonus3,0) + ifnull(Bonus4,0) + ifnull(Bonus5,0) + ifnull(Bonus6,0) + ifnull(Bonus7,0) + ifnull(Bonus8,0)) as TotalBonus 
                                        FROM MatchRewards 
                                        WHERE PlayerId = @PlayerId 
                                        ORDER BY CreatedAtUtc DESC LIMIT 10";
                                    var history = await conn.QueryAsync(sql, new { PlayerId = playerId });
                                    // Use specific serializer options to keep PascalCase if generic object, or just rely on default and handle in JS
                                    await context.Response.WriteAsJsonAsync(history, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = null });
                                });

                                // === NEW API ENDPOINTS ===

                                // GET /api/features (Modified)
                                endpoints.MapGet("/api/features", async (RemoteHandler handler) =>
                                {
                                    List<HackFeatures> sourceList;
                                    if(_mainViewModel.IsConnected)
                                    {
                                        sourceList = await handler.GetFeatures();
                                        // Sync offline config with remote state just in case? 
                                        // For now, remote is truth.
                                    }
                                    else 
                                    {
                                        sourceList = _offlineFeatures;
                                    }

                                    // Map to DTO
                                    var dtos = sourceList.Select(f => ToDto(f)).ToList();
                                    return Results.Ok(dtos);
                                });

                                // POST /api/features
                                endpoints.MapPost("/api/features", async (HttpContext context, RemoteHandler handler) =>
                                {
                                    var req = await context.Request.ReadFromJsonAsync<FeatureChangeRequests>();
                                    if(req == null) return Results.BadRequest();

                                    // 1. Update Offline Config (Single Source of Truth for UI when disconnected)
                                    var offlineItem = _offlineFeatures.FirstOrDefault(x => x.Name == req.FeatureName);
                                    if(offlineItem != null) offlineItem.IsEnabled = req.IsEnabled;

                                    // 2. If Connected, Send to Remote
                                    object result = true;
                                    if(_mainViewModel.IsConnected)
                                    {
                                        result = await handler.SetFeatureEnable(req);
                                    }

                                    return Results.Ok(result);
                                });

                                // GET /api/me (PersonInfo)
                                endpoints.MapGet("/api/me", async (RemoteHandler handler) =>
                                {
                                    if(!_mainViewModel.IsConnected) return Results.Ok(new Info()); // Empty info
                                    var info = await handler.AskForInfo();
                                    return Results.Ok(info);
                                });

                                // GET /api/roommates
                                endpoints.MapGet("/api/roommates", async (RemoteHandler handler) =>
                                {
                                    if(!_mainViewModel.IsConnected) return Results.Ok(new System.Collections.Generic.List<Roommate>());
                                    var list = await handler.GetRoommates();
                                    return Results.Ok(list);
                                });
                                
                                // INJECTION ENDPOINTS
                                endpoints.MapPost("/api/inject", async () => 
                                {
                                    var status = await _mainViewModel.InjectFromWeb();
                                    return Results.Ok(new { status });
                                });
                                
                                endpoints.MapPost("/api/deattach", async () => 
                                {
                                    await _mainViewModel.DeattachFromWeb();
                                    return Results.Ok();
                                });

                                endpoints.MapGet("/api/status", () => 
                                {
                                    return Results.Ok(new { isConnected = _mainViewModel.IsConnected });
                                });

                                // API: App Version
                                endpoints.MapGet("/api/version", () => 
                                {
                                    try
                                    {
                                        var stubPath = "NewGMHack.Stub.dll";
                                        if (System.IO.File.Exists(stubPath))
                                        {
                                            var version = System.Reflection.AssemblyName.GetAssemblyName(stubPath).Version;
                                            return Results.Ok(new { version = version?.ToString() ?? "1.0.0.0" });
                                        }
                                        return Results.Ok(new { version = "1.0.0.0" });
                                    }
                                    catch
                                    {
                                        return Results.Ok(new { version = "1.0.0.0" });
                                    }
                                });
                            });
                        });
                    })
                    .Build();

                _logger.LogInformation($"Starting Web Server at {url}");
                await _webHost.StartAsync(stoppingToken);

                try { await Task.Delay(Timeout.Infinite, stoppingToken); } catch (OperationCanceledException) { }

                await _webHost.StopAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Web Server");
            }
        }

        private int GetAvailablePort(int startingPort)
        {
            var properties = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
            var activeTcpConnections = properties.GetActiveTcpConnections();
            var activeTcpListeners = properties.GetActiveTcpListeners();
            var usedPorts = new System.Collections.Generic.HashSet<int>();

            foreach (var c in activeTcpConnections) usedPorts.Add(c.LocalEndPoint.Port);
            foreach (var l in activeTcpListeners) usedPorts.Add(l.Port);

            for (int p = startingPort; p < startingPort + 1000; p++)
            {
                if (!usedPorts.Contains(p)) return p;
            }

            return 0; // Let OS decide if we run out of range (unlikely)
        }
    }

    public class RewardBroadcaster : BackgroundService
    {
        private readonly System.Threading.Channels.Channel<RewardNotification> _channel;
        private readonly IHubContext<RewardHub>                                _hub;

        public RewardBroadcaster(System.Threading.Channels.Channel<RewardNotification> channel, IHubContext<RewardHub> hub)
        {
            _channel = channel;
            _hub = hub;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach(var item in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                // Broadcast to all clients
                await _hub.Clients.All.SendAsync("ReceiveReward", item, stoppingToken);
            }
        }
    }

    public class WebBroadcaster : BackgroundService
    {
        private readonly System.Threading.Channels.Channel<WebMessage> _channel;
        private readonly IHubContext<RewardHub> _hub;

        public WebBroadcaster(System.Threading.Channels.Channel<WebMessage> channel, IHubContext<RewardHub> hub)
        {
            _channel = channel;
            _hub = hub;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                // MessageType = "UpdatePersonInfo", "UpdateRoommates", etc.
                await _hub.Clients.All.SendAsync(item.MessageType, item.Payload, stoppingToken);
            }
        }
    }

    public class DbConfig { public string ConnectionString { get; set; } = ""; }

    public class FeatureDto
    {
        public string Name { get; set; } = "";
        public bool IsEnabled { get; set; }
        public string DisplayNameEn { get; set; } = "";
        public string DisplayNameCn { get; set; } = "";
        public string DisplayNameTw { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public partial class WebHostService
    {
        private static FeatureDto ToDto(HackFeatures f)
        {
            var dto = new FeatureDto 
            { 
                Name = f.Name.ToString(), 
                IsEnabled = f.IsEnabled 
            };

            var memberInfo = typeof(FeatureName).GetMember(f.Name.ToString()).FirstOrDefault();
            if (memberInfo != null)
            {
                var attr = memberInfo.GetCustomAttribute<FeatureMetadataAttribute>();
                if (attr != null)
                {
                    dto.DisplayNameEn = attr.DisplayNameEn;
                    dto.DisplayNameCn = attr.DisplayNameCn;
                    dto.DisplayNameTw = attr.DisplayNameTw;
                    dto.Description = attr.Description;
                }
            }
            return dto;
        }
    }
}
