// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpeg;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Unpacks gray or RGBX samples, reduces image blocks, and decodes and encodes JPEG in managed code. The configured
/// image codec provides only the final Mitchell cubic resample.
/// </summary>
internal static class ImagePixels
{
    /// <summary>The bytes of an RGBX pixel.</summary>
    internal const int RgbxBytes = 4;

    /// <summary>The bytes of an RGB sample triple.</summary>
    private const int RgbBytes = 3;

    /// <summary>The index of blue in a pixel.</summary>
    private const int Blue = 2;

    /// <summary>The value of an opaque channel.</summary>
    private const byte Opaque = 0xFF;

    /// <summary>The bytes of a 128-bit vector.</summary>
    private const int VectorBytes = 16;

    /// <summary>The pixels converted by one vector step.</summary>
    private const int VectorPixels = 4;

    /// <summary>The smallest shrink factor worth averaging whole blocks for before the cubic resample.</summary>
    private const int MinBoxFactor = 2;

    /// <summary>Gets the bytes per pixel of a pixel layout.</summary>
    /// <param name="components">One for grey, three for RGB.</param>
    /// <returns>1 or 4.</returns>
    internal static int PixelBytes(int components) => components == 1 ? 1 : RgbxBytes;

    /// <summary>Copies 8-bit samples into pixels: grey as is, RGB widened to RGBX.</summary>
    /// <param name="samples">The samples, rows packed.</param>
    /// <param name="components">One for grey, three for RGB.</param>
    /// <param name="pixelCount">The pixels.</param>
    /// <param name="pixels">Receives the pixels.</param>
    internal static void FromSamples(ReadOnlySpan<byte> samples, int components, int pixelCount, ref PooledBuffer pixels)
    {
        if (components == 1)
        {
            pixels.Write(samples[..pixelCount]);
            return;
        }

        var target = pixels.GetSpan(pixelCount * RgbxBytes);
        var done = WidenVectors(samples, target, pixelCount);
        for (var i = done; i < pixelCount; i++)
        {
            var source = samples.Slice(i * RgbBytes, RgbBytes);
            var pixel = target.Slice(i * RgbxBytes, RgbxBytes);
            pixel[0] = source[0];
            pixel[1] = source[1];
            pixel[Blue] = source[Blue];
            pixel[RgbBytes] = Opaque;
        }

        pixels.Advance(pixelCount * RgbxBytes);
    }

    /// <summary>Packs pixels into 8-bit samples: grey as is, RGBX narrowed to RGB.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="components">One for grey, three for RGB.</param>
    /// <param name="pixelCount">The number of pixels to pack.</param>
    /// <param name="samples">Receives the samples.</param>
    internal static void ToSamples(ReadOnlySpan<byte> pixels, int components, int pixelCount, ref PooledBuffer samples)
    {
        if (components == 1)
        {
            samples.Write(pixels[..pixelCount]);
            return;
        }

        var target = samples.GetSpan(pixelCount * RgbBytes);
        var done = NarrowVectors(pixels, target, pixelCount);
        for (var i = done; i < pixelCount; i++)
        {
            var source = pixels.Slice(i * RgbxBytes, RgbBytes);
            source.CopyTo(target.Slice(i * RgbBytes, RgbBytes));
        }

        samples.Advance(pixelCount * RgbBytes);
    }

    /// <summary>
    /// Widens four RGB pixels at a time to RGBX with a byte shuffle. Each step reads sixteen bytes, so the last few pixels are
    /// left to the caller.
    /// </summary>
    /// <param name="samples">The RGB samples.</param>
    /// <param name="target">Receives the RGBX pixels.</param>
    /// <param name="pixelCount">The pixels to convert.</param>
    /// <returns>The pixels converted; the caller converts the rest.</returns>
    internal static int WidenVectors(ReadOnlySpan<byte> samples, Span<byte> target, int pixelCount)
    {
        if (!Vector128.IsHardwareAccelerated)
        {
            return 0;
        }

        // Source bytes 0 1 2 _ 3 4 5 _ 6 7 8 _ 9 10 11 _: a lane of 0x80 selects nothing and comes out zero, and the alpha mask then sets it to opaque.
        var widen = Vector128.Create(0x8005040380020100UL, 0x800B0A0980080706UL).AsByte();
        var alpha = Vector128.Create(0xFF000000FF000000UL).AsByte();
        ref var source = ref MemoryMarshal.GetReference(samples);
        ref var destination = ref MemoryMarshal.GetReference(target);
        var i = 0;
        for (; (i * RgbBytes) + VectorBytes <= pixelCount * RgbBytes; i += VectorPixels)
        {
            var rgb = Vector128.LoadUnsafe(ref source, (nuint)(i * RgbBytes));
            (Vector128.Shuffle(rgb, widen) | alpha).StoreUnsafe(ref destination, (nuint)(i * RgbxBytes));
        }

        return i;
    }

    /// <summary>
    /// Narrows four RGBX pixels at a time to RGB with a byte shuffle. Each step stores sixteen bytes, of which twelve are new,
    /// and the next step overwrites the rest, so the last few pixels are left to the caller.
    /// </summary>
    /// <param name="pixels">The RGBX pixels.</param>
    /// <param name="target">Receives the RGB samples.</param>
    /// <param name="pixelCount">The pixels to convert.</param>
    /// <returns>The pixels converted; the caller converts the rest.</returns>
    internal static int NarrowVectors(ReadOnlySpan<byte> pixels, Span<byte> target, int pixelCount)
    {
        if (!Vector128.IsHardwareAccelerated)
        {
            return 0;
        }

        // Source bytes 0 1 2 4 5 6 8 9 10 12 13 14 then four unused lanes.
        var narrow = Vector128.Create(0x0908060504020100UL, 0x808080800E0D0C0AUL).AsByte();
        ref var source = ref MemoryMarshal.GetReference(pixels);
        ref var destination = ref MemoryMarshal.GetReference(target);
        var i = 0;
        for (; (i * RgbBytes) + VectorBytes <= pixelCount * RgbBytes; i += VectorPixels)
        {
            Vector128.Shuffle(Vector128.LoadUnsafe(ref source, (nuint)(i * RgbxBytes)), narrow).StoreUnsafe(ref destination, (nuint)(i * RgbBytes));
        }

        return i;
    }

    /// <summary>Shrinks pixels to a target size: whole blocks averaged first, then a cubic resample to the exact size.</summary>
    /// <param name="codec">The selected image codec.</param>
    /// <param name="source">The pixels.</param>
    /// <param name="size">The source size and layout.</param>
    /// <param name="targetWidth">The target width.</param>
    /// <param name="targetHeight">The target height.</param>
    /// <param name="output">Receives the target pixels.</param>
    /// <returns><see langword="true"/> when the resample ran.</returns>
    internal static bool Downsample(IPdfImageCodec codec, ReadOnlySpan<byte> source, PixelSize size, int targetWidth, int targetHeight, ref PooledBuffer output)
    {
        var factor = Math.Min(size.Width / targetWidth, size.Height / targetHeight);
        if (factor < MinBoxFactor)
        {
            return Resample(codec, source, size, targetWidth, targetHeight, ref output);
        }

        var reduced = default(PooledBuffer);
        try
        {
            var boxed = BoxReduce(source, size, factor, ref reduced);
            return Resample(codec, reduced.WrittenSpan, boxed, targetWidth, targetHeight, ref output);
        }
        finally
        {
            reduced.Dispose();
        }
    }

    /// <summary>Encodes pixels as a JPEG with the managed encoder.</summary>
    /// <param name="pixels">The gray or RGBX pixels.</param>
    /// <param name="size">The source dimensions and layout.</param>
    /// <param name="options">The JPEG settings.</param>
    /// <param name="output">Receives the JPEG candidate.</param>
    /// <param name="scratchPools">The document or registered service's scratch owner.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void EncodeJpeg(ReadOnlySpan<byte> pixels, PixelSize size, PdfJpegOptions options, ref PooledBuffer output, ScratchPools scratchPools, CancellationToken cancellationToken) =>
        PdfJpegEncoder.Encode(pixels, new(size.Width, size.Height, size.Components == 1 ? PdfJpegPixelFormat.Gray8 : PdfJpegPixelFormat.Rgbx32, options), ref output, scratchPools, cancellationToken);

    /// <summary>Decodes JPEG pixels with the managed decoder.</summary>
    /// <param name="jpeg">The JPEG bytes.</param>
    /// <param name="components">One for gray, three for RGBX.</param>
    /// <param name="pixels">Receives the decoded pixels.</param>
    /// <param name="size">Receives the JPEG dimensions.</param>
    /// <param name="scratchPools">The document or registered service's scratch owner.</param>
    /// <param name="cancellationToken">Cancels the decode.</param>
    /// <returns>True when usable pixels were decoded.</returns>
    internal static bool TryDecodeJpeg(ReadOnlySpan<byte> jpeg, int components, ref PooledBuffer pixels, out PixelSize size, ScratchPools scratchPools, CancellationToken cancellationToken)
    {
        size = default;
        if (!JpegMarkers.TryReadInfo(jpeg, out var info) || !info.IsSupported || info.Components != components)
        {
            return false;
        }

        var count = (long)info.Width * info.Height;
        if (count > ImageHeader.MaxPixels || !JpegDecoder.IsPlausibleSize(info, jpeg.Length))
        {
            return false;
        }

        var samples = new PooledBuffer((int)(count * components));
        try
        {
            var span = samples.GetSpan((int)(count * components))[..(int)(count * components)];
            if (!JpegDecoder.TryDecode(jpeg, info.UsesColorTransform(JpegInfo.NoColorTransform), span, scratchPools, cancellationToken))
            {
                return false;
            }

            FromSamples(span, components, (int)count, ref pixels);
            size = new(info.Width, info.Height, components);
            return true;
        }
        finally
        {
            samples.Dispose();
        }
    }

    /// <summary>Resamples pixels through the configured image codec.</summary>
    /// <param name="codec">The selected image codec.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="size">Their dimensions and layout.</param>
    /// <param name="targetWidth">The target width.</param>
    /// <param name="targetHeight">The target height.</param>
    /// <param name="output">Receives the target pixels.</param>
    /// <returns>True when resampling succeeds.</returns>
    private static bool Resample(IPdfImageCodec codec, ReadOnlySpan<byte> source, PixelSize size, int targetWidth, int targetHeight, ref PooledBuffer output)
    {
        var bytes = PixelBytes(size.Components);
        if (size.Width == targetWidth && size.Height == targetHeight)
        {
            output.Write(source[..(size.Width * size.Height * bytes)]);
            return true;
        }

        var length = checked(targetWidth * targetHeight * bytes);
        if (!codec.Resample(source, Layout(size), targetWidth, targetHeight, output.GetSpan(length)))
        {
            return false;
        }

        output.Advance(length);
        return true;
    }

    /// <summary>Averages square blocks of pixels; blocks cut off by the edge average the pixels they have.</summary>
    /// <param name="source">The pixels.</param>
    /// <param name="size">Their size and layout.</param>
    /// <param name="factor">The block side.</param>
    /// <param name="output">Receives the reduced pixels.</param>
    /// <returns>The reduced size.</returns>
    private static PixelSize BoxReduce(ReadOnlySpan<byte> source, PixelSize size, int factor, ref PooledBuffer output)
    {
        var bytes = PixelBytes(size.Components);
        var width = (size.Width + factor - 1) / factor;
        var height = (size.Height + factor - 1) / factor;
        var target = output.GetSpan(width * height * bytes);
        Span<int> sums = stackalloc int[RgbxBytes];
        for (var y = 0; y < height; y++)
        {
            var top = y * factor;
            var bottom = Math.Min(top + factor, size.Height);
            for (var x = 0; x < width; x++)
            {
                var left = x * factor;
                var right = Math.Min(left + factor, size.Width);
                sums.Clear();
                SumBlock(source, size.Width * bytes, bytes, new(left, top, right, bottom), sums);
                var count = (right - left) * (bottom - top);
                var pixel = target.Slice(((y * width) + x) * bytes, bytes);
                for (var c = 0; c < bytes; c++)
                {
                    pixel[c] = (byte)((sums[c] + (count >> 1)) / count);
                }
            }
        }

        output.Advance(width * height * bytes);
        return new(width, height, size.Components);
    }

    /// <summary>Adds up each channel of a block.</summary>
    /// <param name="source">The pixels.</param>
    /// <param name="stride">The bytes per row.</param>
    /// <param name="bytes">The bytes per pixel.</param>
    /// <param name="block">The block.</param>
    /// <param name="sums">Receives each channel's sum.</param>
    private static void SumBlock(ReadOnlySpan<byte> source, int stride, int bytes, PixelBlock block, Span<int> sums)
    {
        // Grey and RGBX are the only layouts, and each keeps its channel sums in locals instead of a span.
        var first = 0;
        var second = 0;
        var third = 0;
        var fourth = 0;
        for (var row = block.Top; row < block.Bottom; row++)
        {
            var line = source.Slice((row * stride) + (block.Left * bytes), (block.Right - block.Left) * bytes);
            ref var pixel = ref MemoryMarshal.GetReference(line);
            if (bytes == 1)
            {
                for (var i = 0; i < line.Length; i++)
                {
                    first += Unsafe.Add(ref pixel, i);
                }

                continue;
            }

            for (var i = 0; i < line.Length; i += RgbxBytes)
            {
                first += Unsafe.Add(ref pixel, i);
                second += Unsafe.Add(ref pixel, i + 1);
                third += Unsafe.Add(ref pixel, i + Blue);
                fourth += Unsafe.Add(ref pixel, i + RgbBytes);
            }
        }

        sums[0] += first;
        if (bytes != RgbxBytes)
        {
            return;
        }

        sums[1] += second;
        sums[Blue] += third;
        sums[RgbBytes] += fourth;
    }

    /// <summary>Describes the optimizer's gray or RGBX pixels.</summary>
    /// <param name="size">The dimensions and component count.</param>
    /// <returns>The equivalent image codec layout.</returns>
    private static PdfImagePixelLayout Layout(PixelSize size) =>
        new(size.Width, size.Height, size.Components == 1 ? PdfImagePixelFormat.Gray8 : PdfImagePixelFormat.Rgb888x);
}
