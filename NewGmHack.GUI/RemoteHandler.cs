using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.IO;
using NewGMHack.CommunicationModel.IPC;
using SharedMemory;

namespace NewGmHack.GUI
{
    public class RemoteHandler(RpcBuffer master)
    {
        private static readonly RecyclableMemoryStreamManager recyclableMemoryStreamManager =new RecyclableMemoryStreamManager();

        private readonly MessagePackSerializerOptions _options =
            MessagePackSerializerOptions.Standard.WithResolver(new TypelessContractlessStandardResolver());
        public async Task<bool> AskForHealth()
        {
            await using var stream  = recyclableMemoryStreamManager.GetStream("sdhook");
            var             request = new DynamicOperationRequest();
            await MessagePackSerializer.SerializeAsync(stream, request, _options);
            var r = await  master.RemoteRequestAsync(stream.ToArray(), 10);
            return r.Success;
        }
    }
}
