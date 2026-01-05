namespace NewGMHack.Stub.Services;

/// <summary>
/// Interface for TCP packet accumulator that reassembles fragmented recv data.
/// </summary>
public interface IPacketAccumulator
{
    /// <summary>
    /// Appends raw recv data and extracts complete packets.
    /// </summary>
    List<byte[]> AppendAndExtract(ReadOnlySpan<byte> rawRecvData);

    /// <summary>
    /// Clears the accumulator buffer.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets the current buffered data length.
    /// </summary>
    int BufferedLength { get; }
}
