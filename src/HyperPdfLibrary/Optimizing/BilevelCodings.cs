// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Optimizing;

/// <summary>The codings the optimiser may try for one black-and-white image, besides Flate.</summary>
/// <param name="UseFax">Whether CCITT Group 4 is a candidate.</param>
/// <param name="Jbig2">Whether JBIG2 is a candidate, and whether it may be lossy.</param>
internal readonly record struct BilevelCodings(bool UseFax, PdfJbig2Mode Jbig2)
{
    /// <summary>Gets a value indicating whether any coding besides Flate is a candidate.</summary>
    internal bool IsAnyEnabled => UseFax || Jbig2 != PdfJbig2Mode.Off;
}
