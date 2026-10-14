// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>What the headers of a JPEG say about its size, coding and colour transform.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels; zero when the frame header defers it to a DNL marker that is missing.</param>
/// <param name="Components">The number of colour components.</param>
/// <param name="Precision">The bits per sample.</param>
/// <param name="Process">The coding process.</param>
/// <param name="AdobeTransform">The transform byte of the Adobe APP14 marker, or -1 when the marker is missing.</param>
/// <param name="HasRgbIds">Whether three components carry the ASCII ids R, G and B.</param>
internal readonly record struct JpegInfo(
    int Width,
    int Height,
    int Components,
    int Precision,
    JpegProcess Process,
    int AdobeTransform,
    bool HasRgbIds)
{
    /// <summary>The value of <c>/ColorTransform</c> when the decode parameters do not give one.</summary>
    internal const int NoColorTransform = -1;

    /// <summary>The components of a gray JPEG.</summary>
    private const int GrayComponents = 1;

    /// <summary>The components of a two-channel JPEG, which has no colour transform.</summary>
    private const int TwoComponents = 2;

    /// <summary>The components of a YCbCr or RGB JPEG.</summary>
    private const int ThreeComponents = 3;

    /// <summary>The components of a CMYK or YCCK JPEG.</summary>
    private const int FourComponents = 4;

    /// <summary>The sample depth PDF allows: one byte per component (ISO 32000-2, 7.4.8).</summary>
    private const int PdfPrecision = 8;

    /// <summary>The sample depth of extended sequential and progressive files that the decoder reduces to 8 bits.</summary>
    private const int ExtendedPrecision = 12;

    /// <summary>Gets a value indicating whether a JFIF APP0 segment is present, which makes three components YCbCr whatever their ids (ISO/IEC 10918-5, 6.2).</summary>
    internal bool HasJfif { get; init; }

    /// <summary>Gets a value indicating whether the Adobe APP14 marker is present.</summary>
    internal bool HasAdobe => AdobeTransform >= 0;

    /// <summary>Gets a value indicating whether the managed decoder can read this JPEG: Huffman coding, 8 or 12 bits, one to four components.</summary>
    internal bool IsSupported => Process is JpegProcess.Sequential or JpegProcess.Progressive && HasReadableShape;

    /// <summary>Gets a value indicating whether the JPEG is one that <c>/DCTDecode</c> may carry: 8 bits a sample and one, three or four components.</summary>
    internal bool IsPdfCarryable =>
        IsSupported && Precision == PdfPrecision && Components != TwoComponents;

    /// <summary>Gets a value indicating whether the sample depth, size and component count are ones the decoder reads.</summary>
    private bool HasReadableShape =>
        Precision is PdfPrecision or ExtendedPrecision
        && Width > 0
        && Height > 0
        && Components is >= GrayComponents and <= FourComponents;

    /// <summary>
    /// Decides whether the samples are YCbCr (three components) or YCCK (four) and must be converted to RGB or CMYK
    /// (ISO 32000-2, 7.4.8, Table 13). An Adobe APP14 marker decides first. Without one, an explicit <c>/ColorTransform</c>
    /// decides. With neither, three components are YCbCr and four are not, except that three components labelled R, G
    /// and B are left alone, as other readers do.
    /// </summary>
    /// <param name="colorTransform">The <c>/ColorTransform</c> value, or <see cref="NoColorTransform"/>.</param>
    /// <returns><see langword="true"/> when the colour transform applies.</returns>
    internal bool UsesColorTransform(int colorTransform) => Components switch
    {
        ThreeComponents => HasAdobe ? AdobeTransform != 0 : UsesThreeComponentDefault(colorTransform),
        FourComponents => HasAdobe ? AdobeTransform != 0 : colorTransform == 1,
        _ => false,
    };

    /// <summary>Decides the three-component transform when there is no Adobe marker.</summary>
    /// <param name="colorTransform">The <c>/ColorTransform</c> value, or <see cref="NoColorTransform"/>.</param>
    /// <returns><see langword="true"/> for YCbCr.</returns>
    private bool UsesThreeComponentDefault(int colorTransform) =>
        colorTransform == NoColorTransform ? HasJfif || !HasRgbIds : colorTransform != 0;
}
