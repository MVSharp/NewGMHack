using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Logging;
using Microsoft.IO;
using NewGMHack.CommunicationModel.IPC;
using NewGMHack.CommunicationModel.IPC.Requests;
using NewGMHack.CommunicationModel.IPC.Responses;
using ZLinq;
using ZLogger;

namespace NewGMHack.Stub
{
    internal class RemoteHandler(SelfInformation self ,ILogger<RemoteHandler> logger)
    {
        private readonly RecyclableMemoryStreamManager recyclableMemoryStreamManager = new();

        private readonly MessagePackSerializerOptions _options =
            MessagePackSerializerOptions.Standard.WithResolver(new TypelessContractlessStandardResolver());
        private ConfigPropertyIPCResponse BuildResponse(bool     originalValue, object newValueObj,
                                                            out bool updatedValue)
        {
            bool newValue = Convert.ToBoolean(newValueObj);
            updatedValue = newValue;

            return new ConfigPropertyIPCResponse
            {
                Original = originalValue,
                New      = newValue
            };
        }

public async Task<byte[]> HandleAsync(ulong uid, ReadOnlyMemory<byte> payload)
{

    logger.ZLogInformation($"Operations client:{"recviced"}");
    var dynamicRequest = MessagePackSerializer.Deserialize<DynamicOperationRequest>(payload, _options);
    logger.ZLogInformation($"Operations client:{dynamicRequest.Operation}");

    await using var stream = recyclableMemoryStreamManager.GetStream("sdhook");

    switch (dynamicRequest.Operation)
    {
        case Operation.Health:
        {
            var response = new DynamicOperationResponse<HealthIPCResponse>
            {
                Success = true,
                Result = new HealthIPCResponse { IsHealth = true }
            };
            await MessagePackSerializer.SerializeAsync(stream, response, _options);
            break;
        }

        case Operation.Info:
        {
            var response = new DynamicOperationResponse<Info>
            {
                Success = true,
                Result = self.PersonInfo
            };
            await MessagePackSerializer.SerializeAsync(stream, response, _options);
            break;
        }

        case Operation.SetProperty:
        {
            var response = new DynamicOperationResponse<ConfigPropertyIPCResponse>();

            if (dynamicRequest.Parameters is FeatureChangeRequests data)
            {
                var feature = self.ClientConfig.Features.AsValueEnumerable()
                    .FirstOrDefault(x => x.Name == data.FeatureName);

                if (feature != null)
                {
                    var original = feature.IsEnabled;
                    feature.IsEnabled = data.IsEnabled;

                    response.Success = true;
                    response.Result = new ConfigPropertyIPCResponse
                    {
                        Original = original,
                        New = data.IsEnabled
                    };
                }
            }

            await MessagePackSerializer.SerializeAsync(stream, response, _options);
            break;
        }

        case Operation.GetFeaturesList:
        {
            var response = new DynamicOperationResponse<List<HackFeatures>>
            {
                Success = true,
                Result = self.ClientConfig.Features
            };
            await MessagePackSerializer.SerializeAsync(stream, response, _options);
            break;
        }

        case Operation.DeattachRequest:
        {
            var response = new DynamicOperationResponse<object>
            {
                Success = true,
                Result = null
            };
            // TODO: unhook, close, etc.
            await MessagePackSerializer.SerializeAsync(stream, response, _options);
            break;
        }

        case Operation.None:
        default:
        {
            var response = new DynamicOperationResponse<object>
            {
                Success = false,
                Result = null,
                ErrorMessage = "Unknown or unsupported operation"
            };
            await MessagePackSerializer.SerializeAsync(stream, response, _options);
            break;
        }
    }

    return stream.ToArray();
}
    }
}