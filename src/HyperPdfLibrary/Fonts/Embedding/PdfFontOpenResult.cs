// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>The outcome of opening a font file for embedding: the font, or the plain reason it cannot be embedded.</summary>
/// <param name="Source">The font, or <see langword="null"/> when it was refused.</param>
/// <param name="Refusal">The plain reason the font was refused, or <see langword="null"/> when it was accepted.</param>
[DebuggerDisplay("PdfFontOpenResult: {Source?.PostScriptName ?? Refusal}")]
public sealed record PdfFontOpenResult(PdfFontSource? Source, string? Refusal)
{
    /// <summary>Gets a value indicating whether the font can be embedded.</summary>
    [MemberNotNullWhen(true, nameof(Source))]
    [MemberNotNullWhen(false, nameof(Refusal))]
    public bool IsEmbeddable => Source is not null;
}
