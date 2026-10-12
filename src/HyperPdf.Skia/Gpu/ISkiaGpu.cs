// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using SkiaSharp;

namespace HyperPdf.Skia.Gpu;

/// <summary>Provides GPU surfaces and sessions for the renderer.</summary>
internal interface ISkiaGpu : IDisposable
{
    /// <summary>Gets whether the GPU context is lost.</summary>
    bool IsLost { get; }

    /// <summary>Gets the platform graphics context.</summary>
    IPlatformGraphicsContext? PlatformGraphicsContext { get; }

    /// <summary>Creates a GPU target for a supported surface.</summary>
    /// <param name="surfaces">The available surfaces.</param>
    /// <returns>The render target, when supported.</returns>
    ISkiaGpuRenderTarget? TryCreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces);

    /// <summary>Checks whether a supported surface is ready.</summary>
    /// <param name="surfaces">The available surfaces.</param>
    /// <returns>Whether rendering can begin.</returns>
    bool IsReadyToCreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces);

    /// <summary>Creates an intermediate GPU surface.</summary>
    /// <param name="size">The pixel dimensions.</param>
    /// <param name="session">The current rendering session.</param>
    /// <returns>The surface, when supported.</returns>
    ISkiaSurface? TryCreateSurface(PixelSize size, ISkiaGpuRenderSession? session);

    /// <summary>Acquires the Skia graphics context.</summary>
    /// <returns>The owned context lease.</returns>
    IScopedResource<GRContext>? TryGetGrContext();

    /// <summary>Makes the platform graphics context current.</summary>
    /// <returns>The context lease.</returns>
    IDisposable EnsureCurrent();

    /// <summary>Gets an optional graphics feature.</summary>
    /// <param name="featureType">The feature contract.</param>
    /// <returns>The feature, when supported.</returns>
    object? TryGetFeature(Type featureType);
}
