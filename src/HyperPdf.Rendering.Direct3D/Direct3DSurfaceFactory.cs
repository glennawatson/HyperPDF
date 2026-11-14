// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Rendering.Direct3D;

/// <summary>Makes the tiles of the Direct3D backend.</summary>
/// <param name="retired">The queue disposed tiles leave their GPU objects on.</param>
internal sealed class Direct3DSurfaceFactory(ConcurrentQueue<Direct3DRetired> retired) : IRenderSurfaceFactory
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IRenderSurface Create(int width, int height) => new Direct3DTileSurface(width, height, retired);
}
