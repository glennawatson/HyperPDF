// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The state of reading a document's content streams for the PDF/A syntax rules: the streams still to read, the ones
/// already read, and the operands and font of the stream being read.
/// </summary>
/// <param name="scan">The pass.</param>
[DebuggerDisplay("PdfASyntaxWalk: {Work.Count} streams queued")]
internal sealed class PdfASyntaxWalk(PdfAFileScan scan)
{
    /// <summary>Gets the pass.</summary>
    internal PdfAFileScan Scan { get; } = scan;

    /// <summary>Gets the rules the waiting operands break, before their operator decides which count.</summary>
    internal PdfAObjectFindings OperandFindings { get; } = new();

    /// <summary>Gets the operands waiting for their operator.</summary>
    internal List<PdfASyntaxOperand> Operands { get; } = [];

    /// <summary>Gets the content streams still to read.</summary>
    internal Stack<PdfASyntaxWork> Work { get; } = new();

    /// <summary>Gets the stream dictionaries already queued, so each stream is read once.</summary>
    internal HashSet<PdfDictionary> Seen { get; } = [with(ReferenceEqualityComparer.Instance)];

    /// <summary>Gets the fonts saved by <c>q</c>.</summary>
    internal Stack<PdfDictionary?> SavedFonts { get; } = new();

    /// <summary>Gets or sets the resources of the stream being read.</summary>
    internal PdfDictionary? Resources { get; set; }

    /// <summary>Gets or sets the font <c>Tf</c> selected.</summary>
    internal PdfDictionary? Font { get; set; }

    /// <summary>Gets the objects.</summary>
    internal PdfObjectStore Store => Scan.Store;
}
