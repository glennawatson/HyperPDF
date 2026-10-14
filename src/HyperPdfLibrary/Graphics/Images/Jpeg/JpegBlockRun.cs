// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>
/// The end-of-band run of a progressive AC scan: the blocks so far that have nothing left to code in the band, and the
/// correction bits those blocks collected, which are written after the run's symbol.
/// </summary>
internal ref struct JpegBlockRun
{
    /// <summary>The space for the correction bits, one per byte.</summary>
    private readonly Span<byte> _bits;

    /// <summary>Initializes a new instance of the <see cref="JpegBlockRun"/> struct.</summary>
    /// <param name="bits">The space for the correction bits, one per byte.</param>
    internal JpegBlockRun(Span<byte> bits) => _bits = bits;

    /// <summary>Gets the space for the correction bits, one per byte.</summary>
    internal readonly Span<byte> Bits => _bits;

    /// <summary>Gets or sets the blocks in the run.</summary>
    internal int Count { get; set; }

    /// <summary>Gets or sets the correction bits collected.</summary>
    internal int BitCount { get; set; }
}
