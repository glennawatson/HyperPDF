// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Rendering;

/// <summary>A 32-bit premultiplied BGRA bitmap owned by the UI layer that the render thread can write into.</summary>
public interface IRenderSurface : IDisposable
{
    /// <summary>Gets the width in pixels.</summary>
    int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    int Height { get; }

    /// <summary>Gets the approximate memory used by the surface, in bytes.</summary>
    long ByteSize { get; }

    /// <summary>Locks the surface and lets <paramref name="writer"/> fill it.</summary>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <param name="state">The state handed to the writer.</param>
    /// <param name="writer">The writer.</param>
    /// <returns>The writer's result.</returns>
    bool Write<TState>(in TState state, SurfaceWriter<TState> writer);
}
