// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Conformance;

/// <summary>A substitute font ready to embed.</summary>
/// <param name="Name">The subset name, such as ABCDEF+Arimo-Regular.</param>
/// <param name="Face">The open face.</param>
/// <param name="Standard">The standard font it replaces.</param>
/// <param name="Program">The parsed face, for its metrics.</param>
/// <param name="Subset">The subset program to embed.</param>
/// <param name="Plan">The codes, glyphs, widths and texts.</param>
[DebuggerDisplay("PdfAConversionSubstitution: {Name}")]
internal sealed record PdfAConversionSubstitution(
    string Name,
    PdfAConversionFace Face,
    StandardFont Standard,
    TrueTypeProgram Program,
    byte[] Subset,
    PdfAConversionGlyphPlan Plan);
