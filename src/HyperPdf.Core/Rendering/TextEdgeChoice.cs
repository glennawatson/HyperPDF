// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Rendering;

/// <summary>How the edges of text are drawn.</summary>
public enum TextEdgeChoice
{
    /// <summary>Edges are smoothed with grey levels.</summary>
    Smooth = 0,

    /// <summary>Edges are not smoothed, so every pixel is fully dark or fully clear.</summary>
    Sharp = 1,

    /// <summary>Edges are smoothed separately for the red, green and blue parts of each pixel, which makes small text look sharper on most screens. It works best on a plain page colour.</summary>
    Lcd = 2,
}
