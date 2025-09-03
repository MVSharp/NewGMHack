using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;

namespace NewGMHack.CommunicationModel.IPC
{
    [MessagePackObject]
    public class DynamicOperationRequest
    {
        [Key(0)]
        public string Operation { get; set; } = string.Empty;

        [Key(1)]
        public object Parameters { get; set; } = default!;

        [Key(2)]
        public Type ParameterType { get; set; } = default!;
    }

    [MessagePackObject]
    public class DynamicOperationResponse
    {

        [Key(0)]
        public bool Success { get; set; }

        [Key(1)]
        public object? Result { get; set; }

        [Key(2)]
        public Type? ResultType { get; set; }
        [Key(3)]
        public string? ErrorMessage { get; set; }
    }
}
