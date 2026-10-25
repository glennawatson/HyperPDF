// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>Gathers the chunks of a PNG file as they are read.</summary>
[DebuggerDisplay("PngBuilder: {_header}")]
internal sealed class PngBuilder
{
    /// <summary>The image data chunks, in order.</summary>
    private readonly List<byte[]> _data = [];

    /// <summary>The header, once read.</summary>
    private PngHeader? _header;

    /// <summary>The palette, once read.</summary>
    private byte[] _palette = [];

    /// <summary>The transparency chunk's data, once read.</summary>
    private byte[] _transparency = [];

    /// <summary>Takes in one chunk.</summary>
    /// <param name="type">The chunk's four type bytes.</param>
    /// <param name="body">The chunk's data.</param>
    /// <returns><see langword="false"/> at the end chunk, which ends the file.</returns>
    /// <exception cref="InvalidDataException">The header chunk is not valid.</exception>
    internal bool Add(ReadOnlySpan<byte> type, ReadOnlySpan<byte> body)
    {
        if (type.SequenceEqual("IEND"u8))
        {
            return false;
        }

        if (type.SequenceEqual("IHDR"u8))
        {
            _header = PngFileDecoder.ReadHeader(body);
        }
        else if (type.SequenceEqual("PLTE"u8))
        {
            _palette = body.ToArray();
        }
        else if (type.SequenceEqual("tRNS"u8))
        {
            _transparency = body.ToArray();
        }
        else if (type.SequenceEqual("IDAT"u8))
        {
            _data.Add(body.ToArray());
        }

        return true;
    }

    /// <summary>Gets what was gathered.</summary>
    /// <returns>The parts.</returns>
    internal PngParts ToParts()
    {
        var total = 0;
        foreach (var chunk in _data)
        {
            total += chunk.Length;
        }

        var joined = new byte[total];
        var offset = 0;
        foreach (var chunk in _data)
        {
            chunk.CopyTo(joined, offset);
            offset += chunk.Length;
        }

        return new(_header, _palette, _transparency, joined);
    }
}
