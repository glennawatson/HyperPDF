// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics.CodeAnalysis;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using PdfViewerLite.Skia.Rendering;
using SkiaSharp;

namespace PdfViewerLite.Skia.RenderTargets;

/// <summary>Renders Avalonia scenes directly to an in-memory framebuffer.</summary>
internal sealed class FramebufferRenderTarget : IRenderTarget
{
    /// <summary>The surface props.</summary>
    private static readonly SKSurfaceProperties SurfaceProps = new(SKPixelGeometry.RgbHorizontal);

    /// <summary>The use scaled drawing.</summary>
    private readonly bool _useScaledDrawing;

    /// <summary>The render target.</summary>
    private IFramebufferRenderTarget? _renderTarget;

    /// <summary>The current image info.</summary>
    private SKImageInfo _currentImageInfo;

    /// <summary>The current framebuffer address.</summary>
    private IntPtr _currentFramebufferAddress;

    /// <summary>The framebuffer surface.</summary>
    private SKSurface? _framebufferSurface;

    /// <summary>The current framebuffer row stride.</summary>
    private int _currentRowBytes;

    /// <summary>Initializes a new instance of the <see cref = "FramebufferRenderTarget"/> class.</summary>
    /// <param name = "platformSurface">The framebuffer platform surface.</param>
    /// <param name = "useScaledDrawing">Whether to scale drawing to the framebuffer DPI.</param>
    public FramebufferRenderTarget(IFramebufferPlatformSurface platformSurface, bool useScaledDrawing = false)
    {
        ArgumentNullException.ThrowIfNull(platformSurface);
        _useScaledDrawing = useScaledDrawing;
        _renderTarget = platformSurface.CreateFramebufferRenderTarget();
    }

    /// <inheritdoc/>
    public RenderTargetProperties Properties => new() { RetainsPreviousFrameContents = _renderTarget?.RetainsFrameContents == true, IsSuitableForDirectRendering = true, };

    /// <inheritdoc/>
    public PlatformRenderTargetState PlatformRenderTargetState => _renderTarget?.State ?? PlatformRenderTargetState.Disposed;

    /// <inheritdoc/>
    public IDrawingContextImpl CreateDrawingContext(IRenderTarget.RenderTargetSceneInfo sceneInfo, out RenderTargetDrawingContextProperties properties)
    {
        ObjectDisposedException.ThrowIf(_renderTarget is null, this);
        var framebuffer = _renderTarget.Lock(sceneInfo, out var lockProperties);
        var framebufferImageInfo = new SKImageInfo(framebuffer.Size.Width, framebuffer.Size.Height, framebuffer.Format.ToSkColorType(), framebuffer.AlphaFormat.ToSkAlphaType());
        CreateSurface(framebufferImageInfo, framebuffer);
        var canvas = _framebufferSurface.Canvas;
        canvas.RestoreToCount(-1);
        _ = canvas.Save();
        canvas.ResetMatrix();
        var createInfo = new DrawingContextCreateInfo { Surface = _framebufferSurface, Dpi = framebuffer.Dpi, ScaleDrawingToDpi = _useScaledDrawing, };
        properties = new() { PreviousFrameIsRetained = lockProperties.PreviousFrameIsRetained, };
        return new DrawingContextImpl(createInfo, framebuffer);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _renderTarget?.Dispose();
        _renderTarget = null;
        FreeSurface();
    }

    /// <summary>Checks whether the framebuffer surface can be reused.</summary>
    /// <param name = "a">The a.</param>
    /// <param name = "b">The b.</param>
    /// <returns>The requested result.</returns>
    private static bool AreImageInfosCompatible(SKImageInfo a, SKImageInfo b) => a.Width == b.Width && a.Height == b.Height && a.ColorType == b.ColorType && a.AlphaType == b.AlphaType;

    /// <summary>Creates a surface compatible with the target pixels.</summary>
    /// <param name = "desiredImageInfo">The desired image info.</param>
    /// <param name = "framebuffer">The framebuffer.</param>
    /// <exception cref="ArgumentException">Thrown when <c>desiredImageInfo.Width &lt;= 0 || desiredImageInfo.Height &lt;= 0</c>.</exception>
    [MemberNotNull(nameof(_framebufferSurface))]
    private void CreateSurface(SKImageInfo desiredImageInfo, ILockedFramebuffer framebuffer)
    {
        if (_framebufferSurface is not null && AreImageInfosCompatible(
            _currentImageInfo,
            desiredImageInfo) && _currentFramebufferAddress == framebuffer.Address && _currentRowBytes == framebuffer.RowBytes)
        {
            return;
        }

        FreeSurface();
        _currentFramebufferAddress = framebuffer.Address;
        if (desiredImageInfo.Width <= 0 || desiredImageInfo.Height <= 0)
        {
            throw new ArgumentException($"Unable to create a surface with size {desiredImageInfo.Width}x{desiredImageInfo.Height}.", nameof(desiredImageInfo));
        }

        _framebufferSurface = SKSurface.Create(desiredImageInfo, _currentFramebufferAddress, framebuffer.RowBytes, SurfaceProps);
        _currentImageInfo = desiredImageInfo;
        _currentRowBytes = framebuffer.RowBytes;
    }

    /// <summary>Releases the framebuffer surface and conversion buffer.</summary>
    private void FreeSurface()
    {
        _framebufferSurface?.Dispose();
        _framebufferSurface = null;
        _currentFramebufferAddress = IntPtr.Zero;
    }
}
