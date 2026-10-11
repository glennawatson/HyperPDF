// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>Decodes generic, refinement and halftone regions.</summary>
internal static class Jbig2Regions
{
    /// <summary>The generic region flag for MMR coding.</summary>
    internal const int MmrFlag = 0x01;

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
    /// Decodes an arithmetic generic region. A region whose data runs out keeps the rows decoded, as PDFium's
    /// progressive decoder does.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="region">The white region bitmap.</param>
    /// <param name="parameters">The region parameters.</param>
    internal static void DecodeArithmeticRegion(ref Jbig2Reader reader, Jbig2Bitmap region, in Jbig2GenericParameters parameters)
    {
        using var contexts = new Jbig2ContextBuffer(parameters.ContextCount);
        var decoder = new Jbig2ArithmeticDecoder(reader.Data, reader.Offset, reader.CancellationToken);
        _ = Jbig2GenericRegion.Decode(ref decoder, contexts.Span, parameters, region, null);
        Jbig2Segments.FinishArithmetic(ref reader, decoder.Position);
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
    /// <returns>The copy; white when the region's corner is off the page, as in PDFium.</returns>
    internal static Jbig2Bitmap? CopyPageArea(Jbig2Bitmap page, in Jbig2RegionInfo info)
    {
        var area = Jbig2Bitmap.Create(info.Width, info.Height);
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
    internal static bool TryReadHalftone(ref Jbig2Reader reader, out Jbig2RegionInfo info, out Jbig2HalftoneSettings settings)
    {
        settings = new();
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
        if (!Jbig2RegionInfo.TryRead(ref reader, out var info) || !reader.TryReadByte(out var flags))
        {
            return Jbig2Status.Failure;
        }

        var mmr = (flags & MmrFlag) != 0;
        var template = (flags >> TemplateShift) & TemplateMask;
        var at = default(Jbig2AtPixels);
        if ((!mmr && !TryReadAt(ref reader, template, out at)) || !Jbig2Limits.IsValidSize(info.Width, info.Height) || !state.Workspace.TryCharge((long)info.Width * info.Height))
        {
            return Jbig2Status.Failure;
        }

        var region = Jbig2Bitmap.Create(info.Width, info.Height);
        if (region is null)
        {
            return Jbig2Status.Failure;
        }

        if (mmr)
        {
            DecodeMmrRegion(ref reader, region);
        }
        else
        {
            DecodeArithmeticRegion(ref reader, region, new(template, ((flags >> TypicalShift) & 1) != 0, at));
        }

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

        using var pageArea = segment.Referred.IsEmpty ? CopyPageArea(page, info) : null;
        if ((pageArea ?? FindReferredRegion(state, segment)) is not { } reference || Jbig2Bitmap.Create(info.Width, info.Height) is not { } region)
        {
            return Jbig2Status.Failure;
        }

        using var contexts = new Jbig2ContextBuffer(parameters.ContextCount);
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
        if (!TryReadHalftone(ref reader, out var info, out var settings) || segment.Referred.Length != 1
            || Jbig2Segments.Find(state, segment.Referred[0]) is not { Patterns: { } patterns } || patterns.Patterns.Count == 0)
        {
            return Jbig2Status.Failure;
        }

        var region = Jbig2Bitmap.Create(info.Width, info.Height);
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

        using var contexts = new Jbig2ContextBuffer(Jbig2GenericParameters.ContextCountFor(settings.Template));
        var decoder = new Jbig2ArithmeticDecoder(reader.Data, reader.Offset, reader.CancellationToken);
        var decoded = Jbig2HalftoneRegion.DecodeArithmetic(ref decoder, contexts.Span, settings, patterns, region, state.Workspace);
        Jbig2Segments.FinishArithmetic(ref reader, decoder.Position);
        return decoded;
    }
}
