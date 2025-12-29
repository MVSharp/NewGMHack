using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using NewGMHack.CommunicationModel.IPC.Responses;

namespace NewGmHack.GUI.Services
{
    public class RewardHub : Hub
    {
        // Clients can connect and listen for "ReceiveReward"
        // This method can be called by clients to echo, but mostly we use it to broadcast from Server.
        public async Task SendRewardUpdate(RewardNotification notification)
        {
            await Clients.All.SendAsync("ReceiveReward", notification);
        }
    }
}
