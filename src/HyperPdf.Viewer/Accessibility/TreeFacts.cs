// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Reading;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Windowing.Rendering;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Viewer.Accessibility;

/// <summary>Everything the accessibility tree is built from, read without allocating, so the tree is built again only when one of these changed.</summary>
/// <param name="Tab">The tab shown.</param>
/// <param name="Page">The page shown.</param>
/// <param name="PageCount">The number of pages.</param>
/// <param name="WindowName">The tab's name in the window title.</param>
/// <param name="Reading">The reading order, which arrives after the document opens.</param>
/// <param name="Selected">The picked annotation.</param>
/// <param name="Offset">The scroll offset.</param>
/// <param name="PageScale">The scale pages are laid out at.</param>
/// <param name="Size">The size of the window's drawing area.</param>
/// <param name="Origin">Where the drawing area is on the screen.</param>
/// <param name="Controls">A number that changes when the window's controls change.</param>
[DebuggerDisplay("TreeFacts page {Page} of {PageCount}")]
internal readonly record struct TreeFacts(
    DocumentTabViewModel? Tab,
    int Page,
    int PageCount,
    string? WindowName,
    ReadingDocument? Reading,
    object? Selected,
    CanvasPoint Offset,
    double PageScale,
    SurfaceSize Size,
    ScreenPoint Origin,
    int Controls)
{
    /// <summary>Reads the facts of what the viewer shows now.</summary>
    /// <param name="tab">The tab shown, or <see langword="null"/> when none is open.</param>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="window">The window.</param>
    /// <returns>The facts.</returns>
    internal static TreeFacts Read(DocumentTabViewModel? tab, ViewerCanvas canvas, IPlatformWindow window) => tab is null
        ? new(null, 0, 0, null, null, null, canvas.Offset, canvas.State.PageScale, window.ClientSize, window.ScreenOrigin, canvas.Chrome?.ControlsVersion ?? 0)
        : new(
            tab,
            tab.CurrentPageIndex,
            tab.PageCount,
            tab.WindowName,
            tab.GetReadingDocument(),
            tab.Annotations.Selected,
            canvas.Offset,
            canvas.State.PageScale,
            window.ClientSize,
            window.ScreenOrigin,
            canvas.Chrome?.ControlsVersion ?? 0);
}
