// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>Counts the transparency effects a PDF/A-1 conversion finds.</summary>
[DebuggerDisplay("PdfAConversionTransparencyTally: {Effects}")]
internal sealed class PdfAConversionTransparencyTally
{
    /// <summary>Gets or sets the soft masks, alpha values below 1 and blend modes found.</summary>
    internal int Effects { get; set; }
}
