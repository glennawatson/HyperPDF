// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>What reading a file's cross-reference sections found, in the order veraPDF reads them.</summary>
[DebuggerDisplay("PdfAXrefLayout: {Sections.Count} sections")]
internal sealed class PdfAXrefLayout
{
    /// <summary>Gets the sections, newest first.</summary>
    internal List<PdfAXrefSection> Sections { get; } = [];

    /// <summary>Gets or sets a value indicating whether every <c>xref</c> keyword is followed by one line end and a digit.</summary>
    internal bool LineEnds { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether every subsection header separates its numbers with one space.</summary>
    internal bool SubsectionSpacing { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether any section is a cross-reference stream.</summary>
    internal bool HasStream { get; set; }

    /// <summary>Gets the section that starts first in the file.</summary>
    internal PdfAXrefSection? First => Sections.Count > 0 ? Sections.MinBy(static section => section.Offset) : null;

    /// <summary>Gets the section that starts last in the file.</summary>
    internal PdfAXrefSection? Last => Sections.Count > 0 ? Sections.MaxBy(static section => section.Offset) : null;
}
