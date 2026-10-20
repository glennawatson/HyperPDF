// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Rendering;

/// <summary>How a glyph mask's edges are made.</summary>
internal enum GlyphMaskEdges
{
    /// <summary>Grey levels of coverage.</summary>
    Smooth = 0,

    /// <summary>Hard edges: each pixel is covered or not.</summary>
    Sharp = 1,

    /// <summary>Coverage for the red, green and blue parts of each pixel.</summary>
    Lcd = 2,
}
