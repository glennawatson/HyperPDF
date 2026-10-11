// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using SkiaSharp;

namespace PdfViewerLite.Skia.Gpu;

/// <summary>Represents an active Skia GPU render session.</summary>
internal interface ISkiaGpuRenderSession : IDisposable
{
    /// <summary>Gets the GrContext used by this session.</summary>
    GRContext GrContext { get; }

    /// <summary>Gets the Skia surface being rendered to.</summary>
    SKSurface SkSurface { get; }

    /// <summary>Gets the compositor surface's channel order.</summary>
    SKColorType ColorType { get; }

    /// <summary>Gets the scaling factor for this session.</summary>
    double ScaleFactor { get; }

    /// <summary>Gets the surface origin (top-left or bottom-left).</summary>
    GRSurfaceOrigin SurfaceOrigin { get; }
}
