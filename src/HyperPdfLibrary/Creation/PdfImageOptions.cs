// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>What to add to an image beyond its pixels.</summary>
[DebuggerDisplay("PdfImageOptions: ICC {IccColorSpace.IsValid}, interpolate {Interpolate}, stencil {AsStencil}")]
public sealed record PdfImageOptions
{
    /// <summary>Gets the options that add nothing.</summary>
    public static PdfImageOptions Default { get; } = new();

    /// <summary>
    /// Gets an ICC colour space made with <see cref="PdfImageWriter.AddIccColorSpace(HyperPdfLibrary.Writing.IPdfObjectSink, ReadOnlySpan{byte})"/>
    /// to use instead of the device colour space. Its component count must match the image.
    /// </summary>
    public PdfIccColorSpace IccColorSpace { get; init; }

    /// <summary>
    /// Gets JPEG 2000 options to store gray, RGB and BGRA colour data as a <c>JPXDecode</c> image instead of Flate, or
    /// <see langword="null"/> for Flate. Alpha still goes to a Flate soft mask. JPEG 2000 needs PDF 1.5 and is not allowed in PDF/A-1.
    /// </summary>
    public PdfJpxOptions? Jpeg2000 { get; init; }

    /// <summary>Gets a value indicating whether a viewer should smooth the image when it scales it (<c>/Interpolate</c>).</summary>
    public bool Interpolate { get; init; }

    /// <summary>Gets a value indicating whether a bilevel image is a stencil mask that paints the current fill colour where its bits are clear (ISO 32000-2 8.9.6.2).</summary>
    public bool AsStencil { get; init; }

    /// <summary>Gets the CCITT coding used for a one-bit image; Group 4 by default.</summary>
    public PdfBilevelCoding BilevelCoding { get; init; }
}
