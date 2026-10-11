// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Content;

/// <summary>Runs the interpreter's Colors operations over its owned state.</summary>
internal static class ContentColors
{
    /// <summary>The paint type of an uncoloured tiling pattern.</summary>
    internal const int UncoloredPaintType = 2;

    /// <summary>The pattern type of a shading pattern.</summary>
    internal const int ShadingPatternType = 2;

    /// <summary>The ratio between neighbouring pattern resolutions, and the divisor that averages two axis scales.</summary>
    internal const float PatternScaleBase = 2;

    /// <summary>The smallest power-of-two pattern scale kept.</summary>
    internal const int MinPatternExponent = -8;

    /// <summary>The largest power-of-two pattern scale kept.</summary>
    internal const int MaxPatternExponent = 8;

    /// <summary>Handles <c>g</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetFillGray(ContentInterpreter self, ref ContentReader reader) => ContentColors.SetDeviceColor(self, false, PdfColorSpace.DeviceGray, ref reader);

    /// <summary>Handles <c>G</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetStrokeGray(ContentInterpreter self, ref ContentReader reader) => ContentColors.SetDeviceColor(self, true, PdfColorSpace.DeviceGray, ref reader);

    /// <summary>Handles <c>rg</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetFillRgb(ContentInterpreter self, ref ContentReader reader) => ContentColors.SetDeviceColor(self, false, PdfColorSpace.DeviceRgb, ref reader);

    /// <summary>Handles <c>RG</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetStrokeRgb(ContentInterpreter self, ref ContentReader reader) => ContentColors.SetDeviceColor(self, true, PdfColorSpace.DeviceRgb, ref reader);

    /// <summary>Handles <c>k</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetFillCmyk(ContentInterpreter self, ref ContentReader reader) => ContentColors.SetDeviceColor(self, false, PdfColorSpace.DeviceCmyk, ref reader);

    /// <summary>Handles <c>K</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetStrokeCmyk(ContentInterpreter self, ref ContentReader reader) => ContentColors.SetDeviceColor(self, true, PdfColorSpace.DeviceCmyk, ref reader);

    /// <summary>Handles <c>cs</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetFillColorSpace(ContentInterpreter self, ref ContentReader reader) => ContentColors.SetColorSpace(self, false, reader.Operand(0).Name);

    /// <summary>Handles <c>CS</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetStrokeColorSpace(ContentInterpreter self, ref ContentReader reader) => ContentColors.SetColorSpace(self, true, reader.Operand(0).Name);

    /// <summary>Handles <c>sc</c> and <c>scn</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetFillColor(ContentInterpreter self, ref ContentReader reader) => ContentColors.SetColor(self, false, ref reader);

    /// <summary>Handles <c>SC</c> and <c>SCN</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetStrokeColor(ContentInterpreter self, ref ContentReader reader) => ContentColors.SetColor(self, true, ref reader);

    /// <summary>Copies the numeric operands of an operator.</summary>
    /// <param name = "reader">The reader.</param>
    /// <param name = "count">The operands to read.</param>
    /// <param name = "destination">Receives the numbers; entries past the operands are zero.</param>
    internal static void CopyComponents(ref ContentReader reader, int count, scoped Span<float> destination)
    {
        destination.Clear();
        var limit = Math.Min(count, destination.Length);
        for (var i = 0; i < limit; i++)
        {
            destination[i] = reader.Number(i);
        }
    }

    /// <summary>Gets the power of two at or above the page units one pattern unit spans, so cells are reused across nearby scales.</summary>
    /// <param name = "matrix">The matrix from pattern space to the page.</param>
    /// <returns>The exponent, clamped to a sane range.</returns>
    internal static int PatternScaleBucket(Matrix3x2 matrix)
    {
        var scale = (MathF.Sqrt((matrix.M11 * matrix.M11) + (matrix.M12 * matrix.M12)) + MathF.Sqrt((matrix.M21 * matrix.M21) + (matrix.M22 * matrix.M22))) / ContentColors.PatternScaleBase;
        var exponent = scale > 0 && float.IsFinite(scale) ? (int)MathF.Ceiling(MathF.Log2(scale)) : 0;
        return Math.Clamp(exponent, ContentColors.MinPatternExponent, ContentColors.MaxPatternExponent);
    }

    /// <summary>Sets a colour in a device colour space.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "stroke">Whether to set the stroke colour.</param>
    /// <param name = "space">The device colour space.</param>
    /// <param name = "reader">The reader holding the components.</param>
    internal static void SetDeviceColor(ContentInterpreter self, bool stroke, PdfColorSpace space, ref ContentReader reader)
    {
        if (self.ColorLocked)
        {
            return;
        }

        Span<float> components = stackalloc float[PdfColorSpace.MaxComponents];
        ContentColors.CopyComponents(ref reader, space.Components, components);
        ContentColors.Store(self, stroke, ColorState.Resolve(ContentDeviceColors.MapDeviceSpace(self, space), components));
    }

    /// <summary>Stores a colour as the fill or stroke colour.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "stroke">Whether to set the stroke colour.</param>
    /// <param name = "colour">The colour.</param>
    internal static void Store(ContentInterpreter self, bool stroke, in ColorState colour)
    {
        if (stroke)
        {
            self.State.Stroke = colour;
        }
        else
        {
            self.State.Fill = colour;
        }
    }

    /// <summary>Selects a colour space and sets its initial colour.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "stroke">Whether to set the stroke space.</param>
    /// <param name = "name">The colour space name.</param>
    internal static void SetColorSpace(ContentInterpreter self, bool stroke, PdfName name)
    {
        if (self.ColorLocked)
        {
            return;
        }

        var space = ContentColors.LookupColorSpace(self, name);
        Span<float> initial = stackalloc float[PdfColorSpace.MaxComponents];
        initial.Clear();
        space.GetInitialColor(initial);
        var colour = ColorState.Resolve(space, initial);
        if (space.Kind == PdfColorSpaceKind.Pattern)
        {
            // Painting in a pattern space paints nothing until a pattern is selected.
            colour = colour with
            {
                PaintsNothing = true
            };
        }

        ContentColors.Store(self, stroke, colour);
    }

    /// <summary>Finds a colour space by name: a device space, or a /ColorSpace resource.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "name">The name.</param>
    /// <returns>The colour space.</returns>
    internal static PdfColorSpace LookupColorSpace(ContentInterpreter self, PdfName name)
    {
        switch (name.ToKnownName())
        {
            case KnownName.DeviceGray or KnownName.G:
                {
                    return ContentDeviceColors.MapDeviceSpace(self, PdfColorSpace.DeviceGray);
                }

            case KnownName.DeviceRGB or KnownName.RGB:
                {
                    return ContentDeviceColors.MapDeviceSpace(self, PdfColorSpace.DeviceRgb);
                }

            case KnownName.DeviceCMYK or KnownName.CMYK:
                {
                    return ContentDeviceColors.MapDeviceSpace(self, PdfColorSpace.DeviceCmyk);
                }

            case KnownName.Pattern:
                {
                    return PatternColorSpace.Colored;
                }

            default:
                {
                    var value = ContentExecution.FindResource(self, KnownName.ColorSpace, name);
                    return value.IsNull ? PdfColorSpace.DeviceGray : self.Cache.GetColorSpace(value, ContentExecution.CurrentResources(self)?.GetDictionary(KnownName.ColorSpace));
                }
        }
    }

    /// <summary>Sets a colour in the current colour space, or selects a pattern.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "stroke">Whether to set the stroke colour.</param>
    /// <param name = "reader">The reader holding the components and an optional pattern name.</param>
    internal static void SetColor(ContentInterpreter self, bool stroke, ref ContentReader reader)
    {
        if (self.ColorLocked)
        {
            return;
        }

        var space = stroke ? self.State.Stroke.Space : self.State.Fill.Space;
        var count = reader.OperandCount;
        var last = reader.Operand(count - 1);
        Span<float> components = stackalloc float[PdfColorSpace.MaxComponents];
        if (last.Kind == ContentOperandKind.Name && space is PatternColorSpace pattern)
        {
            ContentColors.CopyComponents(ref reader, count - 1, components);
            ContentColors.SelectPattern(self, stroke, pattern, last.Name, components);
            return;
        }

        ContentColors.CopyComponents(ref reader, count, components);
        ContentColors.Store(self, stroke, ColorState.Resolve(space, components));
    }

    /// <summary>Selects a pattern as the fill or stroke colour.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "stroke">Whether to set the stroke colour.</param>
    /// <param name = "space">The pattern colour space.</param>
    /// <param name = "name">The pattern's resource name.</param>
    /// <param name = "components">The components of an uncoloured pattern's colour.</param>
    internal static void SelectPattern(ContentInterpreter self, bool stroke, PatternColorSpace space, PdfName name, ReadOnlySpan<float> components)
    {
        var rgb = space.Underlying is { } underlying ? ColorState.Resolve(underlying, components).Rgb : 0U;
        var paint = ContentColors.CreatePatternPaint(self, name, rgb);
        ContentColors.Store(self, stroke, new(space, rgb, paint, paint is null));
    }

    /// <summary>Creates the paint for a pattern resource.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "name">The pattern's resource name.</param>
    /// <param name = "rgb">The colour for an uncoloured pattern.</param>
    /// <returns>The paint, or null when the pattern is missing or damaged or no drawing backend is registered.</returns>
    internal static PdfPatternPaint? CreatePatternPaint(ContentInterpreter self, PdfName name, uint rgb)
    {
        if (self.Cache.Document.DrawingServices.ConfiguredBackend is null)
        {
            return null;
        }

        var value = ContentExecution.FindResource(self, KnownName.Pattern, name);
        if (value.AsDictionary() is not { } dictionary)
        {
            return null;
        }

        var matrix = ContentOperands.ReadMatrix(dictionary, KnownName.Matrix) * self.PatternBase;
        if (dictionary.GetInt32(KnownName.PatternType) == ContentColors.ShadingPatternType)
        {
            var shadingValue = dictionary.Get(KnownName.Shading);
            var shading = ContentColors.GetShading(self, shadingValue);
            return shading is null ? null : new PdfPatternPaint(shading, matrix);
        }

        if (value.AsStream() is not { } stream)
        {
            return null;
        }

        var uncolored = dictionary.GetInt32(KnownName.PaintType) == ContentColors.UncoloredPaintType;
        var bucket = ContentColors.PatternScaleBucket(matrix);
        var cell = self.Cache.Cells.GetOrCreate(
            new(dictionary, uncolored ? rgb : 0, bucket),
            new CellRequest(self, stream, uncolored, rgb),
            static (key, request) => ContentColors.RecordCell(
                request.Owner,
                request.Pattern,
                request.Uncolored,
                request.Rgb,
                MathF.Pow(ContentColors.PatternScaleBase, key.ScaleBucket)));
        return cell is null ? null : new PdfPatternPaint(cell, matrix);
    }

    /// <summary>Gets a parsed shading.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "value">The shading dictionary or stream, resolved.</param>
    /// <returns>The shading, or null when it is damaged.</returns>
    internal static PdfShading? GetShading(ContentInterpreter self, PdfValue value)
    {
        var dictionary = value.AsDictionary();
        if (dictionary is null)
        {
            return null;
        }

        var spaces = ContentExecution.CurrentResources(self)?.GetDictionary(KnownName.ColorSpace);
        using var scope = OutputIntentColors.Enter(self.Cache.DeviceColors);
        return self.Cache.Shadings.GetOrCreate(dictionary, new ShadingRequest(value, spaces), static (key, request) => PdfShading.Parse(request.Value, request.Spaces));
    }

    /// <summary>Records a tiling pattern's cell.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "pattern">The pattern stream.</param>
    /// <param name = "uncolored">Whether the pattern is uncoloured.</param>
    /// <param name = "rgb">The colour for an uncoloured pattern.</param>
    /// <param name = "scale">The page units one pattern unit spans, which sets the resolution of a rasterised period.</param>
    /// <returns>The cell, or null when the pattern is damaged, nested too deeply or has a zero step, which PDFium does not draw.</returns>
    internal static PatternCell? RecordCell(ContentInterpreter self, PdfStream pattern, bool uncolored, uint rgb, float scale)
    {
        var dictionary = pattern.Dictionary;
        if (self.Depth >= PdfLimits.MaxDrawDepth || !dictionary.TryGetRectangle(KnownName.BBox, out var box))
        {
            return null;
        }

        var stepX = dictionary.GetSingle(KnownName.XStep);
        var stepY = dictionary.GetSingle(KnownName.YStep);
        if (stepX == 0 || stepY == 0 || !float.IsFinite(stepX) || !float.IsFinite(stepY))
        {
            return null;
        }

        using var recorder = self.Device.CreatePictureDevice(new(box.Left, box.Bottom, box.Right, box.Top));
        using var child = new ContentInterpreter(self.Cache, recorder, self.Depth + 1, cancellationToken: self.CancellationToken) { Printing = self.Printing };
        ContentExecution.RunPattern(child, pattern, uncolored, rgb);
        using var cell = recorder.Finish();
        return self.Cache.Document.DrawingServices.Backend.ComposeTile(cell, box, stepX, stepY, scale);
    }
}
