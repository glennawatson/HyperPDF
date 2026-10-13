// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Fonts.Programs;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>What a simple font's dictionary says, gathered before its glyphs are mapped.</summary>
/// <param name="Font">The font dictionary.</param>
/// <param name="BaseFont">The /BaseFont without a subset tag.</param>
/// <param name="Standard">The standard 14 font a /Type1 font names, or <see cref="StandardFont.None"/>.</param>
/// <param name="Descriptor">The font descriptor.</param>
/// <param name="Flags">The effective flags.</param>
/// <param name="Program">The embedded program, or <see langword="null"/>.</param>
/// <param name="IsTrueTypeSubtype">Whether the font's /Subtype is /TrueType.</param>
[DebuggerDisplay("SimpleFontFacts: {BaseFont}")]
internal sealed record SimpleFontFacts(
    PdfDictionary Font,
    string BaseFont,
    StandardFont Standard,
    FontDescriptor Descriptor,
    FontFlags Flags,
    FontProgram? Program,
    bool IsTrueTypeSubtype)
{
    /// <summary>Gets a value indicating whether the descriptor flags say symbolic.</summary>
    internal bool IsSymbolic => (Flags & FontFlags.Symbolic) != 0;

    /// <summary>Gets a value indicating whether glyphs come from a TrueType face, which decides whether the TrueType cmap rules of ISO 32000-2, 9.6.5.4 apply.</summary>
    internal bool HasTrueTypeFace => Program is not null
        ? Program is TrueTypeProgram { HasCffOutlines: false }
        : IsTrueTypeSubtype || Standard == StandardFont.None;

    /// <summary>Gets the built-in encoding of the Symbol and ZapfDingbats standard fonts, else <see cref="FontEncoding.None"/>.</summary>
    internal FontEncoding SymbolEncoding => Standard switch
    {
        StandardFont.Symbol => FontEncoding.Symbol,
        StandardFont.ZapfDingbats => FontEncoding.ZapfDingbats,
        _ => FontEncoding.None,
    };
}
