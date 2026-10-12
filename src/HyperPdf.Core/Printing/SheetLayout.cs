// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Printing;

/// <summary>How exported pages are laid onto sheets.</summary>
/// <param name="PagesPerSheet">Pages on each sheet: 1, 2, 4, 6, 9 or 16.</param>
/// <param name="Paper">The paper used when several pages share a sheet.</param>
/// <param name="IncludeAnnotations">Whether notes, highlights and drawings are printed; filled form fields always are.</param>
[DebuggerDisplay("SheetLayout: {PagesPerSheet} per sheet on {Paper}")]
public readonly record struct SheetLayout(int PagesPerSheet, PaperSize Paper, bool IncludeAnnotations)
{
    /// <summary>The sheets across (and down) a poster page when none is chosen.</summary>
    private const int DefaultPosterTiles = 2;

    /// <summary>Gets one page per sheet with annotations.</summary>
    public static SheetLayout Default => new(1, PaperSize.A4, true);

    /// <summary>Gets how pages are arranged: in order, as a booklet, or as posters.</summary>
    public PrintImposition Imposition { get; init; }

    /// <summary>Gets whether single pages are placed on the chosen paper for direct printing.</summary>
    public bool FitToPaper { get; init; }

    /// <summary>Gets how single pages are sized on the paper when <see cref="FitToPaper"/> is set.</summary>
    public PrintScaling Scaling { get; init; }

    /// <summary>Gets the percentage of true size used with <see cref="PrintScaling.Custom"/>.</summary>
    public int ScalePercent { get; init; } = PrintScale.TrueSize;

    /// <summary>Gets how many sheets across, and down, each poster page takes: 2 makes four sheets a page.</summary>
    public int PosterTiles { get; init; } = DefaultPosterTiles;
}
