// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Rendering;

namespace HyperPdf.Windowing.Rendering;

/// <summary>One way of drawing tiles into a window: it makes the tile surfaces and presents a frame.</summary>
public interface IRenderBackend : IDisposable
{
    /// <summary>Gets which backend this is.</summary>
    RenderBackendKind Kind { get; }

    /// <summary>Gets the factory that makes tile surfaces this backend can draw.</summary>
    IRenderSurfaceFactory Surfaces { get; }

    /// <summary>Gets a value indicating whether the backend is still usable. A lost device turns this false.</summary>
    bool IsAlive { get; }

    /// <summary>Changes the size of the drawing area in device pixels.</summary>
    /// <param name="size">The new size.</param>
    void Resize(in SurfaceSize size);

    /// <summary>Starts a frame.</summary>
    /// <returns>The pass to draw into.</returns>
    IFramePass BeginFrame();
}
