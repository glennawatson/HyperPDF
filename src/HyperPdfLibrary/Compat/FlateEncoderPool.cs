// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET11_0_OR_GREATER
using System.Diagnostics;
using System.IO.Compression;

namespace HyperPdfLibrary.Compat;

/// <summary>
/// Keeps zlib encoders for one scratch owner. The owner leases this whole pool to one compression at a time and
/// disposes its idle native state when the document or registered scratch service closes.
/// </summary>
[DebuggerDisplay("FlateEncoderPool")]
internal sealed class FlateEncoderPool : IDisposable
{
    /// <summary>The window size used when writing, as a power of two.</summary>
    private const int WindowLog2 = 15;

    /// <summary>The lowest zlib level.</summary>
    private const int LowestQuality = 1;

    /// <summary>The highest zlib level.</summary>
    private const int HighestQuality = 9;

    /// <summary>Idle encoders, indexed by zlib level minus <see cref="LowestQuality"/>.</summary>
    private readonly ZLibEncoder?[] _levels = new ZLibEncoder[HighestQuality - LowestQuality + 1];

    /// <summary>Releases all idle native compression state.</summary>
    public void Dispose()
    {
        for (var i = 0; i < _levels.Length; i++)
        {
            _levels[i]?.Dispose();
            _levels[i] = null;
        }
    }

    /// <summary>Creates an encoder with the PDF writer's window size.</summary>
    /// <param name="quality">The zlib compression level.</param>
    /// <returns>The encoder, which its owner disposes.</returns>
    internal static ZLibEncoder Create(int quality) => new(quality, WindowLog2);

    /// <summary>Gets an encoder of a level for one compression, ready to start a new stream.</summary>
    /// <param name="quality">The zlib level, 1 to 9.</param>
    /// <returns>The encoder, which must be returned.</returns>
    internal ZLibEncoder Rent(int quality)
    {
        var encoder = _levels[quality - LowestQuality];
        _levels[quality - LowestQuality] = null;
        if (encoder is null)
        {
            return Create(quality);
        }

        encoder.Reset();
        return encoder;
    }

    /// <summary>Returns an encoder after this lease's compression ends.</summary>
    /// <param name="quality">The zlib level the encoder was rented for.</param>
    /// <param name="encoder">The encoder to return.</param>
    internal void Return(int quality, ZLibEncoder encoder) => _levels[quality - LowestQuality] = encoder;
}
#endif
