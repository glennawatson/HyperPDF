// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A section: content that shares one page setup, with its own headers and footers. A section starts on a new page.</summary>
/// <param name="Blocks">The section's content.</param>
[DebuggerDisplay("PdfSection: {Blocks.Count} blocks")]
public sealed record PdfSection(IReadOnlyList<PdfBlock> Blocks)
{
    /// <summary>Gets the paper, margins and numbering.</summary>
    public PdfPageSetup Setup { get; init; } = new();

    /// <summary>Gets the header of pages that have no more specific one, or <see langword="null"/> for none.</summary>
    public PdfRunningContent? Header { get; init; }

    /// <summary>Gets the footer of pages that have no more specific one, or <see langword="null"/> for none.</summary>
    public PdfRunningContent? Footer { get; init; }

    /// <summary>Gets the header of the first page when <see cref="PdfPageSetup.DifferentFirstPage"/> is set, or <see langword="null"/> for none.</summary>
    public PdfRunningContent? FirstPageHeader { get; init; }

    /// <summary>Gets the footer of the first page when <see cref="PdfPageSetup.DifferentFirstPage"/> is set, or <see langword="null"/> for none.</summary>
    public PdfRunningContent? FirstPageFooter { get; init; }

    /// <summary>Gets the header of even pages when <see cref="PdfPageSetup.DifferentOddAndEven"/> is set, or <see langword="null"/> to use <see cref="Header"/>.</summary>
    public PdfRunningContent? EvenPageHeader { get; init; }

    /// <summary>Gets the footer of even pages when <see cref="PdfPageSetup.DifferentOddAndEven"/> is set, or <see langword="null"/> to use <see cref="Footer"/>.</summary>
    public PdfRunningContent? EvenPageFooter { get; init; }
}
