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
        public int Id { get; set; }
         public int CurrentHp { get; set; }
         public int MaxHp { get; set; }
         public Vector3  Position { get; set; }     
         public float ScreenX { get; set; }
         public float ScreenY { get; set; }
        public bool IsBest { get; set; }
        public uint EntityPtrAddress { get; set; }
        public uint EntityPosPtrAddress { get; set; }
    }
    
    public class SelfInformation
    {
        public Info                    PersonInfo   { get; set; } = new();
        public ClientConfig            ClientConfig { get; set; } = new();
        public ConcurrentBag<Roommate> Roommates    { get; set; } = [];
        public CMap<uint, int>         BombHistory  { get; set; } = new CMap<uint, int>(12);
        public int                     ScreenWidth  { get; set; }
        public int                     ScreenHeight { get; set; }
        public int                     CrossHairX   { get; set; }
        public int                     CrossHairY   { get; set; }
        public float                   AimRadius    { get; set; }
        public nint                    LastSocket   { get; set; }
        public List<Entity>            Targets      { get; set; } = Enumerable.Repeat(new Entity(), 12).ToList();
        public SelfInformation()
        {
        }
    }
}