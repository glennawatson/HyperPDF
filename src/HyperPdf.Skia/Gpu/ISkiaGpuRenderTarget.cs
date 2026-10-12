// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Avalonia.Platform;

namespace HyperPdf.Skia.Gpu;

/// <summary>Represents a Skia GPU-accelerated render target.</summary>
internal interface ISkiaGpuRenderTarget : IDisposable
{
    /// <summary>Gets the current platform render target state.</summary>
    PlatformRenderTargetState State => PlatformRenderTargetState.Ready;

    /// <summary>Begins a new rendering session for the given scene.</summary>
    /// <param name = "sceneInfo">Scene information.</param>
    /// <returns>A new GPU render session.</returns>
    ISkiaGpuRenderSession BeginRenderingSession(IRenderTarget.RenderTargetSceneInfo sceneInfo);
}
