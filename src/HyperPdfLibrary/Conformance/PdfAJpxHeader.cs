// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The JP2 header fields the PDF/A JPEG 2000 rules read (ISO 15444-1 Annex I), as veraPDF reads them.</summary>
/// <param name="Channels">The number of components in the image header box; zero when missing.</param>
/// <param name="BitDepth">The bit depth from the image header box; zero when missing.</param>
/// <param name="HasBitsPerComponentBox">Whether a bits-per-component box is present.</param>
/// <param name="ColourSpecifications">The number of colour specification boxes.</param>
/// <param name="ApproximateSpecifications">The number of colour specification boxes whose APPROX field is 1.</param>
/// <param name="Method">The METH value of the colour specification in effect; zero when there is none.</param>
/// <param name="EnumeratedSpace">The enumerated colour space in effect; -1 when there is none.</param>
/// <param name="Space">The colour space the image data declares, used when the image dictionary has none; null when unknown.</param>
internal sealed record PdfAJpxHeader(
    long Channels,
    long BitDepth,
    bool HasBitsPerComponentBox,
    long ColourSpecifications,
    long ApproximateSpecifications,
    long Method,
    long EnumeratedSpace,
    PdfAJpxSpace? Space)
{
    /// <summary>Gets the header of data that is not a JP2 file.</summary>
    internal static PdfAJpxHeader Empty { get; } = new(0, 0, false, 0, 0, 0, -1, null);
}
