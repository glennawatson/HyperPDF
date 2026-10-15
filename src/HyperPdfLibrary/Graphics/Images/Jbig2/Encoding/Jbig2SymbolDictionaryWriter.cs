// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>
/// Writes symbol dictionary segments (T.88 sections 6.5 and 7.4.2) with arithmetic coding. The base dictionary codes each
/// symbol bitmap with the generic region procedure; the refined dictionary codes each symbol as a refinement of one base
/// symbol (a refinement/aggregate dictionary holding one instance per symbol). Both export all their own symbols.
/// </summary>
internal static class Jbig2SymbolDictionaryWriter
{
    /// <summary>The shift of the template number in the dictionary flags.</summary>
    private const int TemplateShift = 10;

    /// <summary>The dictionary flag for refinement and aggregate coding.</summary>
    private const int RefinementAggregateFlag = 0x0002;

    /// <summary>The integer coders a base dictionary needs: height delta, width delta and export run.</summary>
    private const int IntegerCoders = 3;

    /// <summary>The index of the height delta coder's contexts (IADH) in a base dictionary.</summary>
    private const int HeightCoder = 0;

    /// <summary>The index of the width delta coder's contexts (IADW) in a base dictionary.</summary>
    private const int WidthCoder = 1;

    /// <summary>The index of the export run coder's contexts (IAEX) in a base dictionary.</summary>
    private const int ExportCoder = 2;

    /// <summary>The divisor of the raw symbol bytes that gives the first size guess for the coded data.</summary>
    private const int SizeDivisor = 4;

    /// <summary>The smallest first size guess.</summary>
    private const int MinimumOutput = 256;

    /// <summary>The integer coders of a refined dictionary: all those the decoder numbers, then the symbol ID contexts.</summary>
    private const int RefinedCoders = 13;

    /// <summary>Writes a symbol dictionary of the base symbols, all exported.</summary>
    /// <param name="output">The stream.</param>
    /// <param name="number">The segment number.</param>
    /// <param name="page">The page association: 0 for a global dictionary.</param>
    /// <param name="symbols">The symbols, in dictionary order.</param>
    /// <param name="template">The generic template, 0 to 3, that codes the symbol bitmaps.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    internal static void Write(ref PooledBuffer output, int number, int page, in Jbig2EncoderSymbols symbols, int template, CancellationToken cancellationToken)
    {
        var position = Jbig2SegmentWriter.BeginSegment(ref output, number, Jbig2SegmentWriter.SymbolDictionaryType, [], page, true);
        var parameters = Jbig2GenericRegionWriter.Nominal(template, false);
        Jbig2SegmentWriter.WriteInt16(ref output, template << TemplateShift);
        Jbig2GenericRegionWriter.WriteAdaptivePixels(ref output, parameters);
        Jbig2SegmentWriter.WriteInt32(ref output, symbols.BaseCount);
        Jbig2SegmentWriter.WriteInt32(ref output, symbols.BaseCount);
        var bitmapContexts = ArrayPool<byte>.Shared.Rent(parameters.ContextCount);
        var integerContexts = ArrayPool<byte>.Shared.Rent(IntegerCoders * Jbig2IntegerDecoder.ContextCount);
        var encoder = new Jbig2MqEncoder(Math.Max(symbols.BaseCount * SizeDivisor, MinimumOutput));
        try
        {
            bitmapContexts.AsSpan(0, parameters.ContextCount).Clear();
            integerContexts.AsSpan(0, IntegerCoders * Jbig2IntegerDecoder.ContextCount).Clear();
            EncodeSymbols(ref encoder, symbols, bitmapContexts, integerContexts, parameters, cancellationToken);
            encoder.Finish(ref output);
        }
        catch
        {
            encoder.Dispose();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bitmapContexts);
            ArrayPool<byte>.Shared.Return(integerContexts);
        }

        Jbig2SegmentWriter.EndSegment(ref output, position);
    }

    /// <summary>Writes a refined dictionary: each variant coded as a refinement of a base symbol from the referred dictionary.</summary>
    /// <param name="output">The stream.</param>
    /// <param name="number">The segment number.</param>
    /// <param name="page">The page association: 0 for a global dictionary.</param>
    /// <param name="symbols">The symbols; the variants after the base symbols are written.</param>
    /// <param name="baseNumber">The segment number of the base dictionary.</param>
    /// <param name="template">The generic template, 0 to 3, written in the flags.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    internal static void WriteRefined(ref PooledBuffer output, int number, int page, in Jbig2EncoderSymbols symbols, int baseNumber, int template, CancellationToken cancellationToken)
    {
        var variants = symbols.Count - symbols.BaseCount;
        var position = Jbig2SegmentWriter.BeginSegment(ref output, number, Jbig2SegmentWriter.SymbolDictionaryType, [baseNumber], page, true);
        var parameters = Jbig2GenericRegionWriter.Nominal(template, false);
        Jbig2SegmentWriter.WriteInt16(ref output, (template << TemplateShift) | RefinementAggregateFlag);
        Jbig2GenericRegionWriter.WriteAdaptivePixels(ref output, parameters);
        Jbig2RefinementEncoder.WriteAdaptivePixels(ref output);
        Jbig2SegmentWriter.WriteInt32(ref output, variants);
        Jbig2SegmentWriter.WriteInt32(ref output, variants);
        var codeLength = Jbig2Bits.CeilLog2(symbols.Count);
        var integerLength = (RefinedCoders * Jbig2IntegerDecoder.ContextCount) + (1 << codeLength);
        var integerContexts = ArrayPool<byte>.Shared.Rent(integerLength);
        var refinementContexts = ArrayPool<byte>.Shared.Rent(Jbig2RefinementEncoder.ContextCount);
        var encoder = new Jbig2MqEncoder(Math.Max(variants * SizeDivisor, MinimumOutput));
        try
        {
            integerContexts.AsSpan(0, integerLength).Clear();
            refinementContexts.AsSpan(0, Jbig2RefinementEncoder.ContextCount).Clear();
            EncodeVariants(ref encoder, symbols, new(integerContexts, refinementContexts, codeLength, true), cancellationToken);
            encoder.Finish(ref output);
        }
        catch
        {
            encoder.Dispose();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(integerContexts);
            ArrayPool<byte>.Shared.Return(refinementContexts);
        }

        Jbig2SegmentWriter.EndSegment(ref output, position);
    }

    /// <summary>Gets the contexts of one integer coder in a buffer laid out like the decoder's.</summary>
    /// <param name="buffer">The contexts of all the integer coders.</param>
    /// <param name="kind">The integer coder.</param>
    /// <returns>The coder's contexts.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Span<byte> Coder(byte[] buffer, Jbig2IntegerKind kind) =>
        buffer.AsSpan((int)kind * Jbig2IntegerDecoder.ContextCount, Jbig2IntegerDecoder.ContextCount);

    /// <summary>Codes the height classes, the symbols and the export flags of the base dictionary.</summary>
    /// <param name="encoder">The arithmetic encoder.</param>
    /// <param name="symbols">The symbols.</param>
    /// <param name="bitmapContexts">The generic region contexts.</param>
    /// <param name="integerContexts">The integer contexts: height delta, width delta, export run.</param>
    /// <param name="parameters">The generic region parameters of the symbol bitmaps.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    private static void EncodeSymbols(
        ref Jbig2MqEncoder encoder,
        in Jbig2EncoderSymbols symbols,
        byte[] bitmapContexts,
        byte[] integerContexts,
        in Jbig2GenericParameters parameters,
        CancellationToken cancellationToken)
    {
        var heightContexts = integerContexts.AsSpan(HeightCoder * Jbig2IntegerDecoder.ContextCount, Jbig2IntegerDecoder.ContextCount);
        var widthContexts = integerContexts.AsSpan(WidthCoder * Jbig2IntegerDecoder.ContextCount, Jbig2IntegerDecoder.ContextCount);
        var exportContexts = integerContexts.AsSpan(ExportCoder * Jbig2IntegerDecoder.ContextCount, Jbig2IntegerDecoder.ContextCount);
        var classHeight = 0;
        var index = 0;
        while (index < symbols.BaseCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var height = symbols.Height(index);
            Jbig2IntegerEncoder.Encode(ref encoder, heightContexts, height - classHeight);
            classHeight = height;
            var previousWidth = 0;
            for (; index < symbols.BaseCount && symbols.Height(index) == height; index++)
            {
                var width = symbols.Width(index);
                Jbig2IntegerEncoder.Encode(ref encoder, widthContexts, width - previousWidth);
                previousWidth = width;
                var bitmap = new Jbig2BitmapView(symbols.Rows(index), width, height);
                Jbig2GenericEncoder.Encode(ref encoder, bitmapContexts, bitmap, parameters, cancellationToken);
            }

            Jbig2IntegerEncoder.EncodeOutOfBand(ref encoder, widthContexts);
        }

        // Export flags (T.88 section 6.5.10): a run of unexported symbols, then a run of exported ones.
        Jbig2IntegerEncoder.Encode(ref encoder, exportContexts, 0);
        Jbig2IntegerEncoder.Encode(ref encoder, exportContexts, symbols.BaseCount);
    }

    /// <summary>Codes the variants, each as a refinement of its base symbol (T.88 section 6.5.8.2.2), and the export flags.</summary>
    /// <param name="encoder">The arithmetic encoder.</param>
    /// <param name="symbols">The symbols.</param>
    /// <param name="state">The integer contexts followed by the symbol ID contexts, the refinement contexts and the symbol ID length.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    private static void EncodeVariants(ref Jbig2MqEncoder encoder, in Jbig2EncoderSymbols symbols, in Jbig2TextCoding state, CancellationToken cancellationToken)
    {
        var integerContexts = state.Contexts;
        var classHeight = 0;
        var index = symbols.BaseCount;
        while (index < symbols.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var height = symbols.Height(index);
            Jbig2IntegerEncoder.Encode(ref encoder, Coder(integerContexts, Jbig2IntegerKind.HeightDelta), height - classHeight);
            classHeight = height;
            var previousWidth = 0;
            for (; index < symbols.Count && symbols.Height(index) == height; index++)
            {
                var width = symbols.Width(index);
                Jbig2IntegerEncoder.Encode(ref encoder, Coder(integerContexts, Jbig2IntegerKind.WidthDelta), width - previousWidth);
                previousWidth = width;
                EncodeVariant(ref encoder, symbols, index, state, cancellationToken);
            }

            Jbig2IntegerEncoder.EncodeOutOfBand(ref encoder, Coder(integerContexts, Jbig2IntegerKind.WidthDelta));
        }

        var exportContexts = Coder(integerContexts, Jbig2IntegerKind.ExportRun);
        Jbig2IntegerEncoder.Encode(ref encoder, exportContexts, symbols.BaseCount);
        Jbig2IntegerEncoder.Encode(ref encoder, exportContexts, symbols.Count - symbols.BaseCount);
    }

    /// <summary>Codes one variant: one instance, which symbol it refines, the offset and the refined bitmap.</summary>
    /// <param name="encoder">The arithmetic encoder.</param>
    /// <param name="symbols">The symbols.</param>
    /// <param name="index">The variant's dictionary position.</param>
    /// <param name="state">The contexts.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    private static void EncodeVariant(ref Jbig2MqEncoder encoder, in Jbig2EncoderSymbols symbols, int index, in Jbig2TextCoding state, CancellationToken cancellationToken)
    {
        var integerContexts = state.Contexts;
        var codeLength = state.CodeLength;
        var idContexts = integerContexts.AsSpan(RefinedCoders * Jbig2IntegerDecoder.ContextCount, 1 << codeLength);
        var reference = symbols.ReferenceOf(index);
        var bitmap = new Jbig2BitmapView(symbols.Rows(index), symbols.Width(index), symbols.Height(index));
        var source = new Jbig2BitmapView(symbols.Rows(reference), symbols.Width(reference), symbols.Height(reference));

        // The reference sits with its bottom left corner under the variant's, so the offset is the height difference.
        var offset = new Jbig2Point(0, bitmap.Height - source.Height);
        Jbig2IntegerEncoder.Encode(ref encoder, Coder(integerContexts, Jbig2IntegerKind.AggregateCount), 1);
        Jbig2IntegerEncoder.EncodeId(ref encoder, idContexts, codeLength, reference);
        Jbig2IntegerEncoder.Encode(ref encoder, Coder(integerContexts, Jbig2IntegerKind.RefineX), offset.X);
        Jbig2IntegerEncoder.Encode(ref encoder, Coder(integerContexts, Jbig2IntegerKind.RefineY), offset.Y);
        Jbig2RefinementEncoder.Encode(ref encoder, state.RefinementContexts, bitmap, source, offset, cancellationToken);
    }
}
