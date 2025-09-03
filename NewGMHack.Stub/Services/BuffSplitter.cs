using Microsoft.Extensions.Logging;
using NewGMHack.Stub.PacketStructs;
using ZLogger;

namespace NewGMHack.Stub.Services;

public class BuffSplitter(ILogger<BuffSplitter> logger) : IBuffSplitter
{
    private static readonly byte[] Separator = { 0xF0, 0x03 };

    public List<PacketSegment> Split(ReadOnlySpan<byte> input)
    {
        try
        {
            var segments     = new List<PacketSegment>();
            int currentIndex = 0;
            while (currentIndex < input.Length)
            {
                int delimiterIndex = input.Slice(currentIndex).IndexOf(Separator);
                if (delimiterIndex == -1)
                {
                    break; // No more delimiters found
                }

                // Move to the start of the delimiter
                currentIndex += delimiterIndex;

                // Ensure we have enough bytes for version and method
                if (currentIndex + 4 > input.Length)
                {
                    break; // Not enough bytes for version and method
                }

                // Extract version and method
                var version        = input.Slice(currentIndex,     2);
                var method         = input.Slice(currentIndex + 2, 2);
                int bodyStartIndex = currentIndex + 4;

                // Find the next delimiter to calculate the body length
                delimiterIndex = input.Slice(bodyStartIndex).IndexOf(Separator);
                ReadOnlySpan<byte> body;

                if (delimiterIndex == -1)
                {
                    // No next delimiter, take the rest as the body
                    body         = input.Slice(bodyStartIndex);
                    currentIndex = input.Length; // End processing
                }
                else
                {
                    // Calculate body length based on the next delimiter
                    body         = input.Slice(bodyStartIndex, delimiterIndex - 2);
                    currentIndex = bodyStartIndex + (delimiterIndex - 2); // Move past the current delimiter
                }

                // Create the method segment and add it to the list
                var segment = new PacketSegment(BitConverter.ToInt16(version), BitConverter.ToInt16(method),
                                                body.ToArray());
                segments.Add(segment);
            }

            return segments;
        }
        catch (Exception ex)
        {
            logger.ZLogInformation($"{ex.Message} | {ex.StackTrace}");
        }

        return [];
    }
}