// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Rendering;

/// <summary>Creates <see cref="IRenderSurface"/> instances. Must be callable from the render thread.</summary>
public interface IRenderSurfaceFactory
{
    /// <summary>Creates a surface.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The surface.</returns>
    IRenderSurface Create(int width, int height);
}
