// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Owns a Skia surface used for PDF page replay on its graphics context's thread.</summary>
[DebuggerDisplay("SkiaSurfaceRenderTarget: {Width} x {Height} Valid={IsValid}")]
public sealed class SkiaSurfaceRenderTarget : IPdfRenderTarget
{
    /// <summary>The surface owned until disposal.</summary>
    private SKSurface? _surface;

    /// <summary>Initializes a new instance of the <see cref="SkiaSurfaceRenderTarget"/> class.</summary>
    /// <param name="surface">The surface to own. Its graphics context must be current for replay and disposal.</param>
    /// <param name="info">The surface's actual premultiplied RGBA or BGRA pixel layout.</param>
    /// <exception cref="ArgumentNullException">The surface is null.</exception>
    /// <exception cref="ArgumentException">The layout is unsupported.</exception>
    public SkiaSurfaceRenderTarget(SKSurface surface, SKImageInfo info)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (info.ColorType is not (SKColorType.Bgra8888 or SKColorType.Rgba8888) || info.AlphaType != SKAlphaType.Premul || info.Width <= 0 || info.Height <= 0 || info.ColorSpace is { IsSrgb: false })
        {
            throw new ArgumentException("The surface must have positive dimensions, sRGB colour, and premultiplied RGBA or BGRA pixels.", nameof(info));
        }

        _surface = surface;
        Width = info.Width;
        Height = info.Height;
    }

    /// <inheritdoc/>
    public int Width { get; }

    /// <inheritdoc/>
    public int Height { get; }

    /// <inheritdoc/>
    public bool IsValid => Volatile.Read(ref _surface) is not null;

    /// <summary>Gets the canvas while this target is owned and its graphics context is current.</summary>
    internal SKCanvas Canvas => Volatile.Read(ref _surface)?.Canvas ?? throw new ObjectDisposedException(nameof(SkiaSurfaceRenderTarget));

    /// <summary>Snapshots the completed surface. The image retains its pixels until it is disposed.</summary>
    /// <returns>An independently owned image.</returns>
    /// <exception cref="ObjectDisposedException">The target has been disposed.</exception>
    public SKImage Snapshot() => Volatile.Read(ref _surface)?.Snapshot() ?? throw new ObjectDisposedException(nameof(SkiaSurfaceRenderTarget));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Interlocked.Exchange(ref _surface, null)?.Dispose();
}
