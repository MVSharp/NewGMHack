using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewGMHack.Stub
{
    public class SelfInformation
    {
        public Info         PersonInfo   { get; set; } = new();
        public ClientConfig ClientConfig { get; set; } = new();

        public SelfInformation()
        {
        }
    }
}