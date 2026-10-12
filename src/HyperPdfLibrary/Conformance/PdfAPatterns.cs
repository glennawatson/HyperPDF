// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The pattern types and paint types the PDF/A walk reads.</summary>
internal static class PdfAPatterns
{
    /// <summary>The <c>/PatternType</c> of a tiling pattern.</summary>
    internal const long Tiling = 1;

    /// <summary>The <c>/PatternType</c> of a shading pattern.</summary>
    internal const long Shading = 2;

    /// <summary>The <c>/PaintType</c> of an uncoloured tiling pattern.</summary>
    internal const long Uncoloured = 2;
}
