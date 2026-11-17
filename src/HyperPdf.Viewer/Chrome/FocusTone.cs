// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Chrome;

/// <summary>A colour for the page of focus mode and for the text on it.</summary>
/// <param name="Ground">The colour of the page as ARGB, or zero for the window's own.</param>
/// <param name="Text">The colour of the text as ARGB, or zero for the window's own.</param>
[DebuggerDisplay("FocusTone: page {Ground:X8}, text {Text:X8}")]
internal readonly record struct FocusTone(uint Ground, uint Text);
