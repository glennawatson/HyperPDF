// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>Counts what the annotation fixes changed and found, so each kind is reported once.</summary>
[DebuggerDisplay("PdfAConversionAnnotationTally: {Removed} removed, {Flags} flags")]
internal sealed class PdfAConversionAnnotationTally
{
    /// <summary>Gets the annotation types the level forbids that were found.</summary>
    internal SortedSet<string> Forbidden { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the hidden annotations removed.</summary>
    internal int Removed { get; set; }

    /// <summary>Gets or sets the hidden form fields found.</summary>
    internal int HiddenFields { get; set; }

    /// <summary>Gets or sets the annotations whose flags were set.</summary>
    internal int Flags { get; set; }

    /// <summary>Gets or sets the see-through annotations found in a PDF/A-1 conversion.</summary>
    internal int Transparent { get; set; }

    /// <summary>Gets or sets the annotations whose colour arrays were removed.</summary>
    internal int Colours { get; set; }

    /// <summary>Gets or sets the annotations whose extra appearances were removed.</summary>
    internal int ExtraAppearances { get; set; }

    /// <summary>Gets or sets the annotations whose appearance states were reduced to the one shown.</summary>
    internal int StateAppearances { get; set; }

    /// <summary>Gets or sets the annotations that need an appearance and have none.</summary>
    internal int MissingAppearances { get; set; }
}
