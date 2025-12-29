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
using NewGmHack.GUI; // For RemoteHandler

namespace NewGmHack.GUI.Services
{
    public class WebHostService : BackgroundService
    {
        private readonly ILogger<WebHostService> _logger;
        private readonly System.Threading.Channels.Channel<RewardNotification> _channel;
        private readonly System.Threading.Channels.Channel<WebMessage> _webChannel;
        private readonly IWebServerStatus _webServerStatus;
        private readonly RemoteHandler _remoteHandler;
        private IHost? _webHost;

        public WebHostService(ILogger<WebHostService> logger, 
                              System.Threading.Channels.Channel<RewardNotification> channel,
                              System.Threading.Channels.Channel<WebMessage> webChannel,
                              IWebServerStatus webServerStatus,
                              RemoteHandler remoteHandler)
        {
            _logger = logger;
            _channel = channel;
            _webChannel = webChannel;
            _webServerStatus = webServerStatus;
            _remoteHandler = remoteHandler;
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

                                // GET /api/features
                                endpoints.MapGet("/api/features", async (RemoteHandler handler) =>
                                {
                                    var features = await handler.GetFeatures();
                                    return Results.Ok(features);
                                });

                                // POST /api/features  Body: { FeatureName: "...", IsEnabled: true }
                                endpoints.MapPost("/api/features", async (HttpContext context, RemoteHandler handler) =>
                                {
                                    // Bind manually or use FromBody if using full MVC. Minimal API supports binding.
                                    var req = await context.Request.ReadFromJsonAsync<FeatureChangeRequests>();
                                    if(req == null) return Results.BadRequest();

                                    var result = await handler.SetFeatureEnable(req);
                                    return Results.Ok(result);
                                });

                                // GET /api/me (PersonInfo)
                                endpoints.MapGet("/api/me", async (RemoteHandler handler) =>
                                {
                                    var info = await handler.AskForInfo();
                                    return Results.Ok(info);
                                });

                                // GET /api/roommates
                                endpoints.MapGet("/api/roommates", async (RemoteHandler handler) =>
                                {
                                    var list = await handler.GetRoommates();
                                    return Results.Ok(list);
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
}
