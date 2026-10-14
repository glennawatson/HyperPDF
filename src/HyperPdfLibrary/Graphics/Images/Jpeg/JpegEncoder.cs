// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>
/// A managed JPEG encoder for 8-bit gray, YCbCr and CMYK or YCCK images: baseline sequential and progressive Huffman coding
/// with a quality setting, chroma subsampling, optimised or example Huffman tables and restart intervals. The image is
/// transformed one MCU row at a time into quantized coefficients, then coded one scan at a time.
/// </summary>
internal static class JpegEncoder
{
    /// <summary>The largest side of a JPEG image.</summary>
    internal const int MaxSide = ushort.MaxValue;

    /// <summary>The largest restart interval.</summary>
    private const int MaxRestartInterval = ushort.MaxValue;

    /// <summary>The quality from which the automatic subsampling keeps full colour detail.</summary>
    private const int FullDetailQuality = 90;

    /// <summary>The bits that hold the vertical factor in a packed sampling byte.</summary>
    private const int NibbleBits = 4;

    /// <summary>The mask of a nibble.</summary>
    private const int NibbleMask = 0x0F;

    /// <summary>The components of a gray image.</summary>
    private const int GrayComponents = 1;

    /// <summary>The components of a colour image.</summary>
    private const int ColorComponents = 3;

    /// <summary>The components of an ink image.</summary>
    private const int InkComponents = 4;

    /// <summary>The most data units an interleaved MCU may hold (ISO/IEC 10918-1, B.2.3).</summary>
    private const int MaxBlocksPerMcu = 10;

    /// <summary>The index of the blue chroma component.</summary>
    private const int BlueChromaIndex = 1;

    /// <summary>The index of the red chroma component.</summary>
    private const int RedChromaIndex = 2;

    /// <summary>The wide sampling factor of a subsampled luma component.</summary>
    private const int Wide = 2;

    /// <summary>The widest sampling factor.</summary>
    private const int Widest = 4;

    /// <summary>Encodes pixels as a JPEG.</summary>
    /// <param name="pixels">The source pixels, rows packed.</param>
    /// <param name="request">The size, format and options.</param>
    /// <param name="output">Receives the JPEG bytes.</param>
    /// <param name="scratchPools">The bounded cache for the working arrays.</param>
    /// <param name="cancellationToken">Cancels the encode between MCU rows and scans.</param>
    /// <exception cref="ArgumentOutOfRangeException">The size is not positive or is too large for JPEG, or the pixel format is not known.</exception>
    /// <exception cref="ArgumentException">The pixels do not fill the image, or the sampling is not valid.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static void Encode(ReadOnlySpan<byte> pixels, in JpegEncodeRequest request, ref PooledBuffer output, ScratchPools scratchPools, CancellationToken cancellationToken)
    {
        Validate(pixels, request);
        var state = JpegEncoderState.Rent();
        try
        {
            var geometry = Prepare(request, state, scratchPools);
            PrepareQuantization(request, state);
            Transform(pixels, request, geometry, state, cancellationToken);
            JpegFileWriter.Write(ref output, geometry, state, request, cancellationToken);
        }
        finally
        {
            JpegEncoderState.Return(state);
        }
    }

    /// <summary>Gets the colour model for a source format.</summary>
    /// <param name="format">The source format.</param>
    /// <param name="options">The options.</param>
    /// <returns>The model.</returns>
    internal static JpegColorKind KindOf(PdfJpegPixelFormat format, PdfJpegOptions options) => format switch
    {
        PdfJpegPixelFormat.Gray8 => JpegColorKind.Gray,
        PdfJpegPixelFormat.Cmyk32 => options.TransformCmyk ? JpegColorKind.Ycck : JpegColorKind.Cmyk,
        _ => JpegColorKind.YCbCr,
    };

    /// <summary>Gets the number of components of a colour model.</summary>
    /// <param name="kind">The model.</param>
    /// <returns>1, 3 or 4.</returns>
    internal static int ComponentsOf(JpegColorKind kind) => kind switch
    {
        JpegColorKind.Gray => GrayComponents,
        JpegColorKind.YCbCr => ColorComponents,
        _ => InkComponents,
    };

    /// <summary>Checks the request against the pixels.</summary>
    /// <param name="pixels">The source pixels.</param>
    /// <param name="request">The request.</param>
    /// <exception cref="ArgumentOutOfRangeException">The size or the format is not valid.</exception>
    /// <exception cref="ArgumentException">The pixels do not fill the image.</exception>
    private static void Validate(ReadOnlySpan<byte> pixels, in JpegEncodeRequest request)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Height, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.Width, MaxSide);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.Height, MaxSide);
        if (request.Format is < PdfJpegPixelFormat.Gray8 or > PdfJpegPixelFormat.Cmyk32)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Format, "The pixel format is not known.");
        }

        if (pixels.Length < (long)request.Width * request.Height * JpegPixelConverter.BytesPerPixel(request.Format))
        {
            throw new ArgumentException("The pixels do not fill the image.", nameof(pixels));
        }
    }

    /// <summary>Chooses the components and sampling and sets the components up.</summary>
    /// <param name="request">The request.</param>
    /// <param name="state">The encoder state.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The frame layout.</returns>
    /// <exception cref="ArgumentException">The sampling is not valid or gives an MCU of more than ten blocks.</exception>
    private static JpegFrameGeometry Prepare(in JpegEncodeRequest request, JpegEncoderState state, ScratchPools scratchPools)
    {
        var kind = KindOf(request.Format, request.Options);
        var count = ComponentsOf(kind);
        Span<JpegEncoderSampling> sampling = stackalloc JpegEncoderSampling[JpegEncoderState.MaxComponents];
        ChooseSampling(request, kind, count, sampling);
        var maxHorizontal = 1;
        var maxVertical = 1;
        for (var c = 0; c < count; c++)
        {
            maxHorizontal = Math.Max(maxHorizontal, sampling[c].Horizontal);
            maxVertical = Math.Max(maxVertical, sampling[c].Vertical);
        }

        var blocksPerMcu = 0;
        for (var c = 0; c < count; c++)
        {
            blocksPerMcu += sampling[c].Horizontal * sampling[c].Vertical;
        }

        // ISO/IEC 10918-1, B.2.3: an interleaved scan holds at most ten data units per MCU.
        if (blocksPerMcu > MaxBlocksPerMcu && !request.NonInterleaved)
        {
            throw new ArgumentException("An interleaved MCU can hold at most ten blocks.", nameof(request));
        }

        for (var c = 0; c < count; c++)
        {
            // The chroma reduction averages whole blocks of samples, so every factor must divide the largest.
            if (maxHorizontal % sampling[c].Horizontal != 0 || maxVertical % sampling[c].Vertical != 0)
            {
                throw new ArgumentException("A sampling factor must divide the largest factor.", nameof(request));
            }
        }

        var geometry = new JpegFrameGeometry(
            request.Width,
            request.Height,
            count,
            maxHorizontal,
            maxVertical,
            (request.Width + (JpegBlock.Side * maxHorizontal) - 1) / (JpegBlock.Side * maxHorizontal),
            (request.Height + (JpegBlock.Side * maxVertical) - 1) / (JpegBlock.Side * maxVertical),
            kind);
        for (var c = 0; c < count; c++)
        {
            state.Components[c].Initialize(c + 1, sampling[c], geometry, scratchPools);
        }

        return geometry;
    }

    /// <summary>Chooses the sampling factors and table slots of each component.</summary>
    /// <param name="request">The request.</param>
    /// <param name="kind">The colour model.</param>
    /// <param name="count">The number of components.</param>
    /// <param name="sampling">Receives one entry per component.</param>
    private static void ChooseSampling(in JpegEncodeRequest request, JpegColorKind kind, int count, Span<JpegEncoderSampling> sampling)
    {
        for (var c = 0; c < count; c++)
        {
            var slot = SlotOf(kind, c);
            sampling[c] = request.Sampling.IsEmpty ? DefaultSampling(request.Options, kind, slot) : Explicit(request.Sampling.Span, c, count, slot);
        }
    }

    /// <summary>Reads a caller-chosen sampling factor.</summary>
    /// <param name="packed">The packed factors of every component.</param>
    /// <param name="index">The component.</param>
    /// <param name="count">The number of components.</param>
    /// <param name="slot">The table slot.</param>
    /// <returns>The sampling.</returns>
    /// <exception cref="ArgumentException">The bytes are not one per component, or a factor is not from 1 to 4.</exception>
    private static JpegEncoderSampling Explicit(ReadOnlySpan<byte> packed, int index, int count, int slot)
    {
        if (packed.Length != count)
        {
            throw new ArgumentException("There must be one sampling byte for each component.", nameof(packed));
        }

        var horizontal = packed[index] >> NibbleBits;
        var vertical = packed[index] & NibbleMask;
        if (horizontal is < 1 or > Widest || vertical is < 1 or > Widest)
        {
            throw new ArgumentException("A sampling factor must be from 1 to 4.", nameof(packed));
        }

        return new(horizontal, vertical, slot);
    }

    /// <summary>Chooses the sampling of a component from the options.</summary>
    /// <param name="options">The options.</param>
    /// <param name="kind">The colour model.</param>
    /// <param name="slot">The table slot.</param>
    /// <returns>The sampling.</returns>
    private static JpegEncoderSampling DefaultSampling(PdfJpegOptions options, JpegColorKind kind, int slot) =>
        kind is JpegColorKind.YCbCr or JpegColorKind.Ycck && slot == 0 ? FineSampling(ResolveSubsampling(options), slot) : new(1, 1, slot);

    /// <summary>Gets the sampling of a component that carries brightness; the chroma components and plain ink channels stay at one.</summary>
    /// <param name="subsampling">The subsampling, not automatic.</param>
    /// <param name="slot">The table slot.</param>
    /// <returns>The sampling.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The subsampling is not a known value.</exception>
    private static JpegEncoderSampling FineSampling(PdfJpegSubsampling subsampling, int slot) => subsampling switch
    {
        PdfJpegSubsampling.Chroma422 => new(Wide, 1, slot),
        PdfJpegSubsampling.Chroma420 => new(Wide, Wide, slot),
        PdfJpegSubsampling.Chroma440 => new(1, Wide, slot),
        PdfJpegSubsampling.Chroma411 => new(Widest, 1, slot),
        PdfJpegSubsampling.Automatic or PdfJpegSubsampling.Chroma444 => new(1, 1, slot),
        _ => throw new ArgumentOutOfRangeException(nameof(subsampling), subsampling, "The subsampling is not known."),
    };

    /// <summary>Settles the automatic subsampling choice: full colour detail at high quality, otherwise a quarter of it.</summary>
    /// <param name="options">The options.</param>
    /// <returns>A subsampling other than <see cref="PdfJpegSubsampling.Automatic"/>.</returns>
    private static PdfJpegSubsampling ResolveSubsampling(PdfJpegOptions options) => options.Subsampling switch
    {
        PdfJpegSubsampling.Automatic => options.Quality >= FullDetailQuality ? PdfJpegSubsampling.Chroma444 : PdfJpegSubsampling.Chroma420,
        PdfJpegSubsampling.Chroma444 => PdfJpegSubsampling.Chroma444,
        PdfJpegSubsampling.Chroma422 => PdfJpegSubsampling.Chroma422,
        PdfJpegSubsampling.Chroma420 => PdfJpegSubsampling.Chroma420,
        PdfJpegSubsampling.Chroma440 => PdfJpegSubsampling.Chroma440,
        PdfJpegSubsampling.Chroma411 => PdfJpegSubsampling.Chroma411,
        _ => PdfJpegSubsampling.Chroma420,
    };

    /// <summary>Gets the table slot of a component: luminance for gray, brightness, black and plain ink channels, chrominance for the two colour differences.</summary>
    /// <param name="kind">The colour model.</param>
    /// <param name="index">The component.</param>
    /// <returns>0 or 1.</returns>
    private static int SlotOf(JpegColorKind kind, int index) =>
        kind is JpegColorKind.YCbCr or JpegColorKind.Ycck && index is BlueChromaIndex or RedChromaIndex ? 1 : 0;

    /// <summary>Scales the quantization tables for the quality and builds their reciprocals.</summary>
    /// <param name="request">The request.</param>
    /// <param name="state">The encoder state.</param>
    private static void PrepareQuantization(in JpegEncodeRequest request, JpegEncoderState state)
    {
        for (var slot = 0; slot < JpegEncoderState.QuantSlots; slot++)
        {
            var steps = state.QuantSteps.AsSpan(slot * JpegBlock.Length, JpegBlock.Length);
            JpegQuantizer.Scale(slot == 1, request.Options.Quality, steps);
            JpegQuantizer.BuildReciprocals(steps, state.QuantReciprocals.AsSpan(slot * JpegBlock.Length, JpegBlock.Length));
        }
    }

    /// <summary>Converts the image one MCU row at a time and leaves its quantized coefficients in the components.</summary>
    /// <param name="pixels">The source pixels.</param>
    /// <param name="request">The request.</param>
    /// <param name="geometry">The frame layout.</param>
    /// <param name="state">The encoder state.</param>
    /// <param name="cancellationToken">Cancels between MCU rows.</param>
    private static void Transform(ReadOnlySpan<byte> pixels, in JpegEncodeRequest request, in JpegFrameGeometry geometry, JpegEncoderState state, CancellationToken cancellationToken)
    {
        var components = state.Components.AsSpan(0, geometry.Components);
        var rowBytes = request.Width * JpegPixelConverter.BytesPerPixel(request.Format);
        var stripRows = JpegBlock.Side * geometry.MaxVertical;
        Span<int> work = stackalloc int[JpegBlock.Length];
        for (var mcuRow = 0; mcuRow < geometry.McusPerColumn; mcuRow++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var r = 0; r < stripRows; r++)
            {
                var y = (mcuRow * stripRows) + r;
                if (y < request.Height)
                {
                    JpegPixelConverter.ConvertRow(pixels.Slice(y * rowBytes, rowBytes), request.Format, geometry.Kind, request.Width, components, r * components[0].FullStride);
                }
                else
                {
                    RepeatRow(components, r);
                }
            }

            foreach (var component in components)
            {
                if (component.IsReduced)
                {
                    JpegPixelConverter.Reduce(component, geometry.MaxHorizontal / component.HorizontalSampling, geometry.MaxVertical / component.VerticalSampling);
                }

                TransformBlocks(component, mcuRow, state.QuantReciprocals.AsSpan(component.QuantSlot * JpegBlock.Length, JpegBlock.Length), work);
            }
        }
    }

    /// <summary>Repeats the previous strip row in every component, to pad the image to whole MCUs.</summary>
    /// <param name="components">The frame's components.</param>
    /// <param name="row">The strip row to fill.</param>
    private static void RepeatRow(ReadOnlySpan<JpegEncoderComponent> components, int row)
    {
        foreach (var component in components)
        {
            var strip = component.FullStrip;
            strip.Slice((row - 1) * component.FullStride, component.FullStride).CopyTo(strip.Slice(row * component.FullStride, component.FullStride));
        }
    }

    /// <summary>Transforms and quantizes every block of one MCU row of a component.</summary>
    /// <param name="component">The component.</param>
    /// <param name="mcuRow">The MCU row.</param>
    /// <param name="reciprocals">The reciprocals of the component's quantization table.</param>
    /// <param name="work">A 64-int scratch buffer.</param>
    private static void TransformBlocks(JpegEncoderComponent component, int mcuRow, ReadOnlySpan<int> reciprocals, Span<int> work)
    {
        var plane = component.Plane;
        var stride = component.PlaneStride;
        for (var by = 0; by < component.VerticalSampling; by++)
        {
            for (var bx = 0; bx < component.BlocksPerLine; bx++)
            {
                var origin = (by * JpegBlock.Side * stride) + (bx * JpegBlock.Side);
                JpegForwardDct.Transform(plane[origin..], stride, reciprocals, work, component.GetBlock((mcuRow * component.VerticalSampling) + by, bx));
            }
        }
    }
}
