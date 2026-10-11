// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>An image XObject (<c>Do</c>) or an inline image (<c>BI</c>), painted into the unit square.</summary>
[DebuggerDisplay("PdfImageObject: {Width}x{Height}, inline {IsInline}")]
public sealed class PdfImageObject : PdfPageObject
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBitCount = 8;

    /// <summary>The highest bit of a byte, the first sample of a row byte.</summary>
    private const int MostSignificantBit = 0x80;

    /// <summary>The coverage at or above which a stencil pixel paints.</summary>
    private const byte StencilThreshold = 128;

    /// <summary>The replacement image stream, or null while the image is as the content wrote it.</summary>
    private PdfStream? _replacement;

    /// <summary>Initializes a new instance of the <see cref="PdfImageObject"/> class.</summary>
    internal PdfImageObject()
    {
    }

    /// <inheritdoc/>
    public override PdfPageObjectKind Kind => PdfPageObjectKind.Image;

    /// <summary>Gets the image's name in the resources' /XObject; none for an inline image.</summary>
    public PdfName Name { get; internal init; }

    /// <summary>Gets a value indicating whether the image is written inline in the content stream.</summary>
    public bool IsInline { get; internal init; }

    /// <summary>Gets the image stream; <see langword="null"/> for an inline image.</summary>
    public PdfStream? Stream { get; internal init; }

    /// <summary>Gets the image dictionary: the XObject's, or the inline image's entries (with their abbreviated keys).</summary>
    public PdfDictionary Dictionary { get; internal init; } = null!;

    /// <summary>Gets the width in pixels.</summary>
    public int Width { get; internal init; }

    /// <summary>Gets the height in pixels.</summary>
    public int Height { get; internal init; }

    /// <summary>Gets the bits per colour component, 1 to 16; 1 for a stencil mask.</summary>
    public int BitsPerComponent { get; internal init; }

    /// <summary>Gets a value indicating whether the image is a stencil mask painted with the fill colour.</summary>
    public bool IsMask { get; internal init; }

    /// <summary>Gets the filters applied to the data, in order, with inline abbreviations spelled out.</summary>
    public IReadOnlyList<PdfName> Filters { get; internal init; } = [];

    /// <summary>Gets the colour space value as written: a name or an array; null for a stencil mask.</summary>
    public PdfValue ColorSpace { get; internal init; }

    /// <inheritdoc/>
    public override bool IsModified => base.IsModified || _replacement is not null;

    /// <summary>Gets the inline image's data between <c>ID</c> and <c>EI</c>; empty for an XObject.</summary>
    internal byte[] InlineData { get; init; } = [];

    /// <summary>Gets the replacement image stream, or null.</summary>
    internal PdfStream? Replacement => _replacement;

    /// <summary>Copies the data as stored in the file, before any filter is undone.</summary>
    /// <returns>The raw bytes.</returns>
    public byte[] GetRawData() => Stream is { } stream ? stream.CopyRawData() : InlineData.AsSpan().ToArray();

    /// <summary>Decodes the data through its byte filters. An image codec such as DCT or JPX is left applied.</summary>
    /// <returns>The decoded bytes.</returns>
    public byte[] GetDecodedData()
    {
        if (Stream is { } stream)
        {
            return stream.DecodeToArray();
        }

        var output = default(PooledBuffer);
        try
        {
            var filters = ImageHeader.Get(Dictionary, KnownName.Filter, KnownName.F, true);
            var parameters = ImageHeader.Get(Dictionary, KnownName.DecodeParms, KnownName.DP, true);
            _ = PdfStreamDecoder.Apply(InlineData, filters, parameters, Dictionary.Owner, ref output, CancellationToken.None);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Decodes the image to pixels.</summary>
    /// <param name="resources">The resources a named colour space is looked up in, or <see langword="null"/>.</param>
    /// <returns>The image, or <see langword="null"/> when it cannot be decoded.</returns>
    public PdfImageData? DecodePixels(PdfDictionary? resources) =>
        Stream is { } stream ? PdfImageDecoder.Decode(stream, resources) : PdfImageDecoder.DecodeInline(Dictionary, InlineData, resources);

    /// <summary>Replaces the image's pixels. The regenerated content paints a new Flate-compressed image in the same place.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="format">The layout of <paramref name="pixels"/>.</param>
    /// <param name="pixels">The pixels, row after row from the top, with no padding.</param>
    /// <exception cref="ArgumentOutOfRangeException">A size is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="pixels"/> is not the length the size and format need.</exception>
    public void SetPixels(int width, int height, PdfImagePixelFormat format, ReadOnlySpan<byte> pixels) =>
        _replacement = ImageStreams.Create(Dictionary.Owner, width, height, format, pixels, default);

    /// <summary>Replaces the image's pixels and gives it a soft mask.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="format">The layout of <paramref name="pixels"/>.</param>
    /// <param name="pixels">The pixels, row after row from the top, with no padding.</param>
    /// <param name="alpha">One byte of opacity per pixel, 0 transparent to 255 opaque.</param>
    /// <exception cref="ArgumentOutOfRangeException">A size is not positive.</exception>
    /// <exception cref="ArgumentException">A buffer is not the length the size and format need.</exception>
    public void SetPixels(int width, int height, PdfImagePixelFormat format, ReadOnlySpan<byte> pixels, ReadOnlySpan<byte> alpha) =>
        _replacement = ImageStreams.Create(Dictionary.Owner, width, height, format, pixels, alpha);

    /// <summary>Replaces a stencil mask's shape. The regenerated content paints a new 1-bit mask in the same place.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="coverage">One byte per pixel, row after row from the top; 128 and above paints.</param>
    /// <exception cref="ArgumentOutOfRangeException">A size is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="coverage"/> is not <paramref name="width"/> times <paramref name="height"/> bytes.</exception>
    public void SetStencilCoverage(int width, int height, ReadOnlySpan<byte> coverage)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (coverage.Length != checked(width * height))
        {
            throw new ArgumentException("The coverage does not match the size.", nameof(coverage));
        }

        var rowBytes = (width + ByteBitCount - 1) / ByteBitCount;
        var bits = new byte[rowBytes * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // With the default /Decode [0 1] a sample of 0 paints, so only pixels that do not paint are set.
                if (coverage[(y * width) + x] < StencilThreshold)
                {
                    bits[(y * rowBytes) + (x / ByteBitCount)] |= (byte)(MostSignificantBit >> (x % ByteBitCount));
                }
            }
        }

        _replacement = ImageStreams.CreateStencil(Dictionary.Owner, width, height, bits, null);
    }

    /// <summary>Sets a ready-made replacement stream.</summary>
    /// <param name="stream">The image stream, or null to keep the original.</param>
    internal void SetReplacement(PdfStream? stream) => _replacement = stream;
}
