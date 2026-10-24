// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>How the width of a grid column is worked out.</summary>
public enum PdfColumnWidthKind
{
    /// <summary>The column is exactly this many points wide.</summary>
    Fixed = 0,

    /// <summary>The column takes a share of what the fixed columns leave, in proportion to its weight.</summary>
    Star = 1,
}
