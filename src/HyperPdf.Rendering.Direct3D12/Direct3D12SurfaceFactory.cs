// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Rendering.Direct3D12;

/// <summary>Makes the tiles of the Direct3D 12 backend.</summary>
/// <param name="retired">The queue disposed tiles leave their GPU texture on.</param>
internal sealed class Direct3D12SurfaceFactory(ConcurrentQueue<Direct3D12Retired> retired) : IRenderSurfaceFactory
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IRenderSurface Create(int width, int height) => new Direct3D12TileSurface(width, height, retired);
}
