using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Faster.Map.Concurrent;
using NewGMHack.CommunicationModel.Models;
using NewGMHack.CommunicationModel.Models;
using NewGMHack.Stub.MemoryScanner;
using NewGMHack.Stub.PacketStructs.Recv;
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
        //public Machine CurrentMachine { get; set; } = new();
        
        /// <summary>
        /// Processed machine model for frontend display
        /// </summary>
        public MachineModel? CurrentMachineModel { get; set; }
        
        /// <summary>
        /// Full machine base info from memory scan (includes skills, weapons, transform)
        /// </summary>
        public MachineBaseInfo? CurrentMachineBaseInfo { get; set; }
        
        public Info                    PersonInfo   { get; set; } = new();
        public ClientConfig            ClientConfig { get; set; } = new();
        public ConcurrentBag<Roommate> Roommates    { get; set; } = [];
        public ConcurrentDictionary<uint, int> BombHistory  { get; set; } = new ConcurrentDictionary<uint, int>();
        public int                     ScreenWidth  { get; set; }
        public int                     ScreenHeight { get; set; }
        public int                     CrossHairX   { get; set; }
        public int                     CrossHairY   { get; set; }
        public float                   AimRadius    { get; set; }
        public nint                    LastSocket   { get; set; }
        public List<Entity>            Targets      { get; set; } = Enumerable.Repeat(new Entity(), 12).ToList();
        
        /// <summary>
        /// Current battle state for real-time tracking (thread-safe)
        /// </summary>
        public Models.BattleState BattleState { get; } = new();
        public SelfInformation()
        {
        }
    }
}