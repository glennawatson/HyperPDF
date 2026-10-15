// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Filters;
using Microsoft.Win32.SafeHandles;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// Reads cached CMaps and CID-to-Unicode tables generated on demand. Each resource is a zlib stream; the
/// callers cache what they build, so each resource is read at most once.
/// </summary>
internal static class CMapResources
{
    /// <summary>The longest name looked up; longer names are never predefined CMaps.</summary>
    private const int MaxNameLength = 32;

    /// <summary>Reads and decompresses a resource.</summary>
    /// <param name="name">The table name, such as <c>90ms-RKSJ-H</c> or <c>Adobe-Japan1-UCS2</c>.</param>
    /// <param name="output">Receives the decompressed bytes after any it holds.</param>
    /// <returns><see langword="true"/> when the resource exists.</returns>
    internal static bool TryRead(string name, ref PooledBuffer output)
    {
        if (name.Length is 0 or > MaxNameLength || FontDataResources.Locate("CMaps", $"{name}.bin") is not { } path)
        {
            return false;
        }

        using var handle = File.OpenHandle(path);
        var length = (int)RandomAccess.GetLength(handle);
        var packed = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            ReadAll(handle, packed.AsSpan(0, length));
            _ = ZLibCodec.Decompress(packed.AsSpan(0, length), false, ref output);
            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(packed);
        }
    }

    /// <summary>Fills a buffer from the start of a file.</summary>
    /// <param name="handle">The file.</param>
    /// <param name="buffer">The buffer; the file holds at least this many bytes.</param>
    /// <exception cref="EndOfStreamException">The file ended early.</exception>
    private static void ReadAll(SafeFileHandle handle, Span<byte> buffer)
    {
        for (int read = 0, count = 0; read < buffer.Length; read += count)
        {
            count = RandomAccess.Read(handle, buffer[read..], read);
            if (count == 0)
            {
                throw new EndOfStreamException("The font data file ended early.");
            }
        }
    }
}
