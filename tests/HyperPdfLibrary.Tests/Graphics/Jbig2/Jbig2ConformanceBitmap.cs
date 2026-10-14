// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using HyperPdfLibrary.Graphics.Images.Jbig2;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>A 1-bit BMP from the conformance data, as rows of JBIG2 pixels: bit set for black, most significant bit first.</summary>
[DebuggerDisplay("Jbig2ConformanceBitmap: {Width}x{Height}")]
internal sealed class Jbig2ConformanceBitmap
{
    /// <summary>The offset of the pixel data offset in the file header.</summary>
    private const int DataOffsetField = 10;

    /// <summary>The offset of the width in the info header.</summary>
    private const int WidthField = 18;

    /// <summary>The offset of the height in the info header.</summary>
    private const int HeightField = 22;

    /// <summary>The offset of the first palette entry.</summary>
    private const int PaletteField = 54;

    /// <summary>The offset of the second byte of the first palette entry.</summary>
    private const int PaletteSecond = 55;

    /// <summary>The offset of the third byte of the first palette entry.</summary>
    private const int PaletteThird = 56;

    /// <summary>The bytes of a row's padding unit.</summary>
    private const int RowAlignment = 4;

    /// <summary>Initializes a new instance of the <see cref="Jbig2ConformanceBitmap"/> class.</summary>
    /// <param name="file">The BMP file bytes.</param>
    internal Jbig2ConformanceBitmap(byte[] file)
    {
        Width = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(WidthField));
        var height = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(HeightField));
        Height = Math.Abs(height);
        var offset = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(DataOffsetField));

        // Palette entry 0 is white in these files; when it is black, the bits are inverted.
        var firstIsBlack = file[PaletteField] == 0 && file[PaletteSecond] == 0 && file[PaletteThird] == 0;
        var packed = Jbig2Bits.Stride(Width);
        var stride = (packed + RowAlignment - 1) / RowAlignment * RowAlignment;
        Rows = new byte[packed * Height];
        for (var y = 0; y < Height; y++)
        {
            var row = file.AsSpan(offset + ((height > 0 ? Height - 1 - y : y) * stride), packed);
            for (var i = 0; i < packed; i++)
            {
                Rows[(y * packed) + i] = firstIsBlack ? (byte)~row[i] : row[i];
            }
        }
    }

    /// <summary>Gets the width in pixels.</summary>
    internal int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    internal int Height { get; }

    /// <summary>Gets the rows, the bits of black pixels set; padding bits are unspecified.</summary>
    internal byte[] Rows { get; }
}
