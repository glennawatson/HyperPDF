// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;

namespace HyperPdfLibrary.Graphics;

/// <summary>
/// A transfer function (ISO 32000-2 10.5) sampled into one 256-entry table per RGB channel. A table is built once for each
/// function in the document and shared by every graphics state that selects it, so applying it never allocates.
/// </summary>
[DebuggerDisplay("PdfTransfer: {_tables.Length} entries")]
public sealed class PdfTransfer
{
    /// <summary>The entries of one channel's table.</summary>
    internal const int Entries = 256;

    /// <summary>The channels a table set holds: red, green and blue.</summary>
    internal const int Channels = 3;

    /// <summary>The divisor that turns a truncating division into a rounding one when half of it is added first.</summary>
    private const int RoundingDivisor = 2;

    /// <summary>The largest channel value.</summary>
    private const int MaxByte = 255;

    /// <summary>The shift of the red channel in 0xRRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The shift of the green channel in 0xRRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>The position of the green table.</summary>
    private const int GreenTable = 1;

    /// <summary>The position of the blue table.</summary>
    private const int BlueTable = 2;

    /// <summary>The offset of blue in a BGRA pixel.</summary>
    private const int BlueByte = 0;

    /// <summary>The offset of green in a BGRA pixel.</summary>
    private const int GreenByte = 1;

    /// <summary>The offset of red in a BGRA pixel.</summary>
    private const int RedByte = 2;

    /// <summary>The offset of alpha in a BGRA pixel.</summary>
    private const int AlphaByte = 3;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The red, green and blue tables back to back.</summary>
    private readonly byte[] _tables;

    /// <summary>Initializes a new instance of the <see cref="PdfTransfer"/> class.</summary>
    /// <param name="tables">The red, green and blue tables back to back, <see cref="Entries"/> bytes each.</param>
    internal PdfTransfer(byte[] tables)
    {
        _tables = tables;
        IsIdentity = true;
        for (var i = 0; i < tables.Length && IsIdentity; i++)
        {
            IsIdentity = tables[i] == (byte)i;
        }
    }

    /// <summary>Gets a value indicating whether every table maps each value to itself.</summary>
    public bool IsIdentity { get; }

    /// <summary>Gets the table of the red channel.</summary>
    public ReadOnlySpan<byte> Red => _tables.AsSpan(0, Entries);

    /// <summary>Gets the table of the green channel.</summary>
    public ReadOnlySpan<byte> Green => _tables.AsSpan(GreenTable * Entries, Entries);

    /// <summary>Gets the table of the blue channel.</summary>
    public ReadOnlySpan<byte> Blue => _tables.AsSpan(BlueTable * Entries, Entries);

    /// <summary>Gets a value indicating whether the three tables are equal, as they are when one function applies to every channel.</summary>
    public bool IsUniform => Red.SequenceEqual(Green) && Green.SequenceEqual(Blue);

    /// <summary>Applies the transfer function to a colour.</summary>
    /// <param name="rgb">The colour as 0xRRGGBB.</param>
    /// <returns>The colour after the transfer function, as 0xRRGGBB.</returns>
    public uint Apply(uint rgb)
    {
        var red = _tables[(byte)(rgb >>> RedShift)];
        var green = _tables[Entries + (byte)(rgb >>> GreenShift)];
        var blue = _tables[(BlueTable * Entries) + (byte)rgb];
        return ((uint)red << RedShift) | ((uint)green << GreenShift) | blue;
    }

    /// <summary>Applies the transfer function to a decoded image in place; a stencil mask keeps its coverage.</summary>
    /// <param name="image">The image, whose pixels the caller owns.</param>
    internal void ApplyTo(PdfImageData image)
    {
        if (image.IsStencilMask || image.UnsupportedCodec != PdfImageCodec.None)
        {
            return;
        }

        if (image.IsGray)
        {
            ApplyGray(image.Pixels);
        }
        else
        {
            ApplyBgra(image.Pixels);
        }
    }

    /// <summary>Applies the transfer function to 8-bit gray pixels in place.</summary>
    /// <param name="gray">The pixels.</param>
    internal void ApplyGray(Span<byte> gray)
    {
        var table = _tables.AsSpan(GreenTable * Entries, Entries);
        for (var i = 0; i < gray.Length; i++)
        {
            gray[i] = table[gray[i]];
        }
    }

    /// <summary>Applies the transfer function to premultiplied BGRA pixels in place.</summary>
    /// <param name="bgra">The pixels, four bytes each.</param>
    internal void ApplyBgra(Span<byte> bgra)
    {
        for (var i = 0; i + PixelBytes <= bgra.Length; i += PixelBytes)
        {
            var pixel = bgra.Slice(i, PixelBytes);
            var alpha = pixel[AlphaByte];
            if (alpha == byte.MaxValue)
            {
                pixel[BlueByte] = _tables[(BlueTable * Entries) + pixel[BlueByte]];
                pixel[GreenByte] = _tables[Entries + pixel[GreenByte]];
                pixel[RedByte] = _tables[pixel[RedByte]];
            }
            else if (alpha != 0)
            {
                pixel[BlueByte] = Premultiplied(BlueTable, pixel[BlueByte], alpha);
                pixel[GreenByte] = Premultiplied(GreenTable, pixel[GreenByte], alpha);
                pixel[RedByte] = Premultiplied(0, pixel[RedByte], alpha);
            }
        }
    }

    /// <summary>Applies one channel's table to a premultiplied value.</summary>
    /// <param name="channel">The table index.</param>
    /// <param name="value">The premultiplied value.</param>
    /// <param name="alpha">The pixel's alpha, not zero.</param>
    /// <returns>The premultiplied result.</returns>
    private byte Premultiplied(int channel, byte value, byte alpha)
    {
        var straight = Math.Min(MaxByte, ((value * MaxByte) + (alpha / RoundingDivisor)) / alpha);
        var mapped = _tables[(channel * Entries) + straight];
        return (byte)(((mapped * alpha) + (MaxByte / RoundingDivisor)) / MaxByte);
    }
}
