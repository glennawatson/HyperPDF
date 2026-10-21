// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;

namespace HyperPdf.App.Views;

/// <summary>A tab being dragged along the tab strip.</summary>
/// <param name="tab">The tab the pointer pressed.</param>
/// <param name="start">Where the drag started, in tab strip coordinates.</param>
[DebuggerDisplay("TabDrag: {Tab}")]
internal sealed class TabDrag(DocumentTabViewModel tab, Point start)
{
    /// <summary>Gets the tab the pointer pressed.</summary>
    public DocumentTabViewModel Tab { get; } = tab;

    /// <summary>Gets or sets where the drag started or last moved the tab, in tab strip coordinates.</summary>
    public Point Start { get; set; } = start;
}
