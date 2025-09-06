using Microsoft.Extensions.Hosting;
using NewGmHack.GUI.Abstracts;

namespace NewGmHack.GUI.Services
{
    public class HealthCheckServices(
        RemoteHandler       handler,
        IHealthCheckHandler healthCheckHandler,
        IPersonInfoHandler  personInfoHandler,
        IRoomManager roomManager
        ) : BackgroundService
    {
        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var healths = await handler.AskForHealth();
                    healthCheckHandler.SetHealthStatus(healths);
                    var info = await handler.AskForInfo();
                    personInfoHandler.SetInfo(info);
                    var roommates = await handler.GetRoommates();
                    roomManager.UpdateRoomList(roommates);
                }
                catch
                {
                    // ignored
                }

                await Task.Delay(2000, stoppingToken);
            }
        }
    }
}