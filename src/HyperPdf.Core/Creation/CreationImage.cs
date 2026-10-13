// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Creation;

/// <summary>An image on a line of its own.</summary>
/// <param name="Data">A JPEG file, or decoded pixels in the layout <paramref name="Format"/> names.</param>
/// <param name="Format">How <paramref name="Data"/> is coded.</param>
/// <param name="Width">The width in pixels, used for decoded pixels; a JPEG carries its own size.</param>
/// <param name="Height">The height in pixels, used for decoded pixels.</param>
/// <param name="AlternativeText">What the image shows, for people who cannot see it. An image with no text is treated as decoration.</param>
/// <param name="WidthPoints">The width to draw it at in points, or zero for its pixel width at 96 pixels an inch.</param>
[DebuggerDisplay("CreationImage: {Format}, {AlternativeText}")]
public sealed record CreationImage(
    ReadOnlyMemory<byte> Data,
    CreationImageFormat Format,
    int Width,
    int Height,
    string AlternativeText,
    float WidthPoints) : CreationBlock;
