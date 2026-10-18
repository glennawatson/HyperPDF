// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>Writes an immediate generic region segment (T.88 section 7.4.6) holding a bitmap.</summary>
internal static class Jbig2GenericRegionWriter
{
    /// <summary>The flag bit of MMR coding.</summary>
    private const int MmrFlag = 0x01;

    /// <summary>The shift of the template number in the flags.</summary>
    private const int TemplateShift = 1;

    /// <summary>The flag bit of typical prediction.</summary>
    private const int TypicalFlag = 0x08;

    /// <summary>The number of adaptive pixels of template 0.</summary>
    private const int Template0Pixels = 4;

    /// <summary>The divisor of the raw bitmap size that gives the first size guess for the coded data.</summary>
    private const int SizeDivisor = 8;

    /// <summary>The smallest first size guess.</summary>
    private const int MinimumOutput = 256;

    /// <summary>Gets the parameters of a generic region with nominal adaptive pixels.</summary>
    /// <param name="template">The template, 0 to 3.</param>
    /// <param name="typical">Whether typical prediction is on.</param>
    /// <returns>The parameters.</returns>
    internal static Jbig2GenericParameters Nominal(int template, bool typical) => new(template, typical, Jbig2AtPixels.ForTemplate(template));

    /// <summary>Writes a generic region.</summary>
    /// <param name="output">The stream.</param>
    /// <param name="number">The segment number.</param>
    /// <param name="bitmap">The region's pixels, 1 for black, with zero padding bits.</param>
    /// <param name="place">The region's left and top edges on the page.</param>
    /// <param name="parameters">The arithmetic coding parameters.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    internal static void WriteArithmetic(ref PooledBuffer output, int number, in Jbig2BitmapView bitmap, Jbig2Point place, in Jbig2GenericParameters parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var position = BeginRegion(ref output, number, bitmap, place);
        WriteArithmeticData(ref output, bitmap, parameters, cancellationToken);
        Jbig2SegmentWriter.EndSegment(ref output, position);
    }

    /// <summary>Writes a generic region with MMR coding.</summary>
    /// <param name="output">The stream.</param>
    /// <param name="number">The segment number.</param>
    /// <param name="bitmap">The region's packed pixels, 1 for black.</param>
    /// <param name="place">The region's position.</param>
    /// <param name="pools">The caller's line-buffer owner.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    internal static void WriteMmr(ref PooledBuffer output, int number, in Jbig2BitmapView bitmap, Jbig2Point place, ScratchPools pools, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var position = BeginRegion(ref output, number, bitmap, place);
        output.WriteByte(MmrFlag);
        CcittEncoder.EncodeGroup4(bitmap.Data, bitmap.Width, bitmap.Height, true, ref output, pools, cancellationToken);
        Jbig2SegmentWriter.EndSegment(ref output, position);
    }

    /// <summary>Writes the adaptive template pixel offsets of a generic region (T.88 section 7.4.6.3).</summary>
    /// <param name="output">The stream.</param>
    /// <param name="parameters">The coding parameters.</param>
    internal static void WriteAdaptivePixels(ref PooledBuffer output, in Jbig2GenericParameters parameters)
    {
        var at = parameters.At;
        var count = parameters.Template == 0 ? Template0Pixels : 1;
        for (var i = 0; i < count; i++)
        {
            output.WriteByte((byte)(sbyte)at.X(i));
            output.WriteByte((byte)(sbyte)at.Y(i));
        }
    }

    /// <summary>Writes the common generic-region header.</summary>
    /// <param name="output">The stream.</param>
    /// <param name="number">The segment number.</param>
    /// <param name="bitmap">The region shape.</param>
    /// <param name="place">The region's position.</param>
    /// <returns>The segment's length placeholder.</returns>
    private static int BeginRegion(ref PooledBuffer output, int number, in Jbig2BitmapView bitmap, Jbig2Point place)
    {
        var position = Jbig2SegmentWriter.BeginSegment(ref output, number, Jbig2SegmentWriter.ImmediateLosslessGenericType, [], 1, false);
        Jbig2SegmentWriter.WriteRegionInformation(ref output, new(place.X, place.Y, bitmap.Width, bitmap.Height));
        return position;
    }

    /// <summary>Writes the flags, adaptive pixels and arithmetic-coded data of a generic region.</summary>
    /// <param name="output">The stream.</param>
    /// <param name="bitmap">The pixels.</param>
    /// <param name="parameters">The coding parameters.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    private static void WriteArithmeticData(ref PooledBuffer output, in Jbig2BitmapView bitmap, in Jbig2GenericParameters parameters, CancellationToken cancellationToken)
    {
        output.WriteByte((byte)((parameters.Template << TemplateShift) | (parameters.TypicalPrediction ? TypicalFlag : 0)));
        WriteAdaptivePixels(ref output, parameters);
        var count = parameters.ContextCount;
        var contexts = ArrayPool<byte>.Shared.Rent(count);
        var encoder = new Jbig2MqEncoder(Math.Max(bitmap.Data.Length / SizeDivisor, MinimumOutput));
        try
        {
            contexts.AsSpan(0, count).Clear();
            Jbig2GenericEncoder.Encode(ref encoder, contexts.AsSpan(0, count), bitmap, parameters, cancellationToken);
            encoder.Finish(ref output);
        }
        catch
        {
            encoder.Dispose();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(contexts);
        }
    }
}
