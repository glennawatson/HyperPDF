// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Rendering;

namespace HyperPdf.Core.Tests.Fakes;

/// <summary>An in-memory render surface.</summary>
internal sealed class FakeSurface : IRenderSurface
{
    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>Initializes a new instance of the <see cref="FakeSurface"/> class.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    internal FakeSurface(int width, int height)
    {
        Width = width;
        Height = height;
        Pixels = new byte[width * height * BytesPerPixel];
    }

    /// <inheritdoc/>
    public int Width { get; }

    /// <inheritdoc/>
    public int Height { get; }

    /// <inheritdoc/>
    public long ByteSize => Pixels.Length;

    /// <summary>Gets the pixels.</summary>
    internal byte[] Pixels { get; }

    /// <summary>Gets a value indicating whether the surface was disposed.</summary>
    internal bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public bool Write<TState>(in TState state, SurfaceWriter<TState> writer) =>
        writer(new(Pixels, Width, Height, Width * BytesPerPixel), state);

    /// <inheritdoc/>
    public void Dispose() => IsDisposed = true;
}
