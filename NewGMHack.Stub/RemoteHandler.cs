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
using NewGMHack.CommunicationModel.Models;
using ZLinq;
using ZLogger;

namespace NewGMHack.Stub
{
    internal class RemoteHandler(SelfInformation self, ILogger<RemoteHandler> logger, WinsockHookManager manager)
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
                        Result  = new HealthIPCResponse { IsHealth = true }
                    };
                    await MessagePackSerializer.SerializeAsync(stream, response, _options);
                    break;
                }

                case Operation.Info:
                {
                    var response = new DynamicOperationResponse<Info>
                    {
                        Success = true,
                        Result  = self.PersonInfo
                    };
                    await MessagePackSerializer.SerializeAsync(stream, response, _options);
                    break;
                }

                case Operation.SetProperty:
                {
                    var response = new DynamicOperationResponse<ConfigPropertyIPCResponse>();

                    if (dynamicRequest.Parameters is List<object> datas && datas.Count > 0)
                    {
                        foreach (var data in datas.Cast<FeatureChangeRequests>())
                        {
                            var feature = self.ClientConfig.Features.GetFeature(data.FeatureName);

                            logger.LogInformation($"find featres : {data.FeatureName} value:{data.IsEnabled}");
                            if (feature == null) continue;
                            logger.LogInformation($"set featres : {data.FeatureName} value:{data.IsEnabled}");
                            var original = feature.IsEnabled;
                            feature.IsEnabled = data.IsEnabled;

                            response.Success = true;
                            response.Result = new ConfigPropertyIPCResponse
                            {
                                Original = original,
                                New      = data.IsEnabled
                            };
                        }
                    }
                    else
                    {
                        logger.ZLogInformation($"set property request but not type of {typeof(FeatureChangeRequests)}");
                    }

                    await MessagePackSerializer.SerializeAsync(stream, response, _options);
                    break;
                }

                case Operation.GetFeaturesList:
                {
                    var response = new DynamicOperationResponse<List<HackFeatures>>
                    {
                        Success = true,
                        Result  = self.ClientConfig.Features
                    };
                    await MessagePackSerializer.SerializeAsync(stream, response, _options);
                    break;
                }

                case Operation.DeattachRequest:
                {
                    var response = new DynamicOperationResponse<object>
                    {
                        Success = true,
                        Result  = null
                    };
                    manager.UnHookAll();
                    await MessagePackSerializer.SerializeAsync(stream, response, _options);
                    // brutal ways , it is not correct
                    Environment.Exit(0);
                    break;
                }
                case Operation.GetRoomInfo:
                {
                    var response = new DynamicOperationResponse<IEnumerable<Roommate>>();
                    response.Result = self.Roommates.ToList();
                    await MessagePackSerializer.SerializeAsync(stream, response, _options);
                    break;
                }
                case Operation.None:
                default:
                {
                    var response = new DynamicOperationResponse<object>
                    {
                        Success      = false,
                        Result       = null,
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