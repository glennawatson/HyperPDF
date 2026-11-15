// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Render.Managed.Images;

/// <summary>A decoded image held as plain bytes, ready to be sampled when a picture is drawn.</summary>
[DebuggerDisplay("ManagedRenderImage: {Width}x{Height} {Kind}")]
internal sealed class ManagedRenderImage : IPdfRenderImage
{
    /// <summary>The bytes of a colour pixel.</summary>
    internal const int ColorBytes = 4;

    /// <summary>The alpha of an opaque pixel, in its place in the pixel.</summary>
    private const uint Opaque = 0xFF000000;

    /// <summary>The factor that copies a gray byte into the blue, green and red bytes of a pixel.</summary>
    private const uint GrayToColor = 0x00010101;

    /// <summary>The weight of red in a gray made from a colour, out of 256.</summary>
    private const uint RedWeight = 54;

    /// <summary>The weight of green in a gray made from a colour, out of 256.</summary>
    private const uint GreenWeight = 183;

    /// <summary>The weight of blue in a gray made from a colour, out of 256.</summary>
    private const uint BlueWeight = 19;

    /// <summary>The shift of the red byte of a pixel.</summary>
    private const int RedShift = 16;

    /// <summary>The bits of one byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The mask of one byte.</summary>
    private const uint ByteMask = 0xFF;

    /// <summary>The number the next image is known by.</summary>
    private static uint _nextId;

    /// <summary>Initializes a new instance of the <see cref="ManagedRenderImage"/> class.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="kind">What the bytes stand for.</param>
    /// <param name="pixels">The bytes, which the image keeps.</param>
    /// <param name="rowBytes">The bytes from one line to the next.</param>
    internal ManagedRenderImage(int width, int height, ImagePixelKind kind, byte[] pixels, int rowBytes)
    {
        Width = width;
        Height = height;
        Kind = kind;
        Pixels = pixels;
        RowBytes = rowBytes;
        ImageId = Interlocked.Increment(ref _nextId);
    }

    /// <inheritdoc/>
    public int Width { get; }

    /// <inheritdoc/>
    public int Height { get; }

    /// <inheritdoc/>
    public uint ImageId { get; }

    /// <inheritdoc/>
    public long PixelBytes => (long)RowBytes * Height;

    /// <summary>Gets what the bytes stand for.</summary>
    internal ImagePixelKind Kind { get; }

    /// <summary>Gets the bytes.</summary>
    internal byte[] Pixels { get; }

    /// <summary>Gets the bytes from one line to the next.</summary>
    internal int RowBytes { get; }

    /// <inheritdoc/>
    public bool CopyPixels(PdfImagePixelLayout layout, Span<byte> pixels, int rowBytes)
    {
        var bytes = layout.Format == PdfImagePixelFormat.Gray8 ? 1 : ColorBytes;
        var visible = (long)layout.Width * bytes;
        if (layout.Width <= 0 || layout.Height <= 0 || rowBytes < visible || ((long)(layout.Height - 1) * rowBytes) + visible > pixels.Length)
        {
            return false;
        }

        for (var y = 0; y < layout.Height; y++)
        {
            // The nearest source line and pixel stand in when the caller asks for another size.
            var sourceY = (int)((long)y * Height / layout.Height);
            var line = pixels.Slice(y * rowBytes, (int)visible);
            for (var x = 0; x < layout.Width; x++)
            {
                Write(line[(x * bytes)..], layout.Format, Read((int)((long)x * Width / layout.Width), sourceY));
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // The bytes are managed memory, which the collector frees.
    }

    /// <summary>Reads one pixel as a premultiplied colour. A stencil mask reads as white with the mask's coverage as alpha.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The line.</param>
    /// <returns>The pixel, with blue in the lowest byte.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal uint Read(int x, int y)
    {
        ref var first = ref MemoryMarshal.GetArrayDataReference(Pixels);
        if (Kind == ImagePixelKind.Color)
        {
            return Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref first, (y * RowBytes) + (x * ColorBytes)));
        }

        var value = (uint)Unsafe.Add(ref first, (y * RowBytes) + x);
        return Kind == ImagePixelKind.Gray ? Opaque | (value * GrayToColor) : value * (GrayToColor | (1U << (RedShift + ByteBits)));
    }

    /// <summary>Writes one pixel in the layout a caller asked for.</summary>
    /// <param name="output">Where the pixel goes.</param>
    /// <param name="format">The layout.</param>
    /// <param name="pixel">The premultiplied pixel, with blue in the lowest byte.</param>
    private static void Write(Span<byte> output, PdfImagePixelFormat format, uint pixel)
    {
        var blue = pixel & ByteMask;
        var green = (pixel >> ByteBits) & ByteMask;
        var red = (pixel >> RedShift) & ByteMask;
        if (format == PdfImagePixelFormat.Gray8)
        {
            output[0] = (byte)(((red * RedWeight) + (green * GreenWeight) + (blue * BlueWeight)) >> ByteBits);
        }
        else if (format == PdfImagePixelFormat.Bgra8888)
        {
            MemoryMarshal.Write(output, in pixel);
        }
        else
        {
            // Red first, with the last byte unused.
            var swapped = red | (green << ByteBits) | (blue << RedShift) | Opaque;
            MemoryMarshal.Write(output, in swapped);
        }
    }
}
