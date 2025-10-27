using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Faster.Map.Concurrent;
using NewGMHack.CommunicationModel.Models;
using SharpDX;

namespace NewGMHack.Stub
{
    public class Entity
    {
         public int CurrentHp { get; set; }
         public int MaxHp { get; set; }
         public Vector3  Position { get; set; }     
    }
    
    public class SelfInformation
    {
        public Info                    PersonInfo   { get; set; } = new();
        public ClientConfig            ClientConfig { get; set; } = new();
        public ConcurrentBag<Roommate> Roommates    { get; set; } = [];
        public CMap<uint, int>         BombHistory  { get; set; } = new CMap<uint, int>(12);
        public List<Entity> Targets { get; set; } = Enumerable.Repeat( new Entity(),12).ToList();
        public SelfInformation()
        {
        }
    }
}