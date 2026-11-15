// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>Makes <see cref="VulkanTileSurface"/> tiles that report their GPU objects for release to one queue.</summary>
/// <param name="retired">The queue of GPU objects to free.</param>
internal sealed class VulkanSurfaceFactory(ConcurrentQueue<VulkanRetired> retired) : IRenderSurfaceFactory
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IRenderSurface Create(int width, int height) => new VulkanTileSurface(width, height, retired);
}
