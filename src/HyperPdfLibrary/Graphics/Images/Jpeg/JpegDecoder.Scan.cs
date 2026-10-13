// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <content>Scan decoding, reconstruction and output.</content>
internal ref partial struct JpegDecoder
{
    /// <summary>The last zigzag index.</summary>
    private const int LastIndex = JpegBlock.Length - 1;

    /// <summary>The largest successive-approximation bit position.</summary>
    private const int MaxLow = 13;

    /// <summary>The bytes of one component entry in a scan header: the id, then the two table ids.</summary>
    private const int ScanEntryBytes = 2;

    /// <summary>The offset of the first component entry in a scan header, after the component count.</summary>
    private const int ScanEntriesOffset = 1;

    /// <summary>The bytes after the component entries: spectral start, spectral end and approximation.</summary>
    private const int ScanTailBytes = 3;

    /// <summary>The offset of the spectral end in the scan tail.</summary>
    private const int EndOffset = 1;

    /// <summary>The offset of the approximation byte in the scan tail.</summary>
    private const int ApproximationOffset = 2;

    /// <summary>Masks a unit number to the units decoded between checks of the cancellation token (1,024).</summary>
    private const int CancelCheckMask = 1023;

    /// <summary>The ratio of the common chroma upsampling case.</summary>
    private const int DoubleSampling = 2;

    /// <summary>Writes the samples of every pixel, interleaved.</summary>
    /// <param name="output">Receives width * height * components bytes.</param>
    /// <param name="convertColor">Whether to convert YCbCr to RGB or YCCK to CMYK.</param>
    /// <returns><see langword="false"/> when the buffer is too small or nothing was decoded.</returns>
    internal readonly bool Write(Span<byte> output, bool convertColor)
    {
        var components = Components;
        if (components.IsEmpty)
        {
            return false;
        }

        var rowBytes = _width * components.Length;
        if (output.Length < (long)rowBytes * _height)
        {
            return false;
        }

        var pool = _scratchPools.Get<byte>();
        var rows = pool.Rent(rowBytes);
        try
        {
            for (var y = 0; y < _height; y++)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                for (var c = 0; c < components.Length; c++)
                {
                    BuildRow(components[c], y, rows.AsSpan(c * _width, _width));
                }

                JpegColor.Interleave(rows, components.Length, _width, convertColor, output.Slice(y * rowBytes, rowBytes));
            }

            return true;
        }
        finally
        {
            pool.Return(rows);
        }
    }

    /// <summary>Finds a component by id.</summary>
    /// <param name="components">The frame's components.</param>
    /// <param name="id">The component id.</param>
    /// <returns>The index, or -1.</returns>
    private static int FindComponent(ReadOnlySpan<JpegComponent> components, int id)
    {
        for (var i = 0; i < components.Length; i++)
        {
            if (components[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Looks a Huffman table up by id.</summary>
    /// <param name="tables">The tables.</param>
    /// <param name="id">The table id.</param>
    /// <returns>The table, or the empty table when the id is unknown.</returns>
    private static JpegHuffmanTable FindTable(JpegHuffmanTable[] tables, int id) =>
        (uint)id < (uint)tables.Length && tables[id].IsDefined ? tables[id] : JpegHuffmanTable.Empty;

    /// <summary>Chooses the scan mode of a progressive scan.</summary>
    /// <param name="start">The first zigzag index.</param>
    /// <param name="firstPass">Whether this is the first pass over the band.</param>
    /// <returns>The mode.</returns>
    private static JpegScanMode SelectMode(int start, bool firstPass)
    {
        if (start == 0)
        {
            return firstPass ? JpegScanMode.DcFirst : JpegScanMode.DcRefine;
        }

        return firstPass ? JpegScanMode.AcFirst : JpegScanMode.AcRefine;
    }

    /// <summary>Decodes one block for the scan's mode.</summary>
    /// <param name="reader">The bit reader.</param>
    /// <param name="component">The component.</param>
    /// <param name="row">The block row.</param>
    /// <param name="column">The block column.</param>
    /// <param name="scan">The scan parameters.</param>
    /// <param name="endOfBandRun">The end-of-band run, updated.</param>
    private static void DecodeBlock(ref JpegBitReader reader, JpegComponent component, int row, int column, in JpegScan scan, ref int endOfBandRun)
    {
        var block = component.GetBlock(row, column);
        var predictor = component.DcPredictor;
        switch (scan.Mode)
        {
            case JpegScanMode.Sequential:
            {
                JpegBlockDecoder.DecodeSequential(ref reader, component.DcTable, component.AcTable, ref predictor, block);
                break;
            }

            case JpegScanMode.DcFirst:
            {
                JpegBlockDecoder.DecodeDcFirst(ref reader, component.DcTable, ref predictor, block, scan.Low);
                break;
            }

            case JpegScanMode.DcRefine:
            {
                JpegBlockDecoder.DecodeDcRefine(ref reader, block, scan.Low);
                break;
            }

            case JpegScanMode.AcFirst:
            {
                JpegBlockDecoder.DecodeAcFirst(ref reader, component.AcTable, block, scan.Start, scan.End, scan.Low, ref endOfBandRun);
                break;
            }

            default:
            {
                JpegBlockDecoder.DecodeAcRefine(ref reader, component.AcTable, block, scan.Start, scan.End, scan.Low, ref endOfBandRun);
                break;
            }
        }

        component.DcPredictor = predictor;
    }

    /// <summary>Reads a scan header and decodes the entropy-coded data that follows it.</summary>
    /// <param name="segment">The scan header payload.</param>
    /// <returns>What to do next.</returns>
    private Step ReadScan(scoped ReadOnlySpan<byte> segment)
    {
        var components = Components;
        if (components.IsEmpty || segment.IsEmpty)
        {
            return Step.Fail;
        }

        var count = segment[0];
        var tail = ScanEntriesOffset + (ScanEntryBytes * count);
        Span<int> indices = stackalloc int[MaxComponents];
        if (count < 1 || count > components.Length || segment.Length < tail + ScanTailBytes)
        {
            return Step.Fail;
        }

        if (!SelectComponents(segment, count, indices) || !TryReadScanParameters(segment, tail, count, out var scan))
        {
            return Step.Fail;
        }

        var reader = new JpegBitReader(_data, _position);
        DecodeScan(ref reader, indices[..count], in scan);
        _position = reader.Position;
        _scanCount++;
        return Step.Continue;
    }

    /// <summary>Finds the scan's components and gives each its Huffman tables and quantizer.</summary>
    /// <param name="segment">The scan header payload.</param>
    /// <param name="count">The components in the scan.</param>
    /// <param name="indices">Receives the index of each component in the frame.</param>
    /// <returns><see langword="false"/> when a component id is not in the frame.</returns>
    private readonly bool SelectComponents(scoped ReadOnlySpan<byte> segment, int count, scoped Span<int> indices)
    {
        var components = Components;
        for (var i = 0; i < count; i++)
        {
            var entry = ScanEntriesOffset + (ScanEntryBytes * i);
            var index = FindComponent(components, segment[entry]);
            if (index < 0)
            {
                return false;
            }

            var component = components[index];
            var tables = segment[entry + 1];
            component.DcTable = FindTable(_state.DcTables, tables >> NibbleBits);
            component.AcTable = FindTable(_state.AcTables, tables & NibbleMask);

            // A quantization table id beyond the four slots names no table, so the component gets none.
            component.LatchQuant((uint)component.QuantId < JpegState.TableSlots && _state.QuantizationDefined[component.QuantId] ? _state.Quantization[component.QuantId] : null);
            indices[i] = index;
        }

        return true;
    }

    /// <summary>Reads the spectral band and approximation fields of a scan header.</summary>
    /// <param name="segment">The scan header payload.</param>
    /// <param name="tail">The offset of the fields.</param>
    /// <param name="count">The components in the scan.</param>
    /// <param name="scan">Receives the scan parameters.</param>
    /// <returns><see langword="false"/> when the fields are invalid.</returns>
    private readonly bool TryReadScanParameters(scoped ReadOnlySpan<byte> segment, int tail, int count, out JpegScan scan)
    {
        scan = new(JpegScanMode.Sequential, 0, LastIndex, 0);
        if (!_progressive)
        {
            return true;
        }

        int start = segment[tail];
        var end = Math.Min((int)segment[tail + EndOffset], LastIndex);
        var high = segment[tail + ApproximationOffset] >> NibbleBits;
        var low = segment[tail + ApproximationOffset] & NibbleMask;
        if (start > end || low > MaxLow || (start != 0 && count != 1))
        {
            return false;
        }

        scan = new(SelectMode(start, high == 0), start, start == 0 ? 0 : end, low);
        return true;
    }

    /// <summary>Decodes the MCUs of one scan, honouring restart markers, and stops at damage or the end of the data.</summary>
    /// <param name="reader">The bit reader at the start of the entropy-coded data.</param>
    /// <param name="indices">The scan's components, as indices into the frame.</param>
    /// <param name="scan">The scan parameters.</param>
    private readonly void DecodeScan(ref JpegBitReader reader, scoped ReadOnlySpan<int> indices, in JpegScan scan)
    {
        var first = Components[indices[0]];
        var total = indices.Length == 1 ? first.BlocksPerLine * first.BlocksPerColumn : _mcusPerLine * _mcusPerColumn;
        var endOfBandRun = 0;
        var untilRestart = _restartInterval;
        ResetPredictors(indices);
        for (var unit = 0; unit < total; unit++)
        {
            if ((unit & CancelCheckMask) == 0)
            {
                _cancellationToken.ThrowIfCancellationRequested();
            }

            if (_restartInterval != 0 && untilRestart == 0)
            {
                if (!reader.TryRestart())
                {
                    break;
                }

                ResetPredictors(indices);
                endOfBandRun = 0;
                untilRestart = _restartInterval;
            }

            DecodeUnit(ref reader, indices, in scan, unit, ref endOfBandRun);
            untilRestart--;
            if (reader.Failed || reader.Overrun)
            {
                break;
            }
        }
    }

    /// <summary>Resets the DC predictors of the scan's components.</summary>
    /// <param name="indices">The scan's components.</param>
    private readonly void ResetPredictors(scoped ReadOnlySpan<int> indices)
    {
        foreach (var index in indices)
        {
            _state.Components[index].DcPredictor = 0;
        }
    }

    /// <summary>Decodes one MCU, or for a single-component scan one block.</summary>
    /// <param name="reader">The bit reader.</param>
    /// <param name="indices">The scan's components.</param>
    /// <param name="scan">The scan parameters.</param>
    /// <param name="unit">The MCU or block number in the scan.</param>
    /// <param name="endOfBandRun">The end-of-band run, updated.</param>
    private readonly void DecodeUnit(ref JpegBitReader reader, scoped ReadOnlySpan<int> indices, in JpegScan scan, int unit, ref int endOfBandRun)
    {
        var components = Components;
        if (indices.Length == 1)
        {
            var single = components[indices[0]];
            DecodeBlock(ref reader, single, unit / single.BlocksPerLine, unit % single.BlocksPerLine, in scan, ref endOfBandRun);
            return;
        }

        var mcuRow = unit / _mcusPerLine;
        var mcuColumn = unit % _mcusPerLine;
        foreach (var index in indices)
        {
            var component = components[index];
            for (var v = 0; v < component.VerticalSampling; v++)
            {
                for (var h = 0; h < component.HorizontalSampling; h++)
                {
                    DecodeBlock(ref reader, component, (mcuRow * component.VerticalSampling) + v, (mcuColumn * component.HorizontalSampling) + h, in scan, ref endOfBandRun);
                }
            }
        }
    }

    /// <summary>Runs the inverse DCT of every component.</summary>
    /// <returns><see langword="false"/> when a component has no quantization table.</returns>
    private readonly bool Reconstruct()
    {
        var components = Components;
        if (components.IsEmpty)
        {
            return false;
        }

        Span<int> work = stackalloc int[JpegBlock.Length];
        foreach (var component in components)
        {
            if (!component.Reconstruct(work))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Upsamples one row of a component to the image width.</summary>
    /// <param name="component">The component.</param>
    /// <param name="y">The image row.</param>
    /// <param name="destination">Receives the row.</param>
    private readonly void BuildRow(JpegComponent component, int y, Span<byte> destination)
    {
        var sourceRow = component.VerticalSampling == _maxVertical ? y : y * component.VerticalSampling / _maxVertical;
        var source = component.Plane.Slice(sourceRow * component.Stride, component.Stride);
        if (component.HorizontalSampling == _maxHorizontal)
        {
            source[..destination.Length].CopyTo(destination);
        }
        else if (_maxHorizontal == DoubleSampling * component.HorizontalSampling)
        {
            for (var x = 0; x < destination.Length; x++)
            {
                destination[x] = source[x >> 1];
            }
        }
        else
        {
            for (var x = 0; x < destination.Length; x++)
            {
                destination[x] = source[x * component.HorizontalSampling / _maxHorizontal];
            }
        }
    }
}
