using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewGMHack.Stub
{
    public class Info
    {
        public uint PersonId { get; set; }
        public uint GundamId { get; set; }
        public uint Weapon1  { get; set; }
        public uint Weapon2  { get; set; }
        public uint Weapon3  { get; set; }
    }

    public class ClientConfig
    {
        public bool IsMissionBomb { get; set; } = true;

    }

    public class SelfInformation
    {
        public Info         PersonInfo   { get; set; } = new();
        public ClientConfig ClientConfig { get; set; } = new();

        public SelfInformation()
        {
        }
    }
}