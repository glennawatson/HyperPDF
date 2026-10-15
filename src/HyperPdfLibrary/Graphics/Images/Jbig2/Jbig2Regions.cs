// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>Decodes generic, refinement and halftone regions.</summary>
internal static class Jbig2Regions
{
    /// <summary>The generic region flag for MMR coding.</summary>
    internal const int MmrFlag = 0x01;

    /// <summary>The generic region flag for the extended template with 12 adaptive pixels.</summary>
    internal const int ExtendedTemplateFlag = 0x10;

    /// <summary>The bytes of the row count after a generic region of unknown length.</summary>
    internal const int RowCountBytes = 4;

    /// <summary>The shift of a generic template in region flags.</summary>
    internal const int TemplateShift = 1;

    /// <summary>The mask of a generic template.</summary>
    internal const int TemplateMask = 0x03;

    /// <summary>The shift of the generic region typical prediction flag.</summary>
    internal const int TypicalShift = 3;

    /// <summary>The shift of the refinement region typical prediction flag.</summary>
    internal const int RefinementTypicalShift = 1;

    /// <summary>The shift of the halftone skip flag.</summary>
    internal const int SkipShift = 3;

    /// <summary>The shift of the halftone combination operator.</summary>
    internal const int HalftoneOperatorShift = 4;

    /// <summary>The shift of the halftone default pixel flag.</summary>
    internal const int HalftoneDefaultShift = 7;

    /// <summary>Decodes an MMR generic region and aligns the reader after it.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="region">The white region bitmap.</param>
    internal static void DecodeMmrRegion(ref Jbig2Reader reader, Jbig2Bitmap region)
    {
        var start = reader.Offset;
        reader.BitPosition = ((long)start << Jbig2Bits.ByteShift) + Jbig2GenericRegion.DecodeMmr(reader.Data[start..], region);
        reader.AlignByte();
    }

    /// <summary>
    /// Decodes an arithmetic generic region. A region whose data runs out keeps the rows decoded, so a
    /// damaged stream still shows its readable top part.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="region">The white region bitmap.</param>
    /// <param name="parameters">The region parameters.</param>
    internal static void DecodeArithmeticRegion(ref Jbig2Reader reader, Jbig2Bitmap region, in Jbig2GenericParameters parameters)
    {
        using var contexts = new Jbig2ContextBuffer(parameters.ContextCount, reader.ScratchPools);
        var decoder = new Jbig2ArithmeticDecoder(reader.Data, reader.Offset, reader.CancellationToken);
        _ = Jbig2GenericRegion.Decode(ref decoder, contexts.Span, parameters, region, null);
        Jbig2Segments.FinishArithmetic(ref reader, decoder.Position);
    }

    /// <summary>Determines whether a generic region's flags select the extended template (T.88 section 7.4.6.2, EXTTEMPLATE, which applies to template 0 only).</summary>
    /// <param name="flags">The generic region flags.</param>
    /// <param name="template">The generic template.</param>
    /// <returns><see langword="true"/> for the extended template.</returns>
    internal static bool IsExtended(int flags, int template) => template == 0 && (flags & ExtendedTemplateFlag) != 0;

    /// <summary>Decodes an arithmetic generic region that uses the extended template.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="region">The white region bitmap.</param>
    /// <param name="typicalPrediction">Whether typical prediction is on.</param>
    /// <param name="at">The twelve adaptive template pixels.</param>
    internal static void DecodeExtendedRegion(ref Jbig2Reader reader, Jbig2Bitmap region, bool typicalPrediction, in Jbig2ExtendedAtPixels at)
    {
        using var contexts = new Jbig2ContextBuffer(Jbig2ExtendedGenericRegion.ContextCount, reader.ScratchPools);
        var decoder = new Jbig2ArithmeticDecoder(reader.Data, reader.Offset, reader.CancellationToken);
        _ = Jbig2ExtendedGenericRegion.Decode(ref decoder, contexts.Span, typicalPrediction, at, region, null);
        Jbig2Segments.FinishArithmetic(ref reader, decoder.Position);
    }

    /// <summary>Reads the adaptive template pixels of a generic region: twelve with the extended template, else four for template 0 or one.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="template">The template.</param>
    /// <param name="extended">Whether the extended template flag is set.</param>
    /// <param name="at">The pixels; a template with fewer than twelve fills the first group.</param>
    /// <returns><see langword="false"/> when the data ends.</returns>
    internal static bool TryReadGenericAt(ref Jbig2Reader reader, int template, bool extended, out Jbig2ExtendedAtPixels at)
    {
        at = default;
        if (!extended || template != 0)
        {
            var read = TryReadAt(ref reader, template, out var first);
            at = Jbig2ExtendedAtPixels.FromFirst(first);
            return read;
        }

        if (!TryReadAt(ref reader, 0, out var low) || !TryReadAt(ref reader, 0, out var middle) || !TryReadAt(ref reader, 0, out var high))
        {
            return false;
        }

        at = new(low, middle, high);
        return true;
    }

    /// <summary>Reads the adaptive template pixels of a generic template: four for template 0, else one.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="template">The template.</param>
    /// <param name="at">The pixels.</param>
    /// <returns><see langword="false"/> when the data ends.</returns>
    internal static bool TryReadAt(ref Jbig2Reader reader, int template, out Jbig2AtPixels at)
    {
        at = default;
        if (!reader.TryReadSByte(out var x1) || !reader.TryReadSByte(out var y1))
        {
            return false;
        }

        if (template != 0)
        {
            at = new(x1, y1, 0, 0, 0, 0, 0, 0);
            return true;
        }

        if (!reader.TryReadSByte(out var x2) || !reader.TryReadSByte(out var y2) || !reader.TryReadSByte(out var x3)
            || !reader.TryReadSByte(out var y3) || !reader.TryReadSByte(out var x4) || !reader.TryReadSByte(out var y4))
        {
            return false;
        }

        at = new(x1, y1, x2, y2, x3, y3, x4, y4);
        return true;
    }

    /// <summary>Reads the two adaptive template pixels of refinement template 0.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="at">The pixels.</param>
    /// <returns><see langword="false"/> when the data ends.</returns>
    internal static bool TryReadRefinementAt(ref Jbig2Reader reader, out Jbig2AtPixels at)
    {
        at = default;
        if (!reader.TryReadSByte(out var x1) || !reader.TryReadSByte(out var y1) || !reader.TryReadSByte(out var x2) || !reader.TryReadSByte(out var y2))
        {
            return false;
        }

        at = new(x1, y1, x2, y2, 0, 0, 0, 0);
        return true;
    }

    /// <summary>Reads a refinement region's header fields.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="info">The region information.</param>
    /// <param name="parameters">The refinement parameters.</param>
    /// <returns><see langword="false"/> when the data ends or the size is invalid.</returns>
    internal static bool TryReadRefinementRegion(ref Jbig2Reader reader, out Jbig2RegionInfo info, out Jbig2RefinementParameters parameters)
    {
        parameters = default;
        if (!Jbig2RegionInfo.TryRead(ref reader, out info) || !reader.TryReadByte(out var flags) || !Jbig2Limits.IsValidSize(info.Width, info.Height))
        {
            return false;
        }

        var template = flags & 1;
        var at = default(Jbig2AtPixels);
        if (template == 0 && !TryReadRefinementAt(ref reader, out at))
        {
            return false;
        }

        parameters = new(template, ((flags >> RefinementTypicalShift) & 1) != 0, 0, 0, at);
        return true;
    }

    /// <summary>Copies the page area under a region, as the reference for a refinement with no referred region.</summary>
    /// <param name="page">The page.</param>
    /// <param name="info">The region information.</param>
    /// <param name="workspace">The workspace that provides the copy.</param>
    /// <returns>The copy; white when the region's corner is off the page (our rule: nothing exists there to copy).</returns>
    internal static Jbig2Bitmap? CopyPageArea(Jbig2Bitmap page, in Jbig2RegionInfo info, Jbig2Workspace workspace)
    {
        var area = workspace.RentBitmap(info.Width, info.Height);
        if (area is not null && info.X >= 0 && info.X < page.Width && info.Y >= 0 && info.Y < page.Height)
        {
            _ = Jbig2Composer.Compose(area, page.View, -(long)info.X, -(long)info.Y, Jbig2ComposeOperator.Replace);
        }

        return area;
    }

    /// <summary>Reads a halftone region's header fields.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="info">The region information.</param>
    /// <param name="settings">The region parameters.</param>
    /// <returns><see langword="false"/> when the data ends or a size is invalid.</returns>
    internal static bool TryReadHalftone(ref Jbig2Reader reader, out Jbig2RegionInfo info, Jbig2HalftoneSettings settings)
    {
        if (!Jbig2RegionInfo.TryRead(ref reader, out info) || !reader.TryReadByte(out var flags) || !TryReadGrid(ref reader, settings))
        {
            return false;
        }

        settings.Width = info.Width;
        settings.Height = info.Height;
        settings.Mmr = (flags & MmrFlag) != 0;
        settings.Template = (flags >> TemplateShift) & TemplateMask;
        settings.EnableSkip = ((flags >> SkipShift) & 1) != 0;
        settings.Operator = Jbig2RegionInfo.ToOperator(flags >> HalftoneOperatorShift);
        settings.DefaultPixel = ((flags >> HalftoneDefaultShift) & 1) != 0;
        return Jbig2Limits.IsValidSize(info.Width, info.Height);
    }

    /// <summary>Reads a halftone region's grid size, origin and vector.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="settings">The region parameters, which receive the grid.</param>
    /// <returns><see langword="false"/> when the data ends or the grid size is invalid.</returns>
    internal static bool TryReadGrid(ref Jbig2Reader reader, Jbig2HalftoneSettings settings)
    {
        if (!reader.TryReadUInt32(out var gridWidth) || !reader.TryReadUInt32(out var gridHeight)
            || !reader.TryReadInt32(out var gridX) || !reader.TryReadInt32(out var gridY)
            || !reader.TryReadUInt16(out var vectorX) || !reader.TryReadUInt16(out var vectorY)
            || !Jbig2Limits.IsValidSize(gridWidth, gridHeight))
        {
            return false;
        }

        settings.GridWidth = (int)gridWidth;
        settings.GridHeight = (int)gridHeight;
        settings.GridX = gridX;
        settings.GridY = gridY;
        settings.VectorX = vectorX;
        settings.VectorY = vectorY;
        return true;
    }

    /// <summary>Processes a generic region segment (T.88 section 7.4.6).</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="reader">The reader, at the segment data.</param>
    /// <param name="segment">The segment.</param>
    /// <param name="page">The page.</param>
    /// <returns>The status.</returns>
    internal static Jbig2Status DecodeGenericRegion(Jbig2DecodeState state, ref Jbig2Reader reader, Jbig2Segment segment, Jbig2Bitmap page)
    {
        if (!TryReadGeneric(ref reader, segment, out var setup))
        {
            return Jbig2Status.Failure;
        }

        var info = setup.Info;
        if (!Jbig2Limits.IsValidSize(info.Width, info.Height) || !state.Workspace.TryCharge((long)info.Width * info.Height))
        {
            // A region of unknown length whose row count is zero holds nothing; the next segment starts after its row count.
            reader.Offset = setup.End + RowCountBytes;
            return setup.Unknown && info.Height == 0 && info.Width > 0 ? Jbig2Status.Success : Jbig2Status.Failure;
        }

        var region = state.Workspace.RentBitmap(info.Width, info.Height);
        if (region is null)
        {
            return Jbig2Status.Failure;
        }

        DecodeGenericBody(ref reader, region, setup);
        return Jbig2Segments.Finish(state, segment, info, region, segment.Type == Jbig2SegmentType.IntermediateGenericRegion, page);
    }

    /// <summary>Processes a generic refinement region segment (T.88 section 7.4.7).</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="reader">The reader, at the segment data.</param>
    /// <param name="segment">The segment.</param>
    /// <param name="page">The page.</param>
    /// <returns>The status.</returns>
    internal static Jbig2Status DecodeRefinementRegion(Jbig2DecodeState state, ref Jbig2Reader reader, Jbig2Segment segment, Jbig2Bitmap page)
    {
        if (!TryReadRefinementRegion(ref reader, out var info, out var parameters) || !state.Workspace.TryCharge((long)info.Width * info.Height))
        {
            return Jbig2Status.Failure;
        }

        using var pageArea = segment.Referred.IsEmpty ? CopyPageArea(page, info, state.Workspace) : null;
        if ((pageArea ?? FindReferredRegion(state, segment)) is not { } reference || state.Workspace.RentBitmap(info.Width, info.Height) is not { } region)
        {
            return Jbig2Status.Failure;
        }

        using var contexts = new Jbig2ContextBuffer(parameters.ContextCount, state.Workspace.ScratchPools);
        var decoder = new Jbig2ArithmeticDecoder(reader.Data, reader.Offset, reader.CancellationToken);
        if (!Jbig2RefinementRegion.Decode(ref decoder, contexts.Span, parameters, reference.View, region))
        {
            region.Dispose();
            return Jbig2Status.Failure;
        }

        Jbig2Segments.FinishArithmetic(ref reader, decoder.Position);
        return Jbig2Segments.Finish(state, segment, info, region, segment.Type == Jbig2SegmentType.IntermediateRefinementRegion, page);
    }

    /// <summary>Finds the intermediate region a refinement segment refines.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="segment">The refinement segment.</param>
    /// <returns>The region, or <see langword="null"/> when none is referred to.</returns>
    internal static Jbig2Bitmap? FindReferredRegion(Jbig2DecodeState state, Jbig2Segment segment)
    {
        foreach (var number in segment.Referred)
        {
            if (Jbig2Segments.Find(state, number) is { Bitmap: { } bitmap })
            {
                return bitmap;
            }
        }

        return null;
    }

    /// <summary>Processes a halftone region segment (T.88 section 7.4.5).</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="reader">The reader, at the segment data.</param>
    /// <param name="segment">The segment.</param>
    /// <param name="page">The page.</param>
    /// <returns>The status.</returns>
    internal static Jbig2Status DecodeHalftoneRegion(Jbig2DecodeState state, ref Jbig2Reader reader, Jbig2Segment segment, Jbig2Bitmap page)
    {
        var settings = state.Workspace.HalftoneSettings;
        if (!TryReadHalftone(ref reader, out var info, settings) || segment.Referred.Length != 1
            || Jbig2Segments.Find(state, segment.Referred[0]) is not { Patterns: { } patterns } || patterns.Patterns.Count == 0)
        {
            return Jbig2Status.Failure;
        }

        var region = state.Workspace.RentBitmap(info.Width, info.Height);
        if (region is null || !DecodeHalftone(state, ref reader, settings, patterns, region))
        {
            region?.Dispose();
            return Jbig2Status.Failure;
        }

        return Jbig2Segments.Finish(state, segment, info, region, segment.Type == Jbig2SegmentType.IntermediateHalftoneRegion, page);
    }

    /// <summary>Decodes a halftone region's planes and patterns.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="reader">The reader.</param>
    /// <param name="settings">The region parameters.</param>
    /// <param name="patterns">The pattern dictionary.</param>
    /// <param name="region">The region bitmap.</param>
    /// <returns><see langword="false"/> when the region cannot be decoded.</returns>
    internal static bool DecodeHalftone(Jbig2DecodeState state, ref Jbig2Reader reader, Jbig2HalftoneSettings settings, Jbig2PatternDictionary patterns, Jbig2Bitmap region)
    {
        if (settings.Mmr)
        {
            return Jbig2HalftoneRegion.DecodeMmr(ref reader, settings, patterns, region, state.Workspace);
        }

        using var contexts = new Jbig2ContextBuffer(Jbig2GenericParameters.ContextCountFor(settings.Template), state.Workspace.ScratchPools);
        var decoder = new Jbig2ArithmeticDecoder(reader.Data, reader.Offset, reader.CancellationToken);
        var decoded = Jbig2HalftoneRegion.DecodeArithmetic(ref decoder, contexts.Span, settings, patterns, region, state.Workspace);
        Jbig2Segments.FinishArithmetic(ref reader, decoder.Position);
        return decoded;
    }

    /// <summary>
    /// Reads a generic region's header fields. A region of unknown length is cut at its end marker, and its height at the
    /// row count after the marker, which can be lower than the height in the header (T.88 section 7.4.6.4).
    /// </summary>
    /// <param name="reader">The reader, at the segment data.</param>
    /// <param name="segment">The segment.</param>
    /// <param name="setup">The fields.</param>
    /// <returns><see langword="false"/> when the data ends.</returns>
    private static bool TryReadGeneric(ref Jbig2Reader reader, Jbig2Segment segment, out GenericSetup setup)
    {
        setup = default;
        if (!Jbig2RegionInfo.TryRead(ref reader, out var info) || !reader.TryReadByte(out var flags))
        {
            return false;
        }

        var mmr = (flags & MmrFlag) != 0;
        var template = (flags >> TemplateShift) & TemplateMask;
        var at = default(Jbig2ExtendedAtPixels);
        if (!mmr && !TryReadGenericAt(ref reader, template, (flags & ExtendedTemplateFlag) != 0, out at))
        {
            return false;
        }

        var unknown = segment.DataLength == Jbig2Segment.UnknownLength;
        var end = reader.Data.Length;
        var rows = (uint)Math.Max(info.Height, 0);
        if (unknown && !Jbig2GenericExtent.TryFind(reader.Data, reader.Offset, mmr, out end, out rows))
        {
            return false;
        }

        setup = new(info with { Height = (int)Math.Min((uint)Math.Max(info.Height, 0), rows) }, flags, at, end, unknown);
        return true;
    }

    /// <summary>Decodes the coded data of a generic region into its bitmap and moves the reader past the segment data of an unknown length.</summary>
    /// <param name="reader">The reader, at the coded data.</param>
    /// <param name="region">The white region bitmap.</param>
    /// <param name="setup">The region fields.</param>
    private static void DecodeGenericBody(ref Jbig2Reader reader, Jbig2Bitmap region, in GenericSetup setup)
    {
        var body = new Jbig2Reader(reader.Data[..setup.End], reader.ScratchPools, reader.CancellationToken) { Offset = reader.Offset };
        var template = (setup.Flags >> TemplateShift) & TemplateMask;
        var typical = ((setup.Flags >> TypicalShift) & 1) != 0;
        if ((setup.Flags & MmrFlag) != 0)
        {
            DecodeMmrRegion(ref body, region);
        }
        else if (IsExtended(setup.Flags, template))
        {
            DecodeExtendedRegion(ref body, region, typical, setup.At);
        }
        else
        {
            DecodeArithmeticRegion(ref body, region, new(template, typical, setup.At.First));
        }

        reader.Offset = setup.Unknown ? setup.End + RowCountBytes : body.Offset;
    }

    /// <summary>The header fields of a generic region.</summary>
    /// <param name="Info">The region information, with the height cut to the row count of an unknown-length region.</param>
    /// <param name="Flags">The generic region flags.</param>
    /// <param name="At">The adaptive template pixels.</param>
    /// <param name="End">The offset where the coded data ends: just past the end marker, or the end of the stream.</param>
    /// <param name="Unknown">Whether the segment data length is unknown.</param>
    private readonly record struct GenericSetup(Jbig2RegionInfo Info, int Flags, Jbig2ExtendedAtPixels At, int End, bool Unknown);
}
