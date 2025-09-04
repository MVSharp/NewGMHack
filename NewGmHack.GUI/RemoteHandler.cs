using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.IO;
using NewGMHack.CommunicationModel.IPC;
using NewGMHack.CommunicationModel.IPC.Requests;
using NewGMHack.CommunicationModel.IPC.Responses;
using NewGMHack.Stub;
using SharedMemory;

namespace NewGmHack.GUI
{
    public class RemoteHandler(RpcBuffer master) : RemoteHandlerBase(master)
    {
        public async Task<bool> AskForHealth()
        {
            var r = await this.SendRequestAsync<HealthIPCResponse>(Operation.Health);
            return r.IsHealth;
        }

        public async Task<Info> AskForInfo()
        {
            var r = await SendRequestAsync<Info>(Operation.Info);
            return r;
        }

        public async Task<ConfigPropertyIPCResponse> SetFeatureEnable(FeatureChangeRequests request)
        {
            var r = await SendRequestAsync<ConfigPropertyIPCResponse>(Operation.SetProperty , [request]);
            return r;
        }

        public  Task<List<HackFeatures>> GetFeatures()
        {
            return SendRequestAsync<List<HackFeatures>>(Operation.GetFeaturesList);
        }

        public async Task<bool> DeattachRequest()
        {
            //TODO :redesign here later , it never have response
            var a= SendRequestAsync<DeattachResponse>(Operation.DeattachRequest);
            return true;
        }
    }

    public class RemoteHandlerBase(RpcBuffer master)
    {
        private readonly MessagePackSerializerOptions _options =
            MessagePackSerializerOptions.Standard.WithResolver(new TypelessContractlessStandardResolver());

        private static readonly RecyclableMemoryStreamManager recyclableMemoryStreamManager = new();
        //TODO add paramters based 
        public async Task<T?> SendRequestAsync<T>(Operation operation ,List<object> parameters = null, int timeout = 10) where T : class, new()
        {
            await using var stream  = recyclableMemoryStreamManager.GetStream("sdhook");
            var             request = new DynamicOperationRequest
            {
                Operation  = operation,
                Parameters = parameters
            };
            await MessagePackSerializer.SerializeAsync(stream, request, _options);

            var response = await master.RemoteRequestAsync(stream.ToArray(), timeout);
            if (response.Success)
            {
                if (response.Data.Length > 0)
                {
                    
                    var body = MessagePackSerializer.Deserialize<DynamicOperationResponse<T>>(response.Data,_options);
                    return body.Result;
                }
            }

            return new T();
        }
    }
}