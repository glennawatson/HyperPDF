// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Rendering.Metal;

/// <summary>Makes the tiles of the Metal backend.</summary>
/// <param name="retired">The queue disposed tiles leave their textures on.</param>
internal sealed class MetalSurfaceFactory(ConcurrentQueue<ulong> retired) : IRenderSurfaceFactory
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IRenderSurface Create(int width, int height) => new MetalTileSurface(width, height, retired);
}
