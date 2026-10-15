// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpx.Encoding;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Re-encodes one image at a time. Colour and grey images drawn well above the target resolution are downsampled and
/// saved as JPEG; continuous-tone images are saved as JPEG when lossy changes are allowed, or as JPEG 2000 (lossy or
/// lossless) when the options choose it and the document may hold it; black-and-white images are fax, JBIG2 (when the
/// options ask for it) or Flate coded, losslessly unless lossy JBIG2 is chosen. Masks, colour-keyed, CMYK, indexed and
/// other images are left to lossless recompression, and images that are already JBIG2 or JPEG 2000 are left as they are,
/// as decoding and re-encoding them would add loss for little gain. A result is used only when it is smaller.
/// </summary>
[DebuggerDisplay("ImageRecoder")]
internal sealed class ImageRecoder
{
    /// <summary>The bits of an 8-bit sample.</summary>
    private const int ByteBits = 8;

    /// <summary>The pixels sampled to tell photographs from flat graphics.</summary>
    private const int ToneSamples = 4096;

    /// <summary>The distinct colours above which an image counts as continuous tone.</summary>
    private const int FlatColorLimit = 256;

    /// <summary>The slots of the table that counts distinct colours: a power of two, well above <see cref="FlatColorLimit"/>.</summary>
    private const int ColorTableSlots = 1024;

    /// <summary>The multiplier that spreads a colour key over the table.</summary>
    private const uint ColorHash = 2_654_435_761;

    /// <summary>The shift that keeps the table index bits of a multiplied colour key.</summary>
    private const int ColorShift = 22;

    /// <summary>The shift that packs one channel into a colour key.</summary>
    private const int ChannelShift = 8;

    /// <summary>The bytes of an RGB sample triple.</summary>
    private const int RgbBytes = 3;

    /// <summary>The options.</summary>
    private PdfOptimizeOptions _options;

    /// <summary>The document's image codec.</summary>
    private PdfDrawingServices _drawingServices = null!;

    /// <summary>The source store that owns encoding scratch when no shared service pool was selected.</summary>
    private PdfObjectStore _scratchStore = null!;

    /// <summary>Whether the document may hold JPEG 2000 images: false for PDF/A-1.</summary>
    private bool _allowJpx;

    /// <summary>What JBIG2 coding may be tried for black-and-white images, after the document's PDF/A claim is taken into account.</summary>
    private PdfJbig2Mode _jbig2;

    /// <summary>Cancels image decoding.</summary>
    private CancellationToken _cancellationToken;

    /// <summary>The immutable JPEG settings last used by this recoder.</summary>
    private PdfJpegOptions _jpegOptions = PdfJpegOptions.Default;

    /// <summary>The accepted encoded bytes, kept until the writer has consumed the current image.</summary>
    private byte[]? _encodedBytes;

    /// <summary>Gets the image codec that resamples images drawn well above the target resolution; JPEG decoding and encoding are managed.</summary>
    private IPdfImageCodec Codec => _drawingServices.Images
        ?? throw new NotSupportedException("Supply an image codec in PdfOpenOptions.DrawingServices before downsampling images.");

    /// <summary>Sets what the recoder works with, so one recoder serves every run of a session.</summary>
    /// <param name="options">The options.</param>
    /// <param name="drawingServices">The document's image codec.</param>
    /// <param name="scratchStore">The source store that owns encoding scratch.</param>
    /// <param name="allowJpx">Whether the document may hold JPEG 2000 images: false for PDF/A-1.</param>
    /// <param name="jbig2">What JBIG2 coding may be tried for black-and-white images, after the document's PDF/A claim is taken into account.</param>
    /// <param name="cancellationToken">Cancels image decoding.</param>
    internal void Configure(in PdfOptimizeOptions options, PdfDrawingServices drawingServices, PdfObjectStore scratchStore, bool allowJpx, PdfJbig2Mode jbig2, CancellationToken cancellationToken)
    {
        _options = options;
        _drawingServices = drawingServices;
        _scratchStore = scratchStore;
        _allowJpx = allowJpx;
        _jbig2 = jbig2;
        _cancellationToken = cancellationToken;
    }

    /// <summary>Forgets the run's options and document services.</summary>
    internal void Clear()
    {
        ReleaseOutput();
        _options = default;
        _drawingServices = null!;
        _scratchStore = null!;
        _cancellationToken = default;
    }

    /// <summary>Re-encodes an image.</summary>
    /// <param name="image">The image.</param>
    /// <param name="use">How the image is used.</param>
    /// <param name="isMask">Whether another image uses it as a soft or stencil mask.</param>
    /// <returns>The outcome, whose encoded bytes remain valid until the next call or <see cref="Clear"/>.</returns>
    internal ImageOutcome Recode(PdfStream image, ImageUse use, bool isMask)
    {
        ReleaseOutput();
        var dictionary = image.Dictionary;
        var codec = ImageFilters.Codec(dictionary);
        var width = dictionary.GetInt32(KnownName.Width, 0);
        var height = dictionary.GetInt32(KnownName.Height, 0);
        if (width <= 0 || height <= 0 || codec == CodecKind.Crypt)
        {
            return ImageOutcome.Unchanged;
        }

        if (codec is CodecKind.Jbig2 or CodecKind.Jpeg2000)
        {
            return new(null, "JBIG2 and JPEG 2000 images are left as they are.");
        }

        if (BilevelRecoder.IsBilevel(dictionary))
        {
            return RecodeBilevel(image, codec, new(width, height, 1), isMask);
        }

        var eightBit = dictionary.GetInt32(KnownName.BitsPerComponent, ByteBits) == ByteBits;
        return eightBit && codec != CodecKind.Fax ? RecodeContinuous(image, codec, use, isMask) : ImageOutcome.Unchanged;
    }

    /// <summary>Determines whether an image has many colours, as photographs do, by sampling its pixels.</summary>
    /// <param name="samples">The 8-bit samples.</param>
    /// <param name="components">One for grey, three for RGB.</param>
    /// <param name="pixels">The pixels.</param>
    /// <returns><see langword="true"/> when more than 256 distinct colours were seen.</returns>
    private static bool IsContinuousTone(ReadOnlySpan<byte> samples, int components, int pixels)
    {
        // An eight-bit grey image cannot exceed the distinct-colour threshold.
        if (components == 1 || pixels <= FlatColorLimit)
        {
            return false;
        }

        var step = Math.Max(1, pixels / ToneSamples);

        // A small open-addressing table of the colours seen; empty slots hold -1.
        Span<int> table = stackalloc int[ColorTableSlots];
        table.Fill(-1);
        var seen = 0;
        for (var i = 0; i < pixels; i += step)
        {
            var key = (samples[i * RgbBytes] << (ChannelShift + ChannelShift)) | (samples[(i * RgbBytes) + 1] << ChannelShift) | samples[(i * RgbBytes) + RgbBytes - 1];
            var slot = (int)(unchecked((uint)key * ColorHash) >> ColorShift);
            while (table[slot] >= 0 && table[slot] != key)
            {
                slot = (slot + 1) & (ColorTableSlots - 1);
            }

            if (table[slot] == key)
            {
                continue;
            }

            table[slot] = key;
            seen++;
            if (seen > FlatColorLimit)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Describes a size change.</summary>
    /// <param name="from">The old size.</param>
    /// <param name="to">The new size.</param>
    /// <returns>The description.</returns>
    private static string Resized(PixelSize from, PixelSize to) =>
        from == to
            ? string.Create(CultureInfo.InvariantCulture, $"{from.Width}x{from.Height}")
            : string.Create(CultureInfo.InvariantCulture, $"{from.Width}x{from.Height} downsampled to {to.Width}x{to.Height}");

    /// <summary>Works out the codings a black-and-white image may be tried with.</summary>
    /// <param name="isMask">Whether another image uses it as a soft or stencil mask, which is never coded lossily.</param>
    /// <returns>The codings.</returns>
    private BilevelCodings Codings(bool isMask)
    {
        var mode = _jbig2 == PdfJbig2Mode.Lossy && (!_options.AllowLossyImages || isMask) ? PdfJbig2Mode.Lossless : _jbig2;
        return new(_options.EncodeBilevelAsCcitt, mode);
    }

    /// <summary>Re-encodes a 1-bit image with the codings the options allow.</summary>
    /// <param name="image">The image.</param>
    /// <param name="codec">Its codec.</param>
    /// <param name="size">Its size.</param>
    /// <param name="isMask">Whether another image uses it as a mask.</param>
    /// <returns>The outcome.</returns>
    private ImageOutcome RecodeBilevel(PdfStream image, CodecKind codec, PixelSize size, bool isMask)
    {
        var codings = Codings(isMask);
        return codings.IsAnyEnabled ? BilevelRecoder.Recode(image, codec, size, codings, _scratchStore.ScratchPools, _cancellationToken) : ImageOutcome.Unchanged;
    }

    /// <summary>Re-encodes an 8-bit grey or RGB image.</summary>
    /// <param name="image">The image.</param>
    /// <param name="codec">Its codec: bytes or JPEG.</param>
    /// <param name="use">How the image is used.</param>
    /// <param name="isMask">Whether another image uses it as a mask.</param>
    /// <returns>The outcome.</returns>
    private ImageOutcome RecodeContinuous(PdfStream image, CodecKind codec, ImageUse use, bool isMask)
    {
        var dictionary = image.Dictionary;
        var components = ImageColor.LossyComponents(dictionary);
        if (components == 0)
        {
            return ImageOutcome.Unchanged;
        }

        var size = new PixelSize(dictionary.GetInt32(KnownName.Width, 0), dictionary.GetInt32(KnownName.Height, 0), components);
        var lossy = _options.AllowLossyImages && !isMask && !ImageColor.HasColorKey(dictionary);
        var job = new ImageJob(image, size, lossy ? Target(use, size, dictionary) : null, lossy, Codings(isMask));
        return codec == CodecKind.Jpeg ? RecodeJpeg(job) : RecodeSamples(job);
    }

    /// <summary>Works out the size to downsample to, when the image is drawn well above the target resolution.</summary>
    /// <param name="use">How the image is used.</param>
    /// <param name="size">The image's size.</param>
    /// <param name="dictionary">The image dictionary.</param>
    /// <returns>The target size, or <see langword="null"/> to keep the resolution.</returns>
    private PixelSize? Target(ImageUse use, PixelSize size, PdfDictionary dictionary)
    {
        var target = size.Components == 1 ? _options.GrayImageDpi : _options.ColorImageDpi;
        if (target <= 0 || use.IsFixed || use.Uses == 0 || !float.IsFinite(use.MinPpi) || use.MinPpi <= target * _options.DownsampleThreshold)
        {
            return null;
        }

        if (ImageColor.HasMatte(dictionary, OptimizerNames.Matte))
        {
            return null;
        }

        var factor = target / use.MinPpi;
        return size with { Width = Math.Max(1, (int)MathF.Round(size.Width * factor)), Height = Math.Max(1, (int)MathF.Round(size.Height * factor)) };
    }

    /// <summary>Downsamples a JPEG image drawn well above the target resolution.</summary>
    /// <param name="job">The image and its plan.</param>
    /// <returns>The outcome.</returns>
    private ImageOutcome RecodeJpeg(in ImageJob job)
    {
        var dictionary = job.Image.Dictionary;
        if (job.Target is not { } target || dictionary.ContainsKey(KnownName.Decode) || ImageFilters.CodecParameters(dictionary)?.ContainsKey(KnownName.ColorTransform) == true)
        {
            return ImageOutcome.Unchanged;
        }

        var encoded = default(PooledBuffer);
        var pixels = default(PooledBuffer);
        try
        {
            if (job.Image.Decode(ref encoded, _cancellationToken) != PdfImageCodec.Jpeg || JpegMarkers.ReadComponentCount(encoded.WrittenSpan) != job.Size.Components)
            {
                return ImageOutcome.Unchanged;
            }

            return ImagePixels.TryDecodeJpeg(encoded.WrittenSpan, job.Size.Components, ref pixels, out var decoded, _scratchStore.ScratchPools, _cancellationToken) && decoded == job.Size
                ? EncodeJpeg(job, pixels.WrittenSpan, target)
                : ImageOutcome.Unchanged;
        }
        finally
        {
            encoded.Dispose();
            pixels.Dispose();
        }
    }

    /// <summary>Re-encodes an image stored as plain or byte-filtered 8-bit samples.</summary>
    /// <param name="job">The image and its plan.</param>
    /// <returns>The outcome.</returns>
    private ImageOutcome RecodeSamples(in ImageJob job)
    {
        var size = job.Size;
        var count = size.Width * size.Height;
        var needed = count * size.Components;
        var samples = default(PooledBuffer);
        var pixels = default(PooledBuffer);
        try
        {
            if (job.Image.Decode(ref samples, _cancellationToken) != PdfImageCodec.None || samples.Length < needed)
            {
                return ImageOutcome.Unchanged;
            }

            var data = samples.WrittenSpan[..needed];
            if (size.Components == 1 && job.Codings.IsAnyEnabled && BilevelEncoder.IsBlackAndWhite(data))
            {
                return BilevelRecoder.PackAndCode(job.Image, data, size, job.Codings, _scratchStore.ScratchPools, _cancellationToken);
            }

            if (_allowJpx && _options.ImageCodec == PdfOptimizeImageCodec.Jpeg2000)
            {
                return job.Target is null && !IsContinuousTone(data, size.Components, count) ? ImageOutcome.Unchanged : EncodeJpx(job, data);
            }

            return RecodeAsJpeg(job, data, ref pixels);
        }
        finally
        {
            samples.Dispose();
            pixels.Dispose();
        }
    }

    /// <summary>Re-encodes decoded samples as JPEG when lossy changes are allowed and the image looks like a photograph or is being downsampled.</summary>
    /// <param name="job">The image and its plan.</param>
    /// <param name="data">The grey or RGB samples.</param>
    /// <param name="pixels">Receives the pixels the encoder reads.</param>
    /// <returns>The outcome.</returns>
    private ImageOutcome RecodeAsJpeg(in ImageJob job, ReadOnlySpan<byte> data, ref PooledBuffer pixels)
    {
        var size = job.Size;
        if (!job.Lossy || (job.Target is null && !IsContinuousTone(data, size.Components, size.Width * size.Height)))
        {
            return ImageOutcome.Unchanged;
        }

        ImagePixels.FromSamples(data, size.Components, size.Width * size.Height, ref pixels);
        return EncodeJpeg(job, pixels.WrittenSpan, job.Target ?? size);
    }

    /// <summary>Downsamples pixels when needed and encodes them as JPEG, keeping the result only when smaller.</summary>
    /// <param name="job">The image and its plan.</param>
    /// <param name="pixels">The grey or RGBX pixels at full size.</param>
    /// <param name="target">The size to write.</param>
    /// <returns>The outcome.</returns>
    private ImageOutcome EncodeJpeg(in ImageJob job, ReadOnlySpan<byte> pixels, PixelSize target)
    {
        var resized = default(PooledBuffer);
        var jpeg = default(PooledBuffer);
        try
        {
            var source = pixels;
            if (target != job.Size)
            {
                if (!ImagePixels.Downsample(Codec, pixels, job.Size, target.Width, target.Height, ref resized))
                {
                    return ImageOutcome.Unchanged;
                }

                source = resized.WrittenSpan;
            }

            if (_jpegOptions.Quality != _options.JpegQuality)
            {
                _jpegOptions = new() { Quality = _options.JpegQuality };
            }

            ImagePixels.EncodeJpeg(source, target, _jpegOptions, ref jpeg, _scratchStore.ScratchPools, _cancellationToken);
            if (jpeg.Length >= job.Image.RawLength)
            {
                return ImageOutcome.Unchanged;
            }

            var copy = job.Image.Dictionary.Clone();
            _ = copy.Remove(KnownName.Length);
            _ = copy.Remove(KnownName.DL);
            _ = copy.Remove(KnownName.DecodeParms);
            copy.Set(KnownName.Filter, PdfValue.FromName(KnownName.DCTDecode));
            copy.Set(KnownName.Width, PdfValue.FromInteger(target.Width));
            copy.Set(KnownName.Height, PdfValue.FromInteger(target.Height));
            copy.Set(KnownName.BitsPerComponent, PdfValue.FromInteger(ByteBits));
            var note = string.Create(CultureInfo.InvariantCulture, $"Image {Resized(job.Size, target)} saved as JPEG quality {_options.JpegQuality}.");
            return new(RetainOutput(copy, ref jpeg), note);
        }
        finally
        {
            resized.Dispose();
            jpeg.Dispose();
        }
    }

    /// <summary>Downsamples 8-bit samples when needed and encodes them as JPEG 2000, keeping the result only when smaller.</summary>
    /// <param name="job">The image and its plan.</param>
    /// <param name="samples">The grey or RGB samples at full size.</param>
    /// <returns>The outcome.</returns>
    private ImageOutcome EncodeJpx(in ImageJob job, ReadOnlySpan<byte> samples)
    {
        var target = job.Target ?? job.Size;
        var packed = default(PooledBuffer);
        var file = default(PooledBuffer);
        try
        {
            var source = samples;
            if (target != job.Size)
            {
                if (!Shrink(job, samples, target, ref packed))
                {
                    return ImageOutcome.Unchanged;
                }

                source = packed.WrittenSpan;
            }

            var shape = new PdfJpxImage(target.Width, target.Height, target.Components, ByteBits, ReadOnlyMemory<byte>.Empty);
            var jpxOptions = new PdfJpxOptions { Mode = job.Lossy ? PdfJpxMode.Lossy : PdfJpxMode.Lossless, Quality = _options.JpegQuality };
            JpxEncoder.Encode(shape, source, jpxOptions, ref file, _cancellationToken);
            return file.Length >= job.Image.RawLength ? ImageOutcome.Unchanged : JpxOutcome(job, target, ref file);
        }
        finally
        {
            packed.Dispose();
            file.Dispose();
        }
    }

    /// <summary>Downsamples packed samples to a target size and packs the result again.</summary>
    /// <param name="job">The image and its plan.</param>
    /// <param name="samples">The grey or RGB samples at full size.</param>
    /// <param name="target">The size to write.</param>
    /// <param name="packed">Receives the samples at the target size.</param>
    /// <returns><see langword="true"/> when the resample ran.</returns>
    private bool Shrink(in ImageJob job, ReadOnlySpan<byte> samples, PixelSize target, ref PooledBuffer packed)
    {
        var pixels = default(PooledBuffer);
        var resized = default(PooledBuffer);
        try
        {
            ImagePixels.FromSamples(samples, job.Size.Components, job.Size.Width * job.Size.Height, ref pixels);
            if (!ImagePixels.Downsample(Codec, pixels.WrittenSpan, job.Size, target.Width, target.Height, ref resized))
            {
                return false;
            }

            ImagePixels.ToSamples(resized.WrittenSpan, target.Components, target.Width * target.Height, ref packed);
            return true;
        }
        finally
        {
            pixels.Dispose();
            resized.Dispose();
        }
    }

    /// <summary>Makes the replacement image stream for JPEG 2000 data.</summary>
    /// <param name="job">The image and its plan.</param>
    /// <param name="target">The size written.</param>
    /// <param name="file">The JPEG 2000 file, transferred to the recoder when accepted.</param>
    /// <returns>The outcome.</returns>
    private ImageOutcome JpxOutcome(in ImageJob job, PixelSize target, ref PooledBuffer file)
    {
        var copy = job.Image.Dictionary.Clone();
        _ = copy.Remove(KnownName.Length);
        _ = copy.Remove(KnownName.DL);
        _ = copy.Remove(KnownName.DecodeParms);
        copy.Set(KnownName.Filter, PdfValue.FromName(KnownName.JPXDecode));
        copy.Set(KnownName.Width, PdfValue.FromInteger(target.Width));
        copy.Set(KnownName.Height, PdfValue.FromInteger(target.Height));
        copy.Set(KnownName.BitsPerComponent, PdfValue.FromInteger(ByteBits));
        var kind = job.Lossy ? string.Create(CultureInfo.InvariantCulture, $"JPEG 2000 quality {_options.JpegQuality}") : "lossless JPEG 2000";
        var note = string.Create(CultureInfo.InvariantCulture, $"Image {Resized(job.Size, target)} saved as {kind}.");
        return new(RetainOutput(copy, ref file), note);
    }

    /// <summary>Keeps a candidate's rented bytes until the next image is transformed.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <param name="output">The accepted encoded bytes; emptied.</param>
    /// <returns>A stream borrowing those bytes while the writer serializes it.</returns>
    private PdfStream RetainOutput(PdfDictionary dictionary, ref PooledBuffer output)
    {
        _encodedBytes = output.TakeArray(out var length);
        dictionary.Set(KnownName.Length, PdfValue.FromInteger(length));
        return new(dictionary, _encodedBytes ?? [], 0, length, default, false);
    }

    /// <summary>Returns the preceding image's bytes after its writer chunk has been produced.</summary>
    private void ReleaseOutput()
    {
        if (_encodedBytes is not { } bytes)
        {
            return;
        }

        _encodedBytes = null;
        ArrayPool<byte>.Shared.Return(bytes);
    }

    /// <summary>One image being re-encoded.</summary>
    /// <param name="Image">The image.</param>
    /// <param name="Size">Its size and components.</param>
    /// <param name="Target">The size to downsample to, or <see langword="null"/> to keep it.</param>
    /// <param name="Lossy">Whether lossy encoding is allowed.</param>
    /// <param name="Codings">The codings that may be tried if the image turns out to be black and white.</param>
    [DebuggerDisplay("ImageJob: {Size}")]
    private readonly record struct ImageJob(PdfStream Image, PixelSize Size, PixelSize? Target, bool Lossy, BilevelCodings Codings);
}
