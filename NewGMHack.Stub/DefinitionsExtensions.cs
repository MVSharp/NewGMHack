using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ByteStream.Mananged;
using NewGMHack.Stub.PacketStructs.Recv;
using NewGMHack.Stub.PacketStructs.Send;

namespace NewGMHack.Stub.PacketStructs
{
    public static class DefinitionsExtensions
    {
        public static List<Reborn> ReadDamaged(ref this ByteReader reader)
        {
            var reborns = new List<Reborn>();
            try
            {
                var id = reader.Read<uint>();
                reader.SkipBytes(16); // first 20 bytes useless 
                var countByte = reader.Read<byte>();
                var count     = (int)countByte;
                if (count == 0) return reborns;
                for (int i = 0; i < count; i++)
                {
                    //reborns.Add(new Reborn(id, reader.Read<UInt32>()));
                    reader.SkipBytes(14);
                }
            }
            catch (Exception ex)
            {
            }

            return reborns;
        }

        public static Reborn ReadReborn(ref this ByteReader reader)
        {
            try
            {
                var personId = reader.Read<uint>();
                var targetId = reader.Read<uint>();
                reader.SkipBytes(18);
                var location = reader.Read<ushort>();
                return new Reborn(personId, targetId, location);
            }
            catch
            {
                return default;
            }
        }

        public static GetPersionId ReadPersonId(ref this ByteReader reader)
        {
            return new GetPersionId(reader.Read<uint>());
        }

        public static GetChangedMachine ReadChangedMachine(ref this ByteReader reader)
        {
            return new GetChangedMachine(reader.Read<uint>(), reader.Read<ushort>(), reader.Read<uint>());
        }

        public static MapItemExisted ReadMapItemExisted(ref this ByteReader reader)
        {
            MapItemExisted map     = new();
            var            pid     = reader.Read<uint>();
            var            unknown = reader.Read<uint>();
            var            count   = reader.ReadBytes(1)[0];
            map.PersonId = pid;
            map.Count    = count;
            map.Targets  = new uint[count];
            for (int i = 0; i < count; i++)
            {
                map.Targets[i] = reader.Read<uint>();
            }

            return map;
        }

        public static byte[] WriteAttack(Attack att)
        {
            try
            {

            using var ms     = new MemoryStream();
            var       writer = new BinaryWriter(ms);
            writer.Write(att.Version);
            writer.Write(att.Split);
            writer.Write(att.Method);
            writer.Write(att.Unknown1);
            writer.Write(att.PlayerId);
            writer.Write(att.WeaponId);
            // writer.Write(att.WeaponSplit);
            writer.Write(att.WeaponSlot);
            writer.Write(att.PlayerId2);
            writer.Write(att.Unknown2);
            writer.Write(att.TargetCount);
            foreach (var target in att.TargetData)
            {
                if (target.TargetId == 0)
                {
                    target.Damage = 0;
                }

                writer.Write(target.TargetId);
                writer.Write(target.Damage);
                writer.Write(target.Unknown1);
                writer.Write(target.Unknown2);
                writer.Write(target.Unknown3);
            }

            return ms.ToArray();

            }
            catch
            {

            }

            return default;
        }
    }
}