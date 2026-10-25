// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Charts;

/// <summary>Colour sets for charts.</summary>
public static class PdfChartPalette
{
    /// <summary>The bits to shift a red value down to the low byte.</summary>
    private const int RedShift = 16;

    /// <summary>The bits to shift a green value down to the low byte.</summary>
    private const int GreenShift = 8;

    /// <summary>Gets eight colours chosen so that people with the common kinds of colour blindness can tell them apart.</summary>
    /// <remarks>Charts also use dashes, markers and labels, so colour is never the only cue.</remarks>
    public static ReadOnlyMemory<PdfCanvasColor> Accessible { get; } = new PdfCanvasColor[]
    {
        FromHex(0x0072B2),
        FromHex(0xE69F00),
        FromHex(0x009E73),
        FromHex(0xD55E00),
        FromHex(0x56B4E9),
        FromHex(0xCC79A7),
        FromHex(0xF0E442),
        FromHex(0x666666),
    };

    /// <summary>Makes a colour from a value of the form <c>0xRRGGBB</c>.</summary>
    /// <param name="rgb">The value.</param>
    /// <returns>The colour.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfCanvasColor FromHex(int rgb) =>
        PdfCanvasColor.Rgb(((rgb >> RedShift) & byte.MaxValue) / (float)byte.MaxValue, ((rgb >> GreenShift) & byte.MaxValue) / (float)byte.MaxValue, (rgb & byte.MaxValue) / (float)byte.MaxValue);
}
