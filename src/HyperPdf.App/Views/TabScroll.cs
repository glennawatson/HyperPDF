// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;

namespace HyperPdf.App.Views;

/// <summary>A wheel turn that scrolls the tab strip sideways.</summary>
/// <param name="Scroller">The tab strip's scroll viewer.</param>
/// <param name="Offset">The offset to scroll to.</param>
internal readonly record struct TabScroll(ScrollViewer Scroller, Vector Offset);
