// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>What a font descriptor needs to know about a face, in font design units unless said otherwise.</summary>
/// <param name="UnitsPerEm">The design units in one em.</param>
/// <param name="GlyphCount">The number of glyphs.</param>
/// <param name="MetricCount">The number of full entries in the horizontal metrics.</param>
/// <param name="XMin">The left of the face's bounding box.</param>
/// <param name="YMin">The bottom of the face's bounding box.</param>
/// <param name="XMax">The right of the face's bounding box.</param>
/// <param name="YMax">The top of the face's bounding box.</param>
/// <param name="Ascent">The height above the baseline.</param>
/// <param name="Descent">The depth below the baseline, negative.</param>
/// <param name="CapHeight">The height of capital letters.</param>
/// <param name="ItalicAngle">The slant in degrees anticlockwise from vertical; negative for italic.</param>
/// <param name="WeightClass">The weight, 100 (thin) to 900 (black); 400 is regular.</param>
/// <param name="EmbeddingFlags">The licence's embedding flags (OS/2 <c>fsType</c>); zero when the face has no OS/2 table.</param>
/// <param name="Style">The style flags.</param>
/// <param name="PostScriptName">The PostScript name, cleaned for use as a PDF name.</param>
/// <param name="Family">The family name.</param>
[DebuggerDisplay("FontFacts: {PostScriptName}")]
internal sealed record FontFacts(
    int UnitsPerEm,
    int GlyphCount,
    int MetricCount,
    int XMin,
    int YMin,
    int XMax,
    int YMax,
    int Ascent,
    int Descent,
    int CapHeight,
    float ItalicAngle,
    int WeightClass,
    int EmbeddingFlags,
    FaceStyleFlags Style,
    string PostScriptName,
    string Family);
