// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using HyperPdfLibrary.Rendering;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Rendering;
using PdfViewerLite.HyperPdf;
using PdfViewerLite.Skia.Gpu;
using SkiaSharp;

namespace PdfViewerLite.App.Rendering;

/// <summary>Holds a prepared PDF tile and, when available, its retained image on the active graphics context.</summary>
[DebuggerDisplay("GpuPreparedRenderSurface: {Width} x {Height} RefCount={_references}")]
internal sealed class GpuPreparedRenderSurface : IRenderPreparationSurface
{
    /// <summary>The bytes per premultiplied BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The output colour space shared by GPU tile surfaces.</summary>
    private static readonly SKColorSpace Srgb = SKColorSpace.CreateSrgb();

    /// <summary>Does nothing for isolated tiles used by tests.</summary>
    private static readonly Action IgnoreFailure = static () => { };

    /// <summary>Protects the graphics image and software fallback.</summary>
    private readonly Lock _gate = new();

    /// <summary>Graphics availability shared with tiles in the render hub.</summary>
    private readonly GpuAvailability _availability;

    /// <summary>Requests software tiles through the owning render hub.</summary>
    private readonly Action _recoverSoftware;

    /// <summary>The request whose recording this tile uses.</summary>
    private RenderRequest _request;

    /// <summary>The prepared HyperPDF renderer when GPU replay is possible.</summary>
    private IHyperPdfGpuRenderer? _renderer;

    /// <summary>The software image, allocated only when GPU replay cannot preserve the requested output.</summary>
    private AvaloniaRenderSurface? _software;

    /// <summary>The retained GPU image and its owning context.</summary>
    private GpuImageState? _image;

    /// <summary>One cache owner plus any draw operations that have retained this tile.</summary>
    private int _references = 1;

    /// <summary>One after the tile leaves the cache.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="GpuPreparedRenderSurface"/> class.</summary>
    /// <param name="width">The tile width.</param>
    /// <param name="height">The tile height.</param>
    /// <param name="availability">The render hub's graphics path status.</param>
    internal GpuPreparedRenderSurface(int width, int height, GpuAvailability availability)
        : this(width, height, availability, IgnoreFailure)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GpuPreparedRenderSurface"/> class.</summary>
    /// <param name="width">The tile width.</param>
    /// <param name="height">The tile height.</param>
    /// <param name="availability">The render hub's graphics path status.</param>
    /// <param name="recoverSoftware">Schedules UI cache recovery on graphics failure.</param>
    internal GpuPreparedRenderSurface(int width, int height, GpuAvailability availability, Action recoverSoftware)
    {
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(recoverSoftware);
        Width = width;
        Height = height;
        _availability = availability;
        _recoverSoftware = recoverSoftware;
    }

    /// <inheritdoc/>
    public int Width { get; }

    /// <inheritdoc/>
    public int Height { get; }

    /// <inheritdoc/>
    public long ByteSize => (long)Width * Height * BytesPerPixel;

    /// <summary>Gets the fallback bitmap after it has been rendered.</summary>
    internal Bitmap? SoftwareBitmap => Volatile.Read(ref _software)?.Bitmap;

    /// <inheritdoc/>
    public bool Prepare(in RenderRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _request = request;
        if (request.Document is IHyperPdfGpuRenderer renderer && !_availability.IsUnavailable && GpuPageTone.CanApply(request.Tone))
        {
            if (!renderer.Prepare(request.Info, cancellationToken))
            {
                return false;
            }

            _renderer = renderer;
            return !_availability.IsUnavailable || EnsureSoftwareBitmap();
        }

        return EnsureSoftwareBitmap();
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Write<TState>(in TState state, SurfaceWriter<TState> writer) => false;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            Release();
        }
    }

    /// <summary>Routes later tiles to software after a graphics failure.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void MarkGpuUnavailable()
    {
        _availability.MarkUnavailable();
        _recoverSoftware();
    }

    /// <summary>Retains the tile while a queued composition operation may use it.</summary>
    /// <returns>Whether it was retained before the cache released it.</returns>
    internal bool TryRetain()
    {
        while (true)
        {
            var references = Volatile.Read(ref _references);
            if (references == 0 || Volatile.Read(ref _disposed) != 0)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _references, references + 1, references) == references)
            {
                return true;
            }
        }
    }

    /// <summary>Protects an active render even if its queued operation is released concurrently.</summary>
    /// <returns>Whether the tile still has a live owner.</returns>
    internal bool TryRetainForRender()
    {
        while (true)
        {
            var references = Volatile.Read(ref _references);
            if (references == 0)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _references, references + 1, references) == references)
            {
                return true;
            }
        }
    }

    /// <summary>Draws the retained image, creating it from the recording on this context's thread when needed.</summary>
    /// <param name="canvas">The active compositor canvas.</param>
    /// <param name="context">The graphics context shared with the compositor.</param>
    /// <param name="colorType">The compositor surface's channel order.</param>
    /// <param name="destination">The tile bounds in compositor coordinates.</param>
    /// <param name="sampling">The image sampling mode.</param>
    /// <returns>Whether the GPU image was drawn.</returns>
    internal bool DrawGpu(SKCanvas canvas, GRContext context, SKColorType colorType, SKRect destination, SKSamplingOptions sampling)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(context);
        if (_renderer is null || Volatile.Read(ref _references) == 0 || Volatile.Read(ref _software) is not null || _availability.IsUnavailable)
        {
            return false;
        }

        var state = GetOrCreateImage(context, colorType);
        if (state is null)
        {
            return false;
        }

        canvas.DrawImage(state.Image, new(0, 0, Width, Height), destination, sampling);
        TileRenderingEventSource.Log.GpuPresented(Width, Height);
        return true;
    }

    /// <summary>Creates a software tile after a missing or lost graphics context.</summary>
    /// <returns>Whether software pixels are ready.</returns>
    internal bool EnsureSoftwareBitmap()
    {
        lock (_gate)
        {
            if (_software is not null)
            {
                return true;
            }

            if (Volatile.Read(ref _references) == 0)
            {
                return false;
            }

            if (_image is { } gpuImage)
            {
                SkiaGpuImageRetirement.Enqueue(gpuImage.Context, gpuImage.Image);
                _image = null;
            }

            var software = new AvaloniaRenderSurface(Width, Height);
            try
            {
                if (!software.Write(_request, RenderSoftware))
                {
                    software.Dispose();
                    return false;
                }

                _software = software;
                TileRenderingEventSource.Log.SoftwarePrepared(Width, Height);
                return true;
            }
            catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
            {
                software.Dispose();
                return false;
            }
        }
    }

    /// <summary>Releases an operation's reference after the compositor finishes with it.</summary>
    internal void Release()
    {
        if (Interlocked.Decrement(ref _references) != 0)
        {
            return;
        }

        lock (_gate)
        {
            _software?.Dispose();
            _software = null;
            if (_image is { } image)
            {
                SkiaGpuImageRetirement.Enqueue(image.Context, image.Image);
                _image = null;
            }
        }
    }

    /// <summary>Renders with the same tone rule as the CPU scheduler path.</summary>
    /// <param name="target">The locked bitmap.</param>
    /// <param name="request">The prepared request.</param>
    /// <returns>Whether the document rendered.</returns>
    private static bool RenderSoftware(RenderTarget target, in RenderRequest request)
    {
        if (!request.Document.Render(request.Info, target))
        {
            return false;
        }

        request.Tone.Apply(target);
        return true;
    }

    /// <summary>Creates a texture-backed surface when the graphics context supports the layout.</summary>
    /// <param name="context">The compositor's graphics context.</param>
    /// <param name="info">The requested pixel layout.</param>
    /// <returns>The owned surface or null.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static SKSurface? TryCreateSurface(GRContext context, SKImageInfo info)
    {
        try
        {
            return SKSurface.Create(context, false, info);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            return null;
        }
    }

    /// <summary>Chooses the compositor's channel order, then the other common GPU layout.</summary>
    /// <param name="context">The shared graphics context.</param>
    /// <param name="info">The preferred layout, updated when its fallback succeeds.</param>
    /// <returns>A supported surface or null.</returns>
    private static SKSurface? TryCreateCompatibleSurface(GRContext context, ref SKImageInfo info)
    {
        var surface = TryCreateSurface(context, info);
        if (surface is not null)
        {
            return surface;
        }

        var alternate = info.ColorType == SKColorType.Bgra8888 ? SKColorType.Rgba8888 : SKColorType.Bgra8888;
        info = new(info.Width, info.Height, alternate, SKAlphaType.Premul, info.ColorSpace);
        return TryCreateSurface(context, info);
    }

    /// <summary>Gets an image retained for this context, or replays the recording into a new GPU surface.</summary>
    /// <param name="context">The context that owns the image.</param>
    /// <param name="colorType">The compositor surface's channel order.</param>
    /// <returns>The retained image, or null when the context cannot create a target.</returns>
    private GpuImageState? GetOrCreateImage(GRContext context, SKColorType colorType)
    {
        if (colorType is not (SKColorType.Bgra8888 or SKColorType.Rgba8888))
        {
            colorType = SKColorType.Bgra8888;
        }

        lock (_gate)
        {
            if (_image is { } existing && ReferenceEquals(existing.Context, context) && existing.PresentationColorType == colorType)
            {
                return existing;
            }

            if (_image is { } oldImage)
            {
                SkiaGpuImageRetirement.Enqueue(oldImage.Context, oldImage.Image);
                _image = null;
            }

            var presentationColorType = colorType;
            var info = new SKImageInfo(Width, Height, colorType, SKAlphaType.Premul, Srgb);
            var surface = TryCreateCompatibleSurface(context, ref info);
            if (surface is null)
            {
                MarkGpuUnavailable();
                return null;
            }

            using SkiaSurfaceRenderTarget target = new(surface, info);
            if (!RenderIntoTarget(target))
            {
                return null;
            }

            var image = CreateFinalImage(target, info, context);
            if (image is null)
            {
                MarkGpuUnavailable();
                return null;
            }

            _image = new(context, presentationColorType, image);
            TileRenderingEventSource.Log.GpuSnapshot(Width, Height);
            return _image;
        }
    }

    /// <summary>Replays the prepared page unless its document has closed.</summary>
    /// <param name="target">The GPU page surface.</param>
    /// <returns>Whether the page rendered.</returns>
    private bool RenderIntoTarget(SkiaSurfaceRenderTarget target)
    {
        try
        {
            return _renderer?.Render(_request.Info, target) == true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>Snapshots the page and applies its tone on the GPU before any scaled presentation.</summary>
    /// <param name="target">The rendered page surface.</param>
    /// <param name="info">The surface layout.</param>
    /// <param name="context">The owning graphics context.</param>
    /// <returns>An owned display image, or null after graphics failure.</returns>
    private SKImage? CreateFinalImage(SkiaSurfaceRenderTarget target, SKImageInfo info, GRContext context)
    {
        try
        {
            var image = target.Snapshot();
            if (_request.Tone.IsIdentity)
            {
                return image;
            }

            using (image)
            {
                return GpuPageTone.TryApply(image, info, _request.Tone, context);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            return null;
        }
    }

    /// <summary>Pairs one retained image with the context that owns its GPU resources.</summary>
    /// <param name="Context">The owning context.</param>
    /// <param name="PresentationColorType">The compositor's channel order when the image was made.</param>
    /// <param name="Image">The image.</param>
    private sealed record GpuImageState(GRContext Context, SKColorType PresentationColorType, SKImage Image);
}
