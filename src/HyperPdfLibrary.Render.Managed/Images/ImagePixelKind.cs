// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Render.Managed.Images;

/// <summary>What the bytes of an image stand for.</summary>
internal enum ImagePixelKind
{
    /// <summary>Four bytes a pixel: blue, green, red and alpha, with the colour multiplied by the alpha.</summary>
    Color = 0,

    /// <summary>One byte a pixel: an opaque gray.</summary>
    Gray = 1,

    /// <summary>One byte a pixel: how much of the fill colour a stencil mask paints, where 255 is all of it.</summary>
    Coverage = 2,
}
