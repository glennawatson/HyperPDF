// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Chrome;

/// <summary>The side panel as it was laid out.</summary>
/// <param name="Width">The width the panel takes, in device pixels.</param>
/// <param name="View">What the panel lists, as it is now.</param>
[DebuggerDisplay("SidebarLayout: {Width} px, {View.Rows.Length} rows")]
internal sealed record SidebarLayout(int Width, SidebarView View);
