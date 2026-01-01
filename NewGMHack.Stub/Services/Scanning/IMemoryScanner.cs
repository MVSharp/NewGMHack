using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace NewGMHack.Stub.Services.Scanning
{
    public interface IMemoryScanner
    {

        /// <summary>
        /// Scans the process memory for a pattern with support for wildcards.
        /// </summary>
        /// <param name="process">The target process.</param>
        /// <param name="pattern">The pattern to search for (e.g. "E8 ?? ?? ?? ?? 48").</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of addresses where the pattern matches.</returns>
        //Task<List<long>> ScanAsync(Process process, string pattern, CancellationToken token = default);

        /// <summary>
        /// Scans the process memory for a byte pattern with an optional mask.
        /// </summary>
        /// <param name="process">The target process.</param>
        /// <param name="pattern">The byte array to search for.</param>
        /// <param name="mask">The mask string (e.g. "x????x"), where 'x' matches the byte and '?' is a wildcard. If null, exact match is performed.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>A list of addresses where the pattern matches.</returns>
        Task<List<long>> ScanAsync(Process process, byte[] pattern, string? mask = null, CancellationToken token = default);
    }
}
