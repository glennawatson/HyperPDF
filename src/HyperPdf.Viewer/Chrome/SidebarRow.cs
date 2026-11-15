// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Chrome;

/// <summary>One row of the side panel: a page, a bookmark, a note, an attached file or a layer.</summary>
/// <param name="Text">The row's text.</param>
/// <param name="Target">What the row stands for, which a press acts on.</param>
/// <param name="Selected">Whether it is the chosen one of its list, or a layer that is shown.</param>
/// <param name="Enabled">Whether it can be pressed.</param>
/// <param name="Page">The page whose picture the row shows, or -1 for a row of text.</param>
[DebuggerDisplay("SidebarRow: {Text}")]
internal sealed record SidebarRow(string Text, object Target, bool Selected, bool Enabled, int Page);
