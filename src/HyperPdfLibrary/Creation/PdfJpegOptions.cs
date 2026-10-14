// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>How <see cref="PdfJpegEncoder"/> codes an image.</summary>
[DebuggerDisplay("PdfJpegOptions: quality {Quality}, {Subsampling}, progressive {Progressive}")]
public sealed record PdfJpegOptions
{
    /// <summary>The quality used when none is given.</summary>
    private const int DefaultQuality = 75;

    /// <summary>Gets the default options: quality 75, automatic subsampling, optimised Huffman tables, sequential coding.</summary>
    public static PdfJpegOptions Default { get; } = new();

    /// <summary>Gets the quality from 1 (smallest file) to 100 (best picture), 75 by default. Values outside the range are clamped.</summary>
    public int Quality { get; init; } = DefaultQuality;

    /// <summary>Gets how much colour detail to keep. Gray images ignore it.</summary>
    public PdfJpegSubsampling Subsampling { get; init; }

    /// <summary>
    /// Gets a value indicating whether to build Huffman tables for the image, which takes a second pass and makes a smaller
    /// file. When <see langword="false"/> the example tables of ISO/IEC 10918-1 Annex K.3 are used. Progressive coding
    /// always builds its own tables.
    /// </summary>
    public bool OptimizeHuffman { get; init; } = true;

    /// <summary>Gets the number of MCUs between restart markers, or zero for none. Restart markers let a reader recover from damage.</summary>
    public int RestartInterval { get; init; }

    /// <summary>
    /// Gets a value indicating whether to write a progressive JPEG (spectral selection and successive approximation) instead
    /// of a sequential one. ISO 32000-2, 7.4.8, allows it but notes that it brings no benefit inside a PDF.
    /// </summary>
    public bool Progressive { get; init; }

    /// <summary>
    /// Gets a value indicating whether four-component images are written as YCCK (the Adobe transform value 2), which
    /// compresses better, instead of plain CMYK (transform value 0).
    /// </summary>
    public bool TransformCmyk { get; init; } = true;
}
