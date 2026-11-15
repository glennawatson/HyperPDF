// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>Which of the canvas's shaders gives a paint its colours. The canvas owns one of each and sets it up before the shape is filled.</summary>
internal enum RasterShaderKind
{
    /// <summary>No shader: the paint is one colour.</summary>
    None = 0,

    /// <summary>The gradient shader.</summary>
    Gradient = 1,

    /// <summary>The tiling pattern shader.</summary>
    Tile = 2,

    /// <summary>The image shader.</summary>
    Image = 3,
}
