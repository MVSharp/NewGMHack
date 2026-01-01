using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Logging;
using NewGMHack.CommunicationModel.IPC;
using NewGMHack.CommunicationModel.IPC.Responses;

namespace NewGmHack.GUI.Services;

public class NotificationHandler
{
    private readonly ILogger<NotificationHandler> _logger;
    private readonly System.Threading.Channels.Channel<RewardNotification> _channel;
    private readonly System.Threading.Channels.Channel<WebMessage> _webChannel;
    private readonly MessagePackSerializerOptions _options =
        MessagePackSerializerOptions.Standard.WithResolver(TypelessContractlessStandardResolver.Instance);

    public NotificationHandler(ILogger<NotificationHandler> logger, 
                               System.Threading.Channels.Channel<RewardNotification> channel,
                               System.Threading.Channels.Channel<WebMessage> webChannel)
    {
        _logger = logger;
        _channel = channel;
        _webChannel = webChannel;
    }

    public async Task<byte[]> HandleAsync(ulong uid, ReadOnlyMemory<byte> payload)
    {
        try
        {
            var request = MessagePackSerializer.Deserialize<DynamicOperationRequest>(payload, _options);

            if (request.Operation == Operation.RewardNotification)
            {
                if (request.Parameters is RewardNotification notification)
                {
                    _logger.LogInformation($"Received Reward Notification for Player {notification.PlayerId} - Points: {notification.Points}");
                    _channel.Writer.TryWrite(notification);
                }
            }
            else if (request.Operation == Operation.MachineInfoUpdate)
            {
                // Verify parameters is not null, then forward
                if (request.Parameters != null)
                {
                    _logger.LogInformation($"Received Machine Info Update. Type: {request.Parameters.GetType().FullName}");
                    _webChannel.Writer.TryWrite(new WebMessage("UpdateMachineInfo", request.Parameters));
                }
            }

            // Acknowledge success
            var response = new DynamicOperationResponse { Success = true };
            return MessagePackSerializer.Serialize(response, _options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling IPC notification");
            return [];
        }
    }
}
