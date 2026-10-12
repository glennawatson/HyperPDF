// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Platform;
using HyperPdf.Skia.Gpu;
using SkiaSharp;

namespace HyperPdf.Skia.Bitmaps;

/// <summary>Creation parameters for a <see cref="SurfaceRenderTarget"/>.</summary>
internal readonly record struct SurfaceRenderTargetCreateInfo
{
    /// <summary>Gets width in pixels.</summary>
    public int Width { get; init; }

    /// <summary>Gets height in pixels.</summary>
    public int Height { get; init; }

    /// <summary>Gets surface DPI.</summary>
    public Vector Dpi { get; init; }

    /// <summary>Gets surface pixel format.</summary>
    public PixelFormat? Format { get; init; }

    /// <summary>Gets whether to disable subpixel LCD rendering for text.</summary>
    public bool DisableTextLcdRendering { get; init; }

    /// <summary>Gets optional GPU context.</summary>
    public GRContext? GrContext { get; init; }

    /// <summary>Gets optional GPU provider.</summary>
    public ISkiaGpu? Gpu { get; init; }

    /// <summary>Gets optional GPU render session.</summary>
    public ISkiaGpuRenderSession? Session { get; init; }

    /// <summary>Gets whether manual FBO creation is disabled.</summary>
    public bool DisableManualFbo { get; init; }

    /// <summary>Gets whether drawing should scale to DPI.</summary>
    public bool UseScaledDrawing { get; init; }
}
