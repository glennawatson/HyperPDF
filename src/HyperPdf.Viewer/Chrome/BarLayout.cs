// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;

namespace HyperPdf.Viewer.Chrome;

/// <summary>The bars and the side panel as they were laid out.</summary>
/// <param name="Canvas">The part of the window left for the pages, in device pixels.</param>
/// <param name="Sidebar">What the side panel lists, as it is now.</param>
[DebuggerDisplay("BarLayout: canvas {Canvas}")]
internal sealed record BarLayout(LayoutRect Canvas, SidebarView Sidebar);
