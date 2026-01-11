using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewGMHack.CommunicationModel.Models
{
    public static class ItemDisplayExtensions
    {
        public static Dictionary<uint, string> itemsID = new()
        {
			// { "54C70000", "瞬移" },
			// { "43C70000", "隱形" },
			// { "4EC70000", "細餅" },
			// { "4DC70000", "大餅" },
			// { "4AC70000", "精骨" },
			// { "45C70000", "主防" },
			// { "41C70000", "推下" },
			// { "53C70000", "推升" },
			// { "3CC70000", "抽SP" },
			// { "39C70000", "屌瞬" },
			// { "4BC70000", "獨眼巨人" },
			// { "00000000", "無" }
                {51028,	"瞬移"},
                {51011, "隱形"},
                {51022, "細餅"},
                {51021, "大餅"},
                {51018, "精骨"},
                {51013, "主防"},
                {51009, "推下"},
                {51027, "推升"},
                {51004, "抽SP"},
                {51001, "屌瞬"},
                {51019,	"獨眼巨人"},
                {0,	"無"}
		};

        public static string ToDisplay(this uint itemId)
        {
            return itemsID.GetValueOrDefault(itemId, "無");
        }
    }
    public class Roommate
    {
        public uint    PlayerId { get; set; }
        public uint    ItemId   { get; set; }
        public string  ItemName => ItemId.ToDisplay();
        public string? Name     { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{nameof(PlayerId)}: {PlayerId}, {nameof(ItemId)}: {ItemId}, {nameof(Name)}: {Name}";
        }
    }
}
