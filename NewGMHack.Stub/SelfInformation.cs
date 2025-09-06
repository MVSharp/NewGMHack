using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NewGMHack.CommunicationModel.Models;

namespace NewGMHack.Stub
{
    public class SelfInformation
    {
        public Info           PersonInfo   { get; set; } = new();
        public ClientConfig   ClientConfig { get; set; } = new();
        public ConcurrentBag<Roommate> Roommates    { get; set; } = [];
        public SelfInformation()
        {
        }
    }
}