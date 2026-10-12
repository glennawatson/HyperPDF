// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>One segment of a format 4 'cmap' subtable.</summary>
/// <param name="Index">The segment index.</param>
/// <param name="Count">The number of segments.</param>
/// <param name="Start">The first code.</param>
/// <param name="End">The last code.</param>
/// <param name="Delta">The id delta, unsigned.</param>
/// <param name="RangeOffset">The id range offset.</param>
internal readonly record struct PdfATrueTypeSegment(int Index, int Count, int Start, int End, int Delta, int RangeOffset);
