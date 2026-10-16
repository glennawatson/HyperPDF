// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>A contiguous UTF-16 slice of text resolved to a single <see cref="PdfCanvasFont"/> during glyph-aware font fallback.</summary>
/// <param name="Start">The zero-based UTF-16 character index where the run starts.</param>
/// <param name="Length">The UTF-16 character length of the run.</param>
/// <param name="Font">The font selected to measure and draw the run.</param>
[DebuggerDisplay("PdfFontFallbackRun: {Start}..{Start + Length} ({Font.Name})")]
public readonly record struct PdfFontFallbackRun(int Start, int Length, PdfCanvasFont Font);
