// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>The entries of an inline image dictionary the syntax checks read; the abbreviated keys win, as in veraPDF.</summary>
[DebuggerDisplay("PdfAInlineImageParameters: L={Length}")]
internal sealed class PdfAInlineImageParameters
{
    /// <summary>Gets or sets a value indicating whether the dictionary has /F.</summary>
    internal bool HasAbbreviatedFilter { get; set; }

    /// <summary>Gets or sets what the /F filters break.</summary>
    internal PdfAInlineFilters AbbreviatedFilter { get; set; } = PdfAInlineFilters.None;

    /// <summary>Gets or sets what the /Filter filters break.</summary>
    internal PdfAInlineFilters Filter { get; set; } = PdfAInlineFilters.None;

    /// <summary>Gets a value indicating whether the dictionary has an integer /L.</summary>
    internal bool HasAbbreviatedLength { get; private set; }

    /// <summary>Gets the /L value.</summary>
    internal long? AbbreviatedLength { get; private set; }

    /// <summary>Gets the /Length value.</summary>
    internal long? Length { get; private set; }

    /// <summary>Notes a data length.</summary>
    /// <param name="abbreviated">Whether the key is /L rather than /Length.</param>
    /// <param name="value">The length.</param>
    internal void SetLength(bool abbreviated, long value)
    {
        if (abbreviated)
        {
            HasAbbreviatedLength = true;
            AbbreviatedLength = value;
        }
        else
        {
            Length = value;
        }
    }
}
