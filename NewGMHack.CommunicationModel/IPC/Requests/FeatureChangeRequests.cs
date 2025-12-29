using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using NewGMHack.Stub;

namespace NewGMHack.CommunicationModel.IPC.Requests
{
    [MessagePackObject]
    public class FeatureChangeRequests
    {
        [Key(0)]
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
        public FeatureName FeatureName { get; set; }
        [Key(1)]
        public bool        IsEnabled   { get; set; }
    }
}
