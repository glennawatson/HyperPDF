// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>What writing a run needs to know about the font it is shown in.</summary>
/// <param name="Sink">The document, for names and the <c>/ActualText</c> dictionaries.</param>
/// <param name="Source">The font.</param>
/// <param name="Use">The glyph codes in use.</param>
/// <param name="Mode">The writing direction.</param>
[DebuggerDisplay("RunContext: {Source.PostScriptName}")]
internal readonly record struct RunContext(IPdfObjectSink Sink, PdfFontSource Source, EmbeddedGlyphUse Use, PdfWritingMode Mode)
{
    /// <summary>Gets a glyph's advance along the writing direction as the font's widths give it.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The advance in thousandths of an em, rounded as it is in <c>/W</c> or <c>/W2</c>; negative for vertical writing, which advances downward.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal float Width(int glyph) => Mode == PdfWritingMode.Vertical
        ? -MathF.Round(Source.GetAdvanceAlong(glyph, Mode))
        : MathF.Round(Source.GetAdvanceAlong(glyph, Mode));
}
