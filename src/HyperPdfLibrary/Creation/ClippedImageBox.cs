// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>The destination rectangle and clipping rectangle for a clipped image draw operation.</summary>
/// <param name="X">The image rectangle's left edge.</param>
/// <param name="Y">The image rectangle's bottom edge.</param>
/// <param name="Width">The image rectangle's width.</param>
/// <param name="Height">The image rectangle's height.</param>
/// <param name="ClipX">The clip rectangle's left edge.</param>
/// <param name="ClipY">The clip rectangle's bottom edge.</param>
/// <param name="ClipWidth">The clip rectangle's width.</param>
/// <param name="ClipHeight">The clip rectangle's height.</param>
internal readonly record struct ClippedImageBox(
    float X,
    float Y,
    float Width,
    float Height,
    float ClipX,
    float ClipY,
    float ClipWidth,
    float ClipHeight);
