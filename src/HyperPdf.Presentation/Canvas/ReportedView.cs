// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>What the last position report was made from; the same view gives the same report.</summary>
/// <param name="Tab">The tab reported to.</param>
/// <param name="Viewport">The viewport, with its offset.</param>
/// <param name="Options">The layout options.</param>
/// <param name="PageCount">The pages laid out.</param>
/// <param name="ExtentWidth">The width of the laid out content.</param>
/// <param name="ExtentHeight">The height of the laid out content.</param>
[DebuggerDisplay("ReportedView: {Tab}")]
public readonly record struct ReportedView(DocumentTabViewModel Tab, LayoutRect Viewport, LayoutOptions Options, int PageCount, double ExtentWidth, double ExtentHeight);
