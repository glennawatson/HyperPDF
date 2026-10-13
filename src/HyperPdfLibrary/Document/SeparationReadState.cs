// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>The working state of one read of a document's separation dictionaries.</summary>
/// <param name="document">The document being read.</param>
/// <param name="colorantKey">The document's <c>/DeviceColorant</c> name.</param>
[DebuggerDisplay("SeparationReadState: {Entries.Count} entries")]
internal sealed class SeparationReadState(PdfDocument document, PdfName colorantKey)
{
    /// <summary>Gets the document being read.</summary>
    internal PdfDocument Document { get; } = document;

    /// <summary>Gets the document's <c>/DeviceColorant</c> name.</summary>
    internal PdfName ColorantKey { get; } = colorantKey;

    /// <summary>Gets the pages with a separation dictionary, in page order.</summary>
    internal List<SeparationEntry> Entries { get; } = [];

    /// <summary>Gets the same entries by page index.</summary>
    internal Dictionary<int, SeparationEntry> ByPage { get; } = [];

    /// <summary>Gets the problems found so far.</summary>
    internal List<PdfSeparationFinding> Findings { get; } = [];

    /// <summary>Gets scratch space for the page indexes one <c>/Pages</c> array has listed.</summary>
    internal HashSet<int> Listed { get; } = [];

    /// <summary>Gets scratch space for the page indexes one <c>/Pages</c> array lists, in order.</summary>
    internal List<int> Members { get; } = [];

    /// <summary>Records a problem.</summary>
    /// <param name="pageIndex">The page whose dictionary has it.</param>
    /// <param name="problem">The problem.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Report(int pageIndex, PdfSeparationProblem problem) => Findings.Add(new(pageIndex, problem));
}
