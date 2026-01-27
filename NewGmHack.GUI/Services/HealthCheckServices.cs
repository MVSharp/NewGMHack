using Microsoft.Extensions.Hosting;
using NewGmHack.GUI.Abstracts;

namespace NewGmHack.GUI.Services
{
    public class HealthCheckServices(
        RemoteHandler       handler,
        IHealthCheckHandler healthCheckHandler,
        IPersonInfoHandler  personInfoHandler,
        IRoomManager roomManager,
        System.Threading.Channels.Channel<WebMessage> webChannel
        ) : BackgroundService
    {
        private bool _wasConnected = false;

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //return;
            bool isConnected = false;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var health = await handler.AskForHealth();
                    healthCheckHandler.SetHealthStatus(health);

                    // Track connection state
                    isConnected = health;

                    // Broadcast connection status change
                    if (isConnected != _wasConnected)
                    {
                        _wasConnected = isConnected;
                        webChannel.Writer.TryWrite(new WebMessage("UpdateConnectionStatus", new { IsConnected = isConnected }));
                    }

                    var info = await handler.AskForInfo();
                    personInfoHandler.SetInfo(info);
                    webChannel.Writer.TryWrite(new WebMessage("UpdatePersonInfo", info));

                    var roommates = await handler.GetRoommates();
                    roomManager.UpdateRoomList(roommates);
                    webChannel.Writer.TryWrite(new WebMessage("UpdateRoommates", roommates));

                    await Task.Delay(1000, stoppingToken).ConfigureAwait(false); // Success - wait 1s
                }
                catch
                {
                    // Connection lost
                    if (_wasConnected)
                    {
                        _wasConnected = false;
                        webChannel.Writer.TryWrite(new WebMessage("UpdateConnectionStatus", new { IsConnected = false }));
                    }

                    // If failed, wait longer to avoid spamming exceptions on disconnect
                    await Task.Delay(2000, stoppingToken).ConfigureAwait(false);
                }
            }
        }
    }
}