// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>
/// A managed decoder for 8-bit sequential and progressive Huffman JPEGs of one to four components. It returns the
/// component samples as the file holds them, interleaved, with an optional YCbCr-to-RGB or YCCK-to-CMYK conversion.
/// It applies no Adobe inversion and no colour-space rules: the PDF colour pipeline and /Decode array do that.
/// A damaged or truncated scan keeps the image decoded so far.
/// </summary>
internal ref partial struct JpegDecoder
{
    /// <summary>The Huffman and quantization table slots.</summary>
    private const int TableSlots = 4;

    /// <summary>The most components in a frame.</summary>
    private const int MaxComponents = 4;

    /// <summary>The largest sampling factor.</summary>
    private const int MaxSampling = 4;

    /// <summary>The most coefficients one component may need.</summary>
    private const long MaxCoefficients = 1L << 29;

    /// <summary>The sample precision the decoder reads.</summary>
    private const int Precision = 8;

    /// <summary>The offset of the height in the frame payload.</summary>
    private const int HeightOffset = 1;

    /// <summary>The offset of the width in the frame payload.</summary>
    private const int WidthOffset = 3;

    /// <summary>The offset of the component count in the frame payload.</summary>
    private const int CountOffset = 5;

    /// <summary>The offset of the first component in the frame payload.</summary>
    private const int FirstComponentOffset = 6;

    /// <summary>The bytes of a component in the frame payload.</summary>
    private const int ComponentBytes = 3;

    /// <summary>The offset of the quantization table id within a component entry.</summary>
    private const int QuantOffset = 2;

    /// <summary>The bits that hold the low nibble of a packed byte.</summary>
    private const int NibbleMask = 0x0F;

    /// <summary>The bits in a nibble.</summary>
    private const int NibbleBits = 4;

    /// <summary>The bytes of a 16-bit table entry.</summary>
    private const int WideBytes = 2;

    /// <summary>The bytes before the symbols in a Huffman table definition: the class and id byte and sixteen counts.</summary>
    private const int HuffmanHeaderBytes = 1 + JpegHuffmanTable.LengthCounts;

    /// <summary>The table class of AC tables.</summary>
    private const int AcClass = 1;

    /// <summary>The data being decoded.</summary>
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>The token for this image decode.</summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>The DC Huffman tables by id.</summary>
    private readonly JpegHuffmanTable?[] _codesDc;

    /// <summary>The AC Huffman tables by id.</summary>
    private readonly JpegHuffmanTable?[] _codesAc;

    /// <summary>The quantizer steps by id, in storage order; a redefinition replaces the array.</summary>
    private readonly int[]?[] _quantTables;

    /// <summary>The position in <see cref="_data"/>.</summary>
    private int _position;

    /// <summary>The frame's components, or <see langword="null"/> before the frame header.</summary>
    private JpegComponent[]? _components;

    /// <summary>The restart interval in MCUs, or zero.</summary>
    private int _restartInterval;

    /// <summary>The image width.</summary>
    private int _width;

    /// <summary>The image height.</summary>
    private int _height;

    /// <summary>The largest horizontal sampling factor.</summary>
    private int _maxHorizontal;

    /// <summary>The largest vertical sampling factor.</summary>
    private int _maxVertical;

    /// <summary>The MCUs across the image.</summary>
    private int _mcusPerLine;

    /// <summary>The MCUs down the image.</summary>
    private int _mcusPerColumn;

    /// <summary>Whether the frame is progressive.</summary>
    private bool _progressive;

    /// <summary>The scans decoded so far.</summary>
    private int _scanCount;

    /// <summary>Initializes a new instance of the <see cref="JpegDecoder"/> struct.</summary>
    /// <param name="data">The JPEG data.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    internal JpegDecoder(ReadOnlySpan<byte> data, CancellationToken cancellationToken = default)
    {
        _data = data;
        _cancellationToken = cancellationToken;
        _codesDc = new JpegHuffmanTable?[TableSlots];
        _codesAc = new JpegHuffmanTable?[TableSlots];
        _quantTables = new int[]?[TableSlots];
    }

    /// <summary>What to do after a marker segment.</summary>
    private enum Step
    {
        /// <summary>Read the next marker.</summary>
        Continue = 0,

        /// <summary>The image ended.</summary>
        Stop = 1,

        /// <summary>The data cannot be decoded.</summary>
        Fail = 2,
    }

    /// <summary>Decodes a JPEG to interleaved component samples.</summary>
    /// <param name="data">The JPEG data.</param>
    /// <param name="convertColor">Whether to convert YCbCr to RGB (three components) or YCCK to CMYK (four).</param>
    /// <param name="output">Receives width * height * components bytes.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns><see langword="true"/> when at least one scan decoded and <paramref name="output"/> was filled.</returns>
    internal static bool TryDecode(ReadOnlySpan<byte> data, bool convertColor, Span<byte> output, CancellationToken cancellationToken = default)
    {
        // Not a using declaration: that would make the decoder read-only and the calls below would act on a copy.
        var decoder = new JpegDecoder(data, cancellationToken);
        try
        {
            return decoder.Decode() && decoder.Write(output, convertColor);
        }
        finally
        {
            decoder.Dispose();
        }
    }

    /// <summary>Returns the pooled buffers.</summary>
    internal void Dispose()
    {
        if (_components is null)
        {
            return;
        }

        foreach (var component in _components)
        {
            component.Dispose();
        }

        _components = null;
    }

    /// <summary>Maps a success flag to a step.</summary>
    /// <param name="succeeded">Whether the segment was read.</param>
    /// <returns><see cref="Step.Continue"/> or <see cref="Step.Fail"/>.</returns>
    private static Step Succeeded(bool succeeded) => succeeded ? Step.Continue : Step.Fail;

    /// <summary>Divides and rounds up.</summary>
    /// <param name="value">The dividend.</param>
    /// <param name="divisor">The divisor.</param>
    /// <returns>The rounded-up quotient.</returns>
    private static int CeilingDivide(int value, int divisor) => (value + divisor - 1) / divisor;

    /// <summary>Checks that a frame is one this decoder reads and its payload holds the components it declares.</summary>
    /// <param name="marker">The frame marker.</param>
    /// <param name="segment">The payload.</param>
    /// <param name="count">The declared component count.</param>
    /// <returns><see langword="true"/> for an 8-bit Huffman frame with a valid component count.</returns>
    private static bool IsReadableFrame(byte marker, ReadOnlySpan<byte> segment, int count)
    {
        var huffman = marker is JpegMarkers.BaselineFrame or JpegMarkers.ExtendedFrame or JpegMarkers.ProgressiveFrame;
        return huffman
            && count is >= 1 and <= MaxComponents
            && segment.Length >= FirstComponentOffset + (count * ComponentBytes)
            && segment[0] == Precision;
    }

    /// <summary>Reads every marker segment and scan, then reconstructs the sample planes.</summary>
    /// <returns><see langword="true"/> when the frame header and at least one scan were read.</returns>
    private bool Decode()
    {
        while (JpegMarkers.TryNextMarker(_data, ref _position, out var marker))
        {
            var step = Handle(marker);
            if (step == Step.Fail)
            {
                return false;
            }

            if (step == Step.Stop)
            {
                break;
            }
        }

        return _scanCount > 0 && Reconstruct();
    }

    /// <summary>Handles one marker and its segment.</summary>
    /// <param name="marker">The marker byte.</param>
    /// <returns>What to do next.</returns>
    private Step Handle(byte marker)
    {
        if (!JpegMarkers.HasLength(marker))
        {
            return marker == JpegMarkers.EndOfImage ? Step.Stop : Step.Continue;
        }

        if (!JpegMarkers.TryReadSegment(_data, ref _position, out var segment))
        {
            return Step.Stop;
        }

        return marker == JpegMarkers.StartOfScan ? ReadScan(segment) : HandleTable(marker, segment);
    }

    /// <summary>Handles a table, restart-interval or frame segment; other segments are skipped.</summary>
    /// <param name="marker">The marker byte.</param>
    /// <param name="segment">The payload.</param>
    /// <returns>What to do next.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Step HandleTable(byte marker, scoped ReadOnlySpan<byte> segment) => Succeeded(marker switch
    {
        JpegMarkers.QuantizationTables => ReadQuantization(segment),
        JpegMarkers.HuffmanTables => ReadHuffman(segment),
        JpegMarkers.RestartInterval => ReadRestartInterval(segment),
        _ => !JpegMarkers.IsFrame(marker) || ReadFrame(marker, segment),
    });

    /// <summary>Reads a DQT segment.</summary>
    /// <param name="segment">The payload.</param>
    /// <returns><see langword="false"/> when the segment is malformed.</returns>
    private readonly bool ReadQuantization(scoped ReadOnlySpan<byte> segment)
    {
        var rest = segment;
        while (!rest.IsEmpty)
        {
            var wide = rest[0] >> NibbleBits != 0;
            var id = rest[0] & NibbleMask;
            var size = 1 + (JpegBlock.Length * (wide ? WideBytes : 1));
            if (id >= TableSlots || rest.Length < size)
            {
                return false;
            }

            var table = new int[JpegBlock.Length];
            var zigzag = JpegBlock.Zigzag;
            for (var i = 0; i < JpegBlock.Length; i++)
            {
                table[zigzag[i]] = wide ? BinaryPrimitives.ReadUInt16BigEndian(rest[(1 + (i * WideBytes))..]) : rest[1 + i];
            }

            _quantTables[id] = table;
            rest = rest[size..];
        }

        return true;
    }

    /// <summary>Reads a DHT segment.</summary>
    /// <param name="segment">The payload.</param>
    /// <returns><see langword="false"/> when the segment is malformed.</returns>
    private readonly bool ReadHuffman(scoped ReadOnlySpan<byte> segment)
    {
        var rest = segment;
        while (rest.Length >= HuffmanHeaderBytes)
        {
            var id = rest[0] & NibbleMask;
            var counts = rest.Slice(1, JpegHuffmanTable.LengthCounts);
            var total = 0;
            foreach (var count in counts)
            {
                total += count;
            }

            if (id >= TableSlots || rest.Length < HuffmanHeaderBytes + total)
            {
                return false;
            }

            var table = JpegHuffmanTable.Create(counts, rest.Slice(HuffmanHeaderBytes, total));
            if (table is null)
            {
                return false;
            }

            (rest[0] >> NibbleBits == AcClass ? _codesAc : _codesDc)[id] = table;
            rest = rest[(HuffmanHeaderBytes + total)..];
        }

        return true;
    }

    /// <summary>Reads a DRI segment.</summary>
    /// <param name="segment">The payload.</param>
    /// <returns><see langword="false"/> when the segment is too short.</returns>
    private bool ReadRestartInterval(scoped ReadOnlySpan<byte> segment)
    {
        if (segment.Length < WideBytes)
        {
            return false;
        }

        _restartInterval = BinaryPrimitives.ReadUInt16BigEndian(segment);
        return true;
    }

    /// <summary>Reads a frame header and allocates the components.</summary>
    /// <param name="marker">The frame marker.</param>
    /// <param name="segment">The payload.</param>
    /// <returns><see langword="false"/> when the frame is not one this decoder reads.</returns>
    private bool ReadFrame(byte marker, scoped ReadOnlySpan<byte> segment)
    {
        var count = segment.Length > CountOffset ? segment[CountOffset] : 0;
        if (_components is not null || !IsReadableFrame(marker, segment, count))
        {
            return false;
        }

        _width = BinaryPrimitives.ReadUInt16BigEndian(segment[WidthOffset..]);
        _height = BinaryPrimitives.ReadUInt16BigEndian(segment[HeightOffset..]);
        _progressive = marker == JpegMarkers.ProgressiveFrame;
        return _width > 0 && _height > 0 && CreateComponents(segment, count);
    }

    /// <summary>Allocates the components of a frame.</summary>
    /// <param name="segment">The frame payload.</param>
    /// <param name="count">The component count.</param>
    /// <returns><see langword="false"/> when a sampling factor is invalid or the image is too large.</returns>
    private bool CreateComponents(scoped ReadOnlySpan<byte> segment, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var sampling = segment[FirstComponentOffset + (i * ComponentBytes) + 1];
            var horizontal = sampling >> NibbleBits;
            var vertical = sampling & NibbleMask;
            if (horizontal is < 1 or > MaxSampling || vertical is < 1 or > MaxSampling)
            {
                return false;
            }

            _maxHorizontal = Math.Max(_maxHorizontal, horizontal);
            _maxVertical = Math.Max(_maxVertical, vertical);
        }

        _mcusPerLine = CeilingDivide(_width, JpegBlock.Side * _maxHorizontal);
        _mcusPerColumn = CeilingDivide(_height, JpegBlock.Side * _maxVertical);
        var components = new JpegComponent[count];
        _components = components;
        for (var i = 0; i < count; i++)
        {
            var offset = FirstComponentOffset + (i * ComponentBytes);
            var horizontal = segment[offset + 1] >> NibbleBits;
            var vertical = segment[offset + 1] & NibbleMask;
            if ((long)_mcusPerLine * horizontal * _mcusPerColumn * vertical * JpegBlock.Length > MaxCoefficients)
            {
                // Only the components created so far are disposed with the decoder.
                _components = components[..i];
                return false;
            }

            var spec = new JpegComponentSpec(segment[offset], horizontal, vertical, segment[offset + QuantOffset] & NibbleMask);
            var grid = new JpegGrid(
                _mcusPerLine,
                _mcusPerColumn,
                CeilingDivide(CeilingDivide(_width * horizontal, _maxHorizontal), JpegBlock.Side),
                CeilingDivide(CeilingDivide(_height * vertical, _maxVertical), JpegBlock.Side));
            components[i] = new(spec, grid);
        }

        return true;
    }
}
