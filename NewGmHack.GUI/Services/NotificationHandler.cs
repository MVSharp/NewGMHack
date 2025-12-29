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
    private readonly MessagePackSerializerOptions _options =
        MessagePackSerializerOptions.Standard.WithResolver(TypelessContractlessStandardResolver.Instance);

    public NotificationHandler(ILogger<NotificationHandler> logger, System.Threading.Channels.Channel<RewardNotification> channel)
    {
        _logger = logger;
        _channel = channel;
    }

    public async Task<byte[]> HandleAsync(ulong uid, ReadOnlyMemory<byte> payload)
    {
        try
        {
            var request = MessagePackSerializer.Deserialize<DynamicOperationRequest>(payload, _options);

            if (request.Operation == Operation.RewardNotification)
            {
                // The parameters should be the RewardNotification object
                // However, due to typeless deserialization, it might need casting or re-deserialization if it came as object.
                // But since we used TypelessContractlessStandardResolver, it might be an object that fits.
                // Let's safe cast.
                
                if (request.Parameters is RewardNotification notification)
                {
                    _logger.LogInformation($"Received Reward Notification for Player {notification.PlayerId} - Points: {notification.Points}");
                    _channel.Writer.TryWrite(notification);
                }
                else if (request.Parameters is byte[] bytes) 
                {
                     // Fallback if parameters came as bytes (nested serialization)
                     // var n = MessagePackSerializer.Deserialize<RewardNotification>(bytes);
                }
                else
                {
                    // If MessagePack deserialized it into a PropertyBag or Hashtable (dynamic), we might need mapping.
                    // Given shared models, it should deserialize correctly if Typeless is used on both ends.
                    // Actually, if 'Parameters' is defined as object, MessagePack stores type info if Typeless is used.
                    
                    if (request.Parameters != null)
                    {
                         // _logger.LogInformation($"Received update: {System.Text.Json.JsonSerializer.Serialize(request.Parameters)}");
                    }
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
