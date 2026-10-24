// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Render.Skia;

namespace HyperPdfLibrary.Tests.Drawing;

/// <summary>Checks that the Skia backend owns copied pixel storage and validates image input.</summary>
public sealed class SkiaRenderImageTests
{
    /// <summary>The image width in pixels.</summary>
    private const int Width = 2;

    /// <summary>The image height in pixels.</summary>
    private const int Height = 2;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The bytes in one visible pixel row.</summary>
    private const int RowBytes = Width * BytesPerPixel;

    /// <summary>The source row padding.</summary>
    private const int SourcePadding = 3;

    /// <summary>The destination row padding.</summary>
    private const int DestinationPadding = 5;

    /// <summary>The source stride.</summary>
    private const int SourceStride = RowBytes + SourcePadding;

    /// <summary>The destination stride.</summary>
    private const int DestinationStride = RowBytes + DestinationPadding;

    /// <summary>The byte count needed for the visible source rows and their leading stride.</summary>
    private const int SourceLength = ((Height - 1) * SourceStride) + RowBytes;

    /// <summary>The byte count needed for the visible destination rows and their leading stride.</summary>
    private const int DestinationLength = ((Height - 1) * DestinationStride) + RowBytes;

    /// <summary>The byte written into destination padding before copying.</summary>
    private const byte DestinationPaddingByte = 0xA5;

    /// <summary>The value written to the source after the backend creates its image.</summary>
    private const byte MutatedSourceByte = 0xEE;

    /// <summary>An unsupported pixel-format value.</summary>
    private const int UnknownFormat = 3;

    /// <summary>Gets the premultiplied source pixels, including source row padding.</summary>
    private static ReadOnlySpan<byte> SourcePixels =>
    [
        0x08, 0x10, 0x18, 0x20, 0x00, 0x00, 0xFF, 0xFF, 0xCC, 0xCC, 0xCC,
        0x04, 0x08, 0x0C, 0x10, 0x00, 0x00, 0x00, 0x00,
    ];

    /// <summary>Gets the pixel format used by the copy test.</summary>
    private static PdfImagePixelLayout Layout => new(Width, Height, PdfImagePixelFormat.Bgra8888);

    /// <summary>Copies premultiplied source pixels before retaining them, preserving row padding and pixel alpha.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CreateImageCopiesStackAllocatedPixels()
    {
        using var image = CreateImageFromMutatedSource();
        var destination = new byte[DestinationLength];
        destination.AsSpan().Fill(DestinationPaddingByte);
        var copied = image.CopyPixels(Layout, destination, DestinationStride);
        var firstRowMatches = destination.AsSpan(0, RowBytes).SequenceEqual(SourcePixels[..RowBytes]);
        var secondRowMatches = destination.AsSpan(DestinationStride, RowBytes).SequenceEqual(SourcePixels.Slice(SourceStride, RowBytes));
        var rowPaddingIsUntouched = destination.AsSpan(RowBytes, DestinationPadding).IndexOfAnyExcept(DestinationPaddingByte) < 0;

        using (Assert.Multiple())
        {
            await Assert.That(copied).IsTrue();
            await Assert.That(firstRowMatches).IsTrue();
            await Assert.That(secondRowMatches).IsTrue();
            await Assert.That(rowPaddingIsUntouched).IsTrue();
        }
    }

    /// <summary>Rejects layouts that cannot be copied into the supplied span.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CreateImageRejectsInvalidLayoutsAndStorage()
    {
        var backend = new SkiaRenderBackend();

        using (Assert.Multiple())
        {
            await Assert.That(() => backend.CreateImage(new(0, Height, PdfImagePixelFormat.Bgra8888), new byte[1], RowBytes)).Throws<ArgumentOutOfRangeException>();
            await Assert.That(() => backend.CreateImage(new(Width, 0, PdfImagePixelFormat.Bgra8888), new byte[1], RowBytes)).Throws<ArgumentOutOfRangeException>();
            await Assert.That(() => backend.CreateImage(Layout, new byte[1], RowBytes)).Throws<ArgumentOutOfRangeException>();
            await Assert.That(() => backend.CreateImage(Layout, new byte[SourceLength], RowBytes - 1)).Throws<ArgumentOutOfRangeException>();
            await Assert.That(() => backend.CreateImage(new(int.MaxValue, Height, PdfImagePixelFormat.Bgra8888), ReadOnlySpan<byte>.Empty, int.MaxValue)).Throws<ArgumentOutOfRangeException>();
            await Assert.That(() => backend.CreateImage(new(Width, Height, (PdfImagePixelFormat)UnknownFormat), new byte[1], RowBytes)).Throws<ArgumentOutOfRangeException>();
        }
    }

    /// <summary>Creates an image from stack memory and overwrites the source before returning it.</summary>
    /// <returns>The owned image.</returns>
    private static IPdfRenderImage CreateImageFromMutatedSource()
    {
        Span<byte> source = stackalloc byte[SourceLength];
        SourcePixels.CopyTo(source);
        var image = new SkiaRenderBackend().CreateImage(Layout, source, SourceStride);
        source.Fill(MutatedSourceByte);
        return image;
    }
}
