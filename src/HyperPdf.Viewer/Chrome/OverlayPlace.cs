// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;
using HyperPdf.Viewer.Text;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Viewer.Chrome;

/// <summary>What a menu, a dialog or the search bar is laid out with.</summary>
/// <param name="Runs">The text measurer.</param>
/// <param name="Metrics">The sizes.</param>
/// <param name="Size">The window size in device pixels.</param>
/// <param name="Area">The part of the window left for the pages, in device pixels. Its top is the bottom of the bars.</param>
[DebuggerDisplay("OverlayPlace {Size} area {Area}")]
internal readonly record struct OverlayPlace(TextRuns Runs, ChromeMetrics Metrics, SurfaceSize Size, LayoutRect Area);
