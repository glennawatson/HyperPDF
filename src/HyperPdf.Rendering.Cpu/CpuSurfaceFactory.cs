// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Rendering.Cpu;

/// <summary>Makes <see cref="CpuPixelSurface"/> tiles.</summary>
public sealed class CpuSurfaceFactory : IRenderSurfaceFactory
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IRenderSurface Create(int width, int height) => new CpuPixelSurface(width, height);
}
