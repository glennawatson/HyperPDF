// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>The two vertical values <c>/DW2</c> holds, in thousandths of an em.</summary>
/// <param name="OriginY">The height of the vertical origin above the horizontal origin.</param>
/// <param name="Advance">The signed displacement along the vertical writing direction.</param>
[DebuggerDisplay("VerticalPair: {OriginY}, {Advance}")]
internal readonly record struct VerticalPair(float OriginY, float Advance);
