// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Text;

/// <summary>A glyph already placed on the line being built.</summary>
/// <param name="Font">The font.</param>
/// <param name="Code">The character code.</param>
/// <param name="Origin">The glyph origin in user space.</param>
/// <param name="Em">The font size in user space units.</param>
[DebuggerDisplay("RecentGlyph: {Code} at {Origin}")]
internal readonly record struct RecentGlyph(PdfFont Font, int Code, Vector2 Origin, float Em);
