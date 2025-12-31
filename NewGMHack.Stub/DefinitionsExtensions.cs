using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ByteStream.Mananged;
using NewGMHack.Stub.PacketStructs.Recv;
using NewGMHack.Stub.PacketStructs.Send;

namespace NewGMHack.Stub.PacketStructs
{


public static class SpanExtensions
{
    public static List<byte[]> SliceBetweenMarkers(this ReadOnlySpan<byte> data, ReadOnlySpan<byte> startMarker, ReadOnlySpan<byte> endMarker)
    {
        var result = new List<byte[]>();
        int index = 0;

        while (index < data.Length)
        {
            int startIndex = data.Slice(index).IndexOfPattern(startMarker);
            if (startIndex == -1) break;
            startIndex += index + startMarker.Length;

            int endIndex = data.Slice(startIndex).IndexOfPattern(endMarker);
            if (endIndex == -1) break;
            endIndex += startIndex;

            var slice = data.Slice(startIndex, endIndex - startIndex).ToArray();
            result.Add(slice);

            index = endIndex + endMarker.Length;
        }

        return result;
    }

    public static int IndexOfPattern(this ReadOnlySpan<byte> span, ReadOnlySpan<byte> pattern)
    {
        for (int i = 0; i <= span.Length - pattern.Length; i++)
        {
            if (span.Slice(i, pattern.Length).SequenceEqual(pattern))
                return i;
        }
        return -1;
    }
    /// <summary>
    /// Casts a Span<byte> to a Span<T> for blittable types.
    /// </summary>
    public static Span<T> CastTo<T>(this Span<byte> span) where T : unmanaged =>
        MemoryMarshal.Cast<byte, T>(span);
public static Span<T> ToSpan<T>(this ReadOnlySpan<T> readOnlySpan)
{
    T[] result=GC.AllocateUninitializedArray<T>(readOnlySpan.Length);
    ref var space=ref MemoryMarshal.GetReference(readOnlySpan);
    for(var c=0;c<readOnlySpan.Length;c++)
    result[c]=Unsafe.Add(ref space, c);
    return result;          
}
    public static Span<T> CastTo<T>(this ReadOnlySpan<byte> span) where T : unmanaged =>
        MemoryMarshal.Cast<byte, T>(span.ToSpan());
    /// <summary>
    /// Reads the first struct of type T from a Span<byte>.
    /// </summary>
    public static T ReadStruct<T>(this Span<byte> span) where T : unmanaged =>
        span.CastTo<T>()[0];

    public static T ReadStruct<T>(this ReadOnlySpan<byte> span) where T : unmanaged =>
        span.CastTo<T>()[0];
    /// <summary>
    /// Slices a Span<byte> after a struct of type T.
    /// </summary>
    public static Span<byte> SliceAfter<T>(this Span<byte> span) where T : unmanaged =>
        span.Slice(Marshal.SizeOf<T>());

    public static ReadOnlySpan<byte> SliceAfter<T>(this ReadOnlySpan<byte> span) where T : unmanaged =>
        span.Slice(Marshal.SizeOf<T>());
    /// <summary>
    /// Converts a blittable struct to a byte array.
    /// </summary>
    public static byte[] ToByteArray<T>(this T value) where T : unmanaged
    {
        int size = Marshal.SizeOf<T>();
        Span<byte> buffer = new byte[size];
        MemoryMarshal.Write(buffer, in value);
        return buffer.ToArray();
    }

    /// <summary>
    /// Converts a Span<T> to a Span<byte>.
    /// </summary>
    public static Span<byte> AsByteSpan<T>(this Span<T> span) where T : unmanaged =>
        MemoryMarshal.AsBytes(span);

    /// <summary>
    /// Combines two byte arrays into a single Span<byte>.
    /// </summary>
    public static Span<byte> CombineWith(this Span<byte> first, Span<byte> second)
    {
        Span<byte> combined = new byte[first.Length + second.Length];
        first.CopyTo(combined);
        second.CopyTo(combined.Slice(first.Length));
        return combined;
    }

    public static ReadOnlySpan<byte> CombineWith(this ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        Span<byte> combined = new byte[first.Length + second.Length];
        first.CopyTo(combined);
        second.CopyTo(combined.Slice(first.Length));
        return combined;
    }
    /// <summary>
    /// Converts a Span<byte> to a hex string.
    /// </summary>
    public static string ToHex(this Span<byte> span) =>
        Convert.ToHexString(span);
}
    }

    public ref struct PacketBuilder
    {
        private Span<byte> _buffer;
        private int _position;

        public PacketBuilder(Span<byte> buffer)
        {
            _buffer = buffer;
            _position = 0;
        }

        public void Write<T>(T value) where T : unmanaged
        {
            if (MemoryMarshal.TryWrite(_buffer.Slice(_position), in value))
            {
                _position += Marshal.SizeOf<T>();
            }
            else 
            {
                 throw new InvalidOperationException("Buffer too small for Write operation.");
            }
        }

        public void WriteBytes(ReadOnlySpan<byte> bytes)
        {
             if (bytes.Length == 0) return;
             bytes.CopyTo(_buffer.Slice(_position));
             _position += bytes.Length;
        }
        
        public ReadOnlySpan<byte> ToSpan() => _buffer.Slice(0, _position);
    }

    public static class DefinitionsExtensions
    {
        public static void ToByteSpan<T>(this T value, Span<byte> destination) where T : unmanaged
        {
             MemoryMarshal.Write(destination, in value);
        }

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
            catch (Exception)
            {
                // ignored
            }

            return reborns;
        }

        public static Reborn ReadReborn(ref this ByteReader reader , bool isReadLocation= true )
        {
            try
            {
                var personId = reader.Read<uint>();
                var targetId = reader.Read<uint>();
                if(!isReadLocation) return new Reborn(personId, targetId , 0);
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

        // public static GetChangedMachine ReadChangedMachine(ref this ByteReader reader)
        // {
        //     return new GetChangedMachine(reader.Read<uint>(), reader.Read<ushort>(), reader.Read<uint>(),reader.Read<uint>(),reader.Read<uint>());
        // }

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
    }