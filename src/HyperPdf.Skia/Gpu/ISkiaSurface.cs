// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using SkiaSharp;

namespace HyperPdf.Skia.Gpu;

/// <summary>Wraps a Skia surface for GPU rendering.</summary>
internal interface ISkiaSurface : IDisposable
{
    /// <summary>Gets the Skia surface.</summary>
    SKSurface Surface { get; }

    /// <summary>Gets a value indicating whether this surface supports direct blitting.</summary>
    bool CanBlit { get; }

    /// <summary>Blits the surface contents to the target canvas.</summary>
    /// <param name = "canvas">The target canvas.</param>
    void Blit(SKCanvas canvas);
}
