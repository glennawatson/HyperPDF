// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using SkiaSharp;

namespace HyperPdfLibrary.Render.Skia;

/// <summary>Creates Skia drawing resources behind the managed PDF drawing contracts.</summary>
[DebuggerDisplay("Skia drawing backend")]
public sealed class SkiaRenderBackend : IPdfRenderBackend
{
    /// <summary>The reusable drawing session for each thread that uses this backend.</summary>
    private readonly ConditionalWeakTable<Thread, SkiaDrawingSession> _sessions = new();

    /// <summary>Gets the current thread's session within this registered backend.</summary>
    private SkiaDrawingSession Session => _sessions.GetValue(Thread.CurrentThread, static _ => new());

    /// <summary>Gets the recording pool, creating it when the registered backend first records a picture.</summary>
    private ReusableSlots<SkiaContentDevice> RecordingDevices
    {
        get
        {
            if (Volatile.Read(ref field) is { } existing)
            {
                return existing;
            }

            var created = new ReusableSlots<SkiaContentDevice>();
            return Interlocked.CompareExchange(ref field, created, null) ?? created;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPdfDrawingSession GetDrawingSession() => Session;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPictureDevice CreatePictureDevice(PdfRect bounds) => SkiaContentDevice.Rent(RecordingDevices, SkiaConversions.ToSkRect(bounds), null);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPictureDevice CreatePictureDevice(PdfRect bounds, in PdfPictureStyle style) => SkiaContentDevice.Rent(RecordingDevices, SkiaConversions.ToSkRect(bounds), null, style);

    /// <inheritdoc/>
    public IPdfRenderImage? CreateImage(PdfImageData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Width <= 0 || data.Height <= 0 || data.Pixels.Length == 0)
        {
            return null;
        }

        var info = ImageInfo(data);
        var rowBytes = data.Width * info.BytesPerPixel;
        if (!data.IsPinned)
        {
            return new SkiaRenderImage(SKImage.FromPixelCopy(info, data.Pixels, rowBytes));
        }

        // The decoder allocated this array on the pinned object heap. The native release context retains its lifetime.
        using var pixmap = new SKPixmap(info, Marshal.UnsafeAddrOfPinnedArrayElement(data.Pixels, 0), rowBytes);
        var native = SKImage.FromPixels(pixmap, ReleasePixels, data.Pixels);
        return native is null ? null : new SkiaRenderImage(native);
    }

    /// <inheritdoc/>
    public unsafe IPdfRenderImage CreateImage(PdfImagePixelLayout layout, ReadOnlySpan<byte> pixels, int rowBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(layout.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(layout.Height);
        var info = ImageInfo(layout);
        var visibleRow = (long)layout.Width * info.BytesPerPixel;
        ArgumentOutOfRangeException.ThrowIfLessThan(rowBytes, visibleRow);
        var required = ((long)(layout.Height - 1) * rowBytes) + visibleRow;
        ArgumentOutOfRangeException.ThrowIfLessThan(pixels.Length, required);
        if (rowBytes % info.BytesPerPixel != 0)
        {
            return CopyUnalignedRows(info, pixels, rowBytes, (int)visibleRow);
        }

        fixed (byte* pointer = pixels)
        {
            return new SkiaRenderImage(SKImage.FromPixelCopy(info, (nint)pointer, rowBytes));
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPdfRenderShader CreateLinearGradient(PdfPoint start, PdfPoint end, ReadOnlySpan<PdfColor> colors, ReadOnlySpan<float> positions, PdfShaderTileMode mode)
    {
        var stops = Session.GradientStops;
        return new SkiaRenderShader(SKShader.CreateLinearGradient(new(start.X, start.Y), new(end.X, end.Y), stops.Colors(colors), stops.Positions(positions), TileMode(mode)));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPdfRenderShader CreateTwoPointConicalGradient(
        PdfPoint start,
        float startRadius,
        PdfPoint end,
        float endRadius,
        ReadOnlySpan<PdfColor> colors,
        ReadOnlySpan<float> positions,
        PdfShaderTileMode mode)
    {
        var stops = Session.GradientStops;
        return new SkiaRenderShader(SKShader.CreateTwoPointConicalGradient(
            new(start.X, start.Y),
            startRadius,
            new(end.X, end.Y),
            endRadius,
            stops.Colors(colors),
            stops.Positions(positions),
            TileMode(mode)));
    }

    /// <inheritdoc/>
    public IPdfRenderShader CreateImageShader(IPdfRenderImage image, PdfShaderTileMode x, PdfShaderTileMode y, bool linear, Matrix3x2 matrix)
    {
        var sampling = linear ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None) : new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
        return new SkiaRenderShader(SkiaResources.Image(image).ToShader(TileMode(x), TileMode(y), sampling, SkiaConversions.ToSkMatrix(matrix)));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPdfRenderVertices CreateVertices(PdfPoint[] positions, PdfColor[] colors)
    {
        var arrays = Session.VertexArrays;
        return new SkiaRenderVertices(SKVertices.CreateCopy(SKVertexMode.Triangles, arrays.Positions(positions), arrays.Colors(colors)));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPdfRenderVertices CreateVertices(PdfPoint[] positions, PdfPoint[] texture, PdfColor[]? colors)
    {
        var arrays = Session.VertexArrays;
        return new SkiaRenderVertices(SKVertices.CreateCopy(
            SKVertexMode.Triangles,
            arrays.Positions(positions),
            arrays.Texture(texture),
            colors is null ? null : arrays.Colors(colors)));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
public PatternCell ComposeTile(IPdfRenderPicture cell, PdfRectangle box, float stepX, float stepY, float scale) =>
        Session.TileComposer.Compose(SkiaResources.Picture(cell), box, stepX, stepY, scale);

    /// <summary>Converts a managed raster layout.</summary>
    /// <param name="layout">The managed pixel layout.</param>
    /// <returns>The native pixel layout.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The pixel format is not supported.</exception>
    internal static SKImageInfo ImageInfo(PdfImagePixelLayout layout) => layout.Format switch
    {
        PdfImagePixelFormat.Gray8 => new(layout.Width, layout.Height, SKColorType.Gray8, SKAlphaType.Opaque),
        PdfImagePixelFormat.Rgb888x => new(layout.Width, layout.Height, SKColorType.Rgb888x, SKAlphaType.Opaque),
        PdfImagePixelFormat.Bgra8888 => new(layout.Width, layout.Height, SKColorType.Bgra8888, SKAlphaType.Premul),
        _ => throw new ArgumentOutOfRangeException(nameof(layout)),
    };

    /// <summary>Packs valid byte-strided rows before calling the native aligned-stride copy.</summary>
    /// <param name="info">The native pixel layout.</param>
    /// <param name="pixels">The borrowed source rows.</param>
    /// <param name="rowBytes">The source stride.</param>
    /// <param name="visibleRow">The visible bytes in a row.</param>
    /// <returns>The independently owned image.</returns>
    private static unsafe SkiaRenderImage CopyUnalignedRows(SKImageInfo info, ReadOnlySpan<byte> pixels, int rowBytes, int visibleRow)
    {
        var packed = ArrayPool<byte>.Shared.Rent(visibleRow * info.Height);
        try
        {
            for (var row = 0; row < info.Height; row++)
            {
                pixels.Slice(row * rowBytes, visibleRow).CopyTo(packed.AsSpan(row * visibleRow, visibleRow));
            }

            fixed (byte* pointer = packed)
            {
                return new(SKImage.FromPixelCopy(info, (nint)pointer, visibleRow));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(packed);
        }
    }

    /// <summary>Describes decoder output as coverage, gray or premultiplied color pixels.</summary>
    /// <param name="data">The decoded pixels.</param>
    /// <returns>The native pixel layout.</returns>
    private static SKImageInfo ImageInfo(PdfImageData data) => data switch
    {
        { IsStencilMask: true } => new(data.Width, data.Height, SKColorType.Alpha8, SKAlphaType.Premul),
        { IsGray: true } => new(data.Width, data.Height, SKColorType.Gray8, SKAlphaType.Opaque),
        _ => new(data.Width, data.Height, SKColorType.Bgra8888, SKAlphaType.Premul),
    };

    /// <summary>Converts shader edge behavior.</summary>
    /// <param name="mode">The managed edge behavior.</param>
    /// <returns>The native edge behavior.</returns>
    private static SKShaderTileMode TileMode(PdfShaderTileMode mode) => mode switch
    {
        PdfShaderTileMode.Repeat => SKShaderTileMode.Repeat,
        PdfShaderTileMode.Mirror => SKShaderTileMode.Mirror,
        PdfShaderTileMode.Decal => SKShaderTileMode.Decal,
        _ => SKShaderTileMode.Clamp,
    };

    /// <summary>Lets Skia release its last reference to the pinned decoder array.</summary>
    /// <param name="address">The borrowed pixel address.</param>
    /// <param name="context">The retained managed pixel array.</param>
    private static void ReleasePixels(nint address, object context)
    {
        // Skia drops the managed release context after this callback; the pinned object heap owns the memory.
    }
}
