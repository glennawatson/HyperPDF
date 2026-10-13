// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>Changes to the viewer preferences (ISO 32000-2 12.2, Table 147); a property left <see langword="null"/> is not changed.</summary>
[DebuggerDisplay("PdfViewerPreferencesEdit")]
public sealed record PdfViewerPreferencesEdit
{
    /// <summary>Gets whether the toolbars are hidden.</summary>
    public bool? HideToolbar { get; init; }

    /// <summary>Gets whether the menu bar is hidden.</summary>
    public bool? HideMenubar { get; init; }

    /// <summary>Gets whether window controls are hidden.</summary>
    public bool? HideWindowUI { get; init; }

    /// <summary>Gets whether the window fits the first page.</summary>
    public bool? FitWindow { get; init; }

    /// <summary>Gets whether the window is centred on the screen.</summary>
    public bool? CenterWindow { get; init; }

    /// <summary>Gets whether the window title shows the document title rather than the file name.</summary>
    public bool? DisplayDocTitle { get; init; }

    /// <summary>Gets the page mode to return to from full screen: <c>UseNone</c>, <c>UseOutlines</c>, <c>UseThumbs</c> or <c>UseOC</c>.</summary>
    public string? NonFullScreenPageMode { get; init; }

    /// <summary>Gets the reading direction: <c>L2R</c> or <c>R2L</c>.</summary>
    public string? Direction { get; init; }

    /// <summary>Gets the page boundary shown on screen: <c>MediaBox</c>, <c>CropBox</c>, <c>BleedBox</c>, <c>TrimBox</c> or <c>ArtBox</c>.</summary>
    public string? ViewArea { get; init; }

    /// <summary>Gets the page boundary the screen view is clipped to.</summary>
    public string? ViewClip { get; init; }

    /// <summary>Gets the page boundary printed.</summary>
    public string? PrintArea { get; init; }

    /// <summary>Gets the page boundary printing is clipped to.</summary>
    public string? PrintClip { get; init; }

    /// <summary>Gets the print scaling: <c>None</c> or <c>AppDefault</c>.</summary>
    public string? PrintScaling { get; init; }

    /// <summary>Gets the duplex mode: <c>Simplex</c>, <c>DuplexFlipShortEdge</c> or <c>DuplexFlipLongEdge</c>; an empty string removes it.</summary>
    public string? Duplex { get; init; }

    /// <summary>Gets whether the paper tray is picked from the page size.</summary>
    public bool? PickTrayByPdfSize { get; init; }

    /// <summary>Gets the pages to print as first and last page pairs counted from 1; an empty list removes the preference.</summary>
    public IReadOnlyList<int>? PrintPageRange { get; init; }

    /// <summary>Gets the number of copies to print, from 1 to 5.</summary>
    public int? NumCopies { get; init; }
}
