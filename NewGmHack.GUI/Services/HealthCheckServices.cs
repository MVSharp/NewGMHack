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
        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //return;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var healths = await handler.AskForHealth();
                    healthCheckHandler.SetHealthStatus(healths);
                    
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
                    // If failed, wait longer to avoid spamming exceptions on disconnect
                    await Task.Delay(2000, stoppingToken).ConfigureAwait(false);
                }
            }
        }
    }
}