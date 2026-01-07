using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NewGMHack.CommunicationModel.PacketStructs.Recv
{
    /// <summary>
    /// Game message packet (Method 2574/0x0A0E)
    /// Total struct size: 123 bytes
    /// Contains sender name, tag, and message in GBK encoding
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct GameMessage2574
    {
        public const ushort MethodId = 0x0A0E;  // 2574
        public const int StructSize = 123;
        public const uint FixedValue = 0x00001DFD;
        
        public uint MyPlayerId;             // 4 bytes - My player ID
        public uint Fixed;                  // 4 bytes - Always 0x00001DFD
        public fixed byte NameBytes[19];    // 19 bytes - Sender name (GBK encoded, null-terminated)
        public fixed byte TagBytes[18];     // 18 bytes - Sender tag/clan (GBK encoded, null-terminated)
        public fixed byte MessageBytes[78]; // 78 bytes - Message content (GBK encoded, null-terminated)
        
        // Total: 4 + 4 + 19 + 18 + 78 = 123 bytes
        
        /// <summary>
        /// Create a new GameMessage2574 with the given parameters
        /// </summary>
        public static GameMessage2574 Create(uint myPlayerId, string name, string tag, string message)
        {
            var msg = new GameMessage2574
            {
                MyPlayerId = myPlayerId,
                Fixed = FixedValue
            };
            msg.SetName(name);
            msg.SetTag(tag);
            msg.SetMessage(message);
            return msg;
        }
        
        /// <summary>
        /// Set sender name (max 18 chars, null-terminated)
        /// </summary>
        public void SetName(string name)
        {
            fixed (byte* ptr = NameBytes)
            {
                WriteString(ptr, 19, name);
            }
        }
        
        /// <summary>
        /// Set sender tag (max 17 chars, null-terminated)
        /// </summary>
        public void SetTag(string tag)
        {
            fixed (byte* ptr = TagBytes)
            {
                WriteString(ptr, 18, tag);
            }
        }
        
        /// <summary>
        /// Set message content (max 77 chars, null-terminated)
        /// </summary>
        public void SetMessage(string message)
        {
            fixed (byte* ptr = MessageBytes)
            {
                WriteString(ptr, 78, message);
            }
        }
        
        /// <summary>
        /// Get sender name as string (GBK decoded)
        /// </summary>
        public readonly string GetName()
        {
            fixed (byte* ptr = NameBytes)
            {
                return DecodeGBK(ptr, 19);
            }
        }
        
        /// <summary>
        /// Get sender tag as string (GBK decoded)
        /// </summary>
        public readonly string GetTag()
        {
            fixed (byte* ptr = TagBytes)
            {
                return DecodeGBK(ptr, 18);
            }
        }
        
        /// <summary>
        /// Get message content as string (GBK decoded)
        /// </summary>
        public readonly string GetMessage()
        {
            fixed (byte* ptr = MessageBytes)
            {
                return DecodeGBK(ptr, 78);
            }
        }
        
        private static void WriteString(byte* ptr, int maxLength, string value)
        {
            // Clear buffer first
            for (int i = 0; i < maxLength; i++) ptr[i] = 0;
            
            if (string.IsNullOrEmpty(value)) return;
            
            // Use ASCII for simplicity (GBK is compatible with ASCII for basic chars)
            var bytes = Encoding.ASCII.GetBytes(value);
            int len = Math.Min(bytes.Length, maxLength - 1); // Leave room for null terminator
            for (int i = 0; i < len; i++) ptr[i] = bytes[i];
        }
        
        private static string DecodeGBK(byte* ptr, int maxLength)
        {
            // Find null terminator
            int length = 0;
            while (length < maxLength && ptr[length] != 0)
            {
                length++;
            }
            
            if (length == 0) return string.Empty;
            
            try
            {
                // GBK is codepage 936
                Encoding gbk = Encoding.GetEncoding(936);
                return gbk.GetString(ptr, length);
            }
            catch
            {
                // Fallback to ASCII if GBK fails
                return Encoding.ASCII.GetString(ptr, length);
            }
        }
    }
}
