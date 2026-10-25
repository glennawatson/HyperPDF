// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>Flips every bit of a row of samples, which turns black into white in a one-bit or eight-bit picture.</summary>
internal static class RowInvert
{
    /// <summary>Flips every bit of some bytes.</summary>
    /// <param name="bytes">The bytes, changed in place.</param>
    internal static void Invert(Span<byte> bytes)
    {
        var i = 0;
        if (Vector.IsHardwareAccelerated)
        {
            for (; i <= bytes.Length - Vector<byte>.Count; i += Vector<byte>.Count)
            {
                Vector.OnesComplement(new Vector<byte>(bytes[i..])).CopyTo(bytes[i..]);
            }
        }

        for (; i < bytes.Length; i++)
        {
            bytes[i] = (byte)~bytes[i];
        }
    }
}
