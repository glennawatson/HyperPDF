// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Render.Managed.Images;
using HyperPdfLibrary.Render.Managed.Pictures;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Render.Managed;

/// <summary>
/// Drawing services written wholly in managed code: pages are recorded as pictures and rasterised on the CPU, with no native library.
/// </summary>
[DebuggerDisplay("ManagedRenderBackend")]
public sealed class ManagedRenderBackend : IPdfRenderBackend
{
    /// <summary>The drawing session of each thread that draws. A session dies with its thread.</summary>
    private readonly ConditionalWeakTable<Thread, ManagedDrawingSession> _sessions = [];

    /// <summary>Gets a value indicating whether a picture is recorded for one page matrix. It is not: text and lines are fitted to pixels when a picture is drawn.</summary>
    public bool RecordsForDevice => false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPdfDrawingSession GetDrawingSession() => _sessions.GetValue(Thread.CurrentThread, static _ => new());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPictureDevice CreatePictureDevice(PdfRect bounds) => new ManagedContentDevice(null, PdfRenderQuality.Default);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPictureDevice CreatePictureDevice(PdfRect bounds, in PdfPictureStyle style) => new ManagedContentDevice(null, style.Quality);

    /// <inheritdoc/>
    public IPdfRenderImage? CreateImage(PdfImageData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Width <= 0 || data.Height <= 0 || data.Pixels.Length == 0)
        {
            return null;
        }

        var kind = KindOf(data);
        var rowBytes = kind == ImagePixelKind.Color ? data.Width * ManagedRenderImage.ColorBytes : data.Width;

        // Pinned pixels stay where they are for the life of the image data, so they are used in place. Others are copied.
        return new ManagedRenderImage(data.Width, data.Height, kind, data.IsPinned ? data.Pixels : [.. data.Pixels], rowBytes);
    }

    /// <inheritdoc/>
    public IPdfRenderImage CreateImage(PdfImagePixelLayout layout, ReadOnlySpan<byte> pixels, int rowBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(layout.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(layout.Height);
        var bytes = layout.Format == PdfImagePixelFormat.Gray8 ? 1 : ManagedRenderImage.ColorBytes;
        var visible = layout.Width * bytes;
        ArgumentOutOfRangeException.ThrowIfLessThan(rowBytes, visible);
        ArgumentOutOfRangeException.ThrowIfLessThan(pixels.Length, ((long)(layout.Height - 1) * rowBytes) + visible);
        var copy = new byte[visible * layout.Height];
        for (var y = 0; y < layout.Height; y++)
        {
            var line = copy.AsSpan(y * visible, visible);
            pixels.Slice(y * rowBytes, visible).CopyTo(line);
            if (layout.Format == PdfImagePixelFormat.Rgb888x)
            {
                ToBgra(line);
            }
        }

        return new ManagedRenderImage(layout.Width, layout.Height, layout.Format == PdfImagePixelFormat.Gray8 ? ImagePixelKind.Gray : ImagePixelKind.Color, copy, visible);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPdfRenderShader CreateLinearGradient(PdfPoint start, PdfPoint end, ReadOnlySpan<PdfColor> colors, ReadOnlySpan<float> positions, PdfShaderTileMode mode) =>
        new ManagedRenderShader(start, -1F, end, -1F, [.. colors], [.. positions], mode);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPdfRenderShader CreateTwoPointConicalGradient(
        PdfPoint start,
        float startRadius,
        PdfPoint end,
        float endRadius,
        ReadOnlySpan<PdfColor> colors,
        ReadOnlySpan<float> positions,
        PdfShaderTileMode mode) =>
        new ManagedRenderShader(start, startRadius, end, endRadius, [.. colors], [.. positions], mode);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPdfRenderShader CreateImageShader(IPdfRenderImage image, PdfShaderTileMode x, PdfShaderTileMode y, bool linear, Matrix3x2 matrix) =>
        new ManagedRenderShader(default, -1F, default, -1F, [], [], x) { Image = image, ImageMatrix = matrix };

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPdfRenderVertices CreateVertices(PdfPoint[] positions, PdfColor[] colors) => new ManagedRenderVertices(positions, null, colors);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPdfRenderVertices CreateVertices(PdfPoint[] positions, PdfPoint[] texture, PdfColor[]? colors) => new ManagedRenderVertices(positions, texture, colors);

    /// <inheritdoc/>
    public PatternCell ComposeTile(IPdfRenderPicture cell, PdfRectangle box, float stepX, float stepY, float scale)
    {
        // One period starts at the cell's corner and is one step wide and high. Cells larger than the step reach into it from its neighbours.
        var tile = new PdfRect(box.Left, box.Bottom, box.Left + MathF.Abs(stepX), box.Bottom + MathF.Abs(stepY));
        return new(cell is ManagedPicture picture ? TilePeriod.Compose(picture, box, tile) : cell, null, tile, Matrix3x2.Identity);
    }

    /// <summary>Finds what the bytes of decoded image data stand for.</summary>
    /// <param name="data">The decoded image.</param>
    /// <returns>The kind of pixel.</returns>
    private static ImagePixelKind KindOf(PdfImageData data)
    {
        if (data.IsStencilMask)
        {
            return ImagePixelKind.Coverage;
        }

        return data.IsGray ? ImagePixelKind.Gray : ImagePixelKind.Color;
    }

    /// <summary>Swaps the red and blue bytes of a line of pixels and makes them opaque.</summary>
    /// <param name="line">The line, four bytes a pixel with red first.</param>
    private static void ToBgra(Span<byte> line)
    {
        for (var i = 0; i + ManagedRenderImage.ColorBytes <= line.Length; i += ManagedRenderImage.ColorBytes)
        {
            var red = line[i];
            line[i] = line[i + 1 + 1];
            line[i + 1 + 1] = red;
            line[i + ManagedRenderImage.ColorBytes - 1] = byte.MaxValue;
        }
    }
}
