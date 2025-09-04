using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;

namespace NewGMHack.CommunicationModel.IPC
{
    public enum Operation
    {
        None,
        Health,
        Info,
        SetProperty,
        GetFeaturesList,
        DeattachRequest,
    }

    [MessagePackObject]
    public class DynamicOperationRequest
    {
        [Key(0)] public Operation Operation { get; set; } = Operation.None;

        [Key(1)] public object Parameters { get; set; } = default!;

        [Key(2)] public Type ParameterType { get; set; } = default!;
    }

    [MessagePackObject]
    public class DynamicOperationResponse
    {
        [Key(0)] public bool Success { get; set; }

        //[Key(1)] public object? Result { get; set; }

        [Key(2)] public Type?   ResultType   { get; set; }
        [Key(3)] public string? ErrorMessage { get; set; }
    }

    [MessagePackObject]
    public class DynamicOperationResponse<T> : DynamicOperationResponse
    {
        //[Key(0)] public bool Success { get; set; }

        [Key(1)] public T? Result { get; set; }

        //[Key(2)] public string? ErrorMessage { get; set; }
    }
}