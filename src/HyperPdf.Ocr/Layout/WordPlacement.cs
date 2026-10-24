// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Ocr.Layout;

/// <summary>Where a line was cut from a page, and the page's ink, which words are fitted to.</summary>
/// <param name="Window">The part of the page the line was cut from.</param>
/// <param name="Ink">The ink of the page.</param>
[DebuggerDisplay("WordPlacement: {Window}")]
public readonly record struct WordPlacement(LineWindow Window, InkMask Ink);
