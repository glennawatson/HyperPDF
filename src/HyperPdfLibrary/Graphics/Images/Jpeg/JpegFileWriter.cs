// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>Writes the headers and scans of an encoded JPEG from the quantized coefficients of the components.</summary>
internal static class JpegFileWriter
{
    /// <summary>The largest restart interval.</summary>
    private const int MaxRestartInterval = ushort.MaxValue;

    /// <summary>The DC table slot of the chrominance components.</summary>
    private const int ChrominanceDc = 1;

    /// <summary>The AC table slot of the chrominance components.</summary>
    private const int ChrominanceAc = JpegHuffmanBuilder.FirstAcSlot + 1;

    /// <summary>Writes a whole JPEG.</summary>
    /// <param name="output">Receives the bytes.</param>
    /// <param name="geometry">The frame layout.</param>
    /// <param name="state">The encoder state that holds the coefficients and tables.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels between scans and MCUs.</param>
    internal static void Write(ref PooledBuffer output, in JpegFrameGeometry geometry, JpegEncoderState state, in JpegEncodeRequest request, CancellationToken cancellationToken)
    {
        var options = request.Options;
        var components = state.Components.AsSpan(0, geometry.Components);
        var restartInterval = Math.Clamp(options.RestartInterval, 0, MaxRestartInterval);
        JpegHeaderWriter.WriteStartOfImage(ref output);
        JpegHeaderWriter.WriteApplication(ref output, geometry.Kind);
        var quantSlots = geometry.Kind is JpegColorKind.YCbCr or JpegColorKind.Ycck ? JpegEncoderState.QuantSlots : 1;
        JpegHeaderWriter.WriteQuantization(ref output, state.QuantSteps, quantSlots);
        JpegHeaderWriter.WriteFrame(ref output, geometry, components, options.Progressive);
        if (restartInterval != 0)
        {
            JpegHeaderWriter.WriteRestartInterval(ref output, restartInterval);
        }

        Span<JpegScanSpec> scans = stackalloc JpegScanSpec[JpegScanScript.MaxScans];
        var scanCount = JpegScanScript.Build(geometry.Components, options.Progressive, !request.NonInterleaved, scans);
        var optimize = options.OptimizeHuffman || options.Progressive;
        if (!optimize)
        {
            SetExampleTables(state, geometry.Kind);
            JpegHeaderWriter.WriteHuffman(ref output, state);
        }

        for (var i = 0; i < scanCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteScan(ref output, geometry, state, scans[i], restartInterval, optimize, cancellationToken);
        }

        JpegHeaderWriter.WriteEndOfImage(ref output);
    }

    /// <summary>Writes one scan: its tables when they are built for it, its header and its entropy-coded data.</summary>
    /// <param name="output">Receives the bytes.</param>
    /// <param name="geometry">The frame layout.</param>
    /// <param name="state">The encoder state.</param>
    /// <param name="spec">The scan.</param>
    /// <param name="restartInterval">The MCUs between restart markers, or zero.</param>
    /// <param name="optimize">Whether to build the tables from the scan's symbol counts.</param>
    /// <param name="cancellationToken">Cancels between MCUs.</param>
    private static void WriteScan(
        ref PooledBuffer output,
        in JpegFrameGeometry geometry,
        JpegEncoderState state,
        in JpegScanSpec spec,
        int restartInterval,
        bool optimize,
        CancellationToken cancellationToken)
    {
        var components = state.Components.AsSpan(0, geometry.Components);
        if (optimize)
        {
            state.ClearFrequencies();
            var counter = new JpegSymbolCounter(state.Frequencies);
            RunScan(ref counter, state, geometry, spec, restartInterval, cancellationToken);
            state.BuildOptimalTables();
            JpegHeaderWriter.WriteHuffman(ref output, state);
        }

        JpegHeaderWriter.WriteScan(ref output, components, spec);
        var writer = new JpegBitWriter(output, state.Codes);
        try
        {
            RunScan(ref writer, state, geometry, spec, restartInterval, cancellationToken);
        }
        finally
        {
            // The writer worked on a copy of the buffer and may have grown it; take the current one back, even after a cancel.
            output = writer.Output;
        }
    }

    /// <summary>Runs the scan routine of the scan's kind against a receiver.</summary>
    /// <typeparam name="TSink">The receiver of the symbols.</typeparam>
    /// <param name="sink">The receiver.</param>
    /// <param name="state">The encoder state.</param>
    /// <param name="geometry">The frame layout.</param>
    /// <param name="spec">The scan.</param>
    /// <param name="restartInterval">The MCUs between restart markers, or zero.</param>
    /// <param name="cancellationToken">Cancels between MCUs.</param>
    private static void RunScan<TSink>(ref TSink sink, JpegEncoderState state, in JpegFrameGeometry geometry, in JpegScanSpec spec, int restartInterval, CancellationToken cancellationToken)
        where TSink : IJpegEntropySink, allows ref struct
    {
        var components = state.Components.AsSpan(0, geometry.Components);
        if (spec.Progressive)
        {
            JpegProgressiveScan.Encode(ref sink, components, spec, geometry, restartInterval, state.CorrectionBits, cancellationToken);
            return;
        }

        JpegSequentialScan.Encode(ref sink, components, spec, geometry, restartInterval, cancellationToken);
    }

    /// <summary>Loads the example tables of Annex K.3 for the slots the colour model uses.</summary>
    /// <param name="state">The encoder state.</param>
    /// <param name="kind">The colour model.</param>
    private static void SetExampleTables(JpegEncoderState state, JpegColorKind kind)
    {
        state.ClearTables();
        state.SetTable(0, JpegHuffmanBuilder.DcLuminanceCounts, JpegHuffmanBuilder.DcValues);
        state.SetTable(JpegHuffmanBuilder.FirstAcSlot, JpegHuffmanBuilder.AcLuminanceCounts, JpegHuffmanBuilder.AcLuminanceValues);
        if (kind is not (JpegColorKind.YCbCr or JpegColorKind.Ycck))
        {
            return;
        }

        state.SetTable(ChrominanceDc, JpegHuffmanBuilder.DcChrominanceCounts, JpegHuffmanBuilder.DcValues);
        state.SetTable(ChrominanceAc, JpegHuffmanBuilder.AcChrominanceCounts, JpegHuffmanBuilder.AcChrominanceValues);
    }
}
