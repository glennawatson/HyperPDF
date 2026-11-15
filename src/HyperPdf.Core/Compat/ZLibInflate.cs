// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if NET11_0_OR_GREATER
using System.Buffers;
#endif
using System.IO.Compression;

namespace HyperPdf.Core.Compat;

/// <summary>
/// Inflates zlib data held in memory into an array. .NET 11 decodes straight from the span with <c>ZLibDecoder</c>; .NET 10 copies
/// the data into a stream and inflates it with <see cref="ZLibStream"/>. Both give the same bytes for whole data, both throw
/// <see cref="InvalidDataException"/> for damaged data, and both give back what was decoded when the data stops early.
/// </summary>
internal static class ZLibInflate
{
#if NET11_0_OR_GREATER
    /// <summary>The usual compression ratio, used to size the first output buffer.</summary>
    private const int ExpectedRatio = 4;

    /// <summary>The smallest output buffer.</summary>
    private const int MinimumBuffer = 256;
#endif

    /// <summary>Inflates zlib data.</summary>
    /// <param name="source">The compressed bytes.</param>
    /// <returns>The decoded bytes.</returns>
    /// <exception cref="InvalidDataException">The data is not zlib data, or is damaged.</exception>
    internal static byte[] ToArray(ReadOnlySpan<byte> source)
    {
#if NET11_0_OR_GREATER
        using var decoder = new ZLibDecoder();
        var buffer = ArrayPool<byte>.Shared.Rent(Math.Max(MinimumBuffer, source.Length * ExpectedRatio));
        try
        {
            var written = 0;
            while (true)
            {
                var status = decoder.Decompress(source, buffer.AsSpan(written), out var consumed, out var produced);
                source = source[consumed..];
                written += produced;
                if (status == OperationStatus.InvalidData)
                {
                    throw new InvalidDataException("The compressed data is damaged.");
                }

                if (status != OperationStatus.DestinationTooSmall)
                {
                    // The data ended, or stopped early. What was decoded is given back either way, as the stream reader does.
                    return buffer.AsSpan(0, written).ToArray();
                }

                var larger = ArrayPool<byte>.Shared.Rent(buffer.Length + buffer.Length);
                buffer.AsSpan(0, written).CopyTo(larger);
                ArrayPool<byte>.Shared.Return(buffer);
                buffer = larger;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
#else
        using var input = new MemoryStream(source.ToArray());
        using var inflater = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        inflater.CopyTo(output);
        return output.ToArray();
#endif
    }
}
