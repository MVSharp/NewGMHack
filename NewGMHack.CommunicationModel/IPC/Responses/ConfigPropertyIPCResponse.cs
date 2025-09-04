using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;

namespace NewGMHack.CommunicationModel.IPC.Responses
{
    [MessagePackObject]
    public class ConfigPropertyIPCResponse
    {
        [Key(0)]
        public bool Original { get; set; }
        [Key(1)]
        public bool New    { get; set; }
    }
}
