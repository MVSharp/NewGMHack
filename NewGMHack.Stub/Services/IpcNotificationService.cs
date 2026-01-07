using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Logging;
using Microsoft.IO;
using NewGMHack.CommunicationModel.IPC;
using NewGMHack.CommunicationModel.IPC.Responses;
using SharedMemory;

namespace NewGMHack.Stub.Services;

public class IpcNotificationService
{
    private readonly ILogger<IpcNotificationService> _logger;
    private readonly RpcBuffer _rpcBuffer;
    private readonly MessagePackSerializerOptions _options =
        MessagePackSerializerOptions.Standard.WithResolver(TypelessContractlessStandardResolver.Instance);
    
    private readonly RecyclableMemoryStreamManager _streamManager = new();

    public IpcNotificationService(ILogger<IpcNotificationService> logger, RpcBuffer rpcBuffer)
    {
        _logger = logger;
        _rpcBuffer = rpcBuffer;
    }

    public async Task SendRewardNotificationAsync(RewardNotification notification)
    {
        try
        {
            await using var stream = _streamManager.GetStream();
            
            var request = new DynamicOperationRequest
            {
                Operation = Operation.RewardNotification,
                Parameters = notification 
            };

            await MessagePackSerializer.SerializeAsync(stream, request, _options);
            
            // Channel "Sdhook" is bidirectional
            await _rpcBuffer.RemoteRequestAsync(stream.ToArray(), 500); 
        }
        catch (Exception ex)
        {
            // _logger.LogWarning($"Failed to send IPC notification: {ex.Message}");
        }
    }
    public async Task SendMachineInfoUpdateAsync(object machineInfo)
    {
        try
        {
            await using var stream = _streamManager.GetStream();
            
            var request = new DynamicOperationRequest
            {
                Operation = Operation.MachineInfoUpdate,
                Parameters = machineInfo 
            };

            await MessagePackSerializer.SerializeAsync(stream, request, _options);
            
            await _rpcBuffer.RemoteRequestAsync(stream.ToArray(), 500); 
        }
        catch (Exception ex)
        {
            // _logger.LogWarning($"Failed to send IPC notification: {ex.Message}");
        }
    }
}
