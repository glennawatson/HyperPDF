// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>
/// Writes an immediate text region segment (T.88 sections 6.4 and 7.4.3) with arithmetic coding. Symbols are placed by their
/// bottom left corner in strips of eight rows, so the symbols on one line of text share a strip. An instance that is only a
/// near copy of its symbol is coded as a refinement of it.
/// </summary>
internal static class Jbig2TextRegionWriter
{
    /// <summary>The log base 2 of the strip height.</summary>
    internal const int LogStripHeight = 3;

    /// <summary>The integer coders of a text region with refinement, numbered as the decoder numbers them.</summary>
    private const int IntegerCoders = 9;

    /// <summary>The shift of the strip size in the text region flags.</summary>
    private const int StripShift = 2;

    /// <summary>The text region flag for refinement.</summary>
    private const int RefineFlag = 0x0002;

    /// <summary>The divisor of the instance count that gives the first size guess, in bytes per instance.</summary>
    private const int BytesPerInstance = 2;

    /// <summary>The smallest first size guess.</summary>
    private const int MinimumOutput = 256;

    /// <summary>Writes a text region.</summary>
    /// <param name="output">The stream.</param>
    /// <param name="number">The segment number.</param>
    /// <param name="referred">The segment numbers of the symbol dictionaries the region uses, at most four.</param>
    /// <param name="placement">The region's size and place, the symbol count, whether the page is exact and what refined instances use.</param>
    /// <param name="instances">The symbols to place, in strip order and left to right within a strip.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    internal static void Write(
        ref PooledBuffer output,
        int number,
        scoped ReadOnlySpan<int> referred,
        in Jbig2TextPlacement placement,
        ReadOnlySpan<Jbig2TextInstance> instances,
        CancellationToken cancellationToken)
    {
        var refine = HasRefinement(instances);
        var type = placement.IsLossless ? Jbig2SegmentWriter.ImmediateLosslessTextType : Jbig2SegmentWriter.ImmediateTextType;
        var position = Jbig2SegmentWriter.BeginSegment(ref output, number, type, referred, 1, false);
        Jbig2SegmentWriter.WriteRegionInformation(ref output, placement.Region);
        Jbig2SegmentWriter.WriteInt16(ref output, (LogStripHeight << StripShift) | (refine ? RefineFlag : 0));
        if (refine)
        {
            Jbig2RefinementEncoder.WriteAdaptivePixels(ref output);
        }

        Jbig2SegmentWriter.WriteInt32(ref output, instances.Length);
        var codeLength = Jbig2Bits.CeilLog2(placement.SymbolCount);
        var contextCount = (IntegerCoders * Jbig2IntegerDecoder.ContextCount) + (1 << codeLength);
        var contexts = ArrayPool<byte>.Shared.Rent(contextCount);
        var refinementContexts = ArrayPool<byte>.Shared.Rent(Jbig2RefinementEncoder.ContextCount);
        var encoder = new Jbig2MqEncoder(Math.Max(instances.Length * BytesPerInstance, MinimumOutput));
        try
        {
            contexts.AsSpan(0, contextCount).Clear();
            refinementContexts.AsSpan(0, Jbig2RefinementEncoder.ContextCount).Clear();
            EncodeStrips(ref encoder, new(contexts, refinementContexts, codeLength, refine), placement, instances, cancellationToken);
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
            ArrayPool<byte>.Shared.Return(refinementContexts);
        }

        Jbig2SegmentWriter.EndSegment(ref output, position);
    }

    /// <summary>Gets the strip a row falls in.</summary>
    /// <param name="bottom">The row of a symbol's bottom edge.</param>
    /// <returns>The strip number.</returns>
    internal static int StripOf(int bottom) => bottom >> LogStripHeight;

    /// <summary>Determines whether any instance is refined.</summary>
    /// <param name="instances">The instances.</param>
    /// <returns><see langword="true"/> when one is.</returns>
    private static bool HasRefinement(ReadOnlySpan<Jbig2TextInstance> instances)
    {
        foreach (var instance in instances)
        {
            if (instance.Component >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the contexts of one integer coder.</summary>
    /// <param name="contexts">The contexts of all the coders.</param>
    /// <param name="kind">The coder.</param>
    /// <returns>The coder's contexts.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Span<byte> Coder(byte[] contexts, Jbig2IntegerKind kind) =>
        contexts.AsSpan((int)kind * Jbig2IntegerDecoder.ContextCount, Jbig2IntegerDecoder.ContextCount);

    /// <summary>Codes the strips of a text region (T.88 section 6.4.5).</summary>
    /// <param name="encoder">The arithmetic encoder.</param>
    /// <param name="state">The contexts and the symbol ID length.</param>
    /// <param name="placement">The region placement.</param>
    /// <param name="instances">The instances in strip order.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    private static void EncodeStrips(
        ref Jbig2MqEncoder encoder,
        in Jbig2TextCoding state,
        in Jbig2TextPlacement placement,
        ReadOnlySpan<Jbig2TextInstance> instances,
        CancellationToken cancellationToken)
    {
        var contexts = state.Contexts;

        // The initial strip position is zero.
        Jbig2IntegerEncoder.Encode(ref encoder, Coder(contexts, Jbig2IntegerKind.StripDelta), 0);
        var strip = 0;
        var firstS = 0;
        var index = 0;
        while (index < instances.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = StripOf(instances[index].Bottom);
            Jbig2IntegerEncoder.Encode(ref encoder, Coder(contexts, Jbig2IntegerKind.StripDelta), current - strip);
            strip = current;
            Jbig2IntegerEncoder.Encode(ref encoder, Coder(contexts, Jbig2IntegerKind.FirstS), instances[index].Left - firstS);
            firstS = instances[index].Left;
            var cursor = firstS;
            var first = true;
            for (; index < instances.Length && StripOf(instances[index].Bottom) == current; index++)
            {
                var instance = instances[index];
                if (!first)
                {
                    Jbig2IntegerEncoder.Encode(ref encoder, Coder(contexts, Jbig2IntegerKind.SDelta), instance.Left - cursor);
                }

                first = false;
                Jbig2IntegerEncoder.Encode(ref encoder, Coder(contexts, Jbig2IntegerKind.InstanceT), instance.Bottom - (current << LogStripHeight));
                var idContexts = contexts.AsSpan(IntegerCoders * Jbig2IntegerDecoder.ContextCount, 1 << state.CodeLength);
                Jbig2IntegerEncoder.EncodeId(ref encoder, idContexts, state.CodeLength, instance.Rank);
                if (state.Refine)
                {
                    EncodeRefinement(ref encoder, state, placement, instance, cancellationToken);
                }

                // After a symbol placed by its bottom left corner, S moves to the symbol's last column.
                cursor = instance.Left + instance.Width - 1;
            }

            Jbig2IntegerEncoder.EncodeOutOfBand(ref encoder, Coder(contexts, Jbig2IntegerKind.SDelta));
        }
    }

    /// <summary>Codes the refinement flag of an instance and, when it is set, the size change, offset and refined bitmap (T.88 section 6.4.11).</summary>
    /// <param name="encoder">The arithmetic encoder.</param>
    /// <param name="state">The contexts.</param>
    /// <param name="placement">The region placement, which holds the symbols and components.</param>
    /// <param name="instance">The instance.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    private static void EncodeRefinement(
        ref Jbig2MqEncoder encoder,
        in Jbig2TextCoding state,
        in Jbig2TextPlacement placement,
        Jbig2TextInstance instance,
        CancellationToken cancellationToken)
    {
        var contexts = state.Contexts;
        var refined = instance.Component >= 0;
        Jbig2IntegerEncoder.Encode(ref encoder, Coder(contexts, Jbig2IntegerKind.Refine), refined ? 1 : 0);
        if (!refined)
        {
            return;
        }

        var components = placement.Components;
        var symbols = placement.Symbols;
        var source = new Jbig2BitmapView(symbols.Rows(instance.Rank), symbols.Width(instance.Rank), symbols.Height(instance.Rank));
        var bitmap = new Jbig2BitmapView(components.Rows(instance.Component), components.Width(instance.Component), components.Height(instance.Component));
        var deltaWidth = bitmap.Width - source.Width;
        var deltaHeight = bitmap.Height - source.Height;

        // The reference offset is half the size change plus the coded offset; aim for the bottom left corners together.
        Jbig2IntegerEncoder.Encode(ref encoder, Coder(contexts, Jbig2IntegerKind.RefineWidth), deltaWidth);
        Jbig2IntegerEncoder.Encode(ref encoder, Coder(contexts, Jbig2IntegerKind.RefineHeight), deltaHeight);
        Jbig2IntegerEncoder.Encode(ref encoder, Coder(contexts, Jbig2IntegerKind.RefineX), -(deltaWidth >> 1));
        Jbig2IntegerEncoder.Encode(ref encoder, Coder(contexts, Jbig2IntegerKind.RefineY), deltaHeight - (deltaHeight >> 1));
        Jbig2RefinementEncoder.Encode(ref encoder, state.RefinementContexts, bitmap, source, new(0, deltaHeight), cancellationToken);
    }
}
