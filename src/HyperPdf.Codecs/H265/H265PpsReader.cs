// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H265;

/// <summary>Reads picture parameter sets (7.3.2.3).</summary>
internal static class H265PpsReader
{
    /// <summary>The largest picture parameter set number.</summary>
    private const int MaxId = 63;

    /// <summary>The largest sequence parameter set number.</summary>
    private const int MaxSpsId = 15;

    /// <summary>The bits of the number of reserved slice header bits.</summary>
    private const int ExtraBitsBits = 3;

    /// <summary>The quantiser every slice starts from before the coded offset.</summary>
    private const int BaseQp = 26;

    /// <summary>The offset added to the coded parallel merge level.</summary>
    private const int ParMrgBase = 2;

    /// <summary>The offset added to the coded smallest transform skip size.</summary>
    private const int TransformSkipBase = 2;

    /// <summary>The most tile columns or rows a picture may have.</summary>
    private const int MaxTiles = 20;

    /// <summary>The most entries a chroma quantiser offset list may have.</summary>
    private const int MaxOffsetList = 6;

    /// <summary>The bits of the extension flags that follow the first four.</summary>
    private const int ExtensionBits = 4;

    /// <summary>The largest reference picture count in a list.</summary>
    private const int MaxRefIdx = 15;

    /// <summary>Reads a picture parameter set.</summary>
    /// <param name="rbsp">The payload of the NAL unit, without its header and emulation prevention bytes.</param>
    /// <returns>The set.</returns>
    /// <exception cref="InvalidDataException">The set is damaged.</exception>
    /// <exception cref="NotSupportedException">The set uses a feature this decoder does not read.</exception>
    internal static H265Pps Read(ReadOnlySpan<byte> rbsp)
    {
        var reader = new MsbBitReader(rbsp);
        var id = (int)ExpGolomb.ReadUe(ref reader);
        var spsId = (int)ExpGolomb.ReadUe(ref reader);
        var dependent = reader.ReadBit();
        var outputFlag = reader.ReadBit();
        var extraBits = (int)reader.Read(ExtraBitsBits);
        var signHiding = reader.ReadBit();
        var cabacInit = reader.ReadBit();
        var l0 = 1 + (int)ExpGolomb.ReadUe(ref reader);
        var l1 = 1 + (int)ExpGolomb.ReadUe(ref reader);
        var initQp = BaseQp + ExpGolomb.ReadSe(ref reader);
        var constrained = reader.ReadBit();
        var transformSkip = reader.ReadBit();
        var quant = ReadQuant(ref reader, initQp, out var sliceChroma, out var weighted);
        var transquant = reader.ReadBit();
        var tiles = ReadTiles(ref reader, out var sync);
        var loopFilter = reader.ReadBit();
        var deblocking = ReadDeblocking(ref reader);
        var scaling = reader.ReadBit() ? H265ScalingLists.Read(ref reader) : null;
        var listsModification = reader.ReadBit();
        var parMrg = ParMrgBase + (int)ExpGolomb.ReadUe(ref reader);
        var headerExtension = reader.ReadBit();
        var range = ReadExtensions(ref reader, transformSkip);
        var flags = new H265PpsFlags(
            dependent,
            outputFlag,
            signHiding,
            cabacInit,
            constrained,
            transformSkip,
            sliceChroma,
            weighted.Pred,
            weighted.Bipred,
            transquant,
            sync,
            loopFilter,
            listsModification,
            headerExtension);
        var pps = new H265Pps(id, spsId, flags, extraBits, l0, l1, quant, tiles, deblocking, scaling, parMrg, range);
        Validate(pps, reader.IsOverrun);
        return pps;
    }

    /// <summary>Reads the quantiser settings and the weighted prediction flags that sit among them.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="initQp">The initial quantiser.</param>
    /// <param name="sliceChroma">Receives whether slice headers carry chroma quantiser offsets.</param>
    /// <param name="weighted">Receives the weighted prediction flags.</param>
    /// <returns>The settings.</returns>
    private static H265PpsQuant ReadQuant(ref MsbBitReader reader, int initQp, out bool sliceChroma, out H265WeightedFlags weighted)
    {
        var deltaEnabled = reader.ReadBit();
        var depth = deltaEnabled ?(int)ExpGolomb.ReadUe(ref reader) : 0;
        var cb = ExpGolomb.ReadSe(ref reader);
        var cr = ExpGolomb.ReadSe(ref reader);
        sliceChroma = reader.ReadBit();
        weighted = new(reader.ReadBit(), reader.ReadBit());
        return new(initQp, deltaEnabled, depth, cb, cr);
    }

    /// <summary>Reads the tile layout and the entropy coding sync flag that precedes it.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="sync">Receives the entropy coding sync flag.</param>
    /// <returns>The tile layout.</returns>
    /// <exception cref="InvalidDataException">The layout has too many tiles.</exception>
    private static H265TileSpec ReadTiles(ref MsbBitReader reader, out bool sync)
    {
        var enabled = reader.ReadBit();
        sync = reader.ReadBit();
        if (!enabled)
        {
            return new(false, 1, 1, true, [], [], true);
        }

        var columns = 1 + (int)ExpGolomb.ReadUe(ref reader);
        var rows = 1 + (int)ExpGolomb.ReadUe(ref reader);
        if (columns > MaxTiles || rows > MaxTiles)
        {
            throw new InvalidDataException("The H.265 picture parameter set has too many tiles.");
        }

        var uniform = reader.ReadBit();
        var widths = new int[uniform ? 0 : columns - 1];
        var heights = new int[uniform ? 0 : rows - 1];
        for (var i = 0; i < widths.Length; i++)
        {
            widths[i] = 1 + (int)ExpGolomb.ReadUe(ref reader);
        }

        for (var i = 0; i < heights.Length; i++)
        {
            heights[i] = 1 + (int)ExpGolomb.ReadUe(ref reader);
        }

        return new(true, columns, rows, uniform, widths, heights, reader.ReadBit());
    }

    /// <summary>Reads the deblocking filter settings.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The settings.</returns>
    private static H265PpsDeblocking ReadDeblocking(ref MsbBitReader reader)
    {
        if (!reader.ReadBit())
        {
            return default;
        }

        var overrideEnabled = reader.ReadBit();
        var disabled = reader.ReadBit();
        var beta = 0;
        var tc = 0;
        if (!disabled)
        {
            beta = ExpGolomb.ReadSe(ref reader);
            tc = ExpGolomb.ReadSe(ref reader);
        }

        return new(overrideEnabled, disabled, beta, tc);
    }

    /// <summary>Reads the extension flags and the range extension.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="transformSkip">Whether transform skip is enabled, which decides whether the range extension carries a size.</param>
    /// <returns>The range settings.</returns>
    /// <exception cref="NotSupportedException">The set uses an extension this decoder does not read.</exception>
    private static H265PpsRange ReadExtensions(ref MsbBitReader reader, bool transformSkip)
    {
        var none = new H265PpsRange(TransformSkipBase, false, false, 0, [], [], 0, 0);
        if (!reader.ReadBit())
        {
            return none;
        }

        var range = reader.ReadBit();
        var multilayer = reader.ReadBit();
        var extension3d = reader.ReadBit();
        var screenContent = reader.ReadBit();
        reader.Skip(ExtensionBits);
        if (multilayer || extension3d || screenContent)
        {
            throw new NotSupportedException("Multilayer, 3D and screen content extensions of H.265 are not supported.");
        }

        return range ? ReadRangeExtension(ref reader, transformSkip) : none;
    }

    /// <summary>Reads the range extension.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="transformSkip">Whether transform skip is enabled.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="InvalidDataException">The chroma offset list is too long.</exception>
    private static H265PpsRange ReadRangeExtension(ref MsbBitReader reader, bool transformSkip)
    {
        var skipSize = transformSkip ? TransformSkipBase + (int)ExpGolomb.ReadUe(ref reader) : TransformSkipBase;
        var crossComponent = reader.ReadBit();
        var listEnabled = reader.ReadBit();
        var depth = 0;
        var cb = Array.Empty<int>();
        var cr = Array.Empty<int>();
        if (listEnabled)
        {
            depth = (int)ExpGolomb.ReadUe(ref reader);
            var length = 1 + (int)ExpGolomb.ReadUe(ref reader);
            if (length > MaxOffsetList)
            {
                throw new InvalidDataException("The H.265 chroma quantiser offset list is too long.");
            }

            cb = new int[length];
            cr = new int[length];
            for (var i = 0; i < length; i++)
            {
                cb[i] = ExpGolomb.ReadSe(ref reader);
                cr[i] = ExpGolomb.ReadSe(ref reader);
            }
        }

        var scaleLuma = (int)ExpGolomb.ReadUe(ref reader);
        var scaleChroma = (int)ExpGolomb.ReadUe(ref reader);
        return new(skipSize, crossComponent, listEnabled, depth, cb, cr, scaleLuma, scaleChroma);
    }

    /// <summary>Checks the values of a set.</summary>
    /// <param name="pps">The set.</param>
    /// <param name="overrun">Whether the bits ran out.</param>
    /// <exception cref="InvalidDataException">A value is out of range.</exception>
    private static void Validate(H265Pps pps, bool overrun)
    {
        if (overrun
            || pps.Id > MaxId
            || pps.SpsId > MaxSpsId
            || pps.NumRefIdxL0Default > MaxRefIdx
            || pps.NumRefIdxL1Default > MaxRefIdx)
        {
            throw new InvalidDataException("The H.265 picture parameter set is damaged or out of range.");
        }
    }
}
