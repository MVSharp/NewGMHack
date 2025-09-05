using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewGMHack.CommunicationModel.Models
{
    public class Roommate
    {
        public uint   PlayerId { get; set; }
        public uint   ItemId   { get; set; }
        public string Name     { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{nameof(PlayerId)}: {PlayerId}, {nameof(ItemId)}: {ItemId}, {nameof(Name)}: {Name}";
        }
    }
}
