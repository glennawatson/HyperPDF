// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts;

/// <summary>Font-wide metrics and drawing flags, in text space units.</summary>
/// <param name="Ascent">The ascent.</param>
/// <param name="Descent">The descent, usually negative.</param>
/// <param name="IsBold">Whether the font is bold.</param>
/// <param name="AdjustSpacing">
/// Whether glyphs of a substituted font are centred or narrowed to the PDF widths, used when the system lacks the
/// named font.
/// </param>
[DebuggerDisplay("FontMetrics: {Ascent} {Descent}")]
internal readonly record struct FontMetrics(float Ascent, float Descent, bool IsBold, bool AdjustSpacing);
