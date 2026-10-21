// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.ViewModels.Sidebar;

/// <summary>A page thumbnail in the sidebar.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Label">The page label shown under the thumbnail.</param>
/// <param name="Width">The thumbnail width in device independent pixels.</param>
/// <param name="Height">The thumbnail height in device independent pixels.</param>
[DebuggerDisplay("ThumbnailItemViewModel: Page {Label}")]
public sealed record ThumbnailItemViewModel(int PageIndex, string Label, double Width, double Height)
{
    /// <summary>Gets the colourant the page paints when it is a colour separation of a preseparated file; null for an ordinary page.</summary>
    public string? Colorant { get; init; }

    /// <summary>Gets the text shown under the thumbnail: the label, and the colourant of a separation page, for example "3 (Cyan)".</summary>
    public string Caption => Colorant is { } colorant ? $"{Label} ({colorant})" : Label;
}
