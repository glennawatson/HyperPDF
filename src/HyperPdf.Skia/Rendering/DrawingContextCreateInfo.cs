// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using HyperPdf.Skia.Gpu;
using SkiaSharp;

namespace HyperPdf.Skia.Rendering;

/// <summary>Creation information for a drawing context.</summary>
internal readonly record struct DrawingContextCreateInfo
{
    /// <summary>Gets the target canvas.</summary>
    public SKCanvas? Canvas { get; init; }

    /// <summary>Gets the target surface.</summary>
    public SKSurface? Surface { get; init; }

    /// <summary>Gets whether drawing should scale to DPI.</summary>
    public bool ScaleDrawingToDpi { get; init; }

    /// <summary>Gets the DPI of the drawing context.</summary>
    public Vector Dpi { get; init; }

    /// <summary>Gets whether subpixel text rendering is disabled.</summary>
    public bool DisableSubpixelTextRendering { get; init; }

    /// <summary>Gets the Skia GPU context if available.</summary>
    public GRContext? GrContext { get; init; }

    /// <summary>Gets the pixel channel order of the target surface.</summary>
    public SKColorType? SurfaceColorType { get; init; }

    /// <summary>Gets the Skia GPU platform provider if available.</summary>
    public ISkiaGpu? Gpu { get; init; }

    /// <summary>Gets the current GPU render session if available.</summary>
    public ISkiaGpuRenderSession? CurrentSession { get; init; }
}
