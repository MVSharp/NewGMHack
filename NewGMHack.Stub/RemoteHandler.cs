using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.IO;
using NewGMHack.CommunicationModel.IPC;

namespace NewGMHack.Stub
{
    internal class RemoteHandler(SelfInformation self)
    {
        private readonly RecyclableMemoryStreamManager recyclableMemoryStreamManager = new();

        private readonly MessagePackSerializerOptions _options = MessagePackSerializerOptions.Standard
           .WithResolver(new TypelessContractlessStandardResolver());
        public async Task<byte[]> HandleAsync(ulong uid, ReadOnlyMemory<byte> payload)
        {
            var                      sharedBuffer = MessagePackSerializer.Deserialize<DynamicOperationRequest>(payload);
            DynamicOperationResponse response     = new(){Success = false};
            switch (sharedBuffer.Operation)
            {
                case "HealthCheck":
                    response.Success = true;
                    break;
                case "SelfInfo":
                    response.Success = true;
                    response.Result  = self.PersonInfo;
                    break;
            }
            await using var          stream       = recyclableMemoryStreamManager.GetStream("sdhook");
            await MessagePackSerializer.SerializeAsync(stream, response,
                                                       _options);
            return stream.ToArray();
        }
    }
}
