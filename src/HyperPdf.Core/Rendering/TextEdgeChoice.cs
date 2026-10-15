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
}
