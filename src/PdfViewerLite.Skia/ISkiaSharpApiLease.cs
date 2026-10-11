// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using SkiaSharp;

namespace PdfViewerLite.Skia;

/// <summary>Represents an active lease of SkiaSharp drawing surfaces.</summary>
public interface ISkiaSharpApiLease : IDisposable
{
    /// <summary>Gets the leased Skia canvas.</summary>
    SKCanvas SkCanvas { get; }

    /// <summary>Gets the GPU context if available.</summary>
    GRContext? GrContext { get; }

    /// <summary>Gets the compositor surface's channel order for compatible GPU tile storage.</summary>
    SKColorType SurfaceColorType { get; }

    /// <summary>Gets the leased Skia surface if available.</summary>
    SKSurface? SkSurface { get; }

    /// <summary>Gets the current drawing opacity.</summary>
    double CurrentOpacity { get; }

    /// <summary>Attempts to lease the underlying platform graphics API.</summary>
    /// <returns>The platform graphics API lease or null.</returns>
    ISkiaSharpPlatformGraphicsApiLease? TryLeasePlatformGraphicsApi();
}
